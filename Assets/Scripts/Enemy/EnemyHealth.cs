using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int expReward = 3;

    public delegate void MonsterDefeated(int exp);
    public static event MonsterDefeated OnMonsterDefeated;

    public int currenyHealth;
    public int maxHealth;

    private bool isDead;

    private void Start()
    {
        currenyHealth = maxHealth;
    }

    public void ChangeHealth(int amount)
    {
        // 已经死亡的敌人不再结算：Destroy 是延迟到帧末执行的，
        // 同一帧内被近战和箭矢同时命中会重复触发击败事件、重复给经验。
        if (isDead)
            return;

        currenyHealth += amount;

        if (currenyHealth > maxHealth)
        {
            currenyHealth = maxHealth;
            return;
        }

        if (currenyHealth <= 0)
        {
            currenyHealth = 0;
            isDead = true;

            // 静态事件在没有任何订阅者时是 null，直接 Invoke 会抛 NullReferenceException
            OnMonsterDefeated?.Invoke(expReward);
            Destroy(gameObject);
        }
    }
}
