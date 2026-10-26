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

        Object.DontDestroyOnLoad(host);
        return canvas;
    }

    /// <summary>创建一个铺满父级的 RectTransform。</summary>
    public static RectTransform CreateFullScreen(string name, Transform parent)
    {
        var rect = NewRect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
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
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = WhiteSprite;
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    /// <summary>居中的面板，指定尺寸。</summary>
    public static RectTransform CreateCenteredPanel(Transform parent, Vector2 size, Color color)
    {
        var rect = NewRect("Panel", parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = WhiteSprite;
        image.color = color;
        image.raycastTarget = true;

        return rect;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent, string content, float fontSize, Color color,
        TextAlignmentOptions alignment, Vector2 size, Vector2 anchoredPosition)
    {
        var rect = NewRect("Text", parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
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
        var rect = NewRect("Button", parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = WhiteSprite;
        image.color = Color.white; // 真正的颜色交给 ColorBlock，便于悬停变色

        var button = rect.gameObject.AddComponent<Button>();
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

        if (onClick != null)
            button.onClick.AddListener(onClick);

        // 文字作为子对象，铺满按钮
        var textRect = CreateFullScreen("Label", rect);
        textRect.offsetMin = new Vector2(12f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);

        var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
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
