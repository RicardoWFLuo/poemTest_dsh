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
}
