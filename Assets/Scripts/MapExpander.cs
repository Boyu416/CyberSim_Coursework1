using UnityEngine;

public class MapExpander : MonoBehaviour
{
    [Header("Source Map")]
    public Transform sourceRoot;
    public string sourceRootName = "Demo";

    [Header("Connected Tile Layout")]
    public bool expandOnStart = true;
    public int extraTiles = 1;
    public ExpansionDirection expansionDirection = ExpansionDirection.PositiveZ;
    public float tileOverlap = 0.4f;

    [Header("Middle Wall Removal")]
    public bool removeMiddleWalls = true;
    public float seamStripWidth = 4f;
    public string[] wallNameContains =
    {
        "Wall",
        "WallPart"
    };

    public enum ExpansionDirection
    {
        PositiveX,
        NegativeX,
        PositiveZ,
        NegativeZ
    }

    private Bounds sourceBounds;

    void Start()
    {
        if (expandOnStart)
        {
            ExpandMap();
        }
    }

    public void ExpandMap()
    {
        if (!TryResolveSourceRoot())
        {
            Debug.LogWarning("MapExpander could not find source map root.");
            return;
        }

        if (!TryGetBounds(sourceRoot, out sourceBounds))
        {
            Debug.LogWarning("MapExpander could not calculate source map bounds.");
            return;
        }

        SetWallsActive(sourceRoot, true);

        GameObject expandedParent = new GameObject("Generated_ConnectedMapExpansion");
        Vector3 tileOffset = GetTileOffset();

        for (int i = 1; i <= extraTiles; i++)
        {
            GameObject tileClone = Instantiate(
                sourceRoot.gameObject,
                sourceRoot.position + tileOffset * i,
                sourceRoot.rotation,
                expandedParent.transform
            );

            tileClone.name = sourceRoot.name + "_ConnectedTile_" + i;
            SetWallsActive(tileClone.transform, true);

            if (removeMiddleWalls)
            {
                RemoveWallsBetweenConnectedTiles(sourceRoot, tileClone.transform);
            }
        }

        Debug.Log("MapExpander connected " + (extraTiles + 1) + " map tiles.");
    }

    bool TryResolveSourceRoot()
    {
        if (sourceRoot != null)
        {
            return true;
        }

        GameObject foundRoot = GameObject.Find(sourceRootName);

        if (foundRoot == null)
        {
            return false;
        }

        sourceRoot = foundRoot.transform;
        return true;
    }

    Vector3 GetTileOffset()
    {
        switch (expansionDirection)
        {
            case ExpansionDirection.PositiveX:
                return Vector3.right * Mathf.Max(1f, sourceBounds.size.x - tileOverlap);
            case ExpansionDirection.NegativeX:
                return Vector3.left * Mathf.Max(1f, sourceBounds.size.x - tileOverlap);
            case ExpansionDirection.NegativeZ:
                return Vector3.back * Mathf.Max(1f, sourceBounds.size.z - tileOverlap);
            default:
                return Vector3.forward * Mathf.Max(1f, sourceBounds.size.z - tileOverlap);
        }
    }

    bool TryGetBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        bool hasBounds = false;
        bounds = new Bounds(root.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
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

        for (int i = 0; i < colliders.Length; i++)
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

    void RemoveWallsBetweenConnectedTiles(Transform firstTile, Transform secondTile)
    {
        if (!TryGetBounds(firstTile, out Bounds firstBounds) ||
            !TryGetBounds(secondTile, out Bounds secondBounds))
        {
            return;
        }

        switch (expansionDirection)
        {
            case ExpansionDirection.PositiveX:
                RemoveWallsOnBoundary(firstTile, firstBounds.max.x, Axis.X, BoundarySide.Max);
                RemoveWallsOnBoundary(secondTile, secondBounds.min.x, Axis.X, BoundarySide.Min);
                break;
            case ExpansionDirection.NegativeX:
                RemoveWallsOnBoundary(firstTile, firstBounds.min.x, Axis.X, BoundarySide.Min);
                RemoveWallsOnBoundary(secondTile, secondBounds.max.x, Axis.X, BoundarySide.Max);
                break;
            case ExpansionDirection.NegativeZ:
                RemoveWallsOnBoundary(firstTile, firstBounds.min.z, Axis.Z, BoundarySide.Min);
                RemoveWallsOnBoundary(secondTile, secondBounds.max.z, Axis.Z, BoundarySide.Max);
                break;
            default:
                RemoveWallsOnBoundary(firstTile, firstBounds.max.z, Axis.Z, BoundarySide.Max);
                RemoveWallsOnBoundary(secondTile, secondBounds.min.z, Axis.Z, BoundarySide.Min);
                break;
        }
    }

    void RemoveWallsOnBoundary(Transform tileRoot, float boundaryCoordinate, Axis axis, BoundarySide side)
    {
        foreach (Transform child in tileRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!LooksLikeWall(child.name))
            {
                continue;
            }

            if (!TryGetBounds(child, out Bounds wallBounds))
            {
                continue;
            }

            float wallEdge = GetWallEdge(wallBounds, axis, side);

            if (Mathf.Abs(wallEdge - boundaryCoordinate) <= seamStripWidth)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    float GetWallEdge(Bounds wallBounds, Axis axis, BoundarySide side)
    {
        if (axis == Axis.X)
        {
            return side == BoundarySide.Max ? wallBounds.max.x : wallBounds.min.x;
        }

        return side == BoundarySide.Max ? wallBounds.max.z : wallBounds.min.z;
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

    void SetWallsActive(Transform tileRoot, bool active)
    {
        foreach (Transform child in tileRoot.GetComponentsInChildren<Transform>(true))
        {
            if (LooksLikeWall(child.name))
            {
                child.gameObject.SetActive(active);
            }
        }
    }

    enum Axis
    {
        X,
        Z
    }

    enum BoundarySide
    {
        Min,
        Max
    }
}
