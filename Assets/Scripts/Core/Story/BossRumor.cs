using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【보스 소문 영역】 (결정 2-142 · 덕코프 모드 「보스 실시간 지도 표시」 180만을 우리 식으로)
/// 덕코프 모드는 보스 자리를 정확히 찍는다 — 그러면 찾는 재미가 없어진다. 그래서 【대략의 영역만】 원으로 보인다.
///   · 소문을 듣는 길 = 그 보스를 쓰러뜨리라는 퀘스트가 진행 중일 때(퀘스트를 준 이가 「대략 저쪽」이라고 알려 준 것).
///     [임시] 따로 「소문 사기」 같은 길은 아직 없다 — 주막 · 상인 대화가 생기면 그쪽에서도 연다.
///   · 원의 가운데는 보스 자리에서 반지름의 절반 안쪽으로 비껴 있다 — 원 안 어딘가에 있다는 것만 안다.
///   · 쓰러뜨리면 지운다. 씬이 바뀌면 비운다(SceneFlow).
/// </summary>
public static class BossRumor
{
    /// <summary>원의 반지름(m) [임시값].</summary>
    public const float Radius = 18f;

    public static readonly Color RingColor = new(1f, 0.42f, 0.36f, 0.95f);
    public static readonly Color FillColor = new(1f, 0.36f, 0.30f, 0.28f);

    public readonly struct Area
    {
        public readonly string BossId;
        public readonly string Name;
        public readonly Vector3 Center;
        public readonly float Radius;

        public Area(string bossId, string name, Vector3 center, float radius)
        {
            BossId = bossId;
            Name = name;
            Center = center;
            Radius = radius;
        }
    }

    private static readonly List<Area> areas = new();

    public static IReadOnlyList<Area> Areas => areas;

    /// <summary>보스가 섰다 — 소문을 들었으면(퀘스트 진행 중) 영역을 연다.</summary>
    public static void OnBossPlaced(BossDefinition boss, Vector3 at, System.Random rng = null)
    {
        if (boss == null || !Heard(boss.Id, StoryManager.Quests))
            return;

        rng ??= new System.Random();
        double angle = rng.NextDouble() * System.Math.PI * 2.0;
        float shift = (float)rng.NextDouble() * Radius * 0.5f;
        Vector3 center = at + new Vector3(Mathf.Cos((float)angle), 0f, Mathf.Sin((float)angle)) * shift;

        Remove(boss.Id);
        areas.Add(new Area(boss.Id, boss.Name, center, Radius));
    }

    public static void Remove(string bossId) => areas.RemoveAll(a => a.BossId == bossId);

    public static void Clear() => areas.Clear();

    /// <summary>이 보스의 소문을 들었는가 — 진행 중 퀘스트의 다음 목표가 이 보스를 쓰러뜨리는 것.</summary>
    public static bool Heard(string bossId, QuestTracker tracker)
    {
        if (tracker == null || string.IsNullOrEmpty(bossId))
            return false;

        foreach (QuestDefinition quest in tracker.Visible())
        {
            if (tracker.StateOf(quest) != QuestState.Active)
                continue;

            QuestObjective next = tracker.NextObjective(quest);
            if (next != null && next.Condition == QuestCondition.DefeatBoss && next.Target == bossId)
                return true;
        }

        return false;
    }
}
