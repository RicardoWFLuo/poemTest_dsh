using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public TMP_Text healthText;
    public Animator healthTextAnim;
    public DeathManager deathManager;

    private bool isDead = false;

    public bool IsDead => isDead;

    private void Start()
    {
        isDead = false;
        UpdateHealthText();
    }

    public void ChangeHealth(int amount)
    {
        if (isDead || StatsManager.Instance == null)
            return;

        StatsManager.Instance.currentHealth =
            Mathf.Clamp(StatsManager.Instance.currentHealth + amount, 0, StatsManager.Instance.maxHealth);

        if (healthTextAnim != null)
            healthTextAnim.Play("TextUpdate");

        UpdateHealthText();

        if (StatsManager.Instance.currentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthText()
    {
        if (healthText != null && StatsManager.Instance != null)
            healthText.text = "HP: " + StatsManager.Instance.currentHealth + " / " + StatsManager.Instance.maxHealth;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        StatsManager.Instance.currentHealth = 0;
        UpdateHealthText();

        if (deathManager != null)
            deathManager.ShowDeathScreen();
    }

    public void Revive()
    {
        isDead = false;
        gameObject.SetActive(true);

        if (StatsManager.Instance != null)
            StatsManager.Instance.currentHealth = StatsManager.Instance.maxHealth;

        UpdateHealthText();
    }
}
