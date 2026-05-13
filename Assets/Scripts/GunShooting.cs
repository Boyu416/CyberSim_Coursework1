using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(10000)]
public class GunShooting : MonoBehaviour
{
    public static event System.Action ShotFired;
    public static event System.Action ReloadNeeded;
    public static event System.Action ReloadCompleted;

    [Header("Shooting")]
    public float damage = 25f;
    public float fireRate = 0.1f;
    public int magazineSize = 30;
    public int maxReserveAmmo = 90;
    public float reloadTime = 2f;
    public float shootDistance = 100f;

    [Header("UI And Effects")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI reloadingText;
    public LineRenderer tracerLine;
    public GameObject hitMarker;

    [Header("Audio")]
    public AudioSource weaponAudioSource;
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public float fireVolume = 0.8f;
    public float reloadVolume = 0.8f;
    public float fireSoundStartTime = 0.5f;

    [Header("Tracer")]
    public Vector3 tracerStartOffset = new Vector3(0.35f, -0.22f, 0.75f);
    public float tracerDuration = 0.05f;

    [Header("Recoil Rotation")]
    public bool forceReducedRecoil = true;
    public float recoilX = -2.6f;
    public float recoilY = 0.35f;
    public float recoilZ = 0.18f;
    public float akVerticalBuildUp = -0.14f;
    public float akMaxVerticalRecoil = -4.0f;
    public float akLeftBias = -0.42f;
    public float akHorizontalRandomness = 0.16f;
    public float recoilPatternResetTime = 0.28f;
    public float snappiness = 20f;
    public float returnSpeed = 8f;

    [Header("Recoil Position")]
    public float kickBack = 0.045f;
    public float kickUp = 0.02f;
    public float kickSide = 0.018f;
    public float positionSnappiness = 25f;
    public float positionReturnSpeed = 10f;

    [Header("Debug")]
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
    private Coroutine hitMarkerRoutine;
    private int recoilShotIndex;
    private float lastShotTime;

    void Awake()
    {
        inputActions = new PlayerInputActions();

        playerCam = GetComponent<Camera>();

        if (playerCam == null)
        {
            playerCam = Camera.main;
        }

        if (weaponAudioSource == null)
        {
            weaponAudioSource = GetComponent<AudioSource>();
        }

        if (weaponAudioSource == null)
        {
            weaponAudioSource = gameObject.AddComponent<AudioSource>();
        }

        AutoAssignSoundClips();
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
        if (forceReducedRecoil)
        {
            ApplyReducedRecoil();
        }

        currentAmmo = magazineSize;
        currentReserve = maxReserveAmmo;

        UpdateAmmoUI();

        if (tracerLine != null)
        {
            tracerLine.enabled = false;
            tracerLine.positionCount = 2;
        }

        if (hitMarker != null)
        {
            hitMarker.SetActive(false);
        }

        if (reloadingText == null)
        {
            reloadingText = CreateReloadingText();
        }

        if (reloadingText != null)
        {
            reloadingText.gameObject.SetActive(false);
        }

        if (playerCam == null)
        {
            Debug.LogWarning("GunShooting could not find a Camera. Put this script on MainCamera or assign Camera.main.");
        }

        if (ammoText == null)
        {
            Debug.LogWarning("GunShooting has no Ammo Text assigned.");
        }

        if (tracerLine == null)
        {
            Debug.LogWarning("GunShooting has no Tracer Line assigned.");
        }

        if (weaponAudioSource == null)
        {
            Debug.LogWarning("GunShooting has no AudioSource assigned.");
        }

        if (fireSound == null)
        {
            Debug.LogWarning("GunShooting has no Fire Sound assigned.");
        }

        if (reloadSound == null)
        {
            Debug.LogWarning("GunShooting has no Reload Sound assigned.");
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
            ReloadNeeded?.Invoke();
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
        PlayFireSound();
        AddRecoil();
        ShotFired?.Invoke();

        if (currentAmmo <= 0)
        {
            ReloadNeeded?.Invoke();
        }

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
                    ShowHitMarker();
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

    void PlayFireSound()
    {
        if (weaponAudioSource == null || fireSound == null)
        {
            return;
        }

        PlayClipFromTime(fireSound, fireVolume, fireSoundStartTime, "GunshotAudio");
    }

    void PlayReloadSound()
    {
        if (weaponAudioSource == null || reloadSound == null)
        {
            return;
        }

        weaponAudioSource.PlayOneShot(reloadSound, reloadVolume);
    }

    void PlayClipFromTime(AudioClip clip, float volume, float startTime, string audioObjectName)
    {
        if (clip == null)
        {
            return;
        }

        float clampedStartTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
        GameObject audioObject = new GameObject(audioObjectName);
        audioObject.transform.position = transform.position;

        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.spatialBlend = weaponAudioSource != null ? weaponAudioSource.spatialBlend : 0f;
        audioSource.outputAudioMixerGroup = weaponAudioSource != null ? weaponAudioSource.outputAudioMixerGroup : null;
        audioSource.time = clampedStartTime;
        audioSource.Play();

        Destroy(audioObject, clip.length - clampedStartTime + 0.1f);
    }

    void AddRecoil()
    {
        if (Time.time - lastShotTime > recoilPatternResetTime)
        {
            recoilShotIndex = 0;
        }

        recoilShotIndex++;
        lastShotTime = Time.time;

        float verticalRecoil = Mathf.Max(
            akMaxVerticalRecoil,
            recoilX + akVerticalBuildUp * Mathf.Min(recoilShotIndex, 10)
        );

        float horizontalRecoil =
            akLeftBias +
            Mathf.Sin(recoilShotIndex * 0.75f) * recoilY +
            Random.Range(-akHorizontalRandomness, akHorizontalRandomness);

        float randomZ = Random.Range(-recoilZ, recoilZ);

        targetRecoil += new Vector3(
            verticalRecoil,
            horizontalRecoil,
            randomZ
        );

        float randomSide = Random.Range(-kickSide, kickSide);

        targetPositionKick += new Vector3(
            randomSide,
            kickUp,
            -kickBack
        );
    }

    void ApplyReducedRecoil()
    {
        recoilX = -2.6f;
        recoilY = 0.35f;
        recoilZ = 0.18f;
        akVerticalBuildUp = -0.14f;
        akMaxVerticalRecoil = -4.0f;
        akLeftBias = -0.42f;
        akHorizontalRandomness = 0.16f;
        recoilPatternResetTime = 0.28f;
        kickBack = 0.045f;
        kickUp = 0.02f;
        kickSide = 0.018f;
        returnSpeed = 9f;
        positionReturnSpeed = 11f;
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
        PlayReloadSound();
        SetReloadingTextVisible(true);

        yield return new WaitForSeconds(reloadTime);

        int ammoNeeded = magazineSize - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, currentReserve);

        currentAmmo += ammoToLoad;
        currentReserve -= ammoToLoad;

        isReloading = false;
        reloadRoutine = null;
        SetReloadingTextVisible(false);

        UpdateAmmoUI();
        ReloadCompleted?.Invoke();
    }

    void ShowTracerEffect(Vector3 targetPoint)
    {
        if (tracerLine == null || playerCam == null)
        {
            return;
        }

        Vector3 startPoint =
            playerCam.transform.position +
            playerCam.transform.right * tracerStartOffset.x +
            playerCam.transform.up * tracerStartOffset.y +
            playerCam.transform.forward * tracerStartOffset.z;

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

    void ShowHitMarker()
    {
        if (hitMarker == null)
        {
            return;
        }

        if (hitMarkerRoutine != null)
        {
            StopCoroutine(hitMarkerRoutine);
        }

        hitMarkerRoutine = StartCoroutine(HitMarkerCoroutine());
    }

    IEnumerator HitMarkerCoroutine()
    {
        hitMarker.SetActive(true);

        yield return new WaitForSeconds(0.08f);

        hitMarker.SetActive(false);
        hitMarkerRoutine = null;
    }

    void UpdateAmmoUI()
    {
        if (ammoText != null)
        {
            ammoText.text = currentAmmo + " / " + currentReserve;
        }
    }

    void SetReloadingTextVisible(bool visible)
    {
        if (reloadingText != null)
        {
            reloadingText.gameObject.SetActive(visible);
        }
    }

    TextMeshProUGUI CreateReloadingText()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        GameObject textObject = new GameObject("ReloadingText");
        textObject.transform.SetParent(canvas.transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = "Reloading";
        text.fontSize = 42f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform rectTransform = text.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = new Vector2(0f, 120f);
        rectTransform.sizeDelta = new Vector2(500f, 80f);

        return text;
    }

    void AutoAssignSoundClips()
    {
#if UNITY_EDITOR
        if (fireSound == null)
        {
            fireSound = FindAudioClipInSoundsFolder("Gunshot");
        }

        if (reloadSound == null)
        {
            reloadSound = FindAudioClipInSoundsFolder("Reloading");
        }
#endif
    }

#if UNITY_EDITOR
    AudioClip FindAudioClipInSoundsFolder(string clipName)
    {
        string[] guids = AssetDatabase.FindAssets(clipName + " t:AudioClip", new[] { "Assets/Sounds" });

        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    void OnValidate()
    {
        AutoAssignSoundClips();
    }
#endif

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }

        SetReloadingTextVisible(false);
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
        }
    }
}
