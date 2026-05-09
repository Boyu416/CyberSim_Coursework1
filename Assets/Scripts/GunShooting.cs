using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

// 执行顺序尽量靠后，确保在 Cinemachine 结算之后再叠加后坐力
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
    public Transform gunBarrel;
    public LineRenderer tracerLine;

    [Header("后坐力：画面旋转")]
    public float recoilX = -4f;          // 垂直上跳，负数越大越明显
    public float recoilY = 1.2f;         // 左右随机晃动
    public float recoilZ = 0.4f;         // 轻微倾斜画面
    public float snappiness = 20f;       // 后坐力爆发速度
    public float returnSpeed = 8f;       // 回正速度

    [Header("后坐力：画面位移")]
    public float kickBack = 0.06f;       // 摄像机向后震一下
    public float kickUp = 0.025f;        // 摄像机向上震一下
    public float kickSide = 0.025f;      // 摄像机左右震一下
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

        // 优先找挂载这个脚本的物体上的 Camera
        playerCam = GetComponent<Camera>();

        // 如果找不到，再找场景里的 MainCamera
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
            Debug.LogWarning("GunShooting 的 Ammo Text 没有绑定。请把 Canvas 里的 AmmoText 拖到 Ammo Text 槽里。");
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

            // 防止打到自己
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

        // 旋转后坐力：上跳 + 左右晃 + 轻微倾斜
        targetRecoil += new Vector3(
            recoilX,
            randomY,
            randomZ
        );

        float randomSide = Random.Range(-kickSide, kickSide);

        // 位移后坐力：镜头向后、向上、左右轻微震动
        targetPositionKick += new Vector3(
            randomSide,
            kickUp,
            -kickBack
        );
    }

    void UpdateRecoil()
    {
        // 旋转后坐力逐渐回正
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

        // 位移后坐力逐渐回正
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

        // Cinemachine 会在每帧控制摄像机，
        // 所以这里在 LateUpdate 最后强行叠加画面晃动。
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

        // 这里不再显示 Reloading...，避免文字换行后被屏幕底部挡住
        yield return new WaitForSeconds(reloadTime);

        int ammoNeeded = magazineSize - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, currentReserve);

        currentAmmo += ammoToLoad;
        currentReserve -= ammoToLoad;

        isReloading = false;
        reloadRoutine = null;

        UpdateAmmoUI();
    }

    void ShowTracerEffect(Vector3 targetPoint)
    {
        if (gunBarrel == null || tracerLine == null)
        {
            return;
        }

        if (tracerRoutine != null)
        {
            StopCoroutine(tracerRoutine);
        }

        tracerRoutine = StartCoroutine(
            ShowTracer(gunBarrel.position, targetPoint)
        );
    }

    IEnumerator ShowTracer(Vector3 startPoint, Vector3 endPoint)
    {
        tracerLine.enabled = true;
        tracerLine.positionCount = 2;

        tracerLine.SetPosition(0, startPoint);
        tracerLine.SetPosition(1, endPoint);

        yield return new WaitForSeconds(0.05f);

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