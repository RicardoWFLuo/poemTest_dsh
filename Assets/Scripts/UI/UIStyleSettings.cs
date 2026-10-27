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

    [Header("菜单界面贴图")]
    [Tooltip("开局标题界面的全屏封面（例如 Resources/tt.png）。留空则自动尝试 Resources/tt。")]
    public Sprite menuCover;

    [Tooltip("封面是否压暗。开启会在封面上盖一层半透明黑，让文字更好读。")]
    public bool dimCover = true;

    [Tooltip("标题界面背后的横幅（Banner_Vertical）。留空则用纯色面板。")]
    public Sprite menuTitleBanner;

    [Tooltip("存档界面背后的横幅（Banner_Horizontal）。留空则用纯色面板。")]
    public Sprite menuSlotBanner;

    [Tooltip("标题界面按钮底图（Button_Blue_3Slides）。留空则用纯色按钮。")]
    public Sprite menuButtonSprite;

    [Tooltip("另外两个界面（游戏菜单 / 存档列表）的按钮底图。\n" +
             "留空则沿用标题界面的那张。")]
    public Sprite menuButtonSpriteAlt;

    [Tooltip("游戏菜单界面（Esc）背后的横幅。留空则用纯色面板。")]
    public Sprite menuScreenBanner;

    [Header("菜单界面布局（数值可在 Inspector 直接改）")]
    [Tooltip("是否在运行时用下面的数值强制排版。\n" +
             "关闭（推荐）= 以预制体 / Hierarchy 里的排版为准，你手动拖的位置才会生效；\n" +
             "开启 = 每次运行都用这里的数值覆盖。")]
    public bool applyLayoutAtRuntime = false;

    [Tooltip("标题界面面板尺寸。")]
    public Vector2 titlePanelSize = new Vector2(780f, 640f);

    [Tooltip("标题界面面板位置（相对屏幕中心）。")]
    public Vector2 titlePanelOffset = Vector2.zero;

    [Tooltip("游戏菜单面板尺寸。")]
    public Vector2 menuPanelSize = new Vector2(720f, 600f);

    [Tooltip("游戏菜单面板位置。")]
    public Vector2 menuPanelOffset = Vector2.zero;

    [Tooltip("存档界面面板尺寸。")]
    public Vector2 slotPanelSize = new Vector2(940f, 560f);

    [Tooltip("存档界面面板位置。")]
    public Vector2 slotPanelOffset = Vector2.zero;

    [Tooltip("标题界面按钮尺寸。")]
    public Vector2 titleButtonSize = new Vector2(440f, 88f);

    [Tooltip("标题界面按钮之间的垂直间距。")]
    public float titleButtonSpacing = 110f;

    [Tooltip("游戏菜单按钮尺寸。")]
    public Vector2 menuPanelButtonSize = new Vector2(460f, 84f);

    [Tooltip("游戏菜单按钮之间的垂直间距。")]
    public float menuPanelButtonSpacing = 100f;

    [Tooltip("存档按钮尺寸。")]
    public Vector2 slotButtonSize = new Vector2(860f, 76f);

    [Tooltip("存档按钮之间的垂直间距。")]
    public float slotButtonSpacing = 92f;

    [Tooltip("菜单按钮插入左下角按钮排时的间距。")]
    public float menuButtonRowSpacing = 8f;

    [Header("菜单界面颜色")]
    [Tooltip("横幅上的文字颜色（默认黑色）。")]
    public Color menuTextColor = Color.black;

    [Tooltip("存档按钮的文字颜色（默认白色）。")]
    public Color slotButtonTextColor = Color.white;

    [Tooltip("存档按钮的底色（保持原来的灰色，不套贴图）。")]
    public Color slotButtonColor = new Color(0.30f, 0.30f, 0.36f, 1f);

    [Header("AI 助手")]
    [Tooltip("右下角助手形象。随便一张 Sprite 都行；留空则显示成一块色板（功能不受影响）。")]
    public Sprite assistantPortrait;

    [Tooltip("助手画像外框大小。对话系统的 NPC 头像是 450×450。")]
    public Vector2 assistantPortraitSize = new Vector2(450f, 450f);

    [Tooltip("框内角色的放大倍数。\n" +
             "1 = 整个人都塞进框里（很小）；\n" +
             "2 = 只露头和上半身（同对话系统效果）；\n" +
             "2.5-3 = 只露头部特写。")]
    public float assistantPortraitScale = 2f;

    [Tooltip("框内角色位置微调（像素）。\n" +
             "x 正数=右移；y 负数=角色下移（头顶留白变多）。")]
    public Vector2 assistantPortraitOffset = Vector2.zero;

    [Tooltip("助手的序列帧（可选）。填了就会循环播放，让画像真正动起来。\n" +
             "建议只放一组同方向的循环帧（比如某一行左侧的 3 帧待机）。\n" +
             "留空则只显示静态图。")]
    public Sprite[] assistantFrames;

    [Tooltip("序列帧每秒播放多少帧。")]
    public float assistantFrameRate = 6f;

    [Tooltip("助手接口地址。默认直连 DeepSeek 官方 API。\n" +
             "百炼：https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions")]
    public string assistantApiUrl = "https://api.deepseek.com/chat/completions";

    [Tooltip("助手模型。DeepSeek 官方：deepseek-chat（快、不推理）或 deepseek-reasoner（先推理，更慢更贵）。\n" +
             "百炼：deepseek-v4.1-flash-1017 等。")]
    public string assistantModel = "deepseek-chat";

    [Tooltip("助手单次回复的最大 token。deepseek-chat 不推理，512 足够；\n" +
             "若改用 deepseek-reasoner 这类思考型模型，要给 2048 以上，否则正文会是空的。")]
    public int assistantMaxTokens = 512;

    [Tooltip("用哪个 Key 鉴权。DeepSeek 官方与百炼的 Key 不通用。")]
    public AssistantKeySource assistantKeySource = AssistantKeySource.DeepSeek;

    [Tooltip("仅用于临时测试。\n" +
             "⚠️ 不要在这里填真实 Key —— 这个资源会随项目提交到 Git！\n" +
             "请把 Key 填到 Assets/Resources/api_keys.json 的 deepseekKey 字段（该文件已被 .gitignore 忽略）。")]
    public string assistantApiKeyOverride;
}

/// <summary>助手使用哪个平台的密钥。</summary>
public enum AssistantKeySource
{
    /// <summary>DeepSeek 官方 api.deepseek.com</summary>
    DeepSeek,

    /// <summary>阿里云百炼（DashScope）</summary>
    BailianQwen,

    /// <summary>临时自定义（仅测试用，不要提交真实 Key）</summary>
    Custom
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
