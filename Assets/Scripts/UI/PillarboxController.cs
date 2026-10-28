using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 两侧黑边（Pillarbox / Letterbox）。
///
/// 作用：把游戏画面限制在固定的宽高比区域内（默认 16:9），
/// 屏幕多出来的部分用黑边填上。这样所有 UI 都落在设计稿的位置上，
/// 细长屏（如 20:9 手机）不会出现按钮挤在一起、摇杆和按钮重叠的问题。
///
/// 实现方式（不碰相机、不碰场景）：
///   1. 给每个根 Canvas 插入一个 [PillarboxViewport] 容器，
///      它的锚点等于「目标比例区域 ∩ 安全区」在屏幕中的归一化矩形；
///   2. 把 Canvas 原有子物体整体移进该容器（保持锚点语义，角落元素自动贴合新边界）；
///   3. 另建一个最高层级的 Canvas 画黑边，盖住区域外的画面。
///
/// 开关：UIStyleSettings.enablePillarbox；运行中按 F10 可实时切换。
/// </summary>
public class PillarboxController : MonoBehaviour
{
    public static PillarboxController Instance { get; private set; }

    [Header("开关")]
    [Tooltip("是否启用黑边模式。会被 UIStyleSettings.enablePillarbox 覆盖。")]
    public bool enable;

    [Tooltip("运行中切换开关的按键。")]
    public KeyCode toggleKey = KeyCode.F10;

    [Tooltip("黑边区域是否再避开刘海/挖孔。")]
    public bool useSafeArea = true;

    [Header("比例")]
    public Vector2 targetAspect = new Vector2(16f, 9f);

    [Header("诊断")]
    public bool logInfo = true;

    [Header("调试")]
    [Tooltip("在 Development Build 或编辑器里，屏幕上显示一个切换按钮。\n" +
             "手机上没有键盘，用这个按钮就能实时对比「黑边 / 铺满」两种效果，不用重新打包。")]
    public bool showDebugToggle = true;

    private GameObject debugButton;
    private TMPro.TextMeshProUGUI debugLabel;
    private Canvas debugCanvas;

    private const string ViewportName = "[PillarboxViewport]";
    private const string BarCanvasName = "[PillarboxBars]";

    private Canvas barCanvas;
    private readonly List<Canvas> handledCanvases = new List<Canvas>();
    private readonly HashSet<Canvas> appliedCanvases = new HashSet<Canvas>();
    private int lastWidth;
    private int lastHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var host = new GameObject("[PillarboxController]");
        DontDestroyOnLoad(host);
        host.AddComponent<PillarboxController>();
    }

    private void Awake()
    {
        Instance = this;

        UIStyleSettings settings = UIStyle.Settings;

        if (settings != null)
        {
            enable = settings.enablePillarbox;
            useSafeArea = settings.pillarboxUseSafeArea;

            if (settings.pillarboxAspect.x > 0f && settings.pillarboxAspect.y > 0f)
                targetAspect = settings.pillarboxAspect;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        Apply();

        if (logInfo)
            Debug.Log($"[黑边] 初始状态：{(enable ? "开启" : "关闭")}，目标比例 {targetAspect.x:0.#}:{targetAspect.y:0.#}，按 {toggleKey} 可切换");

        // 始终创建（不再要求 Development Build），
        // 这样普通包里也能实时对比「黑边 / 铺满」，不用为验证专门打开发版。
        if (showDebugToggle)
            CreateDebugToggle();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            enable = !enable;
            Apply();

            if (logInfo)
                Debug.Log($"[黑边] 已{(enable ? "开启" : "关闭")}（按 {toggleKey} 切换）");
        }

        if (Screen.width != lastWidth || Screen.height != lastHeight)
            Apply();
    }

    /// <summary>重新计算并应用。分辨率变化、场景切换、开关切换时调用。</summary>
    public void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        CollectCanvases();

        if (!enable)
        {
            RestoreCanvases();
            HideBars();
            return;
        }

        Rect viewport = ComputeViewport(useSafeArea);

        foreach (Canvas canvas in handledCanvases)
        {
            if (canvas == null || canvas == barCanvas)
                continue;

            ApplyToCanvas(canvas, viewport);
        }

        DrawBars(viewport);
    }

    // ---------------------------------------------------------------- 调试按钮

    /// <summary>
    /// 屏幕上的调试切换按钮（仅 Development Build / 编辑器）。
    /// 手机没有键盘，用这个按钮就能实时对比「黑边 / 铺满」，不用重新打包。
    /// </summary>
    private void CreateDebugToggle()
    {
        if (debugButton != null)
            return;

        var host = new GameObject("[PillarboxDebugToggle]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        host.transform.SetParent(transform, false);

        var canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32500;   // 比黑边更高，保证点得到
        debugCanvas = canvas;

        var scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        var btnGO = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = btnGO.GetComponent<RectTransform>();
        rect.SetParent(host.transform, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(300f, 70f);
        rect.anchoredPosition = new Vector2(0f, -14f);

        var image = btnGO.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.7f);

        var button = btnGO.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(ToggleByButton);

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.SetParent(btnGO.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        debugLabel = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        debugLabel.font = UIStyle.Font;          // 用项目的中文字体，否则中文是方块
        debugLabel.alignment = TMPro.TextAlignmentOptions.Center;
        debugLabel.color = Color.white;
        debugLabel.fontSize = 30f;
        debugLabel.raycastTarget = false;

        UpdateDebugLabel();

        debugButton = host;
    }

    private void ToggleByButton()
    {
        enable = !enable;
        Apply();
        UpdateDebugLabel();

        if (logInfo)
            Debug.Log($"[黑边] 按钮切换为：{(enable ? "开启" : "关闭")}");
    }

    private void UpdateDebugLabel()
    {
        if (debugLabel == null)
            return;

        // 带上版本号，方便确认手机上跑的是不是最新打的包
        string mode = enable ? "黑边" : "铺满";
        debugLabel.text = $"适配：{mode}（点击切换）\nv{Application.version}";
    }

    // ---------------------------------------------------------------- 计算

    /// <summary>算出目标比例区域（含安全区）在屏幕中的归一化矩形。</summary>
    private Rect ComputeViewport(bool respectSafeArea)
    {
        float screenW = Mathf.Max(1, Screen.width);
        float screenH = Mathf.Max(1, Screen.height);

        float want = targetAspect.x / Mathf.Max(0.0001f, targetAspect.y);
        float have = screenW / screenH;

        float w = 1f;
        float h = 1f;

        if (have > want)
            w = want / have;   // 屏幕更宽 → 两侧留黑边
        else
            h = have / want;   // 屏幕更高 → 上下留黑边

        float minX = (1f - w) * 0.5f;
        float minY = (1f - h) * 0.5f;
        Rect rect = new Rect(minX, minY, w, h);

        if (!respectSafeArea)
            return rect;

        // 与屏幕安全区取交集
        Rect safe = Screen.safeArea;
        float sx0 = safe.x / screenW;
        float sy0 = safe.y / screenH;
        float sx1 = (safe.x + safe.width) / screenW;
        float sy1 = (safe.y + safe.height) / screenH;

        float x0 = Mathf.Max(rect.xMin, sx0);
        float y0 = Mathf.Max(rect.yMin, sy0);
        float x1 = Mathf.Min(rect.xMax, sx1);
        float y1 = Mathf.Min(rect.yMax, sy1);

        if (x1 <= x0 || y1 <= y0)
            return rect;

        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    // ---------------------------------------------------------------- 应用

    private void CollectCanvases()
    {
        handledCanvases.Clear();

        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas == null || canvas == barCanvas || canvas == debugCanvas)
                continue;

            // 只处理根 Canvas（嵌套 Canvas 跟随父级一起被移入容器）
            if (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                continue;

            handledCanvases.Add(canvas);
        }
    }

    private void ApplyToCanvas(Canvas canvas, Rect viewport)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null)
            return;

        RectTransform holder = GetOrCreateViewport(canvas.transform);

        // 容器铺在目标区域内
        holder.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
        holder.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
        holder.offsetMin = Vector2.zero;
        holder.offsetMax = Vector2.zero;

        // 首次：把已有子物体整体移进容器。
        // worldPositionStays = false → 保留各自的锚点/偏移语义，
        // 角落对齐的元素会自动贴合新边界。
        if (!appliedCanvases.Contains(canvas))
        {
            List<Transform> children = new List<Transform>();

            foreach (Transform child in canvas.transform)
            {
                if (child != holder && child.name != BarCanvasName)
                    children.Add(child);
            }

            foreach (Transform child in children)
                child.SetParent(holder, false);

            appliedCanvases.Add(canvas);
        }

        // 已经用容器保证安全区了，避免 SafeAreaFitter 再缩一次
        if (useSafeArea)
            DisableSafeAreaFitters(holder);
    }

    private RectTransform GetOrCreateViewport(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find(ViewportName);

        if (existing is RectTransform found)
            return found;

        var host = new GameObject(ViewportName, typeof(RectTransform));
        var rect = host.GetComponent<RectTransform>();
        rect.SetParent(canvasTransform, false);
        rect.SetAsFirstSibling();
        rect.localScale = Vector3.one;

        return rect;
    }

    private static void DisableSafeAreaFitters(RectTransform root)
    {
        foreach (SafeAreaFitter fitter in root.GetComponentsInChildren<SafeAreaFitter>(true))
        {
            if (fitter.enabled)
                fitter.enabled = false;
        }
    }

    /// <summary>关闭黑边时把子物体搬回 Canvas 根节点，并移除容器。</summary>
    private void RestoreCanvases()
    {
        if (appliedCanvases.Count == 0)
            return;

        foreach (Canvas canvas in handledCanvases)
        {
            if (canvas == null)
                continue;

            Transform holder = canvas.transform.Find(ViewportName);

            if (holder == null)
                continue;

            List<Transform> children = new List<Transform>();

            foreach (Transform child in holder)
                children.Add(child);

            foreach (Transform child in children)
                child.SetParent(canvas.transform, false);

            Destroy(holder.gameObject);
        }

        appliedCanvases.Clear();
    }

    // ---------------------------------------------------------------- 黑边

    private void DrawBars(Rect viewport)
    {
        EnsureBarCanvas();

        if (barCanvas == null)
            return;

        barCanvas.gameObject.SetActive(true);

        SetBar("Bar_Left", 0f, 0f, viewport.xMin, 1f);
        SetBar("Bar_Right", viewport.xMax, 0f, 1f, 1f);
        SetBar("Bar_Bottom", viewport.xMin, 0f, viewport.xMax, viewport.yMin);
        SetBar("Bar_Top", viewport.xMin, viewport.yMax, viewport.xMax, 1f);
    }

    private void HideBars()
    {
        if (barCanvas != null)
            barCanvas.gameObject.SetActive(false);
    }

    private void EnsureBarCanvas()
    {
        if (barCanvas != null)
            return;

        var host = new GameObject(BarCanvasName, typeof(Canvas), typeof(CanvasScaler));
        host.transform.SetParent(transform, false);

        barCanvas = host.GetComponent<Canvas>();
        barCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        barCanvas.sortingOrder = 32000;   // 盖住一切

        var scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
    }

    private void SetBar(string name, float xMin, float yMin, float xMax, float yMax)
    {
        Transform existing = barCanvas.transform.Find(name);
        RectTransform rect;

        if (existing is RectTransform found)
        {
            rect = found;
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(barCanvas.transform, false);

            var image = go.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }

        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 宽或高为 0 的边不需要显示
        bool visible = (xMax - xMin) > 0.0001f && (yMax - yMin) > 0.0001f;
        rect.gameObject.SetActive(visible);
    }
}
