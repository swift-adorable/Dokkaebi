using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 퀘스트 추적 HUD — 화면 위 가운데 두 줄 + 목표가 바뀔 때 잠깐 뜨는 알림.
/// 누르면 받은 퀘스트 전부를 글로 펼친다(임시 목록).
///
/// 퀘스트 창 · 아트는 레이어 · 아트 작업 때 한 번에 만든다 (2026-10-06 사용자 결정).
/// 지금은 0장 Vertical Slice에서 「다음 목표가 보이는가」를 확인하는 용도다.
///
/// 【계산은 QuestTracker가 한다】 이 화면은 0.5초마다 묻고 글만 바꾼다.
/// 진행이 바뀌는 곳(보스 · 조각 · 사건 · 밤 · 건물)이 여럿이라 이벤트를 하나하나 걸지 않는다.
/// </summary>
public class QuestHudUI : MonoBehaviour
{
    private static QuestHudUI instance;

    private const float Width = 520f;
    private const float Height = 66f;
    private const float Margin = 16f;
    private const float PollInterval = 0.5f;
    private const float ToastSeconds = 3.5f;

    private RectTransform root;
    private Image panel;
    private Text titleLabel;
    private Text objectiveLabel;
    private Text toastLabel;
    private GameObject listObject;
    private Text listLabel;

    private float pollTimer;
    private float toastTimer;
    private bool hiddenByScreen;
    private bool expanded;

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
        instance.Refresh(force: true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        GameObject go = UIFactory.CreateChild("QuestHud", safe);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(Width, Height);
        root.anchoredPosition = new Vector2(0f, -Margin);

        panel = UIFactory.CreatePanel("Back", root, UIPalette.Panel, Vector2.zero, Vector2.one);

        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;
        button.onClick.AddListener(() => { expanded = !expanded; Refresh(force: true); });

        titleLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 18, FontStyle.Normal,
            new Vector2(0f, 0.55f), new Vector2(1f, 1f), TextAnchor.LowerCenter, UIPalette.TextDim);
        objectiveLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 22, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(1f, 0.55f), TextAnchor.UpperCenter);
        titleLabel.raycastTarget = false;
        objectiveLabel.raycastTarget = false;

        // 알림 — 판 바로 아래.
        toastLabel = UIFactory.CreateLabel(root, string.Empty, 22, FontStyle.Bold,
            new Vector2(0f, -0.7f), new Vector2(1f, 0f), TextAnchor.MiddleCenter, UIPalette.TextAccent);
        toastLabel.raycastTarget = false;

        // 임시 목록 — 판을 누르면 아래로 펼친다.
        Image list = UIFactory.CreatePanel("List", root, UIPalette.Dim, new Vector2(0f, -6f), new Vector2(1f, -0.8f));
        listObject = list.gameObject;
        listLabel = UIFactory.CreateLabel(list.transform, string.Empty, 18, FontStyle.Normal,
            Vector2.zero, Vector2.one, TextAnchor.UpperLeft);
        UIFactory.Inset(listLabel.rectTransform, 12f);
        listLabel.raycastTarget = false;
        listObject.SetActive(false);

        Refresh(force: true);
    }

    private void Update()
    {
        pollTimer -= Time.unscaledDeltaTime;

        if (pollTimer <= 0f)
        {
            pollTimer = PollInterval;
            Refresh(force: false);
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;

            if (toastTimer <= 0f)
                ShowNextToast();
        }
    }

    private void Refresh(bool force)
    {
        QuestTracker tracker = StoryManager.Quests;

        Dictionary<string, (QuestState, int)> now = QuestHudText.Snapshot(tracker);

        foreach (string t in QuestHudText.Diff(last, now))
            toasts.Enqueue(t);

        last = now;

        if (toastTimer <= 0f && toasts.Count > 0)
            ShowNextToast();

        QuestDefinition tracked = tracker.Tracked();

        bool show = !hiddenByScreen && (tracked != null || toastTimer > 0f || expanded);
        root.gameObject.SetActive(show);

        if (!show)
            return;

        titleLabel.text = tracked != null ? QuestHudText.Title(tracked) : "퀘스트";
        objectiveLabel.text = tracked != null ? QuestHudText.Objective(tracker, tracked) : string.Empty;

        listObject.SetActive(expanded);

        if (expanded)
            listLabel.text = QuestHudText.List(tracker);
    }

    private void ShowNextToast()
    {
        if (toasts.Count == 0)
        {
            toastTimer = 0f;
            toastLabel.text = string.Empty;
            return;
        }

        toastLabel.text = toasts.Dequeue();
        toastTimer = ToastSeconds;
    }
}
