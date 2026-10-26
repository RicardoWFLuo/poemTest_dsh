using UnityEngine;

/// <summary>
/// 统一的暂停管理。
///
/// 背景：属性面板和技能树各自直接读写 Time.timeScale，
/// 于是"打开属性(timeScale=0) → 关闭技能树(timeScale=1)"会让游戏在属性面板
/// 还开着的时候恢复运行。这里用引用计数集中管理，谁打开谁 +1，谁关闭谁 -1。
/// </summary>
public static class PauseController
{
    private static int pauseRequests;

    public static bool IsPaused => pauseRequests > 0;

    /// <summary>请求暂停（打开一个面板）。</summary>
    public static void RequestPause()
    {
        pauseRequests++;
        Apply();
    }

    /// <summary>释放暂停（关闭一个面板）。</summary>
    public static void ReleasePause()
    {
        pauseRequests = Mathf.Max(0, pauseRequests - 1);
        Apply();
    }

    /// <summary>强制清空所有暂停请求（复活、切场景等场景使用）。</summary>
    public static void ResetAll()
    {
        pauseRequests = 0;
        Apply();
    }

    private static void Apply()
    {
        Time.timeScale = pauseRequests > 0 ? 0f : 1f;
    }

    // 关闭 Unity 的"域重载"选项时静态字段会跨播放会话残留，这里在进入播放时重置一次。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        pauseRequests = 0;
        Time.timeScale = 1f;
    }
}
