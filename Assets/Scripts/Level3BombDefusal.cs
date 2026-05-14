using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Level3BombDefusal : MonoBehaviour
{
    [Header("Level Setup")]
    public int guardBotCount = 3;
    public int hunterBotCount = 10;
    public float bombTimerSeconds = 60f;
    public float defuseDistance = 4.5f;
    public float defuseHoldSeconds = 5f;

    [Header("Enemy Guard")]
    public float guardMinDistanceFromBomb = 5f;
    public float guardMaxDistanceFromBomb = 18f;
    public float enemyAttackDistance = 10f;
    public float enemyAttackInterval = 1f;
    public int enemyDamage = 5;

    int TotalEnemyCount => guardBotCount + hunterBotCount;

    [Header("UI")]
    public TextMeshProUGUI topRightText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI centerPromptText;
    public TextMeshProUGUI interactText;

    [Header("Mission Complete Audio")]
    public AudioClip missionCompleteMusic;
    public float missionCompleteMusicVolume = 0.75f;

    private readonly List<TargetHealth> enemies = new List<TargetHealth>();
    private BotSpawner botSpawner;
    private Transform player;
    private PlayerHealth playerHealth;
    private GameObject bombObject;
    private Light bombLight;
    private float timerRemaining;
    private float defuseHeldTime;
    private bool levelRunning;
    private bool gameOver;
    private bool levelComplete;
    private AudioSource missionAudioSource;

    void Awake()
    {
        botSpawner = GetComponent<BotSpawner>();
        AutoAssignMissionCompleteMusic();
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
            return;
        }

        if (!levelRunning || levelComplete || gameOver)
        {
            return;
        }

        timerRemaining -= Time.deltaTime;

        if (timerRemaining <= 0f)
        {
            timerRemaining = 0f;
            FailMission("MISSION FAILED\nBOMB EXPLODED\nGAME OVER\nPRESS R TO RESTART");
            return;
        }

        UpdateBombPulse();
        UpdateDefuseInteraction();
        UpdateTopRightText();
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
        DestroyBomb();
        enemies.Clear();
        StopMissionCompleteMusic();

        timerRemaining = bombTimerSeconds;
        defuseHeldTime = 0f;
        gameOver = false;
        levelComplete = false;
        levelRunning = true;

        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }

        ResetPlayerAmmo();
        CreateBomb();
        SpawnGuardEnemies();
        ShowCenterPrompt("LEVEL 3 - BOMB DEFUSAL\nOBJECTIVE: DEFUSE THE BOMB\nFind and defuse the bomb before time runs out.");
        TrainingUiStyle.SetVisible(interactText, false);
        UpdateTopRightText();
    }

    void CreateBomb()
    {
        Vector3 bombPosition = FindBombPosition();

        bombObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bombObject.name = "Bomb";
        bombObject.transform.position = bombPosition + Vector3.up * 0.3f;
        bombObject.transform.localScale = new Vector3(0.8f, 0.45f, 0.8f);

        Renderer renderer = bombObject.GetComponent<Renderer>();

        if (renderer != null)
        {
            Shader bombShader = Shader.Find("Universal Render Pipeline/Lit");

            if (bombShader == null)
            {
                bombShader = Shader.Find("Standard");
            }

            renderer.material = new Material(bombShader);
            renderer.material.color = Color.red;
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", Color.red * 1.4f);
        }

        GameObject lightObject = new GameObject("Bomb_RedLight");
        lightObject.transform.SetParent(bombObject.transform);
        lightObject.transform.localPosition = Vector3.up * 1.1f;
        bombLight = lightObject.AddComponent<Light>();
        bombLight.type = LightType.Point;
        bombLight.color = Color.red;
        bombLight.range = 7f;
        bombLight.intensity = 2.4f;
    }

    Vector3 FindBombPosition()
    {
        Vector3 center = botSpawner != null ? botSpawner.mapCenter : Vector3.zero;
        float halfX = botSpawner != null ? botSpawner.mapHalfSizeX : 35f;
        float halfZ = botSpawner != null ? botSpawner.mapHalfSizeZ : 55f;
        float margin = 9f;

        for (int i = 0; i < 220; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(center.x - halfX + margin, center.x + halfX - margin),
                center.y,
                Random.Range(center.z - halfZ + margin, center.z + halfZ - margin)
            );

            if (player != null && Vector3.Distance(candidate, player.position) < 22f)
            {
                continue;
            }

            if (TryGetGroundPosition(candidate, out Vector3 groundPosition) && HasBombClearance(groundPosition))
            {
                return groundPosition;
            }
        }

        Vector3 fallback = player != null ? player.position + player.forward * 28f : center;

        if (TryGetGroundPosition(fallback, out Vector3 fallbackGround))
        {
            return fallbackGround;
        }

        return fallback;
    }

    bool TryGetGroundPosition(Vector3 candidate, out Vector3 groundPosition)
    {
        Vector3 rayOrigin = candidate + Vector3.up * 40f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 90f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.82f)
            {
                groundPosition = Vector3.zero;
                return false;
            }

            if (player != null && hit.point.y > player.position.y + 1.4f)
            {
                groundPosition = Vector3.zero;
                return false;
            }

            groundPosition = hit.point;
            return true;
        }

        groundPosition = Vector3.zero;
        return false;
    }

    bool HasBombClearance(Vector3 groundPosition)
    {
        Vector3 center = groundPosition + Vector3.up * 1f;
        Vector3 halfExtents = new Vector3(1.4f, 0.75f, 1.4f);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].gameObject.name == "BG")
            {
                continue;
            }

            return false;
        }

        return true;
    }

    void SpawnGuardEnemies()
    {
        if (botSpawner == null || player == null || bombObject == null)
        {
            Debug.LogWarning("Level3BombDefusal needs a BotSpawner, Player, and Bomb.");
            return;
        }

        botSpawner.ClearSpawnedPositionHistory();
        botSpawner.useExpandedMapBounds = true;
        botSpawner.enforcePlayerDistanceInMapBounds = false;
        botSpawner.useNavMesh = true;
        botSpawner.navMeshSearchRadius = 8f;
        botSpawner.spawnHeightOffset = 0.03f;
        botSpawner.maxSpawnHeightAbovePlayer = 2.5f;
        botSpawner.maxAttemptsPerBot = 800;
        botSpawner.relaxSpawnChecks = true;
        botSpawner.minDistanceBetweenBots = 2f;
        botSpawner.requiredFlatRadius = 0.45f;
        botSpawner.botClearanceRadius = 0.25f;
        botSpawner.maxFlatHeightDifference = 0.35f;
        botSpawner.minSurfaceUpDot = 0.9f;
        botSpawner.requireOpenGroundAroundBot = false;

        SpawnFixedBombGuards();
        SpawnHunterBots();

        UpdateTopRightText();
    }

    void SpawnFixedBombGuards()
    {
        Vector3 bombPosition = bombObject.transform.position;
        Vector3[] guardOffsets =
        {
            new Vector3(4.5f, 0f, 0f),
            new Vector3(-4.5f, 0f, 0f),
            new Vector3(0f, 0f, 4.5f),
        };

        int spawnedGuards = 0;

        for (int i = 0; i < guardOffsets.Length && spawnedGuards < guardBotCount; i++)
        {
            GameObject enemyObject = botSpawner.SpawnOneBotAt(bombPosition + guardOffsets[i]);

            if (enemyObject != null)
            {
                RegisterEnemy(enemyObject, true);
                spawnedGuards++;
            }
        }

        int fallbackAttempts = 0;

        while (spawnedGuards < guardBotCount && fallbackAttempts < guardBotCount * 8)
        {
            fallbackAttempts++;
            GameObject enemyObject = botSpawner.SpawnOneBotNear(bombPosition, 4f, 8f);

            if (enemyObject != null)
            {
                RegisterEnemy(enemyObject, true);
                spawnedGuards++;
            }
        }
    }

    void SpawnHunterBots()
    {
        int spawnedHunters = 0;
        int attempts = 0;

        while (spawnedHunters < hunterBotCount && attempts < hunterBotCount * 14)
        {
            attempts++;
            GameObject enemyObject = botSpawner.SpawnOneBot();

            if (enemyObject == null)
            {
                continue;
            }

            RegisterEnemy(enemyObject, false);
            spawnedHunters++;
        }
    }

    void TrySpawnGuardWave(float minDistance, float maxDistance, int maxAttempts)
    {
        int attempts = 0;

        while (enemies.Count < TotalEnemyCount && attempts < maxAttempts)
        {
            attempts++;
            GameObject enemyObject = botSpawner.SpawnOneBotNear(bombObject.transform.position, minDistance, maxDistance);

            if (enemyObject == null)
            {
                continue;
            }

            RegisterEnemy(enemyObject, true);
        }
    }

    void RegisterEnemy(GameObject enemyObject, bool guardBomb)
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
        enemyAI.attackDistance = guardBomb ? 18f : enemyAttackDistance;
        enemyAI.attackInterval = enemyAttackInterval;
        enemyAI.damage = enemyDamage;
        enemyAI.detectDistance = guardBomb ? 35f : 90f;
        enemyAI.moveSpeed = guardBomb ? 0f : 5.2f;
        enemyAI.turnSpeed = 11f;
        enemyAI.stopDistance = 2.2f;
        enemyAI.moveWhileAttacking = !guardBomb;
        enemyAI.forceManualMovement = true;
        enemyAI.botFireVolume = 0.65f;
        enemyAI.RefreshMovementSettings();
    }

    void UpdateDefuseInteraction()
    {
        if (bombObject == null || player == null)
        {
            return;
        }

        PruneEnemyList();

        Vector3 playerFlatPosition = new Vector3(player.position.x, 0f, player.position.z);
        Vector3 bombFlatPosition = new Vector3(bombObject.transform.position.x, 0f, bombObject.transform.position.z);
        float distanceToBomb = Vector3.Distance(playerFlatPosition, bombFlatPosition);
        bool closeEnough = distanceToBomb <= defuseDistance;

        if (!closeEnough)
        {
            defuseHeldTime = 0f;
            TrainingUiStyle.SetVisible(interactText, false);
            return;
        }

        bool holdingDefuse = Keyboard.current != null && Keyboard.current.eKey.isPressed;

        if (holdingDefuse)
        {
            defuseHeldTime += Time.deltaTime;
            float progress = Mathf.Clamp01(defuseHeldTime / defuseHoldSeconds);
            ShowInteractPrompt("DEFUSING... " + Mathf.RoundToInt(progress * 100f) + "%");

            if (defuseHeldTime >= defuseHoldSeconds)
            {
                CompleteMission();
            }

            return;
        }

        defuseHeldTime = 0f;
        ShowInteractPrompt("HOLD E TO DEFUSE\n\u957f\u6309 E \u62c6\u9664\u70b8\u5f39");
    }

    void UpdateBombPulse()
    {
        if (bombLight == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.time * 7f) + 1f) * 0.5f;
        bombLight.intensity = Mathf.Lerp(1.5f, 5f, pulse);
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

        PruneEnemyList();
        UpdateTopRightText();
    }

    void PruneEnemyList()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] == null)
            {
                enemies.RemoveAt(i);
            }
        }
    }

    void HandlePlayerDied()
    {
        if (!levelRunning || levelComplete)
        {
            return;
        }

        FailMission("GAME OVER\nPRESS R TO RESTART");
    }

    void CompleteMission()
    {
        levelComplete = true;
        levelRunning = false;
        ShowCenterPrompt("MISSION COMPLETE\nBOMB DEFUSED");
        TrainingUiStyle.SetVisible(interactText, false);
        SetEnemyAiEnabled(false);
        PlayMissionCompleteMusic();
    }

    void FailMission(string message)
    {
        gameOver = true;
        levelRunning = false;
        ShowCenterPrompt(message);
        TrainingUiStyle.SetVisible(interactText, false);
        SetEnemyAiEnabled(false);
        StopMissionCompleteMusic();
    }

    void AutoAssignMissionCompleteMusic()
    {
#if UNITY_EDITOR
        if (missionCompleteMusic == null)
        {
            missionCompleteMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/mvp【Under Bright Lights】.mp3");
        }
#endif
    }

    void PlayMissionCompleteMusic()
    {
        AutoAssignMissionCompleteMusic();

        if (missionCompleteMusic == null)
        {
            return;
        }

        if (missionAudioSource == null)
        {
            GameObject audioObject = new GameObject("MissionCompleteMusic");
            audioObject.transform.SetParent(transform, false);
            missionAudioSource = audioObject.AddComponent<AudioSource>();
            missionAudioSource.playOnAwake = false;
            missionAudioSource.loop = false;
            missionAudioSource.spatialBlend = 0f;
        }

        missionAudioSource.clip = missionCompleteMusic;
        missionAudioSource.volume = missionCompleteMusicVolume;
        missionAudioSource.Stop();
        missionAudioSource.Play();
    }

    void StopMissionCompleteMusic()
    {
        if (missionAudioSource != null && missionAudioSource.isPlaying)
        {
            missionAudioSource.Stop();
        }
    }

    void ClearExistingEnemies()
    {
        TargetHealth[] existingTargets = FindObjectsByType<TargetHealth>(FindObjectsSortMode.None);

        for (int i = 0; i < existingTargets.Length; i++)
        {
            Destroy(existingTargets[i].gameObject);
        }
    }

    void DestroyBomb()
    {
        if (bombObject != null)
        {
            Destroy(bombObject);
            bombObject = null;
            bombLight = null;
        }
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

        if (topRightText == null)
        {
            topRightText = CreateText(canvas.transform, "Level3TopRightText", 26f, TextAlignmentOptions.Right, new Vector2(560f, 105f));
            TrainingUiStyle.PositionPanel(
                topRightText,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-34f, -28f)
            );
        }

        if (hpText == null)
        {
            hpText = CreateText(canvas.transform, "Level3HpText", 30f, TextAlignmentOptions.Left, new Vector2(280f, 68f));
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
            centerPromptText = CreateText(canvas.transform, "Level3CenterPromptText", 35f, TextAlignmentOptions.Center, new Vector2(1120f, 190f));
            TrainingUiStyle.PositionPanel(
                centerPromptText,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 135f)
            );
        }

        if (interactText == null)
        {
            interactText = CreateText(canvas.transform, "Level3InteractText", 32f, TextAlignmentOptions.Center, new Vector2(700f, 92f));
            TrainingUiStyle.PositionPanel(
                interactText,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 155f)
            );
        }
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment, Vector2 sizeDelta)
    {
        return TrainingUiStyle.CreateText(parent, objectName, fontSize, alignment, sizeDelta);
    }

    void ShowCenterPrompt(string message)
    {
        if (centerPromptText == null)
        {
            return;
        }

        TrainingUiStyle.SetMessage(centerPromptText, message);
        TrainingUiStyle.SetVisible(centerPromptText, true);
        TrainingUiStyle.BringToFront(centerPromptText);
    }

    void ShowInteractPrompt(string message)
    {
        if (interactText == null)
        {
            return;
        }

        TrainingUiStyle.SetMessage(interactText, message);
        TrainingUiStyle.SetVisible(interactText, true);
        TrainingUiStyle.BringToFront(interactText);
    }

    void UpdateTopRightText()
    {
        if (topRightText != null)
        {
            PruneEnemyList();
            int timerSeconds = Mathf.CeilToInt(timerRemaining);
            TrainingUiStyle.SetMessage(topRightText, "BOMB TIMER: " + timerSeconds + "\nENEMIES LEFT: " + enemies.Count);
        }
    }
}
