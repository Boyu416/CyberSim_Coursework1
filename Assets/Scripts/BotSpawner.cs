using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    public bool enableLevel1TrainingCamp = true;
    public bool spawnOnStart = true;
    public bool expandMapBeforeSpawning = false;
    public bool buildFloorBoundaryBeforeSpawning = true;
    public bool scatterPropsBeforeSpawning = true;
    public bool showMainMenuOnStart = false;

    [Header("Optional Fixed Spawn Points")]
    public Transform spawnPointsParent;
    public Transform[] spawnPoints;
    public bool preferSpawnPoints = false;

    [Header("Random Spawn Around Player")]
    public float minDistanceFromPlayer = 7f;
    public float maxDistanceFromPlayer = 60f;
    public float minDistanceBetweenBots = 2f;
    public bool enforcePlayerDistanceInMapBounds = false;

    [Header("Expanded Map Bounds")]
    public bool useExpandedMapBounds = true;
    public bool usePlayerAsMapCenter = true;
    public Vector3 mapCenter = Vector3.zero;
    public float mapHalfSizeX = 45f;
    public float mapHalfSizeZ = 85f;
    public float mapBoundaryMargin = 2.5f;
    public bool createOuterWalls = false;
    public float outerWallHeight = 5f;
    public float outerWallThickness = 0.6f;

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
    public bool relaxSpawnChecks = true;

    [Header("Wall Clearance")]
    public float botClearanceRadius = 0.25f;
    public float botClearanceBottom = 0.55f;
    public float botClearanceTop = 1.9f;
    public bool requireOpenGroundAroundBot = true;
    public float openGroundRadius = 2.6f;

    [Header("Bot Visual")]
    public bool replaceGeneratedBotVisual = true;
    public GameObject botVisualPrefab;
    public Vector3 botVisualLocalPosition = Vector3.zero;
    public Vector3 botVisualLocalEulerAngles = Vector3.zero;
    public float botVisualScale = 1f;
    public bool normalizeGeneratedBotVisualSize = true;
    public float botVisualTargetHeight = 1.85f;
    public float botVisualGroundInset = 0.28f;
    public bool normalizeGeneratedBotCollider = true;
    public bool tintGeneratedBotVisual = true;
    public Color botVisualTint = new Color(0.1f, 0.35f, 1f, 1f);

    private readonly List<Vector3> spawnedPositions = new List<Vector3>();

    void Start()
    {
        if (forceStableRuntimeValues)
        {
            ApplyStableRuntimeValues();
        }

        AutoAssignBotVisualPrefab();

        if (player == null)
        {
            GameObject foundPlayer = GameObject.Find("PlayerCapsule");

            if (foundPlayer != null)
            {
                player = foundPlayer.transform;
            }
        }

        if (useExpandedMapBounds && usePlayerAsMapCenter && player != null)
        {
            mapCenter = player.position;
            SnapMapCenterToGround();
        }

        if (useExpandedMapBounds && createOuterWalls)
        {
            CreateOuterWalls();
        }

        if (expandMapBeforeSpawning)
        {
            ExpandMapWithExistingPieces();
        }

        if (buildFloorBoundaryBeforeSpawning)
        {
            BuildFloorBoundaryWalls();
        }

        if (scatterPropsBeforeSpawning)
        {
            ScatterMapProps();
        }

        if (showMainMenuOnStart)
        {
            MainMenuController mainMenu = GetComponent<MainMenuController>();

            if (mainMenu == null)
            {
                mainMenu = gameObject.AddComponent<MainMenuController>();
            }

            mainMenu.ShowMenu();
            return;
        }

        ResetGameplayFromMenu();

        if (enableLevel1TrainingCamp && GetComponent<Level1TrainingCamp>() == null)
        {
            gameObject.AddComponent<Level1TrainingCamp>();
        }

        if (spawnOnStart)
        {
            SpawnBotsAroundPlayer();
        }
    }

    void ApplyStableRuntimeValues()
    {
        botCount = 3;
        enableLevel1TrainingCamp = true;
        spawnOnStart = false;
        expandMapBeforeSpawning = false;
        buildFloorBoundaryBeforeSpawning = true;
        scatterPropsBeforeSpawning = true;
        showMainMenuOnStart = false;
        preferSpawnPoints = false;
        useExpandedMapBounds = true;
        usePlayerAsMapCenter = true;
        createOuterWalls = false;
        mapHalfSizeX = 45f;
        mapHalfSizeZ = 85f;
        mapBoundaryMargin = 2.5f;
        minDistanceFromPlayer = 7f;
        maxDistanceFromPlayer = 60f;
        minDistanceBetweenBots = 2f;
        enforcePlayerDistanceInMapBounds = false;
        navMeshSearchRadius = 6f;
        maxAttemptsPerBot = 2000;
        maxSpawnHeightAbovePlayer = 2.5f;
        requiredFlatRadius = 0.45f;
        maxFlatHeightDifference = 0.35f;
        relaxSpawnChecks = true;
        botClearanceRadius = 0.25f;
        requireOpenGroundAroundBot = true;
        openGroundRadius = 2.6f;
        replaceGeneratedBotVisual = true;
        normalizeGeneratedBotCollider = true;
        botVisualScale = 1f;
        normalizeGeneratedBotVisualSize = true;
        botVisualTargetHeight = 1.85f;
        botVisualGroundInset = 0.28f;
        botVisualLocalPosition = Vector3.zero;
        botVisualLocalEulerAngles = Vector3.zero;
        tintGeneratedBotVisual = true;
        botVisualTint = new Color(0.1f, 0.35f, 1f, 1f);
    }

    void ResetGameplayFromMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameObject[] menuCanvases = GameObject.FindGameObjectsWithTag("Untagged");

        for (int i = 0; i < menuCanvases.Length; i++)
        {
            if (menuCanvases[i] != null && menuCanvases[i].name == "MainMenuCanvas")
            {
                Destroy(menuCanvases[i]);
            }
        }

        MainMenuController[] menus = FindObjectsByType<MainMenuController>(FindObjectsSortMode.None);

        for (int i = 0; i < menus.Length; i++)
        {
            menus[i].enabled = false;
            Destroy(menus[i]);
        }
    }

    void ScatterMapProps()
    {
        MapPropScatterer scatterer = GetComponent<MapPropScatterer>();

        if (scatterer == null)
        {
            scatterer = gameObject.AddComponent<MapPropScatterer>();
        }

        scatterer.scatterOnStart = false;
        scatterer.floorObjectName = "BG";
        scatterer.sourceRootName = "Demo";
        scatterer.boxGroupCount = 25;
        scatterer.minBoxesPerGroup = 3;
        scatterer.maxBoxesPerGroup = 3;
        scatterer.buildingCount = 8;
        scatterer.smallCoverCount = 0;
        scatterer.edgeMargin = 6f;
        scatterer.minDistanceFromPlayer = 8f;
        scatterer.ScatterProps();
    }

    void BuildFloorBoundaryWalls()
    {
        FloorBoundaryWalls boundaryWalls = GetComponent<FloorBoundaryWalls>();

        if (boundaryWalls == null)
        {
            boundaryWalls = gameObject.AddComponent<FloorBoundaryWalls>();
        }

        boundaryWalls.buildOnStart = false;
        boundaryWalls.floorObjectName = "BG";
        boundaryWalls.sourceRootName = "Demo";
        boundaryWalls.disableOldPerimeterWalls = true;
        boundaryWalls.edgeInset = 0.6f;
        boundaryWalls.oldWallEdgeTolerance = 4f;
        boundaryWalls.BuildBoundaryWalls();
    }

    void ExpandMapWithExistingPieces()
    {
        MapExpander mapExpander = GetComponent<MapExpander>();

        if (mapExpander == null)
        {
            mapExpander = gameObject.AddComponent<MapExpander>();
        }

        mapExpander.expandOnStart = false;
        mapExpander.sourceRootName = "Demo";
        mapExpander.extraTiles = 1;
        mapExpander.expansionDirection = MapExpander.ExpansionDirection.PositiveZ;
        mapExpander.tileOverlap = 0.4f;
        mapExpander.removeMiddleWalls = true;
        mapExpander.seamStripWidth = 2.2f;
        mapExpander.ExpandMap();
    }

    public void SpawnBotsAroundPlayer()
    {
        spawnedPositions.Clear();
        SpawnBots(botCount);
    }

    public int SpawnBots(int count)
    {
        int spawnedCount = 0;

        if (botPrefab == null)
        {
            Debug.LogWarning("BotSpawner has no Bot Prefab assigned.");
            return spawnedCount;
        }

        if (player == null)
        {
            Debug.LogWarning("BotSpawner has no Player assigned. Drag PlayerCapsule into the Player field.");
            return spawnedCount;
        }

        for (int i = 0; i < count; i++)
        {
            if (SpawnOneBot() == null)
            {
                Debug.LogWarning("A bot could not find a valid spawn position. Increase max attempts or check map colliders.");
            }
            else
            {
                spawnedCount++;
            }
        }

        Debug.Log("BotSpawner spawned " + spawnedCount + " / " + count + " bots.");
        return spawnedCount;
    }

    public void ClearSpawnedPositionHistory()
    {
        spawnedPositions.Clear();
    }

    public GameObject SpawnOneBot()
    {
        return SpawnOneBotFromCandidate(GetCandidatePosition);
    }

    public GameObject SpawnOneBotNear(Vector3 center, float minDistance, float maxDistance)
    {
        return SpawnOneBotFromCandidate(() => GetRandomPositionAroundPoint(center, minDistance, maxDistance));
    }

    public GameObject SpawnOneBotAt(Vector3 position)
    {
        return SpawnOneBotFromCandidate(() => position);
    }

    GameObject SpawnOneBotFromCandidate(System.Func<Vector3> candidateProvider)
    {
        for (int attempt = 0; attempt < maxAttemptsPerBot; attempt++)
        {
            float relaxation = maxAttemptsPerBot <= 1 ? 1f : (float)attempt / (maxAttemptsPerBot - 1);
            float effectiveFlatRadius = relaxSpawnChecks ? Mathf.Lerp(requiredFlatRadius, 0.2f, relaxation) : requiredFlatRadius;
            float effectiveClearanceRadius = relaxSpawnChecks ? Mathf.Lerp(botClearanceRadius, 0.12f, relaxation) : botClearanceRadius;
            float effectiveBotSpacing = relaxSpawnChecks ? Mathf.Lerp(minDistanceBetweenBots, 0.75f, relaxation) : minDistanceBetweenBots;

            Vector3 candidatePosition = candidateProvider();

            if (!TryGetSpawnPosition(candidatePosition, out Vector3 spawnPosition, effectiveFlatRadius, effectiveClearanceRadius))
            {
                continue;
            }

            if (!IsFarEnoughFromOtherBots(spawnPosition, effectiveBotSpacing))
            {
                continue;
            }

            Quaternion randomRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject spawnedBot = Instantiate(botPrefab, spawnPosition, randomRotation);
            SnapSpawnedBotRootToGround(spawnedBot);
            PrepareSpawnedBot(spawnedBot);
            spawnedPositions.Add(spawnPosition);
            return spawnedBot;
        }

        return null;
    }

    void PrepareSpawnedBot(GameObject spawnedBot)
    {
        if (spawnedBot == null)
        {
            return;
        }

        if (normalizeGeneratedBotCollider)
        {
            CapsuleCollider capsuleCollider = spawnedBot.GetComponent<CapsuleCollider>();

            if (capsuleCollider != null)
            {
                capsuleCollider.radius = 0.45f;
                capsuleCollider.height = 2f;
                capsuleCollider.center = new Vector3(0f, 1f, 0f);
            }
        }

        TargetHealth targetHealth = spawnedBot.GetComponent<TargetHealth>();

        if (targetHealth != null)
        {
            targetHealth.ResetHealth(100f);
        }

        if (!replaceGeneratedBotVisual)
        {
            return;
        }

        AutoAssignBotVisualPrefab();

        if (botVisualPrefab == null)
        {
            return;
        }

        Renderer[] oldRenderers = spawnedBot.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < oldRenderers.Length; i++)
        {
            oldRenderers[i].enabled = false;
        }

        GameObject botVisual = Instantiate(botVisualPrefab, spawnedBot.transform);
        botVisual.name = "BotVisual_FiringRifle";
        botVisual.transform.localPosition = botVisualLocalPosition;
        botVisual.transform.localRotation = Quaternion.Euler(botVisualLocalEulerAngles);
        botVisual.transform.localScale = Vector3.one * botVisualScale;
        NormalizeVisualHeight(botVisual);
        AlignVisualFeetToGround(botVisual, spawnedBot.transform.position.y);
        ApplyVisualTint(botVisual);
    }

    void ApplyVisualTint(GameObject botVisual)
    {
        if (!tintGeneratedBotVisual || botVisual == null)
        {
            return;
        }

        Renderer[] renderers = botVisual.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].materials;

            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j].HasProperty("_BaseColor"))
                {
                    materials[j].SetColor("_BaseColor", botVisualTint);
                }
                else if (materials[j].HasProperty("_Color"))
                {
                    materials[j].SetColor("_Color", botVisualTint);
                }
            }
        }
    }

    void SnapSpawnedBotRootToGround(GameObject spawnedBot)
    {
        if (spawnedBot == null)
        {
            return;
        }

        if (TryGetBestSurfacePosition(spawnedBot.transform.position, out Vector3 surfacePosition, spawnedBot.transform.root))
        {
            spawnedBot.transform.position = surfacePosition;
        }
    }

    void AutoAssignBotVisualPrefab()
    {
#if UNITY_EDITOR
        if (botVisualPrefab == null || botVisualPrefab.name != "Firing Rifle")
        {
            botVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modles/Firing Rifle.fbx");
        }
#endif
    }

    void AlignVisualFeetToGround(GameObject botVisual, float groundY)
    {
        if (!TryGetRendererBounds(botVisual, out Bounds bounds))
        {
            return;
        }

        float yOffset = groundY - bounds.min.y - botVisualGroundInset;
        botVisual.transform.position += Vector3.up * yOffset;
    }

    void NormalizeVisualHeight(GameObject botVisual)
    {
        if (!normalizeGeneratedBotVisualSize)
        {
            return;
        }

        if (!TryGetRendererBounds(botVisual, out Bounds bounds))
        {
            return;
        }

        if (bounds.size.y <= 0.01f)
        {
            return;
        }

        float scaleMultiplier = botVisualTargetHeight / bounds.size.y;
        botVisual.transform.localScale *= scaleMultiplier;
    }

    bool TryGetRendererBounds(GameObject targetObject, out Bounds bounds)
    {
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        AutoAssignBotVisualPrefab();
    }
#endif

    Vector3 GetCandidatePosition()
    {
        if (preferSpawnPoints && TryGetRandomSpawnPoint(out Vector3 spawnPointPosition))
        {
            return spawnPointPosition;
        }

        if (useExpandedMapBounds)
        {
            return GetRandomPositionInsideExpandedMap();
        }

        return GetRandomPositionAroundPlayer();
    }

    Vector3 GetRandomPositionInsideExpandedMap()
    {
        float x = Random.Range(
            mapCenter.x - mapHalfSizeX + mapBoundaryMargin,
            mapCenter.x + mapHalfSizeX - mapBoundaryMargin
        );

        float z = Random.Range(
            mapCenter.z - mapHalfSizeZ + mapBoundaryMargin,
            mapCenter.z + mapHalfSizeZ - mapBoundaryMargin
        );

        return new Vector3(x, mapCenter.y, z);
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
        return GetRandomPositionAroundPoint(player.position, minDistanceFromPlayer, maxDistanceFromPlayer);
    }

    Vector3 GetRandomPositionAroundPoint(Vector3 center, float minDistance, float maxDistance)
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(minDistance, maxDistance);

        return center + new Vector3(
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
        return TryGetBestSurfacePosition(candidatePosition, out surfacePosition, null);
    }

    bool TryGetBestSurfacePosition(Vector3 candidatePosition, out Vector3 surfacePosition, Transform ignoredRoot)
    {
        Vector3 rayOrigin = candidatePosition + Vector3.up * surfaceRaycastHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            surfaceRaycastHeight * 2f,
            spawnSurfaceMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit bestHit = default;
        float bestScore = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (ignoredRoot != null && hits[i].collider.transform.root == ignoredRoot)
            {
                continue;
            }

            if (Vector3.Dot(hits[i].normal, Vector3.up) < minSurfaceUpDot)
            {
                continue;
            }

            if (player != null && hits[i].point.y > player.position.y + maxSpawnHeightAbovePlayer)
            {
                continue;
            }

            float heightScore = player != null ? Mathf.Abs(hits[i].point.y - player.position.y) : Mathf.Abs(hits[i].point.y - candidatePosition.y);
            float candidateScore = heightScore * 10f + Mathf.Abs(hits[i].point.y - candidatePosition.y);

            if (!foundHit || candidateScore < bestScore)
            {
                bestHit = hits[i];
                bestScore = candidateScore;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            surfacePosition = bestHit.point + Vector3.up * spawnHeightOffset;
            return true;
        }

        surfacePosition = Vector3.zero;
        return false;
    }

    bool IsValidSpawnLocation(Vector3 spawnPosition, float effectiveFlatRadius, float effectiveClearanceRadius)
    {
        if (useExpandedMapBounds && enforcePlayerDistanceInMapBounds && player != null)
        {
            Vector2 playerFlatPosition = new Vector2(player.position.x, player.position.z);
            Vector2 spawnFlatPosition = new Vector2(spawnPosition.x, spawnPosition.z);
            float distanceFromPlayer = Vector2.Distance(playerFlatPosition, spawnFlatPosition);

            if (distanceFromPlayer < minDistanceFromPlayer || distanceFromPlayer > maxDistanceFromPlayer)
            {
                return false;
            }
        }

        if (spawnPosition.y > player.position.y + maxSpawnHeightAbovePlayer)
        {
            return false;
        }

        if (!HasEnoughFlatGround(spawnPosition, effectiveFlatRadius))
        {
            return false;
        }

        return HasEnoughWallClearance(spawnPosition, effectiveClearanceRadius) &&
            HasEnoughOpenGround(spawnPosition);
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
            if (!TryGetBestSurfacePosition(spawnPosition + sampleOffsets[i], out Vector3 sampleSurfacePosition, null))
            {
                return false;
            }

            if (i == 0)
            {
                baseHeight = sampleSurfacePosition.y;
            }
            else if (Mathf.Abs(sampleSurfacePosition.y - baseHeight) > maxFlatHeightDifference)
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

    bool HasEnoughOpenGround(Vector3 spawnPosition)
    {
        if (!requireOpenGroundAroundBot)
        {
            return true;
        }

        Vector3 bottom = spawnPosition + Vector3.up * 0.25f;
        Vector3 top = spawnPosition + Vector3.up * 1.85f;
        Collider[] hits = Physics.OverlapCapsule(
            bottom,
            top,
            openGroundRadius,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitRoot = hits[i].transform.root;

            if (player != null && hitRoot == player.root)
            {
                continue;
            }

            if (IsGroundLikeCollider(hits[i], spawnPosition.y))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    bool IsGroundLikeCollider(Collider hitCollider, float spawnY)
    {
        string objectName = hitCollider.gameObject.name.ToLower();

        if (objectName == "bg" || objectName.Contains("floor") || objectName.Contains("ground"))
        {
            return true;
        }

        Bounds bounds = hitCollider.bounds;
        bool veryFlat = bounds.size.y <= 0.35f;
        bool closeToFeet = bounds.max.y <= spawnY + 0.2f;

        return veryFlat && closeToFeet;
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

    void CreateOuterWalls()
    {
        GameObject wallParent = new GameObject("Generated_OuterWalls");

        CreateWall(
            wallParent.transform,
            "OuterWall_North",
            new Vector3(mapCenter.x, mapCenter.y + outerWallHeight * 0.5f, mapCenter.z + mapHalfSizeZ),
            new Vector3(mapHalfSizeX * 2f, outerWallHeight, outerWallThickness)
        );

        CreateWall(
            wallParent.transform,
            "OuterWall_South",
            new Vector3(mapCenter.x, mapCenter.y + outerWallHeight * 0.5f, mapCenter.z - mapHalfSizeZ),
            new Vector3(mapHalfSizeX * 2f, outerWallHeight, outerWallThickness)
        );

        CreateWall(
            wallParent.transform,
            "OuterWall_East",
            new Vector3(mapCenter.x + mapHalfSizeX, mapCenter.y + outerWallHeight * 0.5f, mapCenter.z),
            new Vector3(outerWallThickness, outerWallHeight, mapHalfSizeZ * 2f)
        );

        CreateWall(
            wallParent.transform,
            "OuterWall_West",
            new Vector3(mapCenter.x - mapHalfSizeX, mapCenter.y + outerWallHeight * 0.5f, mapCenter.z),
            new Vector3(outerWallThickness, outerWallHeight, mapHalfSizeZ * 2f)
        );
    }

    void CreateWall(Transform parent, string wallName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    void SnapMapCenterToGround()
    {
        Vector3 rayOrigin = mapCenter + Vector3.up * surfaceRaycastHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, surfaceRaycastHeight * 2f, spawnSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            mapCenter.y = hit.point.y;
        }
    }
}
