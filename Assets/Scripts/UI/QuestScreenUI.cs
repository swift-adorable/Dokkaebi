using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【퀘스트 창】 (결정 2-106 · 본문 3행 「퀘스트는 퀘스트 창에 뜬다」) — 진행 중 · 끝낸 퀘스트를 모아 본다.
/// 퀘스트 HUD를 누르거나 J로 연다. 떠 있는 동안 게임이 멈춘다(결정 2-92).
/// 글은 퀘스트 표(본문 「(퀘스트 …)」 문단)에서만 가져온다. 모양은 아트 작업 때 다시 만든다.
/// </summary>
public class QuestScreenUI : MonoBehaviour
{
    private static QuestScreenUI instance;

    private const float RowGap = 8f;
    private const int ActiveTab = 0;
    private const int DoneTab = 1;

    private UIFactory.ScrollList list;
    private readonly List<Image> tabs = new();
    private int shown = ActiveTab;
    private int openedFrame;

    public static bool IsOpen => instance != null;

    public static void Open()
    {
        if (instance != null)
            return;

        Canvas canvas = UIFactory.CreateCanvas("QuestScreenCanvas (Runtime)", 1250);
        instance = canvas.gameObject.AddComponent<QuestScreenUI>();
        instance.openedFrame = Time.frameCount;
        instance.Build(canvas);
        instance.Refresh();
    }

    public static void Close()
    {
        if (instance != null)
            Destroy(instance.gameObject);
    }

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (Time.frameCount != openedFrame && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.J)))
            Close();
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);
        GamePause.Register(canvas.gameObject);
        Image dim = UIFactory.CreatePanel("Dim", safe, UIPalette.Dim, Vector2.zero, Vector2.one, 0);
        Image box = UIFactory.CreatePanel("Box", dim.transform, UIPalette.Panel,
            new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.96f));

        UIFactory.CreateLabel(box.transform, "퀘스트", 30, FontStyle.Bold,
            new Vector2(0.04f, 0.89f), new Vector2(0.78f, 0.98f), TextAnchor.MiddleLeft);
        UIFactory.CreateButton(box.transform, "닫기", new Vector2(0.8f, 0.9f), new Vector2(0.96f, 0.97f),
            UIPalette.Header, () => Destroy(gameObject), 22);

        string[] names = { "진행 중", "끝낸 것" };
        for (int i = 0; i < names.Length; i++)
        {
            int captured = i;
            float x = 0.04f + i * 0.2f;
            Button tab = UIFactory.CreateButton(box.transform, names[i],
                new Vector2(x, 0.80f), new Vector2(x + 0.19f, 0.88f), UIPalette.Header,
                () => { shown = captured; Refresh(); }, 22);
            tabs.Add(tab.GetComponent<Image>());
        }

        list = UIFactory.CreateScrollList("QuestList", box.transform,
            new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.785f));
    }

    private void Refresh()
    {
        for (int i = 0; i < tabs.Count; i++)
            tabs[i].color = i == shown ? UIPalette.SlotSelected : UIPalette.Header;

        UIFactory.ClearChildren(list.Content);
        float y = 0f;

        QuestTracker tracker = StoryManager.Quests;
        QuestState want = shown == DoneTab ? QuestState.Completed : QuestState.Active;
        int count = 0;

        foreach (QuestKind kind in new[] { QuestKind.Main, QuestKind.Sub })
        {
            foreach (QuestDefinition q in QuestTable.OfKind(kind))
            {
                if (tracker.StateOf(q) != want)
                    continue;

                AddQuest(tracker, q, ref y);
                count++;
            }
        }

        if (count == 0)
            AddLine(shown == DoneTab ? "아직 끝낸 퀘스트가 없다." : "받은 퀘스트가 없다.", ref y);

        list.Content.sizeDelta = new Vector2(0f, y);
    }

    private void AddQuest(QuestTracker tracker, QuestDefinition q, ref float y)
    {
        var sb = new System.Text.StringBuilder();
        string kind = q.Kind == QuestKind.Main ? "메인" : "서브";
        MerchantDefinition giver = StoryTable.Merchant(q.GiverId);

        sb.Append($"<b>{q.Title}</b>  <size={UIFactory.Scaled(16)}><color=#A8A08C>{kind}");
        if (giver != null)
            sb.Append($" · {giver.Name}");
        sb.Append("</color></size>");

        if (!string.IsNullOrEmpty(q.Summary))
            sb.Append($"\n<size={UIFactory.Scaled(18)}><color=#C9C2B0>{q.Summary}</color></size>");

        QuestObjective next = tracker.NextObjective(q);
        int lines = 2 + (string.IsNullOrEmpty(q.Summary) ? 0 : 1 + q.Summary.Length / 40);

        foreach (QuestObjective o in q.Objectives)
        {
            bool met = tracker.IsMet(o);
            string mark = met ? "<color=#7FD08A>✔</color>" : o == next ? "<color=#FFD873>▶</color>" : "<color=#7A7466>·</color>";
            string text = met ? $"<color=#8C8676>{o.Text}</color>" : o == next ? $"<b>{o.Text}</b>" : o.Text;
            string progress = tracker.ProgressText(o);
            sb.Append($"\n  {mark} {text}{(string.IsNullOrEmpty(progress) || met ? string.Empty : $"  <color=#FFD873>{progress}</color>")}");
            lines += 1 + o.Text.Length / 40;
        }

        if (!string.IsNullOrEmpty(q.RewardText))
        {
            sb.Append($"\n<size={UIFactory.Scaled(17)}><color=#E0B860>보상 — {q.RewardText}</color></size>");
            lines++;
        }

        Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
        Place(row.rectTransform, ref y, 24f + lines * 26f);

        Text label = UIFactory.CreateLabel(row.transform, sb.ToString(), 20, FontStyle.Normal,
            new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), TextAnchor.UpperLeft);
        label.supportRichText = true;
        label.rectTransform.offsetMin = new Vector2(0f, 8f);
        label.rectTransform.offsetMax = new Vector2(0f, -8f);
    }

    private void AddLine(string text, ref float y)
    {
        Text line = UIFactory.CreateLabel(list.Content, text, 22, FontStyle.Normal, Vector2.zero, Vector2.zero,
            TextAnchor.MiddleLeft, UIPalette.TextDim);
        Place(line.rectTransform, ref y, 44f);
    }

    private static void Place(RectTransform rect, ref float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(6f, -(y + height));
        rect.offsetMax = new Vector2(-6f, -y);
        y += height + RowGap;
    }
}
