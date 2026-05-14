using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TrainingUiStyle
{
    public const string HighlightColor = "#FFD84A";
    public const string MainTextColor = "#F2F0E6";

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 sizeDelta)
    {
        GameObject panelObject = new GameObject(objectName + "_Panel");
        panelObject.transform.SetParent(parent, false);

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.58f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(panelObject.transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = HexColor(MainTextColor);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.richText = true;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.22f;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.sizeDelta = sizeDelta;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 8f);
        textRect.offsetMax = new Vector2(-18f, -8f);

        return text;
    }

    public static void PositionPanel(
        TextMeshProUGUI text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition)
    {
        RectTransform panelRect = GetPanelRect(text);
        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
        panelRect.pivot = pivot;
        panelRect.anchoredPosition = anchoredPosition;
    }

    public static void SetMessage(TextMeshProUGUI text, string message)
    {
        if (text == null)
        {
            return;
        }

        text.text = HighlightKeywords(message);
    }

    public static void SetVisible(TextMeshProUGUI text, bool visible)
    {
        if (text != null)
        {
            GetStyledRoot(text).gameObject.SetActive(visible);
        }
    }

    public static void BringToFront(TextMeshProUGUI text)
    {
        if (text != null)
        {
            GetStyledRoot(text).SetAsLastSibling();
        }
    }

    public static RectTransform GetPanelRect(TextMeshProUGUI text)
    {
        return GetStyledRoot(text).GetComponent<RectTransform>();
    }

    static Transform GetStyledRoot(TextMeshProUGUI text)
    {
        if (text.transform.parent != null && text.transform.parent.GetComponent<Image>() != null)
        {
            return text.transform.parent;
        }

        return text.transform;
    }

    static string HighlightKeywords(string message)
    {
        string styledMessage = message;
        string[] keywords =
        {
            "LEFT CLICK TO SHOOT",
            "PRESS R TO RELOAD",
            "ELIMINATE ALL BOTS",
            "ELIMINATE THE TRAINING BOT",
            "SURVIVE AND ELIMINATE ALL ENEMIES",
            "LOW AMMO",
            "LEVEL COMPLETE",
            "GAME OVER",
            "PRESS N TO CONTINUE",
            "PRESS R TO RESTART",
            "AIM AT THE TARGET",
            "LEVEL 2 - ENEMY PATROL",
            "LEVEL 3 - BOMB DEFUSAL",
            "OBJECTIVE: DEFUSE THE BOMB",
            "Find and defuse the bomb before time runs out.",
            "BOMB TIMER",
            "HOLD E TO DEFUSE",
            "DEFUSING",
            "MISSION COMPLETE",
            "BOMB DEFUSED",
            "MISSION FAILED",
            "BOMB EXPLODED",
            "ENEMIES LEFT",
            "HP",
            "TIP:",
            "Headshots kill instantly.",
            "Body shots take 2 hits."
        };

        for (int i = 0; i < keywords.Length; i++)
        {
            styledMessage = styledMessage.Replace(
                keywords[i],
                "<color=" + HighlightColor + ">" + keywords[i] + "</color>"
            );
        }

        return styledMessage;
    }

    static Color HexColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            return color;
        }

        return Color.white;
    }
}
