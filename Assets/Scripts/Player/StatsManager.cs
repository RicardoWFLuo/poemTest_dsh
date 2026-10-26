using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance;
    public StatsUI statsUI;
    public TMP_Text healthText;

    [Header("Combat Stats")]
    public int damage;
    public float weaponRange;
    public float knockBackForce;
    public float knockBackTime;
    public float stunTime;

    [Header("Movement Stats")]
    public float speed;

    [Header("Health Stats")]
    public int maxHealth;
    public int currentHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 只销毁重复组件产生的对象；注意这里销毁的是整个 GameObject，
            // 因此保持 StatsManager 独占一个 GameObject 的约定。
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        RefreshHealthText();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        RefreshHealthText();
    }

    public void UpdateMaxHealth(int amount)
    {
        maxHealth = Mathf.Max(1, maxHealth + amount);

        // 最大值降低时（临时增益到期）把当前血量一起夹紧
        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        RefreshHealthText();
    }

    public void UpdateHealth(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        RefreshHealthText();
    }

    public void UpdateDamage(int amount)
    {
        damage = Mathf.Max(0, damage + amount);

        if (statsUI != null)
            statsUI.UpdateAllStats();
    }

    public void UpdateSpeed(int amount)
    {
        speed = Mathf.Max(0f, speed + amount);

        if (statsUI != null)
            statsUI.UpdateAllStats();
    }

    /// <summary>统一刷新血条文本，避免各处重复拼接格式。</summary>
    public void RefreshHealthText()
    {
        if (healthText != null)
            healthText.text = "HP:" + currentHealth + "/" + maxHealth;
    }
}
