using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "New Item")]
public class ItemSO : ScriptableObject, IIdentifiedAsset
{
    [Header("存档标识（自动填充，勿手改）")]
    [Tooltip("存档与资源反查用的稳定 ID。由菜单 Tools → 诗词数据 → 填充资源 ID 自动生成；填写后不要再改，否则老存档会失效。")]
    public string id;

    public string Id => id;

    public string itemName;

    [TextArea]
    public string itemDescription;
    public Sprite icon;

    public bool isGold;
    public bool isEXP;
    public int stackSize = 3;

    [Header("使用设置")]
    [Tooltip("是否可以被消耗（使用后数量 -1）。任务收集类道具请设为 false，" +
             "否则玩家把任务道具当药吃掉会导致任务无法完成。")]
    public bool isConsumable = true;

    [Header("Stats")]
    public int currentHealth;
    public int maxHealth;
    public int speed;
    public int damage;

    [Header("For Temporary Items")]
    public float duration;
}
