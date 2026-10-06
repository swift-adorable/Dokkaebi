using System.Collections.Generic;

/// <summary>계절 — 장마다 고정한다 (결정 2-59). 그 안의 날씨는 파밍마다 뽑는다 [날씨 미구현].</summary>
public enum Season
{
    Winter = 0,
    Spring = 1,
    Summer = 2,
    Autumn = 3
}

/// <summary>장의 일반 적 한 갈래 — 유형과 나오는 비중.</summary>
public readonly struct PoolEntry
{
    public readonly EnemyArchetype Archetype;

    /// <summary>비중. 장 안의 합에 대한 몫이다 [임시값 — 플레이테스트].</summary>
    public readonly int Weight;

    public PoolEntry(EnemyArchetype archetype, int weight)
    {
        Archetype = archetype;
        Weight = weight;
    }
}

/// <summary>
/// 장 하나의 Zone Data — 이야기 표(StoryTable)에 없는, 맵을 만들 때 필요한 것.
/// 계절 · 무기 티어 · 일반 적 풀 · 장별 재료 · 궂은 날 이름.
/// </summary>
public sealed class ChapterData
{
    public readonly int Chapter;
    public readonly Season Season;

    /// <summary>이 장 파밍이 주는 무기 티어 — 장 = 무기 티어 (Story 8절). 0장은 0.</summary>
    public readonly int WeaponTier;

    public readonly PoolEntry[] Pool;

    /// <summary>장별 재료 4종 — 흔한 것 2 · 보통 1 · 귀한 것 1 (마지막이 귀한 것). 아이템 id는 아직 없다 [미구현].</summary>
    public readonly string[] Materials;

    /// <summary>이 장의 궂은 날 Ⅰ / Ⅱ 이름 (결정 2-60 · 2-64). 6장은 꽃비 — 피해 없음.</summary>
    public readonly string BadWeather1;
    public readonly string BadWeather2;

    /// <summary>궂은 날이 피해를 주는가. 6장 꽃비만 false (결정 2-64).</summary>
    public readonly bool BadWeatherHurts;

    public ChapterData(int chapter, Season season, int weaponTier, PoolEntry[] pool, string[] materials,
        string badWeather1, string badWeather2, bool badWeatherHurts = true)
    {
        Chapter = chapter;
        Season = season;
        WeaponTier = weaponTier;
        Pool = pool;
        Materials = materials;
        BadWeather1 = badWeather1;
        BadWeather2 = badWeather2;
        BadWeatherHurts = badWeatherHurts;
    }

    public int TotalWeight
    {
        get
        {
            int sum = 0;
            foreach (PoolEntry e in Pool) sum += e.Weight;
            return sum;
        }
    }
}

/// <summary>구역 하나에 모이는 것 — 맵을 만들 때 Story 문서를 다시 펴지 않게.</summary>
public sealed class ZoneSummary
{
    public ZoneDefinition Zone;
    public ChapterData Chapter;
    public List<BossDefinition> Bosses;
    public PieceDefinition Piece;
    public NoticeDefinition Notice;

    /// <summary>이 구역에서 시작하거나 이 구역을 목표로 삼는 퀘스트.</summary>
    public List<QuestDefinition> Quests;

    /// <summary>물가 태그 — 보름달의 「물가 수귀 ×2」가 여기에만 걸린다 (결정 2-63).</summary>
    public bool IsWaterside;
}

/// <summary>
/// 【Zone Data.】 장 → 구역 → 중간 보스 → 장 보스 → 기억의 조각 → 방 → 퀘스트 + 계절 · 적 풀 · 재료.
/// 이야기 쪽(구역 · 보스 · 조각 · 방)은 StoryTable, 퀘스트는 QuestTable이 원본이다 — 여기서는 모으기만 한다.
/// 여기가 원본인 것: 계절(2-59) · 일반 적 풀(2-57 · Hunting 1절) · 장별 재료 이름(Story 8절) · 물가 태그(2-63).
/// </summary>
public static class ZoneDataTable
{
    private static PoolEntry P(EnemyArchetype a, int w) => new(a, w);

    // 비중은 [임시값]이다 — Hunting 1절 「장별 구성」의 글(「드물게」 「위주」 「조금」)을 숫자로 옮겼다.
    private static readonly ChapterData[] chapters =
    {
        new(0, Season.Spring, 0, new[] { P(EnemyArchetype.Scav, 100) },
            new string[0], "", "", false),

        new(1, Season.Winter, 1, new[] { P(EnemyArchetype.Scav, 90), P(EnemyArchetype.Crusher, 10) },
            new[] { "기름", "벽돌", "쇠가루", "청동조각" }, "한파 Ⅰ", "한파 Ⅱ"),

        new(2, Season.Spring, 2, new[] { P(EnemyArchetype.Scav, 40), P(EnemyArchetype.Dynamo, 30), P(EnemyArchetype.Lurker, 30) },
            new[] { "대나무가지", "창포뿌리", "참나무조각", "너구리털" }, "흙비 Ⅰ", "흙비 Ⅱ"),

        new(3, Season.Summer, 3, new[] { P(EnemyArchetype.Chemic, 50), P(EnemyArchetype.Specimen, 50) },
            new[] { "약초잎사귀", "감초", "장석", "운모" }, "폭염", "독안개"),

        new(4, Season.Autumn, 4, new[] { P(EnemyArchetype.Scav, 60), P(EnemyArchetype.Settled, 20), P(EnemyArchetype.Sentry, 20) },
            new[] { "노송의가지", "꿩의깃털", "흑돌", "물소뿔" }, "짙은 안개 Ⅰ", "짙은 안개 Ⅱ"),

        new(5, Season.Winter, 5, new[] { P(EnemyArchetype.Sentry, 50), P(EnemyArchetype.Wraith, 50) },
            new[] { "석영", "마노", "수정", "금조각" }, "한파 Ⅰ", "한파 Ⅱ"),

        new(6, Season.Spring, 6, new[] { P(EnemyArchetype.Wraith, 80), P(EnemyArchetype.Scav, 20) },
            new[] { "꽃잎", "이슬", "향료가루", "꽃씨" }, "꽃비", "꽃비", false)
    };

    /// <summary>4장의 잡귀는 떼로 나온다 — 한 번에 8~12 (Hunting 1절 · 이야기의 「잡귀 떼」) [스포너 미반영].</summary>
    public const int SwarmChapter = 4;
    public const int SwarmMin = 8;
    public const int SwarmMax = 12;

    /// <summary>1장 첫 방문은 비로 고정한다 (결정 2-63 ①) [날씨 미구현].</summary>
    public const int FirstVisitRainChapter = 1;

    // 물가 태그 [임시 — 구역 맵(9단계) 때 다시 본다]. 본문: 1-3 우물 · 2-1 대숲 강가 방앗간 · 2-3 강 상류 · 4-2 냇물 앞.
    private static readonly HashSet<string> waterside = new() { "1-3", "2-1", "2-3", "4-2" };

    public static IReadOnlyList<ChapterData> Chapters => chapters;

    public static ChapterData Chapter(int number)
        => number >= 0 && number < chapters.Length ? chapters[number] : null;

    public static ChapterData ChapterOfZone(string zoneId)
    {
        ZoneDefinition z = StoryTable.Zone(zoneId);
        return z == null ? null : Chapter(z.Chapter);
    }

    public static bool IsWaterside(string zoneId) => zoneId != null && waterside.Contains(zoneId);

    /// <summary>이 장의 기본 적 풀에 있는가.</summary>
    public static bool InPool(int chapter, EnemyArchetype archetype)
    {
        ChapterData c = Chapter(chapter);

        if (c == null)
            return false;

        foreach (PoolEntry e in c.Pool)
            if (e.Archetype == archetype)
                return true;

        return false;
    }

    /// <summary>
    /// 조건(달 · 날씨 · 퀘스트 작업)이 이 적을 더해도 되는가 — **그 장의 기본 적 풀에 있을 때만** (결정 2-63 ② · 2-64 · 2-65 ⑥).
    /// </summary>
    public static bool AllowsExtra(int chapter, EnemyArchetype archetype) => InPool(chapter, archetype);

    /// <summary>
    /// 풀에서 하나 고른다. roll은 0 이상 1 미만 — 테스트에서 고정할 수 있게 밖에서 받는다.
    /// </summary>
    public static EnemyArchetype Pick(int chapter, float roll)
    {
        ChapterData c = Chapter(chapter) ?? chapters[0];
        int total = c.TotalWeight;
        float target = roll * total;
        float acc = 0f;

        foreach (PoolEntry e in c.Pool)
        {
            acc += e.Weight;
            if (target < acc)
                return e.Archetype;
        }

        return c.Pool[c.Pool.Length - 1].Archetype;
    }

    public static ZoneSummary Summary(string zoneId)
    {
        ZoneDefinition zone = StoryTable.Zone(zoneId);

        if (zone == null)
            return null;

        var quests = new List<QuestDefinition>();

        foreach (QuestDefinition q in QuestTable.Quests)
            if (Touches(q, zoneId))
                quests.Add(q);

        return new ZoneSummary
        {
            Zone = zone,
            Chapter = Chapter(zone.Chapter),
            Bosses = StoryTable.BossesIn(zoneId),
            Piece = StoryTable.PieceIn(zoneId),
            Notice = StoryTable.NoticeIn(zoneId),
            Quests = quests,
            IsWaterside = IsWaterside(zoneId)
        };
    }

    /// <summary>퀘스트가 이 구역과 닿는가 — 이 구역에서 시작하거나, 목표가 이 구역 · 이 구역의 보스다.</summary>
    private static bool Touches(QuestDefinition q, string zoneId)
    {
        if (q.StartEvent == StoryTable.EnterEvent(zoneId))
            return true;

        foreach (QuestObjective o in q.Objectives)
        {
            switch (o.Condition)
            {
                case QuestCondition.Seen when o.Target == StoryTable.EnterEvent(zoneId):
                case QuestCondition.ClearZone when o.Target == zoneId:
                    return true;
                case QuestCondition.DefeatBoss:
                    BossDefinition b = StoryTable.Boss(o.Target);
                    if (b != null && b.ZoneId == zoneId)
                        return true;
                    break;
            }
        }

        return false;
    }
}
