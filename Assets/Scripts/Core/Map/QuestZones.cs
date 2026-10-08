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
}
