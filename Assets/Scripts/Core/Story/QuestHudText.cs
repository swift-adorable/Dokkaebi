using System.Collections.Generic;
using System.Text;

/// <summary>
/// 추적 HUD · 임시 퀘스트 목록에 쓰는 글. 화면과 떼어 둔다 — 테스트할 수 있게.
/// (임시 화면 · 퀘스트 창은 레이어 · 아트 작업 때 — 2026-10-06 사용자 결정)
/// </summary>
public static class QuestHudText
{
    /// <summary>윗줄 — 「메인 · 내 이야기를 찾는 길」.</summary>
    public static string Title(QuestDefinition quest)
        => quest == null ? string.Empty : $"{KindName(quest.Kind)} · {quest.Title}";

    /// <summary>아랫줄 — 「현무패 — 북쪽 젖은 장터길 (0/6)」. 다 채웠으면 빈 글.</summary>
    public static string Objective(QuestTracker tracker, QuestDefinition quest)
    {
        if (tracker == null || quest == null)
            return string.Empty;

        QuestObjective next = tracker.NextObjective(quest);

        if (next == null)
            return string.Empty;

        return quest.Objectives.Length > 1
            ? $"{next.Text} ({tracker.MetCount(quest)}/{quest.Objectives.Length})"
            : next.Text;
    }

    /// <summary>알림 — 퀘스트를 받았다 · 목표가 바뀌었다 · 끝냈다.</summary>
    public static string Started(QuestDefinition q) => $"새 퀘스트 — {q.Title}";
    public static string Updated(QuestDefinition q) => $"목표 갱신 — {q.Title}";
    public static string Completed(QuestDefinition q)
        => string.IsNullOrEmpty(q.RewardText) ? $"완료 — {q.Title}" : $"완료 — {q.Title} · {q.RewardText}";

    /// <summary>임시 목록 — 진행 중 · 끝낸 것 전부, 줄마다 하나.</summary>
    public static string List(QuestTracker tracker)
    {
        var sb = new StringBuilder();

        foreach (QuestDefinition q in tracker.Visible())
        {
            QuestState s = tracker.StateOf(q);
            sb.Append(s == QuestState.Completed ? "[완료] " : "· ");
            sb.Append(Title(q));

            if (s == QuestState.Active)
                sb.Append("\n    ").Append(Objective(tracker, q));

            sb.Append('\n');
        }

        return sb.Length == 0 ? "받은 퀘스트가 없다" : sb.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// 두 시점 사이에 생긴 알림을 고른다. 처음 보는 상태(이전 = null)에서는 알리지 않는다 — 불러오기 직후 알림이 쏟아지지 않게.
    /// </summary>
    public static List<string> Diff(Dictionary<string, (QuestState state, int met)> before,
        Dictionary<string, (QuestState state, int met)> after)
    {
        var list = new List<string>();

        if (before == null || after == null)
            return list;

        foreach (QuestDefinition q in QuestTable.Quests)
        {
            if (!before.TryGetValue(q.Id, out var b) || !after.TryGetValue(q.Id, out var a))
                continue;

            if (a.state == QuestState.Completed && b.state != QuestState.Completed)
                list.Add(Completed(q));
            else if (a.state == QuestState.Active && b.state == QuestState.Hidden)
                list.Add(Started(q));
            else if (a.state == QuestState.Active && a.met > b.met)
                list.Add(Updated(q));
        }

        return list;
    }

    public static Dictionary<string, (QuestState state, int met)> Snapshot(QuestTracker tracker)
    {
        var d = new Dictionary<string, (QuestState, int)>();

        foreach (QuestDefinition q in QuestTable.Quests)
            d[q.Id] = (tracker.StateOf(q), tracker.MetCount(q));

        return d;
    }

    private static string KindName(QuestKind kind) => kind == QuestKind.Main ? "메인" : "서브";
}
