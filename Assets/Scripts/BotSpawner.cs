using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BotSpawner : MonoBehaviour
{
    [Header("Bot Prefab")]
    public GameObject botPrefab;

    [Header("Spawn Target")]
    public Transform player;

    [Header("Spawn Count")]
    public int botCount = 3;

    [Header("Runtime Safety")]
    public bool forceStableRuntimeValues = true;

    [Header("Optional Fixed Spawn Points")]
    public Transform spawnPointsParent;
    public Transform[] spawnPoints;
    public bool preferSpawnPoints = false;

    [Header("Random Spawn Around Player")]
    public float minDistanceFromPlayer = 7f;
    public float maxDistanceFromPlayer = 24f;
    public float minDistanceBetweenBots = 2f;

    [Header("NavMesh Placement")]
    public bool useNavMesh = true;
    public float navMeshSearchRadius = 6f;
    public int maxAttemptsPerBot = 2000;

    [Header("Ground Placement")]
    public LayerMask spawnSurfaceMask = ~0;
    public LayerMask blockingMask = ~0;
    public float surfaceRaycastHeight = 40f;
    public float maxSpawnHeightAbovePlayer = 2.5f;
    public float spawnHeightOffset = 0.03f;

    [Header("Flat Ground Check")]
    public float requiredFlatRadius = 0.45f;
    public float maxFlatHeightDifference = 0.35f;
    [Range(0f, 1f)]
    public float minSurfaceUpDot = 0.9f;

    [Header("Wall Clearance")]
    public float botClearanceRadius = 0.25f;
    public float botClearanceBottom = 0.55f;
    public float botClearanceTop = 1.9f;

    private readonly List<Vector3> spawnedPositions = new List<Vector3>();

    void Start()
    {
        if (forceStableRuntimeValues)
        {
            ApplyStableRuntimeValues();
        }

        if (player == null)
        {
            GameObject foundPlayer = GameObject.Find("PlayerCapsule");

            if (foundPlayer != null)
            {
                player = foundPlayer.transform;
            }
        }

        SpawnBotsAroundPlayer();
    }

    void ApplyStableRuntimeValues()
    {
        botCount = 3;
        preferSpawnPoints = false;
        minDistanceFromPlayer = 7f;
        maxDistanceFromPlayer = 24f;
        minDistanceBetweenBots = 2f;
        navMeshSearchRadius = 6f;
        maxAttemptsPerBot = 2000;
        maxSpawnHeightAbovePlayer = 2.5f;
        requiredFlatRadius = 0.45f;
        maxFlatHeightDifference = 0.35f;
        botClearanceRadius = 0.25f;
    }

    void SpawnBotsAroundPlayer()
    {
        spawnedPositions.Clear();

        if (botPrefab == null)
        {
            Debug.LogWarning("BotSpawner has no Bot Prefab assigned.");
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("BotSpawner has no Player assigned. Drag PlayerCapsule into the Player field.");
            return;
        }

        for (int i = 0; i < botCount; i++)
        {
            if (!TrySpawnOneBot())
            {
                Debug.LogWarning("A bot could not find a valid spawn position. Increase max attempts or check map colliders.");
            }
        }

        Debug.Log("BotSpawner spawned " + spawnedPositions.Count + " / " + botCount + " bots.");
    }

    bool TrySpawnOneBot()
    {
        for (int attempt = 0; attempt < maxAttemptsPerBot; attempt++)
        {
            float relaxation = maxAttemptsPerBot <= 1 ? 1f : (float)attempt / (maxAttemptsPerBot - 1);
            float effectiveFlatRadius = Mathf.Lerp(requiredFlatRadius, 0.2f, relaxation);
            float effectiveClearanceRadius = Mathf.Lerp(botClearanceRadius, 0.12f, relaxation);
            float effectiveBotSpacing = Mathf.Lerp(minDistanceBetweenBots, 0.75f, relaxation);

            Vector3 candidatePosition = GetCandidatePosition();

            if (!TryGetSpawnPosition(candidatePosition, out Vector3 spawnPosition, effectiveFlatRadius, effectiveClearanceRadius))
            {
                continue;
            }

            if (!IsFarEnoughFromOtherBots(spawnPosition, effectiveBotSpacing))
            {
                continue;
            }

            Quaternion randomRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Instantiate(botPrefab, spawnPosition, randomRotation);
            spawnedPositions.Add(spawnPosition);
            return true;
        }

        return false;
    }

    Vector3 GetCandidatePosition()
    {
        if (preferSpawnPoints && TryGetRandomSpawnPoint(out Vector3 spawnPointPosition))
        {
            return spawnPointPosition;
        }

        return GetRandomPositionAroundPlayer();
    }

    bool TryGetRandomSpawnPoint(out Vector3 spawnPointPosition)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            spawnPointPosition = spawnPoint.position;
            return true;
        }

        if (spawnPointsParent != null && spawnPointsParent.childCount > 0)
        {
            Transform spawnPoint = spawnPointsParent.GetChild(Random.Range(0, spawnPointsParent.childCount));
            spawnPointPosition = spawnPoint.position;
            return true;
        }

        spawnPointPosition = Vector3.zero;
        return false;
    }

    Vector3 GetRandomPositionAroundPlayer()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);

        return player.position + new Vector3(
            randomDirection.x * randomDistance,
            0f,
            randomDirection.y * randomDistance
        );
    }

    bool TryGetSpawnPosition(
        Vector3 candidatePosition,
        out Vector3 spawnPosition,
        float effectiveFlatRadius,
        float effectiveClearanceRadius)
    {
        if (useNavMesh && NavMesh.SamplePosition(
            candidatePosition,
            out NavMeshHit navHit,
            navMeshSearchRadius,
            NavMesh.AllAreas))
        {
            Vector3 navPosition = navHit.position + Vector3.up * spawnHeightOffset;

            if (IsValidSpawnLocation(navPosition, effectiveFlatRadius, effectiveClearanceRadius))
            {
                spawnPosition = navPosition;
                return true;
            }
        }

        if (TryGetSurfacePosition(candidatePosition, out Vector3 surfacePosition) &&
            IsValidSpawnLocation(surfacePosition, effectiveFlatRadius, effectiveClearanceRadius))
        {
            spawnPosition = surfacePosition;
            return true;
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    bool TryGetSurfacePosition(Vector3 candidatePosition, out Vector3 surfacePosition)
    {
        Vector3 rayOrigin = candidatePosition + Vector3.up * surfaceRaycastHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, surfaceRaycastHeight * 2f, spawnSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            surfacePosition = hit.point + Vector3.up * spawnHeightOffset;
            return true;
        }

        surfacePosition = Vector3.zero;
        return false;
    }

    bool IsValidSpawnLocation(Vector3 spawnPosition, float effectiveFlatRadius, float effectiveClearanceRadius)
    {
        if (spawnPosition.y > player.position.y + maxSpawnHeightAbovePlayer)
        {
            return false;
        }

        if (!HasEnoughFlatGround(spawnPosition, effectiveFlatRadius))
        {
            return false;
        }

        return HasEnoughWallClearance(spawnPosition, effectiveClearanceRadius);
    }

    bool HasEnoughFlatGround(Vector3 spawnPosition, float effectiveFlatRadius)
    {
        Vector3[] sampleOffsets =
        {
            Vector3.zero,
            Vector3.forward * effectiveFlatRadius,
            Vector3.back * effectiveFlatRadius,
            Vector3.left * effectiveFlatRadius,
            Vector3.right * effectiveFlatRadius,
            (Vector3.forward + Vector3.left).normalized * effectiveFlatRadius,
            (Vector3.forward + Vector3.right).normalized * effectiveFlatRadius,
            (Vector3.back + Vector3.left).normalized * effectiveFlatRadius,
            (Vector3.back + Vector3.right).normalized * effectiveFlatRadius,
        };

        float baseHeight = 0f;

        for (int i = 0; i < sampleOffsets.Length; i++)
        {
            Vector3 sampleOrigin = spawnPosition + sampleOffsets[i] + Vector3.up * surfaceRaycastHeight;

            if (!Physics.Raycast(sampleOrigin, Vector3.down, out RaycastHit hit, surfaceRaycastHeight * 2f, spawnSurfaceMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (Vector3.Dot(hit.normal, Vector3.up) < minSurfaceUpDot)
            {
                return false;
            }

            if (i == 0)
            {
                baseHeight = hit.point.y;
            }
            else if (Mathf.Abs(hit.point.y - baseHeight) > maxFlatHeightDifference)
            {
                return false;
            }
        }

        return true;
    }

    bool HasEnoughWallClearance(Vector3 spawnPosition, float effectiveClearanceRadius)
    {
        Vector3 bottom = spawnPosition + Vector3.up * botClearanceBottom;
        Vector3 top = spawnPosition + Vector3.up * botClearanceTop;

        return !Physics.CheckCapsule(
            bottom,
            top,
            effectiveClearanceRadius,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );
    }

    bool IsFarEnoughFromOtherBots(Vector3 spawnPosition, float effectiveBotSpacing)
    {
        for (int i = 0; i < spawnedPositions.Count; i++)
        {
            if (Vector3.Distance(spawnPosition, spawnedPositions[i]) < effectiveBotSpacing)
            {
                return false;
            }
        }

        return true;
    }
}
