using System;
using System.Collections.Generic;

/// <summary>퀘스트 창에 보이는 상태.</summary>
public enum QuestState
{
    /// <summary>아직 받지 않았다 — 퀘스트 창에 없다.</summary>
    Hidden = 0,

    Active = 1,
    Completed = 2
}

/// <summary>
/// 퀘스트 진행을 계산한다. MonoBehaviour 없는 순수 클래스다.
///
/// 【담지 않는다 — 계산한다】 StoryProgress와 같은 원칙이다. 목표가 채워졌는지는
/// 보스 · 조각 · 본 사건 · 밤 · 지은 건물에서 매번 읽는다. 그래서 세이브 판을 올리지 않는다.
///
/// 【목표는 순서대로 보인다】 — 앞 목표를 채우기 전에 뒤 목표를 먼저 채워도
/// (예: 강철이를 먼저 쓰러뜨림) 그 목표는 채워진 것으로 친다. 화면에는 「첫 번째 안 채운 목표」가 다음 목표다.
/// </summary>
public sealed class QuestTracker
{
    private readonly StoryProgress story;
    private readonly Func<string, bool> isBuilt;

    /// <param name="isBuilt">건물을 지어 놓았는가 — BuildingState.IsPlaced. 없으면 늘 false.</param>
    public QuestTracker(StoryProgress story, Func<string, bool> isBuilt = null)
    {
        this.story = story ?? throw new ArgumentNullException(nameof(story));
        this.isBuilt = isBuilt ?? (_ => false);
    }

    public bool IsMet(QuestObjective objective)
    {
        if (objective == null)
            return false;

        switch (objective.Condition)
        {
            case QuestCondition.Seen:       return story.HasSeen(objective.Target);
            case QuestCondition.DefeatBoss: return story.HasDefeated(objective.Target);
            case QuestCondition.ClearZone:  return story.IsCleared(objective.Target);
            case QuestCondition.Build:      return isBuilt(objective.Target);
            default:                        return false;
        }
    }

    public bool IsStarted(QuestDefinition quest)
        => quest != null && (story.HasSeen(quest.StartEvent) || IsComplete(quest));

    public bool IsComplete(QuestDefinition quest)
    {
        if (quest == null || quest.Objectives.Length == 0)
            return false;

        foreach (QuestObjective o in quest.Objectives)
            if (!IsMet(o))
                return false;

        return true;
    }

    public QuestState StateOf(QuestDefinition quest)
    {
        if (IsComplete(quest))
            return QuestState.Completed;

        return IsStarted(quest) ? QuestState.Active : QuestState.Hidden;
    }

    public QuestState StateOf(string questId) => StateOf(QuestTable.Quest(questId));

    /// <summary>다음 목표 — 아직 안 채운 것 중 맨 앞. 다 채웠으면 null.</summary>
    public QuestObjective NextObjective(QuestDefinition quest)
    {
        if (quest == null)
            return null;

        foreach (QuestObjective o in quest.Objectives)
            if (!IsMet(o))
                return o;

        return null;
    }

    public int MetCount(QuestDefinition quest)
    {
        int n = 0;

        if (quest != null)
            foreach (QuestObjective o in quest.Objectives)
                if (IsMet(o))
                    n++;

        return n;
    }

    /// <summary>퀘스트 창에 보일 것 — 진행 중 먼저(메인 → 서브), 그다음 끝낸 것.</summary>
    public List<QuestDefinition> Visible()
    {
        var active = new List<QuestDefinition>();
        var done = new List<QuestDefinition>();

        foreach (QuestKind kind in new[] { QuestKind.Main, QuestKind.Sub })
            foreach (QuestDefinition q in QuestTable.OfKind(kind))
            {
                QuestState s = StateOf(q);

                if (s == QuestState.Active) active.Add(q);
                else if (s == QuestState.Completed) done.Add(q);
            }

        active.AddRange(done);
        return active;
    }

    /// <summary>추적 HUD에 띄울 퀘스트 — 진행 중인 메인. 없으면 진행 중인 서브 맨 앞. 없으면 null.</summary>
    public QuestDefinition Tracked()
    {
        foreach (QuestKind kind in new[] { QuestKind.Main, QuestKind.Sub })
            foreach (QuestDefinition q in QuestTable.OfKind(kind))
                if (StateOf(q) == QuestState.Active)
                    return q;

        return null;
    }
}
