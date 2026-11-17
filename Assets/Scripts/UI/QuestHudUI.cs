using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【퀘스트 목록】 (결정 2-106 → 2-135) — 오른쪽 위, 장비 · 스킬 · 패시브 · 도감 버튼 바로 아래.
/// 튜토리얼 · 메인 · 서브 퀘스트를 **한 목록**에 줄로 쌓는다(사용자 「퀘스트 및 튜토리얼 관련은 전부 그 패널(리스트)에」).
///   맨 위 = 튜토리얼 단계(있으면) → 진행 중 메인 → 진행 중 서브, 넘치면 「외 n개」
///   목표가 바뀌면 그 위에 잠깐 알림 줄(새 퀘스트 · 목표 갱신 · 완료)
/// 누르면 퀘스트 창. 창(가방 · 가게 · 출발 · 대화 …)이 떠 있으면 숨는다(결정 2-133).
///
/// 【계산은 QuestTracker가 한다】 이 화면은 0.5초마다 묻고 글만 바꾼다.
/// </summary>
public class QuestHudUI : MonoBehaviour
{
    private static QuestHudUI instance;

    private const float Width = 558f;          // 오른쪽 위 버튼 줄(132 × 4 + 10 × 3)과 같은 폭
    private const float ButtonRow = 62f;       // InventoryScreenUI.HudButtonHeight
    private const float Margin = 10f;
    private const float PollInterval = 0.5f;
    private const float ToastSeconds = 3.5f;
    private const int MaxQuests = 4;           // 이보다 많으면 「외 n개」
    /// <summary>
    /// 목록 아래 끝은 화면 위에서 이 비율까지 — 오른쪽 아래 터치 버튼(대시 · 파밍 · 시설) 더미를 덮지 않게.
    /// 넘치면 퀘스트를 뒤에서부터 「외 n개」로 접는다 (2026-11-17 사용자 「버튼이 다른 UI에 가려지는 것 없나」).
    /// </summary>
    private const float MaxBottom = 0.41f;   // 구역의 시설 버튼(파밍 위) 윗끝이 화면 위에서 약 43%

    private static readonly Color EntryBack = new(0.08f, 0.10f, 0.14f, 0.78f);
    private static readonly Color FlashBack = new(0.16f, 0.42f, 0.22f, 0.9f);
    private static readonly Color MainColor = new(1f, 0.82f, 0.38f);
    private static readonly Color SubColor = new(0.6f, 0.82f, 1f);
    private static readonly Color TutorialColor = new(0.55f, 0.95f, 0.6f);
    /// <summary>튜토리얼 줄 바탕 — 퀘스트 줄(짙은 남색)과 다른 짙은 초록. 새 단계면 밝은 초록과 오가며 숨 쉰다 (결정 2-138).</summary>
    private static readonly Color TutorialBack = new(0.07f, 0.24f, 0.14f, 0.9f);
    private static readonly Color TutorialGlow = new(0.18f, 0.48f, 0.28f, 0.95f);

    private RectTransform root;
    private readonly List<(Image back, Text text)> entries = new();

    private float pollTimer;
    private float toastTimer;
    private string toastText = string.Empty;
    private bool hiddenByScreen;
    private int tutorialVersion = -1;
    private string drawn;
    private int tutorialEntry = -1;

    private Dictionary<string, (QuestState state, int met)> last;
    private readonly Queue<string> toasts = new();

    public static QuestHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<QuestHudUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        // 생존 HUD(800)와 같은 층. 가방 화면(1000) · 이야기 장면(1400)이 위를 덮는다.
        Canvas canvas = UIFactory.CreateCanvas("QuestHudCanvas (Runtime)", 810);

        instance = canvas.gameObject.AddComponent<QuestHudUI>();
        instance.Build(canvas);

        return instance;
    }

    public static void SetHiddenByScreen(bool hidden)
    {
        if (instance == null)
            return;

        instance.hiddenByScreen = hidden;
        instance.Refresh();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        GameObject go = UIFactory.CreateChild("QuestList", safe);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(Width, 0f);
        root.anchoredPosition = new Vector2(-Margin, HudLayout.TopRightButtonsY - ButtonRow - Margin);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Refresh();
    }

    private (Image back, Text text) Entry(int index)
    {
        while (entries.Count <= index)
        {
            Image back = UIFactory.CreatePanel($"Entry_{entries.Count}", root, EntryBack, Vector2.zero, Vector2.one, UIFactory.Radius);
            var pad = back.gameObject.AddComponent<VerticalLayoutGroup>();
            pad.padding = new RectOffset(14, 14, 10, 10);
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = true;
            pad.childForceExpandHeight = false;

            var button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.onClick.AddListener(QuestScreenUI.Open);   // 퀘스트 창 (결정 2-106)

            Text text = UIFactory.CreateLabel(back.transform, string.Empty, 20, FontStyle.Normal,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft, UIPalette.Text);
            text.supportRichText = true;
            text.raycastTarget = false;

            entries.Add((back, text));
        }

        return entries[index];
    }

    private void Update()
    {
        // 창이 뜨면 바로 숨긴다 — 다음 갱신(PollInterval)까지 기다리면 잠깐 창 위에 남는다 (결정 2-133).
        if (GamePause.IsHeld && root != null && root.gameObject.activeSelf)
            root.gameObject.SetActive(false);

        // J — 퀘스트 창 (결정 2-106). 다른 창이 떠 있으면 열지 않는다.
        if (Input.GetKeyDown(KeyCode.J) && !GamePause.IsHeld && !QuestScreenUI.IsOpen)
            QuestScreenUI.Open();

        TutorialHudUI tutorial = TutorialHudUI.Current;
        int version = tutorial != null ? tutorial.Version : -1;
        if (version != tutorialVersion)
        {
            tutorialVersion = version;
            Refresh();
        }

        // 튜토리얼 단계를 마쳤다 — 그 줄이 잠깐 초록.
        if (tutorial != null && tutorialEntry >= 0 && tutorialEntry < entries.Count && entries[tutorialEntry].back.gameObject.activeSelf)
            entries[tutorialEntry].back.color = tutorial.Flashing ? FlashBack
                : tutorial.IsNew ? Color.Lerp(TutorialBack, TutorialGlow, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f))
                : TutorialBack;

        pollTimer -= Time.unscaledDeltaTime;
        if (pollTimer <= 0f)
        {
            pollTimer = PollInterval;
            Refresh();
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f)
                ShowNextToast();
        }
    }

    private void Refresh()
    {
        if (root == null)
            return;

        QuestTracker tracker = StoryManager.Quests;

        Dictionary<string, (QuestState, int)> now = QuestHudText.Snapshot(tracker);
        foreach (string t in QuestHudText.Diff(last, now))
            toasts.Enqueue(t);
        last = now;

        if (toastTimer <= 0f && toasts.Count > 0)
            ShowNextToast();

        // ── 줄 모으기 ──
        var lines = new List<(string text, bool tutorial)>();

        if (toastTimer > 0f && !string.IsNullOrEmpty(toastText))
            lines.Add((Rich(UIPalette.TextAccent, toastText, 20, bold: true), false));

        TutorialHudUI tutorial = TutorialHudUI.Current;
        if (tutorial != null)
        {
            string text = Rich(TutorialColor, tutorial.Header, 16, bold: true) + "\n" + Rich(UIPalette.Text, tutorial.Title, 20, bold: true);
            if (!string.IsNullOrEmpty(tutorial.Hint))
                text += "\n" + Rich(UIPalette.TextDim, tutorial.Hint, 16);
            lines.Add((text, true));
        }

        int fixedLines = lines.Count;
        for (int cap = MaxQuests; cap >= 0; cap--)
        {
            lines.RemoveRange(fixedLines, lines.Count - fixedLines);
            AddQuestLines(tracker, lines, cap);
            if (!Draw(lines) || cap == 0 || Fits())
                return;
            drawn = null;   // 줄여서 다시 그린다 — 같은 글 건너뛰기에 걸리지 않게
        }
    }

    /// <summary>목록이 화면 위 MaxBottom 안에 드는가.</summary>
    private bool Fits()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        var parent = root.parent as RectTransform;
        if (parent == null)
            return true;
        float bottom = -root.anchoredPosition.y + root.rect.height;
        return bottom <= parent.rect.height * MaxBottom;
    }

    private static void AddQuestLines(QuestTracker tracker, List<(string text, bool tutorial)> lines, int cap)
    {
        int shown = 0, hidden = 0;
        foreach (QuestDefinition q in tracker.Visible())
        {
            if (tracker.StateOf(q) != QuestState.Active)
                continue;
            if (shown >= cap)
            {
                hidden++;
                continue;
            }

            string objective = QuestHudText.Objective(tracker, q);
            string text = Rich(q.Kind == QuestKind.Main ? MainColor : SubColor, QuestHudText.Title(q), 16);
            if (!string.IsNullOrEmpty(objective))
                text += "\n" + Rich(UIPalette.Text, objective, 20, bold: true);
            lines.Add((text, false));
            shown++;
        }

        if (hidden > 0)
            lines.Add((Rich(UIPalette.TextDim, $"외 {hidden}개 — 눌러서 전부 보기", 16), false));
    }

    /// <summary>줄들을 그린다. 보이지 않거나 같은 글이라 그릴 것이 없으면 false.</summary>
    private bool Draw(List<(string text, bool tutorial)> lines)
    {
        bool show = !hiddenByScreen && !GamePause.IsHeld && lines.Count > 0;
        root.gameObject.SetActive(show);
        if (!show)
            return false;

        // 같은 글이면 다시 그리지 않는다.
        string key = string.Join("\u0001", lines.ConvertAll(l => l.text));
        if (key == drawn)
            return false;
        drawn = key;

        tutorialEntry = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            (Image back, Text text) e = Entry(i);
            e.back.gameObject.SetActive(true);
            e.back.color = lines[i].tutorial ? TutorialBack : EntryBack;
            e.text.text = lines[i].text;
            if (lines[i].tutorial)
                tutorialEntry = i;
        }
        for (int i = lines.Count; i < entries.Count; i++)
            entries[i].back.gameObject.SetActive(false);
        return true;
    }

    /// <summary>한 덩이 글 — 색 · 크기(글자 배율 적용) · 굵기.</summary>
    private static string Rich(Color color, string text, int size, bool bold = false)
    {
        string body = bold ? $"<b>{text}</b>" : text;
        return $"<size={UIFactory.Scaled(size)}><color=#{ColorUtility.ToHtmlStringRGB(color)}>{body}</color></size>";
    }

    private void ShowNextToast()
    {
        if (toasts.Count == 0)
        {
            toastTimer = 0f;
            toastText = string.Empty;
            drawn = null;
            return;
        }

        toastText = toasts.Dequeue();
        toastTimer = ToastSeconds;
        drawn = null;
    }
}
