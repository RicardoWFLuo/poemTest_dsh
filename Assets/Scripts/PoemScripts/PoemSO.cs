using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Poem")]
public class PoemSO : ScriptableObject, IIdentifiedAsset
{
    [Header("存档标识（自动填充，勿手改）")]
    [Tooltip("存档与资源反查用的稳定 ID。由菜单 Tools → 诗词数据 → 填充资源 ID 自动生成；填写后不要再改，否则老存档会失效。")]
    public string id;

    public string Id => id;

    [Header("诗词信息")]
    public string poemTitle;
    public string poet;

    [TextArea(5, 10)]
    public string poemContent;

    [Header("AI Generate")]
    public string promptText;
    public string promptImage;

    [Header("内置内容（无 API Key 或生成失败时使用）")]
    [Tooltip("预置的诗词释义。未配置 AI 密钥或请求失败时直接显示这一段，并在界面上标注为「内置内容」。")]
    [TextArea(5, 12)]
    public string builtinExplanation;

    [Tooltip("预置的诗词插画。未配置 AI 密钥或生图失败时显示，并标注为「内置内容」。")]
    public Sprite builtinIllustration;
}
