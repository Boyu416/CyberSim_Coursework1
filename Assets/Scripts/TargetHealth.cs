using UnityEngine;

public class TargetHealth : MonoBehaviour
{
    public static event System.Action<TargetHealth> TargetKilled;

    public float maxHealth = 100f;
    public bool forceStandardBotHealth = true;
    public int bodyHitsToKill = 2;

    private float currentHealth;
    private int bodyHitsTaken;
    private bool isDead;

    void Awake()
    {
        InitializeHealth();
    }

    void OnEnable()
    {
        InitializeHealth();
    }

    void Start()
    {
        InitializeHealth();
    }

    void InitializeHealth()
    {
        if (isDead)
        {
            return;
        }

        if (forceStandardBotHealth)
        {
            maxHealth = 100f;
        }

        currentHealth = maxHealth;
        bodyHitsTaken = 0;
    }

    public void ResetHealth(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = maxHealth;
        bodyHitsTaken = 0;
        isDead = false;
    }

    public void TakeDamage(float damage, bool headshot = false)
    {
        if (isDead)
        {
            return;
        }

        if (headshot)
        {
            Debug.Log($"{gameObject.name} took a headshot.");
            Die();
            return;
        }

        bodyHitsTaken++;
        currentHealth = Mathf.Max(0f, maxHealth * (1f - (float)bodyHitsTaken / bodyHitsToKill));
        Debug.Log($"{gameObject.name} body hit {bodyHitsTaken}/{bodyHitsToKill}. Health left: {currentHealth}");

        if (bodyHitsTaken >= bodyHitsToKill)
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
