using System.Collections.Generic;
using UnityEngine;

public class FloorBoundaryWalls : MonoBehaviour
{
    [Header("Source Objects")]
    public string floorObjectName = "BG";
    public string sourceRootName = "Demo";
    public string[] wallNameContains =
    {
        "Wall",
        "WallPart"
    };

    [Header("Boundary")]
    public bool buildOnStart = true;
    public float edgeInset = 0.6f;
    public float segmentOverlap = 0.15f;
    public float oldWallEdgeTolerance = 4f;

    [Header("Cleanup")]
    public bool disableOldPerimeterWalls = true;

    void Start()
    {
        if (buildOnStart)
        {
            BuildBoundaryWalls();
        }
    }

    public void BuildBoundaryWalls()
    {
        ClearPreviousBoundaryWalls();

        if (!TryFindFloorBounds(out Bounds floorBounds))
        {
            Debug.LogWarning("FloorBoundaryWalls could not find the yellow floor bounds.");
            return;
        }

        if (!TryFindWallTemplate(out GameObject wallTemplate))
        {
            Debug.LogWarning("FloorBoundaryWalls could not find an existing wall object to clone.");
            return;
        }

        if (disableOldPerimeterWalls)
        {
            DisableOldPerimeterWalls();
        }

        GameObject parent = new GameObject("Generated_FloorBoundaryWalls");

        float wallLength = GetWallLength(wallTemplate);
        float spacing = Mathf.Max(0.5f, wallLength * (1f - segmentOverlap));

        float minX = floorBounds.min.x + edgeInset;
        float maxX = floorBounds.max.x - edgeInset;
        float minZ = floorBounds.min.z + edgeInset;
        float maxZ = floorBounds.max.z - edgeInset;
        float floorY = floorBounds.max.y;

        BuildWallLine(parent.transform, wallTemplate, new Vector3(minX, floorY, maxZ), new Vector3(maxX, floorY, maxZ), spacing, 0f);
        BuildWallLine(parent.transform, wallTemplate, new Vector3(minX, floorY, minZ), new Vector3(maxX, floorY, minZ), spacing, 180f);
        BuildWallLine(parent.transform, wallTemplate, new Vector3(maxX, floorY, minZ), new Vector3(maxX, floorY, maxZ), spacing, 90f);
        BuildWallLine(parent.transform, wallTemplate, new Vector3(minX, floorY, minZ), new Vector3(minX, floorY, maxZ), spacing, -90f);

        Debug.Log("FloorBoundaryWalls built walls around the floor.");
    }

    void ClearPreviousBoundaryWalls()
    {
        GameObject previous = GameObject.Find("Generated_FloorBoundaryWalls");

        if (previous != null)
        {
            Destroy(previous);
        }
    }

    bool TryFindFloorBounds(out Bounds floorBounds)
    {
        GameObject namedFloor = GameObject.Find(floorObjectName);

        if (namedFloor != null && TryGetBounds(namedFloor.transform, out floorBounds))
        {
            return true;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        bool found = false;
        floorBounds = new Bounds(Vector3.zero, Vector3.zero);
        float largestArea = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds bounds = renderers[i].bounds;
            float area = bounds.size.x * bounds.size.z;

            if (area > largestArea && bounds.size.y < 1f)
            {
                largestArea = area;
                floorBounds = bounds;
                found = true;
            }
        }

        return found;
    }

    bool TryFindWallTemplate(out GameObject wallTemplate)
    {
        Transform sourceRoot = null;
        GameObject rootObject = GameObject.Find(sourceRootName);

        if (rootObject != null)
        {
            sourceRoot = rootObject.transform;
        }

        Transform[] searchObjects = sourceRoot != null
            ? sourceRoot.GetComponentsInChildren<Transform>(true)
            : FindObjectsByType<Transform>(FindObjectsSortMode.None);

        for (int i = 0; i < searchObjects.Length; i++)
        {
            Transform candidate = searchObjects[i];

            if (!LooksLikeWall(candidate.name))
            {
                continue;
            }

            if (candidate.GetComponentInChildren<Renderer>(true) == null &&
                candidate.GetComponentInChildren<Collider>(true) == null)
            {
                continue;
            }

            wallTemplate = candidate.gameObject;
            return true;
        }

        wallTemplate = null;
        return false;
    }

    void DisableOldPerimeterWalls()
    {
        GameObject rootObject = GameObject.Find(sourceRootName);

        if (rootObject == null || !TryGetBounds(rootObject.transform, out Bounds rootBounds))
        {
            return;
        }

        Transform[] children = rootObject.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (!LooksLikeWall(child.name) || !TryGetBounds(child, out Bounds wallBounds))
            {
                continue;
            }

            bool nearOuterEdge =
                Mathf.Abs(wallBounds.center.x - rootBounds.min.x) <= oldWallEdgeTolerance ||
                Mathf.Abs(wallBounds.center.x - rootBounds.max.x) <= oldWallEdgeTolerance ||
                Mathf.Abs(wallBounds.center.z - rootBounds.min.z) <= oldWallEdgeTolerance ||
                Mathf.Abs(wallBounds.center.z - rootBounds.max.z) <= oldWallEdgeTolerance;

            if (nearOuterEdge)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    void BuildWallLine(
        Transform parent,
        GameObject wallTemplate,
        Vector3 start,
        Vector3 end,
        float spacing,
        float yRotation)
    {
        float distance = Vector3.Distance(start, end);
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = segmentCount == 0 ? 0f : (float)i / segmentCount;
            Vector3 position = Vector3.Lerp(start, end, t);
            GameObject wall = Instantiate(wallTemplate, position, Quaternion.Euler(0f, yRotation, 0f), parent);
            wall.name = "Boundary_" + wallTemplate.name;
            wall.SetActive(true);
            SnapBottomToGround(wall, position.y);
        }
    }

    void SnapBottomToGround(GameObject wall, float groundY)
    {
        if (!TryGetBounds(wall.transform, out Bounds bounds))
        {
            return;
        }

        Vector3 position = wall.transform.position;
        position.y += groundY - bounds.min.y;
        wall.transform.position = position;
    }

    float GetWallLength(GameObject wallTemplate)
    {
        if (!TryGetBounds(wallTemplate.transform, out Bounds bounds))
        {
            return 3f;
        }

        return Mathf.Max(bounds.size.x, bounds.size.z);
    }

    bool TryGetBounds(Transform root, out Bounds bounds)
    {
        List<Renderer> renderers = new List<Renderer>(root.GetComponentsInChildren<Renderer>(true));
        List<Collider> colliders = new List<Collider>(root.GetComponentsInChildren<Collider>(true));

        bool hasBounds = false;
        bounds = new Bounds(root.position, Vector3.zero);

        for (int i = 0; i < renderers.Count; i++)
        {
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        for (int i = 0; i < colliders.Count; i++)
        {
            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        return hasBounds;
    }

    bool LooksLikeWall(string objectName)
    {
        for (int i = 0; i < wallNameContains.Length; i++)
        {
            if (objectName.Contains(wallNameContains[i]))
            {
                return true;
            }
        }

        return false;
    }
}
