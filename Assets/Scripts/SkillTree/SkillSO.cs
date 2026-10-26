using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "SkillTree/Skill")]
public class SkillSO : ScriptableObject, IIdentifiedAsset
{
    [Header("存档标识（自动填充，勿手改）")]
    [Tooltip("存档与资源反查用的稳定 ID。由菜单 Tools → 诗词数据 → 填充资源 ID 自动生成；填写后不要再改，否则老存档会失效。")]
    public string id;

    public string Id => id;

    public string skillName;
    public int maxLevel;

    [Tooltip("每升一级带来的数值增量。留空或填 0 时按 1 处理。")]
    public int valuePerLevel = 1;

    public Sprite skillIcon;

}
