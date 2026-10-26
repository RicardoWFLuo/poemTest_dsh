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

    private Canvas canvas;

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

    // ---------------------------------------------------------------- 界面搭建

    private void BuildUI()
    {
        canvas = UIFactory.CreateCanvas("[GameMenuCanvas]", canvasSortingOrder);

        BuildTitleScreen();
        BuildMenu();
        BuildSlotList();
        BuildHud();
        BuildToast();
    }

    private void BuildTitleScreen()
    {
        titleRoot = UIFactory.CreateFullScreen("TitleScreen", canvas.transform).gameObject;
        UIFactory.CreateBackground(titleRoot.transform, UIStyle.PanelColor);

        RectTransform panel = UIFactory.CreateCenteredPanel(
            titleRoot.transform, new Vector2(780f, 640f), new Color(0f, 0f, 0f, 0.35f));

        UIFactory.CreateText(panel, "诗词冒险", 96f, UIStyle.AccentColor,
            TextAlignmentOptions.Center, new Vector2(720f, 140f), new Vector2(0f, 200f));

        UIFactory.CreateText(panel, "PoemTest · 古诗词学习 RPG", 32f, UIStyle.TextColor,
            TextAlignmentOptions.Center, new Vector2(720f, 50f), new Vector2(0f, 110f));

        UIFactory.CreateButton(panel, "开始新游戏", new Vector2(440f, 88f), new Vector2(0f, 0f), OnNewGame);
        UIFactory.CreateButton(panel, "读取存档", new Vector2(440f, 88f), new Vector2(0f, -110f),
            () => ShowSlotList(false));
        UIFactory.CreateButton(panel, "退出游戏", new Vector2(440f, 88f), new Vector2(0f, -220f), OnQuit);
    }

    private void BuildMenu()
    {
        menuRoot = UIFactory.CreateFullScreen("MenuScreen", canvas.transform).gameObject;
        UIFactory.CreateBackground(menuRoot.transform, new Color(0f, 0f, 0f, 0.6f));

        RectTransform panel = UIFactory.CreateCenteredPanel(
            menuRoot.transform, new Vector2(720f, 600f), UIStyle.PanelColor);

        UIFactory.CreateText(panel, "游戏菜单", 60f, UIStyle.AccentColor,
            TextAlignmentOptions.Center, new Vector2(660f, 90f), new Vector2(0f, 210f));

        UIFactory.CreateButton(panel, "继续游戏", new Vector2(460f, 84f), new Vector2(0f, 110f), HideAll);
        UIFactory.CreateButton(panel, "保存进度", new Vector2(460f, 84f), new Vector2(0f, 10f),
            () => ShowSlotList(true));
        UIFactory.CreateButton(panel, "读取存档", new Vector2(460f, 84f), new Vector2(0f, -90f),
            () => ShowSlotList(false));
        UIFactory.CreateButton(panel, "返回标题", new Vector2(460f, 84f), new Vector2(0f, -190f), ShowTitle);
    }

    private void BuildSlotList()
    {
        slotRoot = UIFactory.CreateFullScreen("SlotScreen", canvas.transform).gameObject;
        UIFactory.CreateBackground(slotRoot.transform, new Color(0f, 0f, 0f, 0.72f));

        RectTransform panel = UIFactory.CreateCenteredPanel(
            slotRoot.transform, new Vector2(940f, 560f), UIStyle.PanelColor);

        slotHeaderText = UIFactory.CreateText(panel, "选择存档位", 48f, UIStyle.AccentColor,
            TextAlignmentOptions.Center, new Vector2(880f, 80f), new Vector2(0f, 200f));

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i + 1;
            slotButtons[i] = UIFactory.CreateButton(
                panel, "存档位 " + slot,
                new Vector2(860f, 76f),
                new Vector2(0f, 90f - i * 92f),
                () => OnSlotClicked(slot));
        }

        UIFactory.CreateButton(panel, "返回", new Vector2(300f, 76f), new Vector2(0f, -210f), ShowMenu);
    }

    private void BuildHud()
    {
        RectTransform hudRect = UIFactory.CreateFullScreen("HudRoot", canvas.transform);

        // 手机上让整层 HUD 落在安全区内，按钮就不会被刘海/圆角挡住
        hudRect.gameObject.AddComponent<SafeAreaFitter>();
        hudRoot = hudRect.gameObject;

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

        var buttonRect = UIFactory.NewRect("MenuButton", hudRoot.transform);
        buttonRect.anchorMin = anchor;
        buttonRect.anchorMax = anchor;
        buttonRect.pivot = pivot;
        buttonRect.sizeDelta = buttonSize;
        buttonRect.anchoredPosition = position;

        // 直接用 CreateButton 的话锚点会被设成居中，这里手搭一个同款按钮
        var image = buttonRect.gameObject.AddComponent<Image>();
        image.sprite = UIFactory.WhiteSprite;
        image.color = Color.white;

        var button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = new Color(UIStyle.ButtonColor.r, UIStyle.ButtonColor.g, UIStyle.ButtonColor.b, 0.8f);
        colors.highlightedColor = UIStyle.ButtonHighlightColor;
        colors.pressedColor = UIStyle.AccentColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        button.onClick.AddListener(ShowMenu);

        var labelRect = UIFactory.CreateFullScreen("Label", buttonRect);
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = UIStyle.Font;
        label.text = "菜单";
        label.fontSize = 36f;
        label.color = UIStyle.TextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
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
