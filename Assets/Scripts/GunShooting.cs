using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

[DefaultExecutionOrder(10000)]
public class GunShooting : MonoBehaviour
{
    [Header("射击设置")]
    public float damage = 25f;
    public float fireRate = 0.1f;
    public int magazineSize = 30;
    public int maxReserveAmmo = 90;
    public float reloadTime = 2f;
    public float shootDistance = 100f;

    [Header("外接系统：UI 与特效")]
    public TextMeshProUGUI ammoText;
    public LineRenderer tracerLine;

    [Header("曳光弹设置")]
    public Transform muzzleParent; 
    public Vector3 muzzleLocalOffset = new Vector3(0f, 0.08f, 0.75f);
    public float tracerDuration = 0.05f;

    [Header("后坐力：画面旋转")]
    public float recoilX = -4f;
    public float recoilY = 1.2f;
    public float recoilZ = 0.4f;
    public float snappiness = 20f;
    public float returnSpeed = 8f;

    [Header("后坐力：画面位移")]
    public float kickBack = 0.06f;
    public float kickUp = 0.025f;
    public float kickSide = 0.025f;
    public float positionSnappiness = 25f;
    public float positionReturnSpeed = 10f;

    [Header("调试")]
    public bool showDebugLog = false;

    private int currentAmmo;
    private int currentReserve;

    private float nextFireTime;
    private bool isReloading;

    private Camera playerCam;
    private PlayerInputActions inputActions;

    private Vector3 currentRecoil;
    private Vector3 targetRecoil;

    private Vector3 currentPositionKick;
    private Vector3 targetPositionKick;

    private Coroutine reloadRoutine;
    private Coroutine tracerRoutine;

    void Awake()
    {
        inputActions = new PlayerInputActions();

        playerCam = GetComponent<Camera>();

        if (playerCam == null)
        {
            playerCam = Camera.main;
        }
    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }
    }

    void Start()
    {
        currentAmmo = magazineSize;
        currentReserve = maxReserveAmmo;

        UpdateAmmoUI();

        if (tracerLine != null)
        {
            tracerLine.enabled = false;
            tracerLine.positionCount = 2;
        }

        if (playerCam == null)
        {
            Debug.LogWarning("GunShooting 没有找到 Camera。请确认脚本挂在 MainCamera 上。");
        }

        if (ammoText == null)
        {
            Debug.LogWarning("GunShooting 的 Ammo Text 没有绑定。");
        }

        if (tracerLine == null)
        {
            Debug.LogWarning("GunShooting 的 Tracer Line 没有绑定。");
        }

        if (muzzleParent == null)
        {
            Debug.LogWarning("GunShooting 的 Muzzle Parent 没有绑定。曳光弹会退回使用摄像机偏移。");
        }
    }

    void Update()
    {
        HandleShootingInput();
        HandleReloadInput();
        UpdateRecoil();
    }

    void LateUpdate()
    {
        ApplyRecoilAfterCinemachine();
    }

    void HandleShootingInput()
    {
        if (isReloading)
        {
            return;
        }

        if (playerCam == null)
        {
            return;
        }

        bool isShooting = inputActions.Player.Fire.IsPressed();

        if (!isShooting)
        {
            return;
        }

        if (currentAmmo <= 0)
        {
            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        Shoot();
    }

    void HandleReloadInput()
    {
        if (inputActions.Player.Reload.triggered)
        {
            TryReload();
        }
    }

    void Shoot()
    {
        currentAmmo--;
        nextFireTime = Time.time + fireRate;

        UpdateAmmoUI();

        AddRecoil();

        Ray ray = playerCam.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
        );

        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, shootDistance))
        {
            targetPoint = hit.point;

            bool hitSelf = hit.collider.transform.root == transform.root;

            if (!hitSelf)
            {
                TargetHealth target = hit.collider.GetComponentInParent<TargetHealth>();

                if (target != null)
                {
                    target.TakeDamage(damage);
                }

                if (showDebugLog)
                {
                    Debug.Log("Hit: " + hit.collider.name);
                }
            }
        }
        else
        {
            targetPoint = ray.origin + ray.direction * shootDistance;
        }

        ShowTracerEffect(targetPoint);
    }

    void AddRecoil()
    {
        float randomY = Random.Range(-recoilY, recoilY);
        float randomZ = Random.Range(-recoilZ, recoilZ);

        targetRecoil += new Vector3(
            recoilX,
            randomY,
            randomZ
        );

        float randomSide = Random.Range(-kickSide, kickSide);

        targetPositionKick += new Vector3(
            randomSide,
            kickUp,
            -kickBack
        );
    }

    void UpdateRecoil()
    {
        targetRecoil = Vector3.Lerp(
            targetRecoil,
            Vector3.zero,
            returnSpeed * Time.deltaTime
        );

        currentRecoil = Vector3.Lerp(
            currentRecoil,
            targetRecoil,
            snappiness * Time.deltaTime
        );

        targetPositionKick = Vector3.Lerp(
            targetPositionKick,
            Vector3.zero,
            positionReturnSpeed * Time.deltaTime
        );

        currentPositionKick = Vector3.Lerp(
            currentPositionKick,
            targetPositionKick,
            positionSnappiness * Time.deltaTime
        );
    }

    void ApplyRecoilAfterCinemachine()
    {
        if (playerCam == null)
        {
            return;
        }

        playerCam.transform.localEulerAngles += currentRecoil;
        playerCam.transform.localPosition += currentPositionKick;
    }

    void TryReload()
    {
        if (isReloading)
        {
            return;
        }

        if (currentAmmo >= magazineSize)
        {
            return;
        }

        if (currentReserve <= 0)
        {
            return;
        }

        reloadRoutine = StartCoroutine(ReloadCoroutine());
    }

    IEnumerator ReloadCoroutine()
    {
        isReloading = true;

        yield return new WaitForSeconds(reloadTime);

        int ammoNeeded = magazineSize - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, currentReserve);

        currentAmmo += ammoToLoad;
        currentReserve -= ammoToLoad;

        isReloading = false;
        reloadRoutine = null;

        UpdateAmmoUI();
    }

    Vector3 GetTracerStartPoint()
    {
        if (muzzleParent != null)
        {
            return muzzleParent.TransformPoint(muzzleLocalOffset);
        }

        if (playerCam != null)
        {
            return playerCam.transform.position
                + playerCam.transform.right * 0.35f
                + playerCam.transform.up * -0.22f
                + playerCam.transform.forward * 0.75f;
        }

        return transform.position;
    }

    void ShowTracerEffect(Vector3 targetPoint)
    {
        if (tracerLine == null)
        {
            return;
        }

        Vector3 startPoint = GetTracerStartPoint();

        if (tracerRoutine != null)
        {
            StopCoroutine(tracerRoutine);
        }

        tracerRoutine = StartCoroutine(
            ShowTracer(startPoint, targetPoint)
        );
    }

    IEnumerator ShowTracer(Vector3 startPoint, Vector3 endPoint)
    {
        tracerLine.enabled = true;
        tracerLine.positionCount = 2;

        tracerLine.SetPosition(0, startPoint);
        tracerLine.SetPosition(1, endPoint);

        yield return new WaitForSeconds(tracerDuration);

        tracerLine.enabled = false;
        tracerRoutine = null;
    }

    void UpdateAmmoUI()
    {
        if (ammoText != null)
        {
            ammoText.text = currentAmmo + " / " + currentReserve;
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
        }
    }
}