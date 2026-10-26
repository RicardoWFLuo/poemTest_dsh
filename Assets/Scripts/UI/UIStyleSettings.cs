using TMPro;
using UnityEngine;

/// <summary>
/// UI 样式设置。
///
/// 为什么需要它：界面是用代码动态生成的，而中文必须用一个带中文字形的 TMP 字体。
/// Unity 自带的默认字体（LiberationSans SDF）没有中文，直接显示会是方块，
/// 所以这里把字体做成一个可配置的资源，由菜单工具一键创建并自动选中项目里的中文字体。
///
/// 资源位置：Assets/Resources/UIStyleSettings.asset
/// 创建方式：菜单 Tools → 诗词数据 → 创建 UI 设置资源
/// </summary>
[CreateAssetMenu(fileName = "UIStyleSettings", menuName = "诗词数据/UI 设置")]
public class UIStyleSettings : ScriptableObject
{
    [Header("字体")]
    [Tooltip("必须选一个包含中文字形的 TMP 字体，例如项目里的 zpix SDF 或 unifont-17.0。")]
    public TMP_FontAsset fontAsset;

    [Header("配色")]
    public Color panelColor = new Color(0.09f, 0.08f, 0.12f, 0.96f);
    public Color buttonColor = new Color(0.22f, 0.20f, 0.28f, 1f);
    public Color buttonHighlightColor = new Color(0.34f, 0.30f, 0.44f, 1f);
    public Color textColor = new Color(0.96f, 0.94f, 0.88f, 1f);
    public Color accentColor = new Color(0.85f, 0.72f, 0.42f, 1f);

    [Header("行为开关")]
    [Tooltip("启动时是否显示标题界面。关掉就一进游戏直接开始。")]
    public bool showTitleOnStart = true;

    [Tooltip("切场景时自动存档。验证阶段建议先关，避免掩盖「读档是否真的生效」。")]
    public bool autoSaveOnSceneChange = false;

    [Tooltip("退出游戏 / 切后台时自动存档。")]
    public bool autoSaveOnQuit = false;

    [Header("移动端：右上角菜单按钮")]
    [Tooltip("按钮尺寸（参考分辨率 1920×1080 下的像素）。手机上建议不小于 200×88。")]
    public Vector2 menuButtonSize = new Vector2(200f, 88f);

    [Tooltip("按钮距离屏幕边缘的距离。会自动避开刘海与安全区。")]
    public Vector2 menuButtonMargin = new Vector2(28f, 28f);

    [Tooltip("按钮放在哪个角。如果和现有移动端按键重叠，换一个角即可。")]
    public MenuButtonCorner menuButtonCorner = MenuButtonCorner.TopRight;
}

/// <summary>菜单按钮可以放的几个位置。</summary>
public enum MenuButtonCorner
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
    TopCenter
}
