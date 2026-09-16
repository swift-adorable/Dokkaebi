using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mutation 선택창. 조이스틱 UI와 같은 이유로 런타임에 코드로 생성한다.
/// 씬 배치를 잊어 빌드에 빠지는 실패 지점을 만들지 않는다.
/// </summary>
public class MutationSelectionUI : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private const float CardWidth = 420f;
    private const float CardHeight = 560f;
    private const float CardGap = 40f;

    private static readonly Color[] RarityColors =
    {
        new Color(0.45f, 0.48f, 0.52f, 0.95f),   // Common
        new Color(0.20f, 0.45f, 0.80f, 0.95f),   // Rare
        new Color(0.55f, 0.25f, 0.75f, 0.95f)    // Epic
    };

    private MutationManager manager;
    private RectTransform cardRoot;
    private GameObject panel;
    private Font uiFont;

    private readonly List<GameObject> cards = new();

    /// <summary>선택 UI를 런타임에 생성한다.</summary>
    public static MutationSelectionUI Create()
    {
        var canvasObject = new GameObject("MutationSelectionCanvas (Runtime)");
        canvasObject.SetActive(false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 조이스틱(-100)보다 위, 디버그 UI보다도 위에 그린다.
        canvas.sortingOrder = 1000;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var ui = canvasObject.AddComponent<MutationSelectionUI>();
        ui.Build(canvasObject.transform);

        canvasObject.SetActive(true);

        return ui;
    }

    private void Build(Transform parent)
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        panel = CreateChild("Panel", parent);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 배경을 어둡게 깔아 뒤쪽 조작이 통하지 않게 한다.
        var background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);
        background.raycastTarget = true;

        CreateTitle(panel.transform);

        GameObject rootObject = CreateChild("Cards", panel.transform);
        cardRoot = rootObject.GetComponent<RectTransform>();
        cardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cardRoot.sizeDelta = new Vector2(ReferenceWidth, CardHeight);

        panel.SetActive(false);
    }

    private void Start()
    {
        manager = MutationManager.EnsureInstance();

        manager.OnSelectionOpened += HandleSelectionOpened;
        manager.OnSelectionClosed += HandleSelectionClosed;

        // 이미 선택 대기 중이면 즉시 표시한다.
        if (manager.IsSelecting && manager.CurrentChoices.Count > 0)
            HandleSelectionOpened(manager.CurrentChoices);
    }

    private void OnDestroy()
    {
        if (manager == null)
            return;

        manager.OnSelectionOpened -= HandleSelectionOpened;
        manager.OnSelectionClosed -= HandleSelectionClosed;
    }

    private void HandleSelectionOpened(IReadOnlyList<MutationDefinition> choices)
    {
        ClearCards();

        int count = choices.Count;
        float totalWidth = count * CardWidth + (count - 1) * CardGap;
        float startX = -totalWidth * 0.5f + CardWidth * 0.5f;

        for (int i = 0; i < count; i++)
            cards.Add(CreateCard(choices[i], startX + i * (CardWidth + CardGap)));

        panel.SetActive(true);
    }

    private void HandleSelectionClosed()
    {
        ClearCards();

        panel.SetActive(false);
    }

    private GameObject CreateCard(MutationDefinition definition, float x)
    {
        GameObject card = CreateChild($"Card_{definition.Id}", cardRoot);

        var rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        rect.anchoredPosition = new Vector2(x, 0f);

        var image = card.AddComponent<Image>();
        image.color = GetRarityColor(definition.Rarity);
        image.raycastTarget = true;

        var button = card.AddComponent<Button>();
        button.targetGraphic = image;

        MutationDefinition captured = definition;
        button.onClick.AddListener(() => OnCardClicked(captured));

        CreateLabel(card.transform, definition.DisplayName, 46, FontStyle.Bold,
            new Vector2(0f, 0.72f), new Vector2(1f, 0.95f), TextAnchor.MiddleCenter);

        CreateLabel(card.transform, definition.Description, 30, FontStyle.Normal,
            new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.68f), TextAnchor.UpperLeft);

        int owned = manager != null ? manager.Inventory.GetStacks(definition) : 0;

        CreateLabel(card.transform, $"{definition.Rarity}   {owned}/{definition.MaxStacks}", 26,
            FontStyle.Normal, new Vector2(0f, 0.04f), new Vector2(1f, 0.15f), TextAnchor.MiddleCenter);

        return card;
    }

    private void OnCardClicked(MutationDefinition definition)
    {
        if (manager == null)
            return;

        manager.Select(definition);
    }

    private void CreateTitle(Transform parent)
    {
        CreateLabel(parent, "MUTATION", 64, FontStyle.Bold,
            new Vector2(0f, 0.82f), new Vector2(1f, 0.94f), TextAnchor.MiddleCenter);
    }

    private void CreateLabel(
        Transform parent, string text, int fontSize, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment)
    {
        if (uiFont == null)
            return;

        GameObject labelObject = CreateChild("Label", parent);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.AddComponent<RectTransform>();
        child.transform.SetParent(parent, false);

        return child;
    }

    private static Color GetRarityColor(MutationRarity rarity)
    {
        int index = Mathf.Clamp((int)rarity, 0, RarityColors.Length - 1);

        return RarityColors[index];
    }

    private void ClearCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                Destroy(cards[i]);
        }

        cards.Clear();
    }
}
