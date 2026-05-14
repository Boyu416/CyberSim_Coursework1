using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    public TextMeshProUGUI tipText;
    public float openingPromptSeconds = 3.5f;
    public float shootPromptSeconds = 1.5f;

    private BotSpawner botSpawner;
    private int kills;
    private int activeBots;
    private bool completed;
    private bool hasShownHeadshotTip;
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

    void Update()
    {
        if (!completed)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            StartLevel2();
        }
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
            ShowPrompt("LEVEL COMPLETE\nPRESS N TO CONTINUE");
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

        if (!hasShownHeadshotTip)
        {
            hasShownHeadshotTip = true;
            ShowTip("TIP: Headshots kill instantly. Body shots take 4 hits.");
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
            promptText = CreateText(canvas.transform, "Level1PromptText", 36f, TextAlignmentOptions.Center, new Vector2(940f, 190f));
            TrainingUiStyle.PositionPanel(
                promptText,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 170f)
            );
        }

        if (progressText == null)
        {
            progressText = CreateText(canvas.transform, "Level1ProgressText", 34f, TextAlignmentOptions.Center, new Vector2(260f, 68f));
            TrainingUiStyle.PositionPanel(
                progressText,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -32f)
            );
        }

        if (tipText == null)
        {
            tipText = CreateText(canvas.transform, "Level1HeadshotTipText", 25f, TextAlignmentOptions.Center, new Vector2(920f, 62f));
            TrainingUiStyle.PositionPanel(
                tipText,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 108f)
            );
            TrainingUiStyle.SetVisible(tipText, false);
        }
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment, Vector2 sizeDelta)
    {
        return TrainingUiStyle.CreateText(parent, objectName, fontSize, alignment, sizeDelta);
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

        TrainingUiStyle.SetMessage(promptText, message);
        TrainingUiStyle.SetVisible(promptText, true);
        TrainingUiStyle.BringToFront(promptText);

        if (seconds > 0f)
        {
            promptRoutine = StartCoroutine(HidePromptAfterDelay(seconds));
        }
    }

    void ShowTip(string message)
    {
        if (tipText == null)
        {
            return;
        }

        TrainingUiStyle.SetMessage(tipText, message);
        TrainingUiStyle.SetVisible(tipText, true);
        TrainingUiStyle.BringToFront(tipText);
    }

    IEnumerator HidePromptAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (promptText != null)
        {
            TrainingUiStyle.SetVisible(promptText, false);
        }

        promptRoutine = null;
    }

    void UpdateProgressText()
    {
        if (progressText != null)
        {
            TrainingUiStyle.SetMessage(progressText, kills + "/" + killsRequired);
        }
    }

    void StartLevel2()
    {
        if (promptText != null)
        {
            TrainingUiStyle.SetVisible(promptText, false);
        }

        if (progressText != null)
        {
            TrainingUiStyle.SetVisible(progressText, false);
        }

        if (tipText != null)
        {
            TrainingUiStyle.SetVisible(tipText, false);
        }

        Level2EnemyPatrol level2 = GetComponent<Level2EnemyPatrol>();

        if (level2 == null)
        {
            level2 = gameObject.AddComponent<Level2EnemyPatrol>();
        }

        level2.BeginLevel();
        enabled = false;
    }
}
