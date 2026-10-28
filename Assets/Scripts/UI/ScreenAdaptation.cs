using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕适配统一处理（自动创建，无需挂到场景）。
///
/// 解决的问题：
///   1. 不同宽高比下 UI 缩放不一致
///      —— 原来的 CanvasScaler 一律 match=0.5，
///         在 20:9 这种细长屏上会让「垂直可用的参考高度」小于 1080，
///         顶部血条和底部按钮排被挤到一起。
///         这里按屏幕宽高比自动选择匹配方向。
///   2. 高刷新率屏耗电 —— 把目标帧率锁到 60。
///   3. 没有诊断信息 —— 启动时打印分辨率 / 宽高比 / 安全区，方便定位问题。
///
/// 可调项都在下面的公开字段里。
/// </summary>
public class ScreenAdaptation : MonoBehaviour
{
    [Header("帧率")]
    [Tooltip("目标帧率。0 = 不限制。高刷屏建议锁 60，省电且发热更低。")]
    public int targetFrameRate = 60;

    [Header("自适应缩放")]
    [Tooltip("开启后按屏幕宽高比自动调整所有 CanvasScaler 的 Match 值。")]
    public bool adaptiveCanvasMatch = true;

    [Tooltip("宽高比大于此值时视为「细长屏」（如 20:9 手机），改为按高度匹配。")]
    public float tallAspectThreshold = 1.85f;

    [Tooltip("宽高比小于此值时视为「宽屏」（如平板横屏），改为按宽度匹配。")]
    public float wideAspectThreshold = 1.6f;

    [Header("诊断")]
    [Tooltip("启动时在 Console 打印屏幕参数，排查适配问题时很有用。")]
    public bool logScreenInfo = true;

    private readonly List<CanvasScaler> scalers = new List<CanvasScaler>();
    private int lastWidth;
    private int lastHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var host = new GameObject("[ScreenAdaptation]");
        DontDestroyOnLoad(host);
        host.AddComponent<ScreenAdaptation>();
    }

    private void Awake()
    {
        if (targetFrameRate > 0)
            Application.targetFrameRate = targetFrameRate;

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        Rescan();
        Apply();

        if (logScreenInfo)
            LogScreenInfo();
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 场景加载完后重新扫描。
    /// 必须这样做：本组件在 BeforeSceneLoad 就创建了，
    /// 那时场景里的 Canvas 还没实例化，直接扫是扫不到的。
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Rescan();
        Apply();
    }

    private void Update()
    {
        if (Screen.width == lastWidth && Screen.height == lastHeight)
            return;

        Rescan();
        Apply();

        if (logScreenInfo)
            LogScreenInfo();
    }

    /// <summary>重新收集场景里的 CanvasScaler（切场景后会变化）。</summary>
    private void Rescan()
    {
        scalers.Clear();
        scalers.AddRange(FindObjectsOfType<CanvasScaler>(true));
    }

    private void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        if (!adaptiveCanvasMatch || Screen.height <= 0)
            return;

        float aspect = (float)Screen.width / Screen.height;
        float match = GetMatchForAspect(aspect);

        int changed = 0;

        foreach (CanvasScaler scaler in scalers)
        {
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                continue;

            if (!Mathf.Approximately(scaler.matchWidthOrHeight, match))
            {
                scaler.matchWidthOrHeight = match;
                changed++;
            }
        }

        if (logScreenInfo && changed > 0)
            Debug.Log($"[屏幕适配] 宽高比 {aspect:0.00}，已把 {changed} 个 CanvasScaler 的 Match 调整为 {match:0.0}");
    }

    /// <summary>
    /// 按宽高比决定 Match：
    ///   细长屏（20:9 手机）→ 1（按高度），保证 1080 的垂直排版不变形
    ///   宽屏（平板横屏）   → 0（按宽度），保证横向铺满
    ///   其它（接近 16:9）  → 0.5
    /// </summary>
    private float GetMatchForAspect(float aspect)
    {
        if (aspect >= tallAspectThreshold)
            return 1f;

        if (aspect <= wideAspectThreshold)
            return 0f;

        return 0.5f;
    }

    private void LogScreenInfo()
    {
        Rect safe = Screen.safeArea;
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0f;

        string orientation = Screen.width > Screen.height ? "横屏" : "竖屏";

        Debug.Log(
            $"[屏幕适配] {Screen.width} x {Screen.height}（{orientation}，宽高比 {aspect:0.00}）\n" +
            $"          安全区 {safe.x:0},{safe.y:0} / {safe.width:0} x {safe.height:0}\n" +
            $"          目标帧率 {Application.targetFrameRate}，DPI {Screen.dpi:0}\n" +
            $"          适配策略 match={GetMatchForAspect(aspect):0.0}" +
            (TargetIsTall(aspect) ? "（细长屏：按高度匹配）" : ""));
    }

    private bool TargetIsTall(float aspect) => aspect >= tallAspectThreshold;
}
