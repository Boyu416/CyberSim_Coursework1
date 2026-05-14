using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif
using StarterAssets;

public class MainMenuController : MonoBehaviour
{
    const string SensitivityKey = "CyberSim_MouseSensitivity";
    public static bool IsMenuOpen { get; private set; }

    public Texture2D backgroundTexture;
    public float minSensitivity = 0.2f;
    public float maxSensitivity = 4f;
    public float defaultSensitivity = 1f;

    private GameObject menuRoot;
    private GameObject levelPanel;
    private GameObject settingsPanel;
    private Canvas menuCanvas;
    private GraphicRaycaster menuRaycaster;
    private Slider sensitivitySlider;
    private TextMeshProUGUI sensitivityValueText;
    private float currentSensitivity;
    private bool menuOpen;
    private readonly List<MenuButtonBinding> menuButtons = new List<MenuButtonBinding>();
    private readonly List<RaycastResult> menuRaycastResults = new List<RaycastResult>();

    class MenuButtonBinding
    {
        public RectTransform rectTransform;
        public UnityEngine.Events.UnityAction action;
    }

    void Awake()
    {
        LoadBackgroundTexture();
        currentSensitivity = PlayerPrefs.GetFloat(SensitivityKey, defaultSensitivity);
        ApplySensitivity(currentSensitivity);
    }

    public void ShowMenu()
    {
        EnsureMenu();
        menuRoot.SetActive(true);
        levelPanel.SetActive(false);
        settingsPanel.SetActive(false);
        menuOpen = true;
        IsMenuOpen = true;
        SetGameplayEnabled(false);
        DisableAllGunInputs();
        Time.timeScale = 0f;
        ForceMenuCursor();
    }

    void Update()
    {
        if (!menuOpen)
        {
            return;
        }

        ForceMenuCursor();
    }

    void LateUpdate()
    {
        if (!menuOpen)
        {
            return;
        }

        ForceMenuCursor();
        HandleManualMenuInput();
        HandleKeyboardMenuInput();

        if (!menuOpen)
        {
            return;
        }

        SetGameplayEnabled(false);
        ForceMenuCursor();
    }

    void OnGUI()
    {
        if (!menuOpen)
        {
            return;
        }

        ForceMenuCursor();
        GUI.depth = -1000;
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.022f);
        buttonStyle.normal.textColor = new Color(0.93f, 0.93f, 0.86f, 1f);
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.normal.background = Texture2D.blackTexture;

        float buttonWidth = Screen.width * 0.18f;
        float buttonHeight = Screen.height * 0.052f;
        float x = Screen.width * 0.245f;
        float y = Screen.height * 0.39f;
        float gap = Screen.height * 0.04f;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "PLAY", buttonStyle))
        {
            StartLevel(1);
        }

        if (GUI.Button(new Rect(x, y + gap, buttonWidth, buttonHeight), "SELECT LEVEL", buttonStyle))
        {
            ToggleLevelPanel();
        }

        if (GUI.Button(new Rect(x, y + gap * 2f, buttonWidth, buttonHeight), "SETTINGS", buttonStyle))
        {
            ToggleSettingsPanel();
        }

        if (GUI.Button(new Rect(x, y + gap * 3f, buttonWidth, buttonHeight), "EXIT", buttonStyle))
        {
            ExitGame();
        }

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.018f);
        hintStyle.normal.textColor = new Color(0.93f, 0.93f, 0.86f, 0.9f);
        GUI.Label(new Rect(x, y + gap * 4.1f, buttonWidth * 1.9f, buttonHeight), "Keyboard: 1 Play   2 Levels   3 Settings   Esc Exit", hintStyle);

        Event currentEvent = Event.current;

        if (currentEvent != null && currentEvent.type == EventType.KeyDown)
        {
            if (currentEvent.keyCode == KeyCode.Alpha1 || currentEvent.keyCode == KeyCode.Return)
            {
                StartLevel(1);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Alpha2)
            {
                ToggleLevelPanel();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Alpha3)
            {
                ToggleSettingsPanel();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                ExitGame();
                currentEvent.Use();
            }
        }
    }

    void EnsureMenu()
    {
        if (menuRoot != null)
        {
            return;
        }

        EnsureEventSystem();

        GameObject canvasObject = new GameObject("MainMenuCanvas");
        menuRoot = canvasObject;

        menuCanvas = canvasObject.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        menuRaycaster = canvasObject.AddComponent<GraphicRaycaster>();

        CreateBackground(canvasObject.transform);
        CreateTitle(canvasObject.transform);
        CreateMainButtons(canvasObject.transform);
        CreateLevelPanel(canvasObject.transform);
        CreateSettingsPanel(canvasObject.transform);
    }

    void CreateBackground(Transform parent)
    {
        GameObject backgroundObject = new GameObject("MenuBackground");
        backgroundObject.transform.SetParent(parent, false);
        RawImage background = backgroundObject.AddComponent<RawImage>();
        background.texture = backgroundTexture;
        background.color = Color.white;
        background.raycastTarget = false;

        RectTransform rect = background.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject shadeObject = new GameObject("MenuShade");
        shadeObject.transform.SetParent(parent, false);
        Image shade = shadeObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.38f);
        shade.raycastTarget = false;
        RectTransform shadeRect = shade.rectTransform;
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = Vector2.zero;
        shadeRect.offsetMax = Vector2.zero;
    }

    void CreateTitle(Transform parent)
    {
        TextMeshProUGUI title = CreateText(parent, "MenuTitle", "CYBERSIM", 72f, TextAlignmentOptions.Left);
        Position(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -90f), new Vector2(620f, 92f));

        TextMeshProUGUI subtitle = CreateText(parent, "MenuSubtitle", "TACTICAL TRAINING", 24f, TextAlignmentOptions.Left);
        Position(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(116f, -166f), new Vector2(520f, 44f));
    }

    void CreateMainButtons(Transform parent)
    {
        GameObject panel = new GameObject("MainMenuButtons");
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        Position(rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(115f, -60f), new Vector2(360f, 390f));

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateButton(panel.transform, "PLAY", () => StartLevel(1));
        CreateButton(panel.transform, "SELECT LEVEL", ToggleLevelPanel);
        CreateButton(panel.transform, "SETTINGS", ToggleSettingsPanel);
        CreateButton(panel.transform, "EXIT", ExitGame);
    }

    void CreateLevelPanel(Transform parent)
    {
        levelPanel = CreateSidePanel(parent, "LevelSelectPanel", new Vector2(520f, -72f), new Vector2(300f, 260f));
        CreatePanelTitle(levelPanel.transform, "SELECT LEVEL");
        CreateButton(levelPanel.transform, "LEVEL 1", () => StartLevel(1));
        CreateButton(levelPanel.transform, "LEVEL 2", () => StartLevel(2));
        CreateButton(levelPanel.transform, "LEVEL 3", () => StartLevel(3));
    }

    void CreateSettingsPanel(Transform parent)
    {
        settingsPanel = CreateSidePanel(parent, "SettingsPanel", new Vector2(520f, -72f), new Vector2(500f, 265f));
        CreatePanelTitle(settingsPanel.transform, "SETTINGS");

        TextMeshProUGUI label = CreateText(settingsPanel.transform, "SensitivityLabel", "MOUSE SENSITIVITY", 22f, TextAlignmentOptions.Left);
        label.rectTransform.sizeDelta = new Vector2(440f, 36f);

        sensitivitySlider = CreateSlider(settingsPanel.transform);
        sensitivitySlider.minValue = minSensitivity;
        sensitivitySlider.maxValue = maxSensitivity;
        sensitivitySlider.value = currentSensitivity;
        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);

        sensitivityValueText = CreateText(settingsPanel.transform, "SensitivityValue", "", 22f, TextAlignmentOptions.Left);
        sensitivityValueText.rectTransform.sizeDelta = new Vector2(440f, 36f);
        UpdateSensitivityLabel();
    }

    GameObject CreateSidePanel(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.62f);

        RectTransform rect = panel.GetComponent<RectTransform>();
        Position(rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), anchoredPosition, size);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        panel.SetActive(false);
        return panel;
    }

    void CreatePanelTitle(Transform parent, string text)
    {
        TextMeshProUGUI title = CreateText(parent, text + "Title", text, 26f, TextAlignmentOptions.Left);
        title.rectTransform.sizeDelta = new Vector2(440f, 42f);
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label + "Button");
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.04f, 0.08f, 0.12f, 0.86f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.04f, 0.08f, 0.12f, 0.86f);
        colors.highlightedColor = new Color(0.12f, 0.2f, 0.28f, 0.95f);
        colors.pressedColor = new Color(0.9f, 0.72f, 0.18f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(330f, 56f);
        menuButtons.Add(new MenuButtonBinding
        {
            rectTransform = rect,
            action = onClick
        });

        TextMeshProUGUI text = CreateText(buttonObject.transform, label + "Text", label, 24f, TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    Slider CreateSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject("SensitivitySlider");
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(440f, 38f);

        Image background = CreateSliderImage(sliderObject.transform, "Background", new Color(0.08f, 0.12f, 0.16f, 0.9f));
        Position(background.rectTransform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Image fill = CreateSliderImage(sliderObject.transform, "Fill", new Color(0.9f, 0.72f, 0.18f, 1f));
        Position(fill.rectTransform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Image handle = CreateSliderImage(sliderObject.transform, "Handle", new Color(0.95f, 0.95f, 0.9f, 1f));
        handle.rectTransform.sizeDelta = new Vector2(18f, 30f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        return slider;
    }

    Image CreateSliderImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, string text, float size, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = new Color(0.93f, 0.93f, 0.86f, 1f);
        tmp.raycastTarget = false;
        tmp.outlineColor = Color.black;
        tmp.outlineWidth = 0.18f;
        return tmp;
    }

    void StartLevel(int levelNumber)
    {
        Time.timeScale = 1f;
        menuOpen = false;
        IsMenuOpen = false;
        menuRoot.SetActive(false);
        SetGameplayEnabled(true);
        EnableAllGunInputs();
        ApplySensitivity(currentSensitivity);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        ClearTargets();
        DisableLevelScripts();

        if (levelNumber == 1)
        {
            Level1TrainingCamp level1 = gameObject.AddComponent<Level1TrainingCamp>();
            level1.enabled = true;
            return;
        }

        if (levelNumber == 2)
        {
            Level2EnemyPatrol level2 = gameObject.AddComponent<Level2EnemyPatrol>();
            level2.BeginLevel();
            return;
        }

        Level3BombDefusal level3 = gameObject.AddComponent<Level3BombDefusal>();
        level3.BeginLevel();
    }

    void DisableLevelScripts()
    {
        DestroyExistingLevelScripts<Level1TrainingCamp>();
        DestroyExistingLevelScripts<Level2EnemyPatrol>();
        DestroyExistingLevelScripts<Level3BombDefusal>();
    }

    void DestroyExistingLevelScripts<T>() where T : Component
    {
        T[] scripts = GetComponents<T>();

        for (int i = 0; i < scripts.Length; i++)
        {
            Destroy(scripts[i]);
        }
    }

    void ClearTargets()
    {
        TargetHealth[] targets = FindObjectsByType<TargetHealth>(FindObjectsSortMode.None);

        for (int i = 0; i < targets.Length; i++)
        {
            Destroy(targets[i].gameObject);
        }
    }

    void SetSensitivity(float value)
    {
        currentSensitivity = value;
        PlayerPrefs.SetFloat(SensitivityKey, currentSensitivity);
        PlayerPrefs.Save();
        ApplySensitivity(currentSensitivity);
        UpdateSensitivityLabel();
    }

    void ApplySensitivity(float value)
    {
        FirstPersonController controller = FindFirstObjectByType<FirstPersonController>();

        if (controller != null)
        {
            controller.RotationSpeed = value;
        }
    }

    void UpdateSensitivityLabel()
    {
        if (sensitivityValueText != null)
        {
            sensitivityValueText.text = "SENSITIVITY: " + currentSensitivity.ToString("0.00");
        }
    }

    void ForceMenuCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SetGameplayEnabled(bool enabledState)
    {
        StarterAssetsInputs[] inputs = FindObjectsByType<StarterAssetsInputs>(FindObjectsSortMode.None);

        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i].cursorLocked = enabledState;
            inputs[i].cursorInputForLook = enabledState;

            if (!enabledState)
            {
                inputs[i].MoveInput(Vector2.zero);
                inputs[i].LookInput(Vector2.zero);
                inputs[i].JumpInput(false);
                inputs[i].SprintInput(false);
                inputs[i].CrouchInput(false);
            }
        }

        FirstPersonController[] controllers = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            controllers[i].enabled = enabledState;
        }

        GunShooting[] guns = FindObjectsByType<GunShooting>(FindObjectsSortMode.None);

        for (int i = 0; i < guns.Length; i++)
        {
            guns[i].enabled = enabledState;
        }
    }

    void DisableAllGunInputs()
    {
        GunShooting[] guns = FindObjectsByType<GunShooting>(FindObjectsSortMode.None);

        for (int i = 0; i < guns.Length; i++)
        {
            guns[i].enabled = false;
        }
    }

    void EnableAllGunInputs()
    {
        GunShooting[] guns = FindObjectsByType<GunShooting>(FindObjectsSortMode.None);

        for (int i = 0; i < guns.Length; i++)
        {
            guns[i].enabled = true;
        }
    }

    void HandleManualMenuInput()
    {
        if (menuRaycaster == null)
        {
            return;
        }

        if (EventSystem.current == null)
        {
            EnsureEventSystem();
        }

        Vector2 pointerPosition = GetPointerPosition();
        bool pointerPressed = PointerPressedThisFrame();
        bool pointerHeld = PointerHeld();

        if (!pointerPressed && !pointerHeld)
        {
            return;
        }

        if (sensitivitySlider != null &&
            sensitivitySlider.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(sensitivitySlider.GetComponent<RectTransform>(), pointerPosition, null) &&
            pointerHeld)
        {
            UpdateSliderFromPointer(sensitivitySlider, pointerPosition);
            return;
        }

        if (pointerPressed)
        {
            for (int i = menuButtons.Count - 1; i >= 0; i--)
            {
                if (menuButtons[i].rectTransform == null ||
                    !menuButtons[i].rectTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(menuButtons[i].rectTransform, pointerPosition, null))
                {
                    menuButtons[i].action.Invoke();
                    return;
                }
            }
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = pointerPosition;
        menuRaycastResults.Clear();
        menuRaycaster.Raycast(pointerData, menuRaycastResults);

        if (menuRaycastResults.Count == 0)
        {
            return;
        }

        for (int i = 0; i < menuRaycastResults.Count; i++)
        {
            Slider slider = menuRaycastResults[i].gameObject.GetComponentInParent<Slider>();

            if (slider != null)
            {
                UpdateSliderFromPointer(slider, pointerPosition);
                return;
            }
        }

        if (!pointerPressed)
        {
            return;
        }

        for (int i = 0; i < menuRaycastResults.Count; i++)
        {
            Button button = menuRaycastResults[i].gameObject.GetComponentInParent<Button>();

            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
                return;
            }
        }
    }

    void HandleKeyboardMenuInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame ||
            keyboard.enterKey.wasPressedThisFrame)
        {
            StartLevel(1);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
        {
            ToggleLevelPanel();
        }
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
        {
            ToggleSettingsPanel();
        }
        else if (keyboard.escapeKey.wasPressedThisFrame)
        {
            ExitGame();
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ||
            Input.GetKeyDown(KeyCode.Return))
        {
            StartLevel(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            ToggleLevelPanel();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            ToggleSettingsPanel();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitGame();
        }
#endif
    }

    Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#endif
        return Input.mousePosition;
    }

    bool PointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    bool PointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
#endif
        return Input.GetMouseButton(0);
    }

    void UpdateSliderFromPointer(Slider slider, Vector2 pointerPosition)
    {
        RectTransform sliderRect = slider.GetComponent<RectTransform>();

        if (sliderRect == null)
        {
            return;
        }

        Camera uiCamera = menuCanvas != null && menuCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? menuCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(sliderRect, pointerPosition, uiCamera, out Vector2 localPoint))
        {
            return;
        }

        float normalizedValue = Mathf.InverseLerp(
            sliderRect.rect.xMin,
            sliderRect.rect.xMax,
            localPoint.x
        );

        slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(normalizedValue));
    }

    void ToggleLevelPanel()
    {
        levelPanel.SetActive(!levelPanel.activeSelf);
        settingsPanel.SetActive(false);
    }

    void ToggleSettingsPanel()
    {
        settingsPanel.SetActive(!settingsPanel.activeSelf);
        levelPanel.SetActive(false);
    }

    void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();

        for (int i = 0; i < modules.Length; i++)
        {
            modules[i].enabled = modules[i] == inputModule;
        }
#else
        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif
    }

    void LoadBackgroundTexture()
    {
#if UNITY_EDITOR
        if (backgroundTexture == null)
        {
            backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Picture/wallpaper_nologo.jpg");
        }
#endif
        if (backgroundTexture != null)
        {
            return;
        }

        string imagePath = Path.Combine(Application.dataPath, "Picture", "wallpaper_nologo.jpg");

        if (!File.Exists(imagePath))
        {
            return;
        }

        Texture2D loadedTexture = new Texture2D(2, 2);

        if (loadedTexture.LoadImage(File.ReadAllBytes(imagePath)))
        {
            backgroundTexture = loadedTexture;
        }
    }

    void Position(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }
}
