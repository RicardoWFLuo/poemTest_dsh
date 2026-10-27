using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 用代码搭 UI 的小工具集。
///
/// 为什么不直接改场景/预制体：场景文件十几万行，手写 YAML 加 UI 层级极易出错。
/// 用代码生成的好处是：不碰任何现有资源、可编译验证、随时能改。
/// </summary>
public static class UIFactory
{
    /// <summary>
    /// 是否在创建/复用节点时套用代码里的排版数值。
    ///
    /// 关闭时：只绑定组件与文字，不动尺寸和位置 ——
    /// 这样你在 Unity 的 Hierarchy / 预制体里手动拖的位置才会生效。
    /// </summary>
    public static bool ApplyLayout = true;

    /// <summary>
    /// 每次进入 Play 都把开关复位成 true。
    /// 否则上一局运行期把它设成 false 之后，静态字段会残留，
    /// 导致编辑器工具（生成预制体）在错误的模式下构建、生成出叠在一起的界面。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetApplyLayoutOnPlay()
    {
        ApplyLayout = true;
    }

    private static Sprite whiteSprite;

    /// <summary>运行时生成的 1x1 白色贴图，给没有美术资源的纯色面板/按钮用。</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite != null)
                return whiteSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            whiteSprite.name = "UIFactory_White";
            return whiteSprite;
        }
    }

    /// <summary>
    /// 确保当前场景里有 EventSystem，否则按钮点不动。
    ///
    /// 关键：必须等到场景加载完之后再调用，并且要检查「场景里是否已经自带」。
    /// 早前在进入场景前就创建的版本会导致场景自带的 EventSystem 与这份重复，
    /// 从而报 "There can only be one active Event System" 警告。
    /// </summary>
    public static void EnsureEventSystem()
    {
        // 包含未激活的，避免场景里放了一个暂时关闭的 EventSystem 时又建一个
        var existing = Object.FindObjectsOfType<EventSystem>(true);
        if (existing != null && existing.Length > 0)
            return;

        var host = new GameObject("[UI EventSystem]");
        host.AddComponent<EventSystem>();
        host.AddComponent<StandaloneInputModule>();

        // 故意不加 DontDestroyOnLoad：让它跟着场景走，
        // 换场景后由 GameMenuUI 重新检查一次，永远不会和场景自带的重复。
    }

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var host = new GameObject(name,
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // DontDestroyOnLoad 只能在运行时调用。
        // 编辑器工具（例如生成菜单预制体）也会用这个方法，那里必须跳过。
        if (Application.isPlaying)
            Object.DontDestroyOnLoad(host);

        return canvas;
    }

    /// <summary>创建一个铺满父级的 RectTransform。</summary>
    /// <summary>
    /// 铺满父级。**同名子物体已存在时直接复用**，不重复创建。
    ///
    /// 这一条是全套 UI 能"预制体化"的关键：
    /// GameMenuUI 的构建代码保持不变，只要运行时先把预制体实例化到画布下，
    /// 这些 Create* 调用就会复用预制体里的节点，于是你在 Unity 里做的修改才会生效。
    /// </summary>
    public static RectTransform CreateFullScreen(string name, Transform parent)
    {
        var rect = EnsureRect(name, parent);

        if (ApplyLayout)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        return rect;
    }

    /// <summary>有同名的就直接拿来用，没有才新建。</summary>
    public static RectTransform EnsureRect(string name, Transform parent)
    {
        if (parent != null && !string.IsNullOrEmpty(name))
        {
            Transform existing = parent.Find(name);

            if (existing is RectTransform found)
                return found;
        }

        return NewRect(name, parent);
    }

    /// <summary>同类型组件已存在就复用，避免"复用预制体时又加一个 Image"这种重复组件。</summary>
    public static T EnsureComponent<T>(GameObject host) where T : Component
    {
        T existing = host.GetComponent<T>();
        return existing != null ? existing : host.AddComponent<T>();
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var host = new GameObject(name, typeof(RectTransform));
        var rect = host.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    /// <summary>纯色面板（铺满父级）。</summary>
    public static Image CreateBackground(Transform parent, Color color)
    {
        var rect = CreateFullScreen("Background", parent);
        var image = EnsureComponent<Image>(rect.gameObject);
        image.sprite = WhiteSprite;
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    /// <summary>居中的面板，指定尺寸。</summary>
    public static RectTransform CreateCenteredPanel(Transform parent, Vector2 size, Color color)
    {
        var rect = EnsureRect("Panel", parent);

        if (ApplyLayout)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        var image = EnsureComponent<Image>(rect.gameObject);
        image.sprite = WhiteSprite;
        image.color = color;
        image.raycastTarget = true;

        return rect;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent, string content, float fontSize, Color color,
        TextAlignmentOptions alignment, Vector2 size, Vector2 anchoredPosition)
    {
        // 用文本内容当对象名：既方便复用，也让 Hierarchy 里一眼看懂
        string objectName = string.IsNullOrWhiteSpace(content) ? "Text" : content.Trim();

        var rect = EnsureRect(objectName, parent);

        if (ApplyLayout)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        var text = EnsureComponent<TextMeshProUGUI>(rect.gameObject);
        text.font = UIStyle.Font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = true;

        return text;
    }

    /// <summary>创建按钮。注意：按钮颜色由 ColorBlock 控制，所以底图固定为白色再叠色。</summary>
    public static Button CreateButton(
        Transform parent, string label, Vector2 size, Vector2 anchoredPosition, UnityAction onClick)
    {
        // 用按钮文字当对象名，便于复用与在 Hierarchy 中辨认
        string objectName = string.IsNullOrWhiteSpace(label) ? "Button" : label.Trim();

        var rect = EnsureRect(objectName, parent);

        if (ApplyLayout)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        var image = EnsureComponent<Image>(rect.gameObject);
        image.sprite = WhiteSprite;
        image.color = Color.white; // 真正的颜色交给 ColorBlock，便于悬停变色

        var button = EnsureComponent<Button>(rect.gameObject);
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = UIStyle.ButtonColor;
        colors.highlightedColor = UIStyle.ButtonHighlightColor;
        colors.pressedColor = UIStyle.AccentColor;
        colors.selectedColor = UIStyle.ButtonColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        // 复用的时候监听会被重复注册，所以先清空再加
        button.onClick.RemoveAllListeners();

        if (onClick != null)
            button.onClick.AddListener(onClick);

        // 文字作为子对象，铺满按钮
        var textRect = CreateFullScreen("Label", rect);
        textRect.offsetMin = new Vector2(12f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);

        var text = EnsureComponent<TextMeshProUGUI>(textRect.gameObject);
        text.font = UIStyle.Font;
        text.text = label;
        text.fontSize = 34f;
        text.color = UIStyle.TextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableWordWrapping = false;

        return button;
    }

    /// <summary>改按钮上的文字。</summary>
    public static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = label;
    }
}
