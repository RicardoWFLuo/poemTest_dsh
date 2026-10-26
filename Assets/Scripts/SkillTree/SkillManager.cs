using UnityEngine;

/// <summary>
/// 技能点消费后的实际数值结算。
/// 历史版本只实现了 "Max Health Boost"，另外两个技能（攻击、速度）
/// 升级后只会打印"未知技能"，玩家花掉的技能点完全没有效果。
/// </summary>
public class SkillManager : MonoBehaviour
{
    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointSpent;
    }

    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointSpent;
    }

    private void HandleAbilityPointSpent(SkillSlot slot)
    {
        if (slot == null || slot.skillSO == null || StatsManager.Instance == null)
            return;

        string skillName = slot.skillSO.skillName;
        int value = slot.skillSO.valuePerLevel > 0 ? slot.skillSO.valuePerLevel : 1;

        switch (skillName)
        {
            case "Max Health Boost":
                StatsManager.Instance.UpdateMaxHealth(value);
                break;

            case "Max Attack Boost":
                StatsManager.Instance.UpdateDamage(value);
                break;

            case "Max Speed Boost":
                StatsManager.Instance.UpdateSpeed(value);
                break;

            default:
                Debug.LogWarning($"未知技能「{skillName}」，请在 SkillManager 中补充对应的效果实现。");
                break;
        }
    }
}
