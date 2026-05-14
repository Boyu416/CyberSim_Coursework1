using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Level2EnemyPatrol : MonoBehaviour
{
    [Header("Level Setup")]
    public int enemyCount = 15;
    public int killsRequired = 10;
    public float spawnMinDistance = 22f;
    public float spawnMaxDistance = 44f;

    [Header("Enemy Attack")]
    public float enemyAttackDistance = 10f;
    public float enemyAttackInterval = 1f;
    public int enemyDamage = 5;

    [Header("UI")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI centerPromptText;

    private readonly List<TargetHealth> enemies = new List<TargetHealth>();
    private BotSpawner botSpawner;
    private Transform player;
    private PlayerHealth playerHealth;
    private bool levelRunning;
    private bool gameOver;
    private bool levelComplete;
    private int kills;

    void Awake()
    {
        botSpawner = GetComponent<BotSpawner>();
    }

    void OnEnable()
    {
        TargetHealth.TargetKilled += HandleTargetKilled;
    }

    void OnDisable()
    {
        TargetHealth.TargetKilled -= HandleTargetKilled;

        if (playerHealth != null)
        {
            playerHealth.PlayerDied -= HandlePlayerDied;
        }
    }

    void Update()
    {
        if (gameOver && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }

        if (levelComplete && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            StartLevel3();
        }
    }

    public void BeginLevel()
    {
        if (botSpawner == null)
        {
            botSpawner = FindFirstObjectByType<BotSpawner>();
        }

        player = botSpawner != null ? botSpawner.player : null;

        if (player == null)
        {
            GameObject foundPlayer = GameObject.Find("PlayerCapsule");
            player = foundPlayer != null ? foundPlayer.transform : null;
        }

        EnsureUi();
        EnsurePlayerHealth();
        RestartLevel();
    }

    void RestartLevel()
    {
        ClearExistingEnemies();
        enemies.Clear();

        gameOver = false;
        levelComplete = false;
        levelRunning = true;
        kills = 0;

        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }

        ResetPlayerAmmo();
        ShowCenterPrompt("SURVIVE AND ELIMINATE ALL ENEMIES");
        SpawnEnemies();
        UpdateLevelText();
    }

    void SpawnEnemies()
    {
        if (botSpawner == null || player == null)
        {
            Debug.LogWarning("Level2EnemyPatrol needs a BotSpawner and Player.");
            return;
        }

        botSpawner.ClearSpawnedPositionHistory();
        botSpawner.useExpandedMapBounds = true;
        botSpawner.enforcePlayerDistanceInMapBounds = false;
        botSpawner.useNavMesh = true;
        botSpawner.navMeshSearchRadius = 6f;
        botSpawner.spawnHeightOffset = 0.03f;
        botSpawner.maxSpawnHeightAbovePlayer = 2.5f;
        botSpawner.maxAttemptsPerBot = 2000;
        botSpawner.relaxSpawnChecks = true;
        botSpawner.minDistanceBetweenBots = 2f;
        botSpawner.requiredFlatRadius = 0.45f;
        botSpawner.botClearanceRadius = 0.25f;
        botSpawner.maxFlatHeightDifference = 0.35f;
        botSpawner.minSurfaceUpDot = 0.9f;
        botSpawner.requireOpenGroundAroundBot = false;

        int attempts = 0;

        while (enemies.Count < enemyCount && attempts < enemyCount * 12)
        {
            attempts++;
            GameObject enemyObject = botSpawner.SpawnOneBot();

            if (enemyObject == null)
            {
                continue;
            }

            RegisterEnemy(enemyObject);
        }

        UpdateLevelText();
    }

    void TrySpawnEnemyWave(
        float minDistance,
        float maxDistance,
        float minBotSpacing,
        float flatRadius,
        float clearanceRadius,
        float maxHeightDifference,
        float minSurfaceDot,
        int maxWaveAttempts)
    {
        if (enemies.Count >= enemyCount)
        {
            return;
        }

        botSpawner.minDistanceFromPlayer = minDistance;
        botSpawner.maxDistanceFromPlayer = maxDistance;
        botSpawner.minDistanceBetweenBots = minBotSpacing;
        botSpawner.requiredFlatRadius = flatRadius;
        botSpawner.botClearanceRadius = clearanceRadius;
        botSpawner.maxFlatHeightDifference = maxHeightDifference;
        botSpawner.minSurfaceUpDot = minSurfaceDot;

        int spawnAttempts = 0;

        while (enemies.Count < enemyCount && spawnAttempts < maxWaveAttempts)
        {
            spawnAttempts++;
            GameObject enemyObject = botSpawner.SpawnOneBot();

            if (enemyObject == null)
            {
                continue;
            }

            RegisterEnemy(enemyObject);
        }
    }

    void RegisterEnemy(GameObject enemyObject)
    {
        TargetHealth targetHealth = enemyObject.GetComponent<TargetHealth>();

        if (targetHealth != null)
        {
            enemies.Add(targetHealth);
        }

        EnemyBotAI enemyAI = enemyObject.GetComponent<EnemyBotAI>();

        if (enemyAI == null)
        {
            enemyAI = enemyObject.AddComponent<EnemyBotAI>();
        }

        enemyAI.player = player;
        enemyAI.playerHealth = playerHealth;
        enemyAI.attackDistance = enemyAttackDistance;
        enemyAI.attackInterval = enemyAttackInterval;
        enemyAI.damage = enemyDamage;
        enemyAI.detectDistance = 90f;
        enemyAI.moveSpeed = 5.2f;
        enemyAI.turnSpeed = 11f;
        enemyAI.stopDistance = 2.2f;
        enemyAI.moveWhileAttacking = true;
        enemyAI.forceManualMovement = true;
        enemyAI.botFireVolume = 0.55f;
        enemyAI.RefreshMovementSettings();
    }

    void ClearExistingEnemies()
    {
        TargetHealth[] existingTargets = FindObjectsByType<TargetHealth>(FindObjectsSortMode.None);

        for (int i = 0; i < existingTargets.Length; i++)
        {
            Destroy(existingTargets[i].gameObject);
        }
    }

    void HandleTargetKilled(TargetHealth target)
    {
        if (!levelRunning || gameOver || levelComplete)
        {
            return;
        }

        if (!enemies.Remove(target))
        {
            return;
        }

        kills++;
        UpdateLevelText();

        if (kills >= killsRequired)
        {
            CompleteLevel();
        }
    }

    void HandlePlayerDied()
    {
        if (!levelRunning || levelComplete)
        {
            return;
        }

        gameOver = true;
        levelRunning = false;
        ShowCenterPrompt("GAME OVER\nPRESS R TO RESTART");
        SetEnemyAiEnabled(false);
    }

    void CompleteLevel()
    {
        levelComplete = true;
        levelRunning = false;
        ShowCenterPrompt("LEVEL COMPLETE\nPRESS N TO CONTINUE");
        SetEnemyAiEnabled(false);
    }

    void StartLevel3()
    {
        HideUi();

        Level3BombDefusal level3 = GetComponent<Level3BombDefusal>();

        if (level3 == null)
        {
            level3 = gameObject.AddComponent<Level3BombDefusal>();
        }

        level3.BeginLevel();
        enabled = false;
    }

    void SetEnemyAiEnabled(bool enabledState)
    {
        EnemyBotAI[] enemyAis = FindObjectsByType<EnemyBotAI>(FindObjectsSortMode.None);

        for (int i = 0; i < enemyAis.Length; i++)
        {
            enemyAis[i].enabled = enabledState;
        }
    }

    void EnsurePlayerHealth()
    {
        if (player == null)
        {
            return;
        }

        playerHealth = player.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth = player.gameObject.AddComponent<PlayerHealth>();
        }

        playerHealth.PlayerDied -= HandlePlayerDied;
        playerHealth.PlayerDied += HandlePlayerDied;
        playerHealth.SetHealthText(hpText);
    }

    void ResetPlayerAmmo()
    {
        GunShooting gunShooting = FindFirstObjectByType<GunShooting>();

        if (gunShooting != null)
        {
            gunShooting.ResetAmmo();
        }
    }

    void EnsureUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (levelText == null)
        {
            levelText = CreateText(canvas.transform, "Level2TopRightText", 26f, TextAlignmentOptions.Right, new Vector2(540f, 98f));
            TrainingUiStyle.PositionPanel(
                levelText,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-34f, -28f)
            );
        }

        if (hpText == null)
        {
            hpText = CreateText(canvas.transform, "Level2HpText", 30f, TextAlignmentOptions.Left, new Vector2(280f, 68f));
            TrainingUiStyle.PositionPanel(
                hpText,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(34f, 28f)
            );
        }

        if (centerPromptText == null)
        {
            centerPromptText = CreateText(canvas.transform, "Level2CenterPromptText", 38f, TextAlignmentOptions.Center, new Vector2(1020f, 170f));
            TrainingUiStyle.PositionPanel(
                centerPromptText,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 135f)
            );
        }
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment, Vector2 sizeDelta)
    {
        return TrainingUiStyle.CreateText(parent, objectName, fontSize, alignment, sizeDelta);
    }

    void ShowCenterPrompt(string message)
    {
        if (centerPromptText != null)
        {
            TrainingUiStyle.SetMessage(centerPromptText, message);
            TrainingUiStyle.SetVisible(centerPromptText, true);
            TrainingUiStyle.BringToFront(centerPromptText);
        }
    }

    void UpdateLevelText()
    {
        if (levelText != null)
        {
            TrainingUiStyle.SetMessage(levelText, "LEVEL 2 - ENEMY PATROL\n" + kills + "/" + killsRequired);
        }
    }

    void HideUi()
    {
        if (levelText != null)
        {
            TrainingUiStyle.SetVisible(levelText, false);
        }

        if (hpText != null)
        {
            TrainingUiStyle.SetVisible(hpText, false);
        }

        if (centerPromptText != null)
        {
            TrainingUiStyle.SetVisible(centerPromptText, false);
        }
    }
}
