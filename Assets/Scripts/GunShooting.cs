using UnityEngine;
using UnityEngine.InputSystem;

public class GunShooting : MonoBehaviour
{
    [Header("射击设置")]
    public float damage = 25f;      // 单发伤害
    public float fireRate = 0.1f;   // 射速(秒)
    public int magazineSize = 30;   // 弹夹容量
    public float reloadTime = 2f;   // 换弹时间

    private int currentAmmo;
    private float lastFireTime = 0f;
    private bool isReloading = false;
    private Camera playerCam;
    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();
    }

    void Start()
    {
        playerCam = Camera.main;
        currentAmmo = magazineSize;
    }

    void Update()
    {
        // 射击：左键触发
        if (inputActions.Player.Fire.triggered && 
            !isReloading && currentAmmo > 0 && 
            Time.time > lastFireTime + fireRate)
        {
            Shoot();
        }

        // 换弹：R键
        if (inputActions.Player.Reload.triggered && currentAmmo < magazineSize && !isReloading)
        {
            StartCoroutine(ReloadCoroutine());
        }
    }

    void Shoot()
    {
        currentAmmo--;
        lastFireTime = Time.time;

        // 射线从摄像机中心发射
        Ray ray = playerCam.ScreenPointToRay(new Vector3(Screen.width/2f, Screen.height/2f));
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Debug.Log($"射中: {hit.collider.name}");  // Console看击中

            // 伤害靶子
            TargetHealth target = hit.collider.GetComponent<TargetHealth>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }
        }
    }

    System.Collections.IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        Debug.Log("换弹中...");
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = magazineSize;
        isReloading = false;
        Debug.Log($"换弹完成！弹药: {currentAmmo}");
    }

    void OnDestroy()
    {
        inputActions?.Disable();
    }
}
