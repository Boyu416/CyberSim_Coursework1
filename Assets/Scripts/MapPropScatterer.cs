using System.Collections.Generic;
using UnityEngine;

public class MapPropScatterer : MonoBehaviour
{
    [Header("Source Objects")]
    public string floorObjectName = "BG";
    public string sourceRootName = "Demo";

    [Header("Counts")]
    public int boxGroupCount = 25;
    public int minBoxesPerGroup = 3;
    public int maxBoxesPerGroup = 3;
    public int buildingCount = 8;
    public int smallCoverCount = 0;

    [Header("Placement")]
    public bool scatterOnStart = true;
    public float edgeMargin = 5f;
    public float minDistanceFromPlayer = 7f;
    public float maxPlacementHeight = 2f;
    public int maxAttemptsPerObject = 120;
    public LayerMask blockingMask = ~0;

    [Header("Template Names")]
    public string[] boxNameContains =
    {
        "Box",
        "Crate"
    };

    public string[] buildingNameContains =
    {
        "Building"
    };

    public string[] coverNameContains =
    {
        "WoodPlank",
        "Stairs",
        "Brick",
        "Jug"
    };

    private Bounds floorBounds;
    private Transform player;
    private readonly List<Vector3> placedPositions = new List<Vector3>();

    void Start()
    {
        if (scatterOnStart)
        {
            ScatterProps();
        }
    }

    public void ScatterProps()
    {
        ClearPreviousProps();
        placedPositions.Clear();
        player = FindPlayer();

        if (!TryFindFloorBounds(out floorBounds))
        {
            Debug.LogWarning("MapPropScatterer could not find the yellow floor bounds.");
            return;
        }

        GameObject parent = new GameObject("Generated_MapProps");

        ScatterBoxGroups(parent.transform);

        if (smallCoverCount > 0)
        {
            ScatterCategory(parent.transform, coverNameContains, smallCoverCount, 2f);
        }

        ScatterCategory(parent.transform, buildingNameContains, buildingCount, 10f);

        Debug.Log("MapPropScatterer placed random cover props.");
    }

    void ScatterCategory(Transform parent, string[] nameFilters, int count, float minSpacing)
    {
        List<GameObject> templates = FindTemplates(nameFilters);

        if (templates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject template = templates[Random.Range(0, templates.Count)];
            TryPlaceTemplate(parent, template, minSpacing);
        }
    }

    void ScatterBoxGroups(Transform parent)
    {
        List<GameObject> templates = FindTemplates(boxNameContains);

        if (templates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < boxGroupCount; i++)
        {
            GameObject template = templates[Random.Range(0, templates.Count)];
            TryPlaceBoxGroup(parent, template, i);
        }
    }

    bool TryPlaceBoxGroup(Transform parent, GameObject template, int groupIndex)
    {
        for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
        {
            Vector3 groupCenter = GetRandomFloorPosition();

            if (player != null && Vector3.Distance(groupCenter, player.position) < minDistanceFromPlayer)
            {
                continue;
            }

            if (!IsFarEnoughFromPlacedProps(groupCenter, 4f))
            {
                continue;
            }

            GameObject groupParent = new GameObject("Cover_BoxGroup_" + groupIndex);
            groupParent.transform.SetParent(parent);
            groupParent.transform.position = groupCenter;

            int boxCount = Mathf.Clamp(Random.Range(minBoxesPerGroup, maxBoxesPerGroup + 1), 1, 3);
            bool groupPlaced = true;
            float boxHeight = GetObjectHeight(template);
            float boxFootprint = GetObjectFootprintSize(template);
            Quaternion groupRotation = Quaternion.Euler(0f, 90f * Random.Range(0, 4) + Random.Range(-8f, 8f), 0f);

            for (int i = 0; i < boxCount; i++)
            {
                Vector3 localOffset = groupRotation * GetBoxGroupOffset(i, boxFootprint);

                GameObject clone = Instantiate(
                    template,
                    groupCenter + localOffset,
                    groupRotation,
                    groupParent.transform
                );

                clone.name = "Cover_Box_" + i;
                clone.SetActive(true);
                if (i < 2)
                {
                    SnapBottomToGround(clone, floorBounds.max.y);
                }
                else
                {
                    SnapBottomToGround(clone, floorBounds.max.y + boxHeight + 0.03f);
                }

                if (IsBlocked(clone))
                {
                    groupPlaced = false;
                    break;
                }
            }

            if (!groupPlaced)
            {
                Destroy(groupParent);
                continue;
            }

            placedPositions.Add(groupCenter);
            return true;
        }

        return false;
    }

    Vector3 GetBoxGroupOffset(int index, float boxFootprint)
    {
        float spacing = boxFootprint * 1.08f;

        switch (index)
        {
            case 0:
                return new Vector3(-spacing * 0.5f, 0f, 0f);
            case 1:
                return new Vector3(spacing * 0.5f, 0f, 0f);
            default:
                return new Vector3(0f, 0f, 0f);
        }
    }

    float GetObjectHeight(GameObject template)
    {
        if (!TryGetBounds(template.transform, out Bounds bounds))
        {
            return 0.75f;
        }

        return Mathf.Max(0.5f, bounds.size.y);
    }

    float GetObjectFootprintSize(GameObject template)
    {
        if (!TryGetBounds(template.transform, out Bounds bounds))
        {
            return 1f;
        }

        return Mathf.Max(0.7f, Mathf.Max(bounds.size.x, bounds.size.z));
    }

    bool TryPlaceTemplate(Transform parent, GameObject template, float minSpacing)
    {
        for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
        {
            Vector3 position = GetRandomFloorPosition();

            if (player != null && Vector3.Distance(position, player.position) < minDistanceFromPlayer)
            {
                continue;
            }

            if (!IsFarEnoughFromPlacedProps(position, minSpacing))
            {
                continue;
            }

            Quaternion rotation = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
            GameObject clone = Instantiate(template, position, rotation, parent);
            clone.name = "Cover_" + template.name;
            clone.SetActive(true);
            SnapBottomToGround(clone, floorBounds.max.y);

            if (IsBlocked(clone))
            {
                Destroy(clone);
                continue;
            }

            placedPositions.Add(clone.transform.position);
            return true;
        }

        return false;
    }

    Vector3 GetRandomFloorPosition()
    {
        float x = Random.Range(floorBounds.min.x + edgeMargin, floorBounds.max.x - edgeMargin);
        float z = Random.Range(floorBounds.min.z + edgeMargin, floorBounds.max.z - edgeMargin);
        return new Vector3(x, floorBounds.max.y, z);
    }

    bool IsBlocked(GameObject clone)
    {
        if (!TryGetBounds(clone.transform, out Bounds bounds))
        {
            return false;
        }

        if (bounds.size.y > maxPlacementHeight && clone.name.Contains("Cover_Box"))
        {
            return true;
        }

        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.3f, bounds.extents.x * 0.9f),
            Mathf.Max(0.3f, bounds.extents.y * 0.9f),
            Mathf.Max(0.3f, bounds.extents.z * 0.9f)
        );

        Collider[] overlaps = Physics.OverlapBox(
            bounds.center,
            halfExtents,
            clone.transform.rotation,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (IsIgnoredBlockingCollider(overlaps[i], clone))
            {
                continue;
            }

            if (!overlaps[i].transform.IsChildOf(clone.transform))
            {
                return true;
            }
        }

        return false;
    }

    bool IsIgnoredBlockingCollider(Collider collider, GameObject clone)
    {
        if (collider.transform.IsChildOf(clone.transform))
        {
            return true;
        }

        if (clone.transform.parent != null && collider.transform.IsChildOf(clone.transform.parent))
        {
            return true;
        }

        if (collider.gameObject.name == floorObjectName)
        {
            return true;
        }

        if (collider.transform.root != null && collider.transform.root.name == floorObjectName)
        {
            return true;
        }

        return false;
    }

    bool IsFarEnoughFromPlacedProps(Vector3 position, float minSpacing)
    {
        for (int i = 0; i < placedPositions.Count; i++)
        {
            if (Vector3.Distance(position, placedPositions[i]) < minSpacing)
            {
                return false;
            }
        }

        return true;
    }

    List<GameObject> FindTemplates(string[] nameFilters)
    {
        List<GameObject> templates = new List<GameObject>();
        GameObject rootObject = GameObject.Find(sourceRootName);
        Transform[] searchObjects = rootObject != null
            ? rootObject.GetComponentsInChildren<Transform>(true)
            : FindObjectsByType<Transform>(FindObjectsSortMode.None);

        for (int i = 0; i < searchObjects.Length; i++)
        {
            Transform candidate = searchObjects[i];

            if (!NameMatches(candidate.name, nameFilters))
            {
                continue;
            }

            if (candidate.GetComponentInChildren<Renderer>(true) == null)
            {
                continue;
            }

            templates.Add(candidate.gameObject);
        }

        return templates;
    }

    bool NameMatches(string objectName, string[] nameFilters)
    {
        for (int i = 0; i < nameFilters.Length; i++)
        {
            if (objectName.Contains(nameFilters[i]))
            {
                return true;
            }
        }

        return false;
    }

    void SnapBottomToGround(GameObject clone, float groundY)
    {
        if (!TryGetBounds(clone.transform, out Bounds bounds))
        {
            return;
        }

        Vector3 position = clone.transform.position;
        position.y += groundY - bounds.min.y;
        clone.transform.position = position;
    }

    bool TryFindFloorBounds(out Bounds bounds)
    {
        GameObject namedFloor = GameObject.Find(floorObjectName);

        if (namedFloor != null && TryGetBounds(namedFloor.transform, out bounds))
        {
            return true;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        bool found = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        float largestArea = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds candidateBounds = renderers[i].bounds;
            float area = candidateBounds.size.x * candidateBounds.size.z;

            if (area > largestArea && candidateBounds.size.y < 1f)
            {
                largestArea = area;
                bounds = candidateBounds;
                found = true;
            }
        }

        return found;
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

    Transform FindPlayer()
    {
        GameObject foundPlayer = GameObject.Find("PlayerCapsule");
        return foundPlayer != null ? foundPlayer.transform : null;
    }

    void ClearPreviousProps()
    {
        GameObject previous = GameObject.Find("Generated_MapProps");

        if (previous != null)
        {
            Destroy(previous);
        }
    }
}
