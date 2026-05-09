using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

// 执行顺序尽量靠后，确保在 Cinemachine 结算之后再叠加后坐力
[DefaultExecutionOrder(10000)]
public class GunShooting : MonoBehaviour
{
    [Header("射击设置")]
    public float damage = 25f;          // 单发伤害
    public float fireRate = 0.1f;       // 射速，0.1 = 每秒10发
    public int magazineSize = 30;       // 弹匣容量
    public int maxReserveAmmo = 90;     // 最大备弹
    public float reloadTime = 2f;       // 换弹时间
    public float shootDistance = 100f;  // 射线距离

    [Header("外接系统：UI 与特效")]
    public TextMeshProUGUI ammoText;    // 子弹 UI
    public Transform gunBarrel;         // 枪口位置，之后做曳光弹用
    public LineRenderer tracerLine;     // 曳光弹线段，之后做曳光弹用

    [Header("CS2 风格后坐力")]
    public float recoilX = -2f;         // 垂直后坐力，负数 = 枪口上跳
    public float recoilY = 0.5f;        // 水平随机晃动
    public float snappiness = 12f;      // 后坐力爆发速度
    public float returnSpeed = 6f;      // 回正速度

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
            Debug.LogWarning("GunShooting 没有找到 Camera。请确认脚本挂在 MainCamera 上，或者场景里有带 MainCamera 标签的摄像机。");
        }

        if (ammoText == null)
        {
            Debug.LogWarning("GunShooting 的 Ammo Text 没有绑定。请把 Canvas 里的 AmmoText 拖到 MainCamera 的 Ammo Text 槽里。");
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
        float randomHorizontalRecoil = Random.Range(-recoilY, recoilY);

        targetRecoil += new Vector3(
            recoilX,
            randomHorizontalRecoil,
            0f
        );
    }

    void UpdateRecoil()
    {
        // 后坐力逐渐回正
        targetRecoil = Vector3.Lerp(
            targetRecoil,
            Vector3.zero,
            returnSpeed * Time.deltaTime
        );

        // 当前后坐力追赶目标后坐力
        currentRecoil = Vector3.Lerp(
            currentRecoil,
            targetRecoil,
            snappiness * Time.deltaTime
        );
    }

    void ApplyRecoilAfterCinemachine()
    {
        if (playerCam == null)
        {
            return;
        }

        // 这里故意使用完整 currentRecoil 叠加。
        // 因为 Cinemachine 每帧会重新控制摄像机角度，
        // 所以不能只加 delta，否则后坐力会被抵消得很弱甚至看不见。
        playerCam.transform.localEulerAngles += currentRecoil;
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

        if (ammoText != null)
        {
            ammoText.text = currentAmmo + " / " + currentReserve + "  Reloading...";
        }

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