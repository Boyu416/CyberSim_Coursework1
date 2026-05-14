using TMPro;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public System.Action PlayerDied;

    public int maxHealth = 100;
    public TextMeshProUGUI hpText;

    private int currentHealth;
    private bool isDead;

    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;

    void Awake()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        UpdateHealthText();
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthText();

        if (currentHealth <= 0)
        {
            isDead = true;
            PlayerDied?.Invoke();
        }
    }

    public void SetHealthText(TextMeshProUGUI text)
    {
        hpText = text;
        UpdateHealthText();
    }

    void UpdateHealthText()
    {
        if (hpText != null)
        {
            TrainingUiStyle.SetMessage(hpText, "HP: " + currentHealth);
        }
    }
}
