using System.Collections;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyBotAI : MonoBehaviour
{
    public Transform player;
    public PlayerHealth playerHealth;

    [Header("Movement")]
    public float detectDistance = 45f;
    public float attackDistance = 10f;
    public float stopDistance = 2.2f;
    public float moveSpeed = 3.5f;
    public float turnSpeed = 8f;
    public bool forceManualMovement = false;
    public bool moveWhileAttacking = true;

    [Header("Attack")]
    public float attackInterval = 2.5f;
    public int damage = 5;
    public Color tracerColor = Color.yellow;
    public float tracerDuration = 0.12f;
    public LayerMask lineOfSightMask = ~0;
    public AudioClip botFireSound;
    public float botFireVolume = 0.55f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleMask = ~0;
    public float movementClearanceRadius = 0.45f;
    public float movementClearanceHeight = 1.8f;

    [Header("Walk Feel")]
    public float walkBobHeight = 0f;
    public float walkBobSpeed = 9f;
    public float walkLeanAngle = 8f;
    public float walkStrideDistance = 0.06f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundProbeHeight = 3f;
    public float groundProbeDistance = 7f;
    public float groundSnapOffset = 0f;

    private NavMeshAgent agent;
    private AudioSource audioSource;
    private float nextAttackTime;
    private LineRenderer tracerLine;
    private Coroutine tracerRoutine;
    private Transform visualRoot;
    private Vector3 visualRestLocalPosition;
    private Quaternion visualRestLocalRotation;
    private bool isMoving;

    void Awake()
    {
        TrySetupNavMeshAgent();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.spatialBlend = 0f;
        audioSource.maxDistance = 45f;
        audioSource.volume = botFireVolume;
        AutoAssignBotFireSound();

        CreateTracerLine();
    }

    void Start()
    {
        CacheVisualRoot();
    }

    void Update()
    {
        isMoving = false;

        if (player == null || playerHealth == null || playerHealth.IsDead)
        {
            StopMoving();
            UpdateWalkVisual();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer > detectDistance)
        {
            StopMoving();
            UpdateWalkVisual();
            return;
        }

        FacePlayer();

        bool canSeePlayer = CanSeePlayer();

        bool shouldMove = moveWhileAttacking
            ? distanceToPlayer > stopDistance || !canSeePlayer
            : distanceToPlayer > attackDistance || !canSeePlayer;

        if (shouldMove)
        {
            MoveTowardPlayer();
            StickToGround();
            UpdateWalkVisual();
        }
        else
        {
            StopMoving();
            StickToGround();
            UpdateWalkVisual();
        }

        if (canSeePlayer && distanceToPlayer <= attackDistance)
        {
            TryAttack();
        }
    }

    void MoveTowardPlayer()
    {
        if (!forceManualMovement && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            isMoving = true;
            return;
        }

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            MoveWithObstacleAvoidance(direction.normalized);
        }
    }

    void MoveWithObstacleAvoidance(Vector3 desiredDirection)
    {
        Vector3[] directions =
        {
            desiredDirection,
            Quaternion.Euler(0f, 35f, 0f) * desiredDirection,
            Quaternion.Euler(0f, -35f, 0f) * desiredDirection,
            Quaternion.Euler(0f, 75f, 0f) * desiredDirection,
            Quaternion.Euler(0f, -75f, 0f) * desiredDirection,
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i].normalized;

            if (!IsMovementBlocked(direction))
            {
                transform.position += direction * moveSpeed * Time.deltaTime;
                isMoving = true;
                return;
            }
        }

        transform.position += desiredDirection * (moveSpeed * 0.35f) * Time.deltaTime;
        isMoving = true;
    }

    bool IsMovementBlocked(Vector3 direction)
    {
        float moveDistance = moveSpeed * Time.deltaTime + 0.08f;
        Vector3 bottom = transform.position + Vector3.up * 0.35f;
        Vector3 top = transform.position + Vector3.up * movementClearanceHeight;

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            movementClearanceRadius,
            direction,
            moveDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitRoot = hits[i].collider.transform.root;

            if (hitRoot == transform.root || hitRoot == player.root)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    void LateUpdate()
    {
        StickToGround();
    }

    void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    void FacePlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    void TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackInterval;
        Vector3 startPoint = transform.position + Vector3.up * 1.45f;
        Vector3 endPoint = player.position + Vector3.up * 1.25f;

        ShowTracer(startPoint, endPoint);
        PlayBotFireSound();
        playerHealth.TakeDamage(damage);
    }

    void TrySetupNavMeshAgent()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 0.8f, NavMesh.AllAreas) ||
            Mathf.Abs(navHit.position.y - transform.position.y) > 0.25f)
        {
            agent = GetComponent<NavMeshAgent>();

            if (agent != null)
            {
                agent.enabled = false;
            }

            return;
        }

        transform.position = navHit.position;
        agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

        agent.speed = moveSpeed;
        agent.stoppingDistance = attackDistance;
        agent.angularSpeed = 720f;
        agent.acceleration = 16f;
    }

    public void RefreshMovementSettings()
    {
        if (agent != null)
        {
            if (forceManualMovement)
            {
                agent.enabled = false;
                return;
            }

            agent.speed = moveSpeed;
            agent.stoppingDistance = attackDistance;
            agent.angularSpeed = 900f;
            agent.acceleration = 24f;
        }
    }

    void CacheVisualRoot()
    {
        Transform foundVisual = transform.Find("BotVisual_FiringRifle");

        if (foundVisual == null)
        {
            foundVisual = transform.Find("BotVisual_Idle");
        }

        visualRoot = foundVisual != null ? foundVisual : transform;
        visualRestLocalPosition = visualRoot.localPosition;
        visualRestLocalRotation = visualRoot.localRotation;
    }

    void UpdateWalkVisual()
    {
        if (visualRoot == null)
        {
            CacheVisualRoot();
        }

        if (visualRoot == null)
        {
            return;
        }

        if (!isMoving)
        {
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, visualRestLocalPosition, Time.deltaTime * 8f);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, visualRestLocalRotation, Time.deltaTime * 8f);
            return;
        }

        float step = Time.time * walkBobSpeed;
        float bob = Mathf.Sin(step) * walkBobHeight;
        float stride = Mathf.Sin(step) * walkStrideDistance;
        float lean = Mathf.Sin(step) * walkLeanAngle;
        visualRoot.localPosition = visualRestLocalPosition + Vector3.up * bob + Vector3.forward * stride;
        visualRoot.localRotation = visualRestLocalRotation * Quaternion.Euler(0f, lean * 0.35f, lean);
    }

    void PlayBotFireSound()
    {
        if (audioSource != null && botFireSound != null)
        {
            audioSource.volume = botFireVolume;
            audioSource.PlayOneShot(botFireSound, botFireVolume);
        }
    }

    void StickToGround()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            return;
        }

        Vector3 rayOrigin = transform.position + Vector3.up * groundProbeHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            groundProbeDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit closestHit = default;
        bool foundGround = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.root == transform.root)
            {
                continue;
            }

            if (!foundGround || hits[i].distance < closestHit.distance)
            {
                closestHit = hits[i];
                foundGround = true;
            }
        }

        if (foundGround)
        {
            Vector3 position = transform.position;
            position.y = closestHit.point.y + groundSnapOffset;
            transform.position = position;
        }
    }

    void AutoAssignBotFireSound()
    {
#if UNITY_EDITOR
        if (botFireSound == null)
        {
            botFireSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Gunshot for bot.wav");
        }
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        AutoAssignBotFireSound();
    }
#endif

    bool CanSeePlayer()
    {
        Vector3 startPoint = transform.position + Vector3.up * 1.45f;
        Vector3 endPoint = player.position + Vector3.up * 0.9f;
        Vector3 direction = endPoint - startPoint;
        float castDistance = direction.magnitude + 0.5f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            startPoint,
            0.25f,
            direction.normalized,
            castDistance,
            lineOfSightMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit closestHit = default;
        bool foundHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.root == transform.root)
            {
                continue;
            }

            if (!foundHit || hits[i].distance < closestHit.distance)
            {
                closestHit = hits[i];
                foundHit = true;
            }
        }

        return foundHit && closestHit.collider.transform.root == player.root;
    }

    void CreateTracerLine()
    {
        GameObject tracerObject = new GameObject("EnemyYellowTracer");
        tracerObject.transform.SetParent(transform, false);

        tracerLine = tracerObject.AddComponent<LineRenderer>();
        tracerLine.positionCount = 2;
        tracerLine.startWidth = 0.045f;
        tracerLine.endWidth = 0.018f;
        tracerLine.enabled = false;

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = tracerColor;
        tracerLine.material = material;
        tracerLine.startColor = tracerColor;
        tracerLine.endColor = tracerColor;
    }

    void ShowTracer(Vector3 startPoint, Vector3 endPoint)
    {
        if (tracerRoutine != null)
        {
            StopCoroutine(tracerRoutine);
        }

        tracerRoutine = StartCoroutine(TracerCoroutine(startPoint, endPoint));
    }

    IEnumerator TracerCoroutine(Vector3 startPoint, Vector3 endPoint)
    {
        tracerLine.enabled = true;
        tracerLine.SetPosition(0, startPoint);
        tracerLine.SetPosition(1, endPoint);

        yield return new WaitForSeconds(tracerDuration);

        tracerLine.enabled = false;
        tracerRoutine = null;
    }
}
