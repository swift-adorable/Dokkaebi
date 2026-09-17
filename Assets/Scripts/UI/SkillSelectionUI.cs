using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skill 선택창 — v5 구조. (로드맵 5-C)
///
/// 조이스틱 UI와 같은 이유로 런타임에 코드로 생성한다.
/// 씬 배치를 잊어 빌드에 빠지는 실패 지점을 만들지 않는다. (마스터 프롬프트 5-4)
///
/// 화면 구성
///   상단 상태 바 : Lv / 핵심 보유 / 소켓 / Nucleus  — 선택의 근거가 되는 자원을 항상 보여준다
///   카드 3장     : 분류 · 이름 · 설명 · 태그 · 대가
///   소켓 선택    : Support를 두 Core가 모두 받을 수 있을 때만 나타난다
///
/// ※ Reroll 버튼은 두지 않는다. 적재(Loadout) 자체가 이미 풀 정제이므로
///   유저가 직접 고른 8장 안에서 다시 뽑는 것은 의미가 없다.
///   v5가 Banish를 폐지한 것과 같은 논리다. (확정 기획)
/// </summary>
public class SkillSelectionUI : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private const float CardWidth = 440f;
    private const float CardHeight = 600f;
    private const float CardGap = 36f;

    /// <summary>카테고리별 카드 색. v5에는 등급(Rarity) 개념이 없으므로 분류로 구분한다.</summary>
    private static readonly Color[] CategoryColors =
    {
        new Color(0.72f, 0.26f, 0.22f, 0.96f),   // Core       — 붉은색
        new Color(0.20f, 0.45f, 0.80f, 0.96f),   // Support    — 푸른색
        new Color(0.60f, 0.42f, 0.14f, 0.96f),   // Meta       — 황금색
        new Color(0.26f, 0.52f, 0.34f, 0.96f)    // Persistent — 초록색
    };

    private static readonly string[] CategoryNames = { "핵심", "보조", "발동", "유지형" };
    private static readonly string[] FamilyNames = { "", "전달", "적재", "기폭" };

    private SkillManager manager;

    private GameObject panel;
    private RectTransform cardRoot;
    private Text statusLabel;
    private Font uiFont;

    private GameObject socketPrompt;
    private SkillDefinition pendingSupport;

    private readonly List<GameObject> cards = new();
    private readonly List<int> eligibleCores = new();

    /// <summary>선택 UI를 런타임에 생성한다.</summary>
    public static SkillSelectionUI Create()
    {
        var canvasObject = new GameObject("SkillSelectionCanvas (Runtime)");
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

        var ui = canvasObject.AddComponent<SkillSelectionUI>();
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
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = true;

        CreateLabel(panel.transform, "MUTATION", 60, FontStyle.Bold,
            new Vector2(0f, 0.87f), new Vector2(1f, 0.97f), TextAnchor.MiddleCenter);

        statusLabel = CreateLabel(panel.transform, string.Empty, 30, FontStyle.Normal,
            new Vector2(0.05f, 0.81f), new Vector2(0.95f, 0.87f), TextAnchor.MiddleCenter);

        if (statusLabel != null)
            statusLabel.color = new Color(0.78f, 0.82f, 0.88f, 1f);

        GameObject rootObject = CreateChild("Cards", panel.transform);
        cardRoot = rootObject.GetComponent<RectTransform>();
        cardRoot.anchorMin = new Vector2(0.5f, 0.46f);
        cardRoot.anchorMax = new Vector2(0.5f, 0.46f);
        cardRoot.sizeDelta = new Vector2(ReferenceWidth, CardHeight);

        BuildSocketPrompt(panel.transform);

        panel.SetActive(false);
    }

    private void Start()
    {
        manager = SkillManager.EnsureInstance();

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

    private void HandleSelectionOpened(IReadOnlyList<SkillDefinition> choices)
    {
        ClearCards();
        HideSocketPrompt();

        RefreshStatus();

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
        HideSocketPrompt();

        panel.SetActive(false);
    }

    /// <summary>선택의 근거가 되는 자원 현황을 한 줄로 보여준다.</summary>
    private void RefreshStatus()
    {
        if (statusLabel == null || manager == null)
            return;

        RunSkillState state = manager.RunState;

        int level = PlayerStats.HasInstance ? PlayerStats.Instance.Level : 1;

        int usedSockets = 0;
        int totalSockets = state.Cores.Count * RunSkillState.SocketsPerCore;

        for (int i = 0; i < state.Cores.Count; i++)
            usedSockets += state.GetSockets(i).Count;

        string coreText = $"핵심 {state.Cores.Count}/{state.CoreCapacity}";

        if (state.IsSecondCoreLocked)
            coreText += $" (Lv{RunSkillState.SecondCoreUnlockLevel}에 2번째 개방)";

        statusLabel.text =
            $"Lv.{level}    {coreText}    소켓 {usedSockets}/{totalSockets}    " +
            $"Nucleus {state.NucleusSpent}/{state.NucleusCapacity}";
    }

    private GameObject CreateCard(SkillDefinition definition, float x)
    {
        GameObject card = CreateChild($"Card_{definition.Id}", cardRoot);

        var rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        rect.anchoredPosition = new Vector2(x, 0f);

        var image = card.AddComponent<Image>();
        image.color = GetCategoryColor(definition.Category);
        image.raycastTarget = true;

        var button = card.AddComponent<Button>();
        button.targetGraphic = image;

        SkillDefinition captured = definition;
        button.onClick.AddListener(() => OnCardClicked(captured));

        // 분류 배지 — 무엇을 고르는 것인지 먼저 알려준다.
        CreateLabel(card.transform, BuildBadge(definition), 26, FontStyle.Bold,
            new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.97f), TextAnchor.MiddleLeft);

        CreateLabel(card.transform, definition.DisplayName, 46, FontStyle.Bold,
            new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.90f), TextAnchor.MiddleLeft);

        CreateLabel(card.transform, definition.Description, 28, FontStyle.Normal,
            new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.76f), TextAnchor.UpperLeft);

        // 태그 — 어떤 Support가 붙을 수 있는지 판단하는 유일한 근거다.
        Text tagLabel = CreateLabel(card.transform, definition.Tags.ToKoreanString(), 24,
            FontStyle.Normal, new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.35f),
            TextAnchor.UpperLeft);

        if (tagLabel != null)
            tagLabel.color = new Color(0.88f, 0.92f, 1f, 0.95f);

        Text footerLabel = CreateLabel(card.transform, BuildFooter(definition), 24,
            FontStyle.Normal, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.26f),
            TextAnchor.LowerLeft);

        if (footerLabel != null)
            footerLabel.color = new Color(1f, 0.92f, 0.78f, 0.95f);

        return card;
    }

    /// <summary>"핵심 · 전달" / "보조" / "발동 · 기원형" / "유지형" 형태의 배지.</summary>
    private static string BuildBadge(SkillDefinition definition)
    {
        string badge = GetCategoryName(definition.Category);

        if (definition.Category == SkillCategory.Core && definition.Family != CoreFamily.None)
            badge += " · " + FamilyNames[Mathf.Clamp((int)definition.Family, 0, FamilyNames.Length - 1)];

        if (definition.IsInvocation)
            badge += " · 기원형";

        return $"{badge}    Lv{definition.RequiredLevel}";
    }

    /// <summary>대가와 자원 점유. 메커니즘과 대가는 한 쌍이므로 반드시 함께 보여준다.</summary>
    private static string BuildFooter(SkillDefinition definition)
    {
        var lines = new List<string>(3);

        if (definition.Category == SkillCategory.Support &&
            definition.RequiredTags != SkillTag.None)
        {
            lines.Add($"요구 태그: {definition.RequiredTags.ToKoreanString()}");
        }

        if (definition.Category == SkillCategory.Persistent)
            lines.Add($"Nucleus {definition.NucleusCost} 점유");

        if (!string.IsNullOrEmpty(definition.CostDescription))
            lines.Add($"대가: {definition.CostDescription}");

        return string.Join("\n", lines);
    }

    private void OnCardClicked(SkillDefinition definition)
    {
        if (manager == null)
            return;

        // Support를 두 Core가 모두 받을 수 있으면 어디에 넣을지 유저가 정한다.
        if (definition.Category == SkillCategory.Support)
        {
            manager.RunState.GetEligibleCoreIndices(definition, eligibleCores);

            if (eligibleCores.Count > 1)
            {
                ShowSocketPrompt(definition);
                return;
            }
        }

        manager.Select(definition);
    }

    // ────────────────────────────────── 소켓 선택

    private void BuildSocketPrompt(Transform parent)
    {
        socketPrompt = CreateChild("SocketPrompt", parent);

        var rect = socketPrompt.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var background = socketPrompt.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.86f);
        background.raycastTarget = true;

        socketPrompt.SetActive(false);
    }

    private void ShowSocketPrompt(SkillDefinition support)
    {
        pendingSupport = support;

        // 이전 버튼을 지우고 다시 만든다. 선택창은 자주 열리지 않으므로 비용이 문제되지 않는다.
        for (int i = socketPrompt.transform.childCount - 1; i >= 0; i--)
            Destroy(socketPrompt.transform.GetChild(i).gameObject);

        CreateLabel(socketPrompt.transform, $"「{support.DisplayName}」를 어느 핵심에 장착할까요?",
            40, FontStyle.Bold, new Vector2(0f, 0.62f), new Vector2(1f, 0.74f),
            TextAnchor.MiddleCenter);

        RunSkillState state = manager.RunState;

        float buttonWidth = 480f;
        float gap = 40f;
        float totalWidth = eligibleCores.Count * buttonWidth + (eligibleCores.Count - 1) * gap;
        float startX = -totalWidth * 0.5f + buttonWidth * 0.5f;

        for (int i = 0; i < eligibleCores.Count; i++)
        {
            int coreIndex = eligibleCores[i];

            SkillDefinition core = state.Cores[coreIndex];
            int used = state.GetSockets(coreIndex).Count;

            GameObject button = CreateChild($"SocketButton_{coreIndex}", socketPrompt.transform);

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(buttonWidth, 180f);
            rect.anchoredPosition = new Vector2(startX + i * (buttonWidth + gap), 0f);

            var image = button.AddComponent<Image>();
            image.color = GetCategoryColor(SkillCategory.Core);

            var uiButton = button.AddComponent<Button>();
            uiButton.targetGraphic = image;

            int captured = coreIndex;
            uiButton.onClick.AddListener(() => OnSocketChosen(captured));

            CreateLabel(button.transform,
                $"{core.DisplayName}\n소켓 {used}/{RunSkillState.SocketsPerCore}",
                32, FontStyle.Bold, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }

        socketPrompt.SetActive(true);
    }

    private void OnSocketChosen(int coreIndex)
    {
        SkillDefinition support = pendingSupport;

        HideSocketPrompt();

        if (manager != null && support != null)
            manager.Select(support, coreIndex);
    }

    private void HideSocketPrompt()
    {
        pendingSupport = null;

        if (socketPrompt != null)
            socketPrompt.SetActive(false);
    }

    // ────────────────────────────────── 생성 헬퍼

    private Text CreateLabel(
        Transform parent, string text, int fontSize, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment)
    {
        if (uiFont == null)
            return null;

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

        return label;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.AddComponent<RectTransform>();
        child.transform.SetParent(parent, false);

        return child;
    }

    private static Color GetCategoryColor(SkillCategory category)
    {
        int index = Mathf.Clamp((int)category, 0, CategoryColors.Length - 1);

        return CategoryColors[index];
    }

    private static string GetCategoryName(SkillCategory category)
    {
        int index = Mathf.Clamp((int)category, 0, CategoryNames.Length - 1);

        return CategoryNames[index];
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
