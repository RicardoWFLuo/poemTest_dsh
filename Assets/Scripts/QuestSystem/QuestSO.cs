using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "QuestSO", menuName = "QuestSO")]
public class QuestSO : ScriptableObject, IIdentifiedAsset
{
    [Header("存档标识（自动填充，勿手改）")]
    [Tooltip("存档与资源反查用的稳定 ID。由菜单 Tools → 诗词数据 → 填充资源 ID 自动生成；填写后不要再改，否则老存档会失效。")]
    public string id;

    public string Id => id;

    public string questName;
    [TextArea] public string questDescription;
    public int questLevel;

    public List <QuestObjective> objectives;
    public List <QuestReward> rewards;
}

[System.Serializable]
public class QuestObjective
{
    public string description;

    // 原字段名拼写错误（targrt），用 FormerlySerializedAs 保证已有 .asset 数据不丢失。
    [FormerlySerializedAs("targrt")]
    [SerializeField] private Object target;

    public ItemSO targetItem => target as ItemSO;
    public ActorSO targetNPC => target as ActorSO;
    public LocationSO targetLocation => target as LocationSO;

    public int requiredAmount;
}

[System.Serializable]
public class QuestReward
{
    public ItemSO itemSO;
    public int quantity;
}
