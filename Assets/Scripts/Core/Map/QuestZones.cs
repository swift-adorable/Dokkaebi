using System.Collections.Generic;

/// <summary>
/// 【퀘스트 구역】 (결정 2-90) — 전체 지도에 테두리를 그릴 구역. 진행 중인 퀘스트(메인 · 서브)의 다음 목표가
/// 가리키는 구역만 — 핀포인트가 아니라 구역 단위 (Progression 4절). 건물 짓기처럼 구역이 없는 목표는 빠진다.
/// </summary>
public static class QuestZones
{
    /// <summary>목표가 가리키는 구역. 없으면 null.</summary>
    public static string ZoneOf(QuestObjective objective)
    {
        if (objective == null || string.IsNullOrEmpty(objective.Target))
            return null;

        switch (objective.Condition)
        {
            case QuestCondition.ClearZone:
                return StoryTable.Zone(objective.Target) != null ? objective.Target : null;

            case QuestCondition.DefeatBoss:
                return StoryTable.Boss(objective.Target)?.ZoneId;

            case QuestCondition.Seen:
            {
                const string prefix = "enter_";
                if (!objective.Target.StartsWith(prefix))
                    return null;

                string zone = objective.Target.Substring(prefix.Length);
                return StoryTable.Zone(zone) != null ? zone : null;
            }

            default:
                return null;
        }
    }

    /// <summary>이 장에서 테두리를 그릴 구역들 (겹치지 않게).</summary>
    public static List<string> InChapter(QuestTracker tracker, int chapter)
    {
        var zones = new List<string>();

        if (tracker == null)
            return zones;

        foreach (QuestDefinition quest in tracker.Visible())
        {
            if (tracker.StateOf(quest) != QuestState.Active)
                continue;

            string zone = ZoneOf(tracker.NextObjective(quest));
            ZoneDefinition z = zone != null ? StoryTable.Zone(zone) : null;

            if (z != null && z.Chapter == chapter && !zones.Contains(zone))
                zones.Add(zone);
        }

        return zones;
    }

    /// <summary>
    /// 【지도 오른쪽 「진행 중 퀘스트」 목록】 (결정 2-120) — 진행 중인 퀘스트 전부. 따라가는 퀘스트가 맨 앞.
    /// 다음 목표 구역이 이 장에 있으면 그 구역, 없으면(다른 장 · 구역 없는 목표) null — 줄은 남기고 누르면 「갈 곳이 없다」.
    /// </summary>
    public static List<QuestPlace> ActiveList(QuestTracker tracker, int chapter)
    {
        var list = new List<QuestPlace>();

        if (tracker == null)
            return list;

        QuestDefinition tracked = tracker.Tracked();

        foreach (QuestDefinition quest in tracker.Visible())
        {
            if (tracker.StateOf(quest) != QuestState.Active)
                continue;

            string zone = ZoneOf(tracker.NextObjective(quest));
            ZoneDefinition z = zone != null ? StoryTable.Zone(zone) : null;
            var place = new QuestPlace(quest, z != null && z.Chapter == chapter ? zone : null);

            if (quest == tracked)
                list.Insert(0, place);
            else
                list.Add(place);
        }

        return list;
    }
}

/// <summary>진행 중 퀘스트 한 줄 — 그 퀘스트와 이 장에서 갈 구역(없으면 null).</summary>
public readonly struct QuestPlace
{
    public readonly QuestDefinition Quest;
    public readonly string ZoneId;

    public QuestPlace(QuestDefinition quest, string zoneId)
    {
        Quest = quest;
        ZoneId = zoneId;
    }

    public bool HasPlace => !string.IsNullOrEmpty(ZoneId);
}
