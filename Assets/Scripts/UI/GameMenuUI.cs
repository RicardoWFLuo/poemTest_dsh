using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 存档界面（标题界面 + 游戏内菜单 + 存档位列表）。
///
/// 设计约束：
///  · 完全不改任何场景或预制体 —— 通过 RuntimeInitializeOnLoadMethod 自动创建，界面全部用代码生成；
///  · 与现有 UI（属性面板 / 技能树 / 商店）共存 —— 独立 Canvas，sortingOrder 500，打开时统一暂停；
///  · 暂停走 PauseController 引用计数，不会和属性面板抢 Time.timeScale。
///
/// 操作：标题界面在启动时显示；进入游戏后按 Esc 或点右上角「菜单」按钮打开菜单。
/// </summary>
[DisallowMultipleComponent]
public class GameMenuUI : MonoBehaviour
{
    public static GameMenuUI Instance { get; private set; }

    [Header("启动时显示标题界面")]
    public bool showTitleOnStart = true;

    [Header("按键")]
    public KeyCode toggleKey = KeyCode.Escape;

    [Header("画布层级（要比现有 UI 高）")]
    public int canvasSortingOrder = 500;

    [Header("界面预制体（可选）")]
    [Tooltip("自定义菜单界面预制体。留空则自动尝试 Resources/GameMenuCanvas；\n" +
             "两者都没有时才纯代码生成。\n\n" +
             "把预制体填到这里，你就能在 Unity 里直接编辑菜单布局、文字、图片。")]
    public GameObject designPrefab;

    private const string CanvasName = "[GameMenuCanvas]";
    private const string PrefabResourcePath = "GameMenuCanvas";
    private const string MenuButtonInBarName = "GameMenuButton";
    private const string CoverResourcePath = "tt";

    private static Sprite coverCache;

    /// <summary>
    /// 读取封面图。优先按 Sprite 取（tt.png 已经是 Sprite 类型）；
    /// 万一导入类型是 Texture 也能兜底，用代码临时生成一个 Sprite。
    /// </summary>
    private static Sprite LoadCoverSprite()
    {
        if (coverCache != null)
            return coverCache;

        coverCache = Resources.Load<Sprite>(CoverResourcePath);

        if (coverCache != null)
            return coverCache;

        Texture2D texture = Resources.Load<Texture2D>(CoverResourcePath);

        if (texture == null)
            return null;

        coverCache = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return coverCache;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCoverCache()
    {
        coverCache = null;
    }

    private Canvas canvas;

    /// <summary>兜底用的角落菜单按钮（成功放进左下角那排后会被隐藏）。</summary>
    private RectTransform cornerMenuButton;

    private bool menuButtonInBar;

    private GameObject titleRoot;
    private GameObject menuRoot;
    private GameObject slotRoot;
    private GameObject hudRoot;

    private TMP_Text slotHeaderText;
    private TMP_Text toastText;

    private readonly Button[] slotButtons = new Button[SaveManager.SlotCount];
    private bool slotModeIsSave;
    private bool pausedByMe;
    private Coroutine toastRoutine;

    private bool TitleVisible => titleRoot != null && titleRoot.activeSelf;
    private bool MenuVisible => menuRoot != null && menuRoot.activeSelf;
    private bool SlotsVisible => slotRoot != null && slotRoot.activeSelf;
    private bool AnyOverlayVisible => TitleVisible || MenuVisible || SlotsVisible;

    /// <summary>标题/主菜单/存档界面是否有任意一个开着。供其他 UI 判断互斥。</summary>
    public bool IsAnyOverlayVisible => AnyOverlayVisible;

    /// <summary>存档界面（读档/存档列表）是否开着。</summary>
    public bool IsSlotListVisible => SlotsVisible;

    // ---------------------------------------------------------------- 生命周期

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var host = new GameObject("[GameMenuUI]");
        DontDestroyOnLoad(host);
        host.AddComponent<GameMenuUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (UIStyle.Settings != null)
            showTitleOnStart = UIStyle.Settings.showTitleOnStart;

        SceneManager.sceneLoaded += OnSceneLoaded;

        BuildUI();

        if (showTitleOnStart)
            ShowTitle();
        else
            HideAll();
    }

    /// <summary>
    /// 场景加载完之后再检查 EventSystem。
    /// 放在 Start / sceneLoaded 里而不是 Awake：只有这时场景自带的 EventSystem 才已经存在，
    /// 否则会重复创建一个，产生 "There can only be one active Event System" 警告。
    /// </summary>
    private void Start()
    {
        UIFactory.EnsureEventSystem();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UIFactory.EnsureEventSystem();

        // 再试一次：切场景后 MenuBar 是新的对象，Awake 时可能还没加载出来
        if (!menuButtonInBar)
        {
            menuButtonInBar = TryCreateMenuButtonInBar();

            if (menuButtonInBar && cornerMenuButton != null)
                cornerMenuButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // 标题界面时 Esc 不做事，避免把标题顶掉
        if (TitleVisible)
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (MenuVisible || SlotsVisible)
                HideAll();
            else
                ShowMenu();
        }
    }

    // ---------------------------------------------------------------- 贴图应用

    /// <summary>
    /// 在 UIManager 的 MenuBar（左下角那排「属性 / 技能 / 任务」）里，
    /// **克隆一个同款按钮**作为「菜单」。
    ///
    /// 注意：这些按钮不一定是 MenuBar 的直接子物体（可能是 MenuBar → Bar → 按钮），
    /// 所以要用 GetComponentsInChildren 深度查找，并把克隆放到模板的同级父节点下，
    /// 否则位置参照系不对。
    /// </summary>
    private bool TryCreateMenuButtonInBar()
    {
        GameObject barObject = GameObject.Find("MenuBar");

        if (barObject == null)
        {
            Debug.LogWarning("[菜单] 没找到 MenuBar。");
            return false;
        }

        Button[] buttons = barObject.GetComponentsInChildren<Button>(true);

        if (buttons == null || buttons.Length == 0)
        {
            Debug.LogWarning("[菜单] MenuBar 里没有任何按钮。");
            return false;
        }

        // 优先用任务按钮当模板，保证样式一致
        Button template = null;

        foreach (Button candidate in buttons)
        {
            if (candidate.name.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                template = candidate;
                break;
            }
        }

        if (template == null)
            template = buttons[0];

        if (template.transform is not RectTransform templateRect)
        {
            Debug.LogWarning("[菜单] 模板按钮没有 RectTransform。");
            return false;
        }

        // 克隆要放在模板的同一个父节点下，坐标才有可比性
        Transform rowParent = templateRect.parent;

        if (rowParent == null)
            return false;

        Transform existing = rowParent.Find(MenuButtonInBarName);
        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = Instantiate(template.gameObject, rowParent, false);
            go.name = MenuButtonInBarName;
        }

        var rect = (RectTransform)go.transform;

        // 接在同一排最右边那个按钮后面（自适应，不写死坐标）
        if (UIFactory.ApplyLayout)
        {
            RectTransform rightmost = null;
            float maxX = float.NegativeInfinity;

            foreach (Transform child in rowParent)
            {
                if (child == go.transform)
                    continue;

                if (child is not RectTransform candidate || child.GetComponent<Button>() == null)
                    continue;

                if (candidate.anchoredPosition.x > maxX)
                {
                    maxX = candidate.anchoredPosition.x;
                    rightmost = candidate;
                }
            }

            if (rightmost != null)
            {
                rect.anchorMin = rightmost.anchorMin;
                rect.anchorMax = rightmost.anchorMax;
                rect.pivot = rightmost.pivot;
                rect.sizeDelta = rightmost.sizeDelta;

                float spacing = UIStyle.Settings != null ? UIStyle.Settings.menuButtonRowSpacing : 8f;
                rect.anchoredPosition = rightmost.anchoredPosition +
                                        new Vector2(rightmost.sizeDelta.x + spacing, 0f);
            }
            else
            {
                rect.anchorMin = templateRect.anchorMin;
                rect.anchorMax = templateRect.anchorMax;
                rect.pivot = templateRect.pivot;
                rect.sizeDelta = templateRect.sizeDelta;
                rect.anchoredPosition = templateRect.anchoredPosition;
            }
        }

        // 改文字
        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = "菜单";

        // 换掉点击事件：克隆过来的是原按钮的监听，必须清掉
        Button button = go.GetComponent<Button>();

        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(ShowMenu);
        }

        Debug.Log($"[菜单] 已在左下角按钮排（{rowParent.name}）里创建同款「菜单」按钮，模板是「{template.name}」。");
        return true;
    }

    /// <summary>把面板底图换成九宫格横幅。没有配图时保持纯色。</summary>
    private static void ApplyBanner(RectTransform panel, Sprite sprite)
    {
        if (panel == null || sprite == null)
            return;

        var image = UIFactory.EnsureComponent<Image>(panel.gameObject);
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = true;
    }

    /// <summary>
    /// 给按钮套上贴图。
    /// 贴图本身有颜色，所以 ColorBlock 要改成白色系，否则会被旧的深色再染一遍。
    /// </summary>
    private static void ApplyButtonSprite(Button button, Sprite sprite)
    {
        if (button == null || sprite == null)
            return;

        if (button.targetGraphic is Image image)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private Sprite TitleButtonSprite => UIStyle.Settings != null ? UIStyle.Settings.menuButtonSprite : null;

    private Sprite AltButtonSprite
    {
        get
        {
            UIStyleSettings settings = UIStyle.Settings;

            if (settings == null)
                return null;

            return settings.menuButtonSpriteAlt != null ? settings.menuButtonSpriteAlt : settings.menuButtonSprite;
        }
    }

    // ---------------------------------------------------------------- 界面搭建

    private void BuildUI()
    {
        // 优先用「可在 Unity 中编辑」的预制体；没有才退回纯代码生成。
        Canvas prefabCanvas = InstantiateCanvasFromPrefab();

        UIStyleSettings settings = UIStyle.Settings;
        bool forceLayout = settings == null || settings.applyLayoutAtRuntime;

        // 有预制体时默认「以预制体排版为准」，这样你在 Hierarchy 里调的位置才会生效；
        // 纯代码生成时必须套用代码里的布局。
        UIFactory.ApplyLayout = prefabCanvas == null || forceLayout;

        canvas = prefabCanvas != null
            ? prefabCanvas
            : UIFactory.CreateCanvas(CanvasName, canvasSortingOrder);

        // 下面的构建方法会「同名复用」，所以预制体里的节点会被沿用，
        // 你在编辑器里改的位置、文字、图片都会保留。
        BuildTitleScreen();
        BuildMenu();
        BuildSlotList();
        BuildHud();
        BuildToast();
    }

    /// <summary>从预制体实例化界面画布。找不到预制体时返回 null。</summary>
    private Canvas InstantiateCanvasFromPrefab()
    {
        GameObject prefab = designPrefab;

        if (prefab == null)
            prefab = Resources.Load<GameObject>(PrefabResourcePath);

        if (prefab == null)
            return null;

        GameObject instance = Instantiate(prefab, transform, false);
        instance.name = CanvasName;

        Canvas prefabCanvas = instance.GetComponent<Canvas>();

        if (prefabCanvas == null)
        {
            Debug.LogWarning($"[菜单] 预制体「{prefab.name}」根节点上没有 Canvas，改用代码生成界面。");
            Destroy(instance);
            return null;
        }

        prefabCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        prefabCanvas.sortingOrder = canvasSortingOrder;

        // 校验一下：如果预制体是「不套布局」模式下生成的，界面会叠成一团。
        // 这种情况直接退回代码生成，并提示重新生成预制体。
        if (!IsPrefabUsable(instance.transform))
        {
            Debug.LogWarning("[菜单] 预制体里的界面尺寸异常（可能是在错误的模式下生成的），本次改用代码生成。\n" +
                             "请重新执行「Tools → 诗词数据 → 8. 生成菜单界面预制体」。");
            Destroy(instance);
            return null;
        }

        return prefabCanvas;
    }

    /// <summary>检查预制体是否被正确构建过（标题面板必须有正常尺寸）。</summary>
    private static bool IsPrefabUsable(Transform root)
    {
        Transform title = root.Find("TitleScreen");

        if (title == null)
            return false;

        Transform panel = title.Find("Panel");

        if (panel is not RectTransform panelRect)
            return false;

        return panelRect.sizeDelta.x > 100f && panelRect.sizeDelta.y > 100f;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 仅供编辑器生成预制体：用同一套构建代码造一份界面并返回画布对象。
    /// 生成之后你就能在 Unity 里可视化编辑（拖位置、换文字、换图）。
    /// </summary>
    public static GameObject BuildForPrefab()
    {
        // 生成预制体时必须走「套用布局」模式，
        // 否则所有节点会保持默认的 100×100 居中，叠成一团。
        bool previousApplyLayout = UIFactory.ApplyLayout;
        UIFactory.ApplyLayout = true;

        Canvas host = UIFactory.CreateCanvas(CanvasName, 500);

        var tempHost = new GameObject("[GameMenuUI-Temp]");
        GameMenuUI menu = tempHost.AddComponent<GameMenuUI>();
        menu.canvas = host;

        try
        {
            menu.BuildTitleScreen();
            menu.BuildMenu();
            menu.BuildSlotList();
            menu.BuildHud();
            menu.BuildToast();
        }
        finally
        {
            UIFactory.ApplyLayout = previousApplyLayout;
            DestroyImmediate(tempHost);
        }

        return host.gameObject;
    }
#endif

    /// <summary>给按钮文字上色（存档按钮要白色，其余黑色）。</summary>
    private static void ApplyButtonTextColor(Button button, Color color)
    {
        if (button == null)
            return;

        Transform label = button.transform.Find("Label");

        if (label != null && label.TryGetComponent(out TMP_Text text))
            text.color = color;
    }

    /// <summary>存档按钮：不套贴图，保持灰色底 + 白色字。</summary>
    private static void ApplySlotButton(Button button, Color background, Color textColor)
    {
        if (button == null)
            return;

        if (button.targetGraphic is Image image)
        {
            image.sprite = UIFactory.WhiteSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = new Color(background.r * 1.25f, background.g * 1.25f, background.b * 1.25f, background.a);
        colors.pressedColor = new Color(background.r * 0.75f, background.g * 0.75f, background.b * 0.75f, background.a);
        colors.selectedColor = background;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        ApplyButtonTextColor(button, textColor);
    }

    private void BuildTitleScreen()
    {
        UIStyleSettings s = UIStyle.Settings;

        titleRoot = UIFactory.CreateFullScreen("TitleScreen", canvas.transform).gameObject;

        // ① 全屏封面，放在最底层
        Sprite cover = s != null && s.menuCover != null ? s.menuCover : LoadCoverSprite();

        if (cover != null)
        {
            RectTransform coverRect = UIFactory.CreateFullScreen("Cover", titleRoot.transform);
            coverRect.SetAsFirstSibling();

            var coverImage = UIFactory.EnsureComponent<Image>(coverRect.gameObject);
            coverImage.sprite = cover;
            coverImage.type = Image.Type.Simple;
            coverImage.preserveAspect = false; // 铺满整屏
            coverImage.color = Color.white;
            coverImage.raycastTarget = false;
        }

        // ② 遮罩：有封面时压暗一点，让文字读得清
        bool dim = cover != null && (s == null || s.dimCover);
        UIFactory.CreateBackground(titleRoot.transform,
            dim ? new Color(0f, 0f, 0f, 0.45f) : UIStyle.PanelColor);

        Vector2 panelSize = s != null ? s.titlePanelSize : new Vector2(780f, 640f);
        RectTransform panel = UIFactory.CreateCenteredPanel(
            titleRoot.transform, panelSize, new Color(0f, 0f, 0f, 0.35f));

        if (UIFactory.ApplyLayout && s != null)
            panel.anchoredPosition = s.titlePanelOffset;

        // 竖版横幅当背景
        ApplyBanner(panel, s != null ? s.menuTitleBanner : null);

        Color textColor = s != null ? s.menuTextColor : UIStyle.TextColor;
        Vector2 buttonSize = s != null ? s.titleButtonSize : new Vector2(440f, 88f);
        float spacing = s != null ? s.titleButtonSpacing : 110f;

        UIFactory.CreateText(panel, "诗词冒险", 96f, textColor,
            TextAlignmentOptions.Center, new Vector2(720f, 140f), new Vector2(0f, 200f));

        UIFactory.CreateText(panel, "PoemTest · 古诗词学习 RPG", 32f, textColor,
            TextAlignmentOptions.Center, new Vector2(720f, 50f), new Vector2(0f, 110f));

        Sprite buttonSprite = TitleButtonSprite;

        Button newGame = UIFactory.CreateButton(panel, "开始新游戏", buttonSize, new Vector2(0f, 0f), OnNewGame);
        ApplyButtonSprite(newGame, buttonSprite);
        ApplyButtonTextColor(newGame, textColor);

        Button loadGame = UIFactory.CreateButton(panel, "读取存档", buttonSize, new Vector2(0f, -spacing),
            () => ShowSlotList(false));
        ApplyButtonSprite(loadGame, buttonSprite);
        ApplyButtonTextColor(loadGame, textColor);

        Button quit = UIFactory.CreateButton(panel, "退出游戏", buttonSize, new Vector2(0f, -spacing * 2f), OnQuit);
        ApplyButtonSprite(quit, buttonSprite);
        ApplyButtonTextColor(quit, textColor);
    }

    private void BuildMenu()
    {
        UIStyleSettings s = UIStyle.Settings;

        menuRoot = UIFactory.CreateFullScreen("MenuScreen", canvas.transform).gameObject;
        UIFactory.CreateBackground(menuRoot.transform, new Color(0f, 0f, 0f, 0.6f));

        Vector2 panelSize = s != null ? s.menuPanelSize : new Vector2(720f, 600f);
        RectTransform panel = UIFactory.CreateCenteredPanel(menuRoot.transform, panelSize, UIStyle.PanelColor);

        if (UIFactory.ApplyLayout && s != null)
            panel.anchoredPosition = s.menuPanelOffset;

        // 游戏菜单也要有横幅（之前只有标题界面有）
        // 没单独配就用存档界面那张，避免出现"灰色空面板"
        Sprite menuBanner = s != null
            ? (s.menuScreenBanner != null ? s.menuScreenBanner : s.menuSlotBanner)
            : null;

        ApplyBanner(panel, menuBanner);

        Color textColor = s != null ? s.menuTextColor : UIStyle.TextColor;
        Vector2 buttonSize = s != null ? s.menuPanelButtonSize : new Vector2(460f, 84f);
        float spacing = s != null ? s.menuPanelButtonSpacing : 100f;

        UIFactory.CreateText(panel, "游戏菜单", 60f, textColor,
            TextAlignmentOptions.Center, new Vector2(660f, 90f), new Vector2(0f, 210f));

        Sprite buttonSprite = AltButtonSprite;

        Button resume = UIFactory.CreateButton(panel, "继续游戏", buttonSize, new Vector2(0f, spacing), HideAll);
        ApplyButtonSprite(resume, buttonSprite);
        ApplyButtonTextColor(resume, textColor);

        Button save = UIFactory.CreateButton(panel, "保存进度", buttonSize, new Vector2(0f, 0f),
            () => ShowSlotList(true));
        ApplyButtonSprite(save, buttonSprite);
        ApplyButtonTextColor(save, textColor);

        Button load = UIFactory.CreateButton(panel, "读取存档", buttonSize, new Vector2(0f, -spacing),
            () => ShowSlotList(false));
        ApplyButtonSprite(load, buttonSprite);
        ApplyButtonTextColor(load, textColor);

        Button backToTitle = UIFactory.CreateButton(panel, "返回标题", buttonSize, new Vector2(0f, -spacing * 2f), ShowTitle);
        ApplyButtonSprite(backToTitle, buttonSprite);
        ApplyButtonTextColor(backToTitle, textColor);
    }

    private void BuildSlotList()
    {
        UIStyleSettings s = UIStyle.Settings;

        slotRoot = UIFactory.CreateFullScreen("SlotScreen", canvas.transform).gameObject;
        UIFactory.CreateBackground(slotRoot.transform, new Color(0f, 0f, 0f, 0.72f));

        Vector2 panelSize = s != null ? s.slotPanelSize : new Vector2(940f, 560f);
        RectTransform panel = UIFactory.CreateCenteredPanel(slotRoot.transform, panelSize, UIStyle.PanelColor);

        if (UIFactory.ApplyLayout && s != null)
            panel.anchoredPosition = s.slotPanelOffset;

        // 横版横幅当背景
        ApplyBanner(panel, s != null ? s.menuSlotBanner : null);

        Color textColor = s != null ? s.menuTextColor : UIStyle.TextColor;
        Color slotBg = s != null ? s.slotButtonColor : new Color(0.30f, 0.30f, 0.36f, 1f);
        Color slotText = s != null ? s.slotButtonTextColor : Color.white;
        Vector2 buttonSize = s != null ? s.slotButtonSize : new Vector2(860f, 76f);
        float spacing = s != null ? s.slotButtonSpacing : 92f;

        slotHeaderText = UIFactory.CreateText(panel, "选择存档位", 48f, textColor,
            TextAlignmentOptions.Center, new Vector2(880f, 80f), new Vector2(0f, 200f));

        // 存档位按钮：不套贴图，保持灰色底 + 白字
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i + 1;
            slotButtons[i] = UIFactory.CreateButton(
                panel, "存档位 " + slot,
                buttonSize,
                new Vector2(0f, 90f - i * spacing),
                () => OnSlotClicked(slot));

            ApplySlotButton(slotButtons[i], slotBg, slotText);
        }

        Button back = UIFactory.CreateButton(panel, "返回", new Vector2(300f, 76f), new Vector2(0f, -210f), ShowMenu);
        ApplySlotButton(back, slotBg, slotText);
    }

    private void BuildHud()
    {
        RectTransform hudRect = UIFactory.CreateFullScreen("HudRoot", canvas.transform);

        // 预制体里可能已经带了 SafeAreaFitter，用 Ensure 避免重复添加的报错
        UIFactory.EnsureComponent<SafeAreaFitter>(hudRect.gameObject);
        hudRoot = hudRect.gameObject;

        // 优先在左下角那排（属性/技能/任务）里克隆一个同款「菜单」按钮
        menuButtonInBar = TryCreateMenuButtonInBar();

        // 兜底：屏幕角落自建一个；成功放进那排时把兜底按钮隐藏掉，避免出现两个
        cornerMenuButton = EnsureCornerMenuButton();
        cornerMenuButton.gameObject.SetActive(!menuButtonInBar);
    }

    /// <summary>屏幕角落的兜底菜单按钮（正常情况下会被左下角那排取代）。</summary>
    private RectTransform EnsureCornerMenuButton()
    {
        UIStyleSettings settings = UIStyle.Settings;
        Vector2 buttonSize = settings != null ? settings.menuButtonSize : new Vector2(200f, 88f);
        Vector2 margin = settings != null ? settings.menuButtonMargin : new Vector2(28f, 28f);
        MenuButtonCorner corner = settings != null ? settings.menuButtonCorner : MenuButtonCorner.TopRight;

        Vector2 anchor;
        Vector2 pivot;
        Vector2 position;

        switch (corner)
        {
            case MenuButtonCorner.TopLeft:
                anchor = new Vector2(0f, 1f); pivot = new Vector2(0f, 1f); position = new Vector2(margin.x, -margin.y);
                break;

            case MenuButtonCorner.BottomRight:
                anchor = new Vector2(1f, 0f); pivot = new Vector2(1f, 0f); position = new Vector2(-margin.x, margin.y);
                break;

            case MenuButtonCorner.BottomLeft:
                anchor = new Vector2(0f, 0f); pivot = new Vector2(0f, 0f); position = new Vector2(margin.x, margin.y);
                break;

            case MenuButtonCorner.TopCenter:
                anchor = new Vector2(0.5f, 1f); pivot = new Vector2(0.5f, 1f); position = new Vector2(0f, -margin.y);
                break;

            default:
                anchor = new Vector2(1f, 1f); pivot = new Vector2(1f, 1f); position = new Vector2(-margin.x, -margin.y);
                break;
        }

        var buttonRect = UIFactory.EnsureRect("MenuButton", hudRoot.transform);

        if (UIFactory.ApplyLayout)
        {
            buttonRect.anchorMin = anchor;
            buttonRect.anchorMax = anchor;
            buttonRect.pivot = pivot;
            buttonRect.sizeDelta = buttonSize;
            buttonRect.anchoredPosition = position;
        }

        var image = UIFactory.EnsureComponent<Image>(buttonRect.gameObject);
        image.sprite = UIFactory.WhiteSprite;
        image.color = Color.white;

        var button = UIFactory.EnsureComponent<Button>(buttonRect.gameObject);
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(UIStyle.ButtonColor.r, UIStyle.ButtonColor.g, UIStyle.ButtonColor.b, 0.8f);
        colors.highlightedColor = UIStyle.ButtonHighlightColor;
        colors.pressedColor = UIStyle.AccentColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(ShowMenu);

        RectTransform labelRect = UIFactory.CreateFullScreen("Label", buttonRect);
        var label = UIFactory.EnsureComponent<TextMeshProUGUI>(labelRect.gameObject);
        label.font = UIStyle.Font;
        label.text = "菜单";
        label.fontSize = 36f;
        label.color = UIStyle.TextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return buttonRect;
    }

    private void BuildToast()
    {
        toastText = UIFactory.CreateText(canvas.transform, "", 36f, UIStyle.AccentColor,
            TextAlignmentOptions.Center, new Vector2(900f, 80f), new Vector2(0f, -260f));

        toastText.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- 显示逻辑

    private void ShowTitle()
    {
        ShowOnly(titleRoot);
    }

    private void ShowMenu()
    {
        ShowOnly(menuRoot);
    }

    private void ShowSlotList(bool forSave)
    {
        slotModeIsSave = forSave;
        RefreshSlotList();
        ShowOnly(slotRoot);
    }

    private void ShowOnly(GameObject root)
    {
        SetActiveIfNotNull(titleRoot, root == titleRoot);
        SetActiveIfNotNull(menuRoot, root == menuRoot);
        SetActiveIfNotNull(slotRoot, root == slotRoot);

        SetPaused(true);
        SetActiveIfNotNull(hudRoot, false);
    }

    private void HideAll()
    {
        SetActiveIfNotNull(titleRoot, false);
        SetActiveIfNotNull(menuRoot, false);
        SetActiveIfNotNull(slotRoot, false);

        SetPaused(false);
        SetActiveIfNotNull(hudRoot, true);
    }

    private static void SetActiveIfNotNull(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    /// <summary>用 PauseController 的引用计数暂停，不会和属性面板 / 技能树互相打架。</summary>
    private void SetPaused(bool paused)
    {
        if (paused)
        {
            if (pausedByMe)
                return;

            PauseController.RequestPause();
            pausedByMe = true;
        }
        else
        {
            if (!pausedByMe)
                return;

            PauseController.ReleasePause();
            pausedByMe = false;
        }
    }

    // ---------------------------------------------------------------- 存档位

    private void RefreshSlotList()
    {
        SaveManager save = SaveManager.Instance;

        if (slotHeaderText != null)
            slotHeaderText.text = slotModeIsSave ? "保存到哪个存档位？" : "读取哪个存档位？";

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            string label;

            if (save == null)
            {
                label = "存档位 " + slot + "   — 存档系统未就绪 —";
            }
            else if (save.HasSave(slot))
            {
                SaveSlotInfo info = save.GetSlotInfo(slot);
                label = "存档位 " + slot + "   " + FormatTime(info.savedAtUtc)
                        + "   等级 " + info.level + "   金币 " + info.gold;
            }
            else
            {
                label = "存档位 " + slot + "   — 空 —";
            }

            UIFactory.SetButtonLabel(slotButtons[i], label);
        }
    }

    private static string FormatTime(string isoUtc)
    {
        if (string.IsNullOrEmpty(isoUtc))
            return "时间未知";

        if (DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return parsed.ToLocalTime().ToString("MM-dd HH:mm");
        }

        return isoUtc;
    }

    private void OnSlotClicked(int slot)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
        {
            Toast("存档系统未就绪");
            return;
        }

        if (slotModeIsSave)
        {
            if (save.Save(slot))
            {
                Toast("已保存到存档位 " + slot);
                RefreshSlotList();
                ShowMenu();
            }
            else
            {
                Toast("保存失败，详见 Console");
            }

            return;
        }

        if (!save.HasSave(slot))
        {
            Toast("这个存档位还是空的");
            return;
        }

        // 先恢复时间再读档，否则读档协程里的场景加载会卡在暂停状态
        HideAll();

        if (!save.Load(slot))
            Toast("读取失败，详见 Console");
    }

    // ---------------------------------------------------------------- 按钮回调

    private void OnNewGame()
    {
        SaveManager save = SaveManager.Instance;
        if (save != null)
            save.ResetRuntimeState();

        HideAll();
        Toast("新游戏开始");
    }

    private void OnQuit()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---------------------------------------------------------------- 提示

    private void Toast(string message)
    {
        if (toastText == null)
            return;

        toastText.text = message;
        toastText.gameObject.SetActive(true);

        if (toastRoutine != null)
            StopCoroutine(toastRoutine);

        toastRoutine = StartCoroutine(HideToastAfter(1.6f));
    }

    private IEnumerator HideToastAfter(float seconds)
    {
        // 用真实时间，避免被 timeScale = 0 卡住
        yield return new WaitForSecondsRealtime(seconds);

        if (toastText != null)
            toastText.gameObject.SetActive(false);

        toastRoutine = null;
    }
}
