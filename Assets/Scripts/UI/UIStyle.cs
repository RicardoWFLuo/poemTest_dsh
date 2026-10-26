using TMPro;
using UnityEngine;

/// <summary>
/// 运行时样式解析：优先用 Resources 里的 UIStyleSettings，取不到就退回 TMP 默认字体。
/// </summary>
public static class UIStyle
{
    private const string SettingsResourcePath = "UIStyleSettings";

    private static UIStyleSettings settings;
    private static bool loadAttempted;
    private static bool warnedAboutFont;

    public static UIStyleSettings Settings
    {
        get
        {
            if (!loadAttempted)
            {
                loadAttempted = true;
                settings = Resources.Load<UIStyleSettings>(SettingsResourcePath);
            }

            return settings;
        }
    }

    /// <summary>界面用的 TMP 字体。没有配置时退回默认字体（会显示不出中文，同时给出警告）。</summary>
    public static TMP_FontAsset Font
    {
        get
        {
            TMP_FontAsset configured = Settings != null ? Settings.fontAsset : null;

            if (configured != null)
                return configured;

            if (!warnedAboutFont)
            {
                warnedAboutFont = true;
                Debug.LogWarning(
                    "[UIStyle] 没有配置中文字体，界面会显示成方块。\n" +
                    "请执行菜单：Tools → 诗词数据 → 创建 UI 设置资源，" +
                    "然后在生成的 Assets/Resources/UIStyleSettings.asset 里把 Font Asset 设为 zpix SDF 或 unifont-17.0。");
            }

            return TMP_Settings.defaultFontAsset;
        }
    }

    public static Color TextColor => Settings != null ? Settings.textColor : Color.white;
    public static Color AccentColor => Settings != null ? Settings.accentColor : Color.yellow;
    public static Color PanelColor => Settings != null ? Settings.panelColor : new Color(0.09f, 0.08f, 0.12f, 0.96f);
    public static Color ButtonColor => Settings != null ? Settings.buttonColor : new Color(0.22f, 0.20f, 0.28f, 1f);
    public static Color ButtonHighlightColor => Settings != null ? Settings.buttonHighlightColor : new Color(0.34f, 0.30f, 0.44f, 1f);

    /// <summary>重新读取设置（改了资源之后想立刻生效可以调用）。</summary>
    public static void Invalidate()
    {
        loadAttempted = false;
        settings = null;
        warnedAboutFont = false;
    }

    /// <summary>
    /// 每次进入 Play 都清掉缓存。
    ///
    /// 为什么必须这样做：Unity 若关闭了「域重载（Domain Reload）」，
    /// 静态字段会跨 Play 会话保留 —— 上一局读到的 settings 实例（可能还是空的）
    /// 会一直被复用，导致你在编辑器里改了 UIStyleSettings 却完全看不到效果。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Invalidate();
    }
}
