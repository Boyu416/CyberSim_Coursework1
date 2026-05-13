using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Level1TrainingCamp : MonoBehaviour
{
    [Header("Training Goal")]
    public int activeBotTarget = 30;
    public int killsRequired = 15;
    public bool maintainActiveBotCount = true;

    [Header("UI")]
    public TextMeshProUGUI promptText;
    public TextMeshProUGUI progressText;
    public float openingPromptSeconds = 3.5f;
    public float shootPromptSeconds = 1.5f;

    private BotSpawner botSpawner;
    private int kills;
    private int activeBots;
    private bool completed;
    private Coroutine promptRoutine;

    void Awake()
    {
        botSpawner = GetComponent<BotSpawner>();
    }

    void OnEnable()
    {
        TargetHealth.TargetKilled += HandleTargetKilled;
        GunShooting.ShotFired += HandleShotFired;
        GunShooting.ReloadNeeded += HandleReloadNeeded;
        GunShooting.ReloadCompleted += HandleReloadCompleted;
    }

    void Start()
    {
        if (botSpawner == null)
        {
            botSpawner = FindFirstObjectByType<BotSpawner>();
        }

        EnsureUi();
        UpdateProgressText();
        ShowOpeningPrompt();
        StartCoroutine(PrepareTrainingBots());
    }

    void OnDisable()
    {
        TargetHealth.TargetKilled -= HandleTargetKilled;
        GunShooting.ShotFired -= HandleShotFired;
        GunShooting.ReloadNeeded -= HandleReloadNeeded;
        GunShooting.ReloadCompleted -= HandleReloadCompleted;
    }

    IEnumerator PrepareTrainingBots()
    {
        yield return null;

        RemoveExistingTrainingBots();

        if (botSpawner == null)
        {
            Debug.LogWarning("Level1TrainingCamp could not find a BotSpawner.");
            yield break;
        }

        botSpawner.ClearSpawnedPositionHistory();
        activeBots = botSpawner.SpawnBots(activeBotTarget);
        Debug.Log("Level 1 Training Camp spawned " + activeBots + " training bots.");
    }

    void RemoveExistingTrainingBots()
    {
        TargetHealth[] existingTargets = FindObjectsByType<TargetHealth>(FindObjectsSortMode.None);

        for (int i = 0; i < existingTargets.Length; i++)
        {
            Destroy(existingTargets[i].gameObject);
        }

        activeBots = 0;
    }

    void HandleTargetKilled(TargetHealth target)
    {
        if (completed)
        {
            return;
        }

        kills++;
        activeBots = Mathf.Max(0, activeBots - 1);
        UpdateProgressText();

        if (maintainActiveBotCount && botSpawner != null)
        {
            GameObject spawnedBot = botSpawner.SpawnOneBot();

            if (spawnedBot != null)
            {
                activeBots++;
            }
        }

        if (kills >= killsRequired)
        {
            completed = true;
            ShowPrompt("LEVEL 1 COMPLETE");
            return;
        }

        ShowPrompt("ELIMINATE THE TRAINING BOT");
    }

    void HandleShotFired()
    {
        if (completed || kills > 0)
        {
            return;
        }

        ShowPrompt("LEFT CLICK TO SHOOT", shootPromptSeconds);
    }

    void HandleReloadCompleted()
    {
        if (completed)
        {
            return;
        }

        ShowPrompt("ELIMINATE THE TRAINING BOT");
    }

    void HandleReloadNeeded()
    {
        if (completed)
        {
            return;
        }

        ShowPrompt("PRESS R TO RELOAD");
    }

    void EnsureUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (promptText == null)
        {
            promptText = CreateText(canvas.transform, "Level1PromptText", 36f, TextAlignmentOptions.Center);
            RectTransform promptRect = promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0f, 170f);
            promptRect.sizeDelta = new Vector2(900f, 180f);
        }

        if (progressText == null)
        {
            progressText = CreateText(canvas.transform, "Level1ProgressText", 34f, TextAlignmentOptions.Center);
            RectTransform progressRect = progressText.rectTransform;
            progressRect.anchorMin = new Vector2(0.5f, 1f);
            progressRect.anchorMax = new Vector2(0.5f, 1f);
            progressRect.pivot = new Vector2(0.5f, 1f);
            progressRect.anchoredPosition = new Vector2(0f, -32f);
            progressRect.sizeDelta = new Vector2(240f, 60f);
        }
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        return text;
    }

    void ShowOpeningPrompt()
    {
        ShowPrompt(
            "Level 1 - Training Camp\nAIM AT THE TARGET\nLEFT CLICK TO SHOOT",
            openingPromptSeconds
        );
    }

    void ShowPrompt(string message)
    {
        ShowPrompt(message, 0f);
    }

    void ShowPrompt(string message, float seconds)
    {
        if (promptText == null)
        {
            return;
        }

        if (promptRoutine != null)
        {
            StopCoroutine(promptRoutine);
            promptRoutine = null;
        }

        promptText.text = message;
        promptText.gameObject.SetActive(true);

        if (seconds > 0f)
        {
            promptRoutine = StartCoroutine(HidePromptAfterDelay(seconds));
        }
    }

    IEnumerator HidePromptAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }

        promptRoutine = null;
    }

    void UpdateProgressText()
    {
        if (progressText != null)
        {
            progressText.text = kills + "/" + killsRequired;
        }
    }
}
