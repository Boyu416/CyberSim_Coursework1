using UnityEngine;

public class TargetHealth : MonoBehaviour
{
    public static event System.Action<TargetHealth> TargetKilled;

    public float maxHealth = 100f;

    private float currentHealth;
    private bool isDead;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
        {
            return;
        }

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Health left: {currentHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        Debug.Log($"{gameObject.name} destroyed.");
        TargetKilled?.Invoke(this);
        Destroy(gameObject);
    }
}
