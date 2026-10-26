using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UseItem : MonoBehaviour
{
    public void ApplyItemEffects(ItemSO itemSO)
    {
        if (itemSO == null || StatsManager.Instance == null)
            return;

        if (itemSO.maxHealth > 0)
        {
            StatsManager.Instance.UpdateMaxHealth(itemSO.maxHealth);
        }
        if (itemSO.currentHealth > 0)
        {
            StatsManager.Instance.UpdateHealth(itemSO.currentHealth);
        }
        if (itemSO.speed > 0)
        {
            StatsManager.Instance.UpdateSpeed(itemSO.speed);
        }
        // damage 之前被完全忽略了：肉排上配置的"伤害+1"其实从来没生效过。
        if (itemSO.damage > 0)
        {
            StatsManager.Instance.UpdateDamage(itemSO.damage);
        }
        if (itemSO.duration > 0)
        {
            StartCoroutine(EffectTimer(itemSO, itemSO.duration));
        }
    }

    /// <summary>
    /// 临时增益到期后逐项回滚。
    /// 注意：回滚 maxHealth 时会由 StatsManager 负责把 currentHealth 夹回上限，
    /// 因此这里不再额外扣血，避免玩家血量被扣成负数。
    /// </summary>
    private IEnumerator EffectTimer(ItemSO itemSO, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (itemSO == null || StatsManager.Instance == null)
            yield break;

        if (itemSO.maxHealth > 0)
        {
            StatsManager.Instance.UpdateMaxHealth(-itemSO.maxHealth);
        }
        if (itemSO.speed > 0)
        {
            StatsManager.Instance.UpdateSpeed(-itemSO.speed);
        }
        if (itemSO.damage > 0)
        {
            StatsManager.Instance.UpdateDamage(-itemSO.damage);
        }
    }
}
