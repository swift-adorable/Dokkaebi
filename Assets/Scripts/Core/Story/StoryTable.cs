using System.Collections.Generic;

/// <summary>구역을 끝내는 조건. (docs/Dokkaebi_Story.md 2절)</summary>
public enum ZoneClear
{
    /// <summary>그 구역의 마지막 보스를 쓰러뜨린다.</summary>
    Boss = 0,

    /// <summary>그 구역의 기억의 조각을 줍는다.</summary>
    Piece = 1,

    /// <summary>그 구역의 이야기를 본다 — 튜토리얼 · 이야기 구역.</summary>
    Story = 2
}

/// <summary>구역 하나. 장 하나 = 몇 번이고 다시 들어가는 맵 하나, 구역은 그 안의 자리다.</summary>
public sealed class ZoneDefinition
{
    public readonly string Id;
    public readonly int Chapter;
    public readonly string Name;
    public readonly ZoneClear ClearBy;

    public ZoneDefinition(string id, int chapter, string name, ZoneClear clearBy)
    {
        Id = id;
        Chapter = chapter;
        Name = name;
        ClearBy = clearBy;
    }

    /// <summary>「1-1 · 장터 어귀」.</summary>
    public string Title => $"{Id} · {Name}";
}

/// <summary>장 하나.</summary>
public sealed class ChapterDefinition
{
    public readonly int Number;

    /// <summary>길 이름 — 「젖은 장터길 (북)」.</summary>
    public readonly string Path;

    /// <summary>장소 — 「비 오는 폐장터」.</summary>
    public readonly string Place;

    public readonly ZoneDefinition[] Zones;

    public ChapterDefinition(int number, string path, string place, ZoneDefinition[] zones)
    {
        Number = number;
        Path = path;
        Place = place;
        Zones = zones;
    }

    public string Title => Number == 0 ? $"0장 · {Place}" : $"{Number}장 · {Place}";
}

/// <summary>
/// 이야기의 보스 하나 — 중간 보스와 장 보스. (docs/Dokkaebi_Story.md 2~3절)
///
/// 【몸은 기존 적 유형을 빌린다.】 보스마다 고유 기믹은 아직 없다 [미구현].
/// 유형의 수치에 등급 배율(고유 ×15 · 희귀 ×4.5)만 얹는다.
/// </summary>
public sealed class BossDefinition
{
    public readonly string Id;
    public readonly string Name;
    public readonly string ZoneId;

    /// <summary>나오는 몸. 둘 이상이면 함께 나오고, 전부 쓰러져야 끝난다.</summary>
    public readonly EnemyArchetype[] Bodies;

    public readonly EnemyRarity Rarity;

    /// <summary>장 보스인가. 장 보스는 그 구역에 들어갈 때마다 다시 나온다.</summary>
    public readonly bool IsChapterBoss;

    /// <summary>쓰러뜨리면 내주는 사신패. 없으면 비어 있다.</summary>
    public readonly string Tablet;

    public BossDefinition(string id, string name, string zoneId, EnemyArchetype[] bodies,
        EnemyRarity rarity, bool isChapterBoss, string tablet = "")
    {
        Id = id;
        Name = name;
        ZoneId = zoneId;
        Bodies = bodies;
        Rarity = rarity;
        IsChapterBoss = isChapterBoss;
        Tablet = tablet ?? string.Empty;
    }
}

/// <summary>상인 하나. 이야기에서 오면 그 가게 설계도가 열린다. (docs/Dokkaebi_Story.md 1절)</summary>
public sealed class MerchantDefinition
{
    public readonly string Id;
    public readonly string Name;

    /// <summary>오는 밤. 0이면 처음부터 있다. n이면 n장을 끝낸 뒤 「n번째 밤」.</summary>
    public readonly int Night;

    /// <summary>이 상인이 열어 주는 건물(설계도). 없으면 비어 있다.</summary>
    public readonly string BuildingId;

    /// <summary>가게를 지어 주면 받는 것 — (아이템 id, 개수).</summary>
    public readonly (string itemId, int count)[] Rewards;

    public MerchantDefinition(string id, string name, int night, string buildingId,
        (string, int)[] rewards)
    {
        Id = id;
        Name = name;
        Night = night;
        BuildingId = buildingId ?? string.Empty;
        Rewards = rewards ?? System.Array.Empty<(string, int)>();
    }
}

/// <summary>기억의 조각 한 장. 글은 이야기 본문에서 읽는다 (StoryScript).</summary>
public sealed class PieceDefinition
{
    public readonly string Id;
    public readonly int Number;
    public readonly string ZoneId;

    public PieceDefinition(string id, int number, string zoneId)
    {
        Id = id;
        Number = number;
        ZoneId = zoneId;
    }
}

/// <summary>방(榜) — 길에 붙은 글. 힌트 수집품이다 (결정 2-41 · 2-45).</summary>
public sealed class NoticeDefinition
{
    public readonly string Id;
    public readonly string ZoneId;
    public readonly string Text;

    public NoticeDefinition(string id, string zoneId, string text)
    {
        Id = id;
        ZoneId = zoneId;
        Text = text;
    }
}

/// <summary>
/// 【이야기 표.】 장 · 구역 · 보스 · 상인 · 기억의 조각 · 방.
/// 기준 문서는 docs/Dokkaebi_Story.md 2~3절과 본문 docs/Dokkaebi_Story_Script.txt다.
/// 본문의 글은 여기에 옮기지 않는다 — StoryScriptParser가 본문에서 읽는다.
/// </summary>
public static class StoryTable
{
    public const int LastChapter = 6;

    /// <summary>5장(돌문)을 여는 사신패 수.</summary>
    public const int TabletsForGate = 4;

    /// <summary>방 하나를 읽으면 받는 엽전 [미검증 — 정한 값].</summary>
    public const int NoticeReward = 20;

    private static ZoneDefinition Z(string id, string name, ZoneClear clear)
        => new(id, int.Parse(id.Substring(0, 1)), name, clear);

    private static readonly ChapterDefinition[] chapters =
    {
        new(0, "튜토리얼", "대숲 밤길", new[]
        {
            Z("0-1", "대숲 밤길", ZoneClear.Story),
            Z("0-2", "고목 뒤 수풀", ZoneClear.Story)
        }),
        new(1, "젖은 장터길 (북)", "비 오는 폐장터", new[]
        {
            Z("1-1", "장터 어귀", ZoneClear.Boss),
            Z("1-2", "장터 깊은 곳", ZoneClear.Boss),
            Z("1-3", "우물", ZoneClear.Boss)
        }),
        new(2, "물레방아길 (동)", "대숲과 물레방아", new[]
        {
            Z("2-1", "대숲", ZoneClear.Boss),
            Z("2-2", "언덕 너머", ZoneClear.Piece),
            Z("2-3", "강 상류", ZoneClear.Boss),
            Z("2-4", "대숲 끝", ZoneClear.Boss)
        }),
        new(3, "불씨길 (남)", "약방골과 주작의 가마", new[]
        {
            Z("3-1", "약방골", ZoneClear.Piece),
            Z("3-2", "불씨 골목", ZoneClear.Boss),
            Z("3-3", "주작의 가마", ZoneClear.Boss)
        }),
        new(4, "장승들길 (서)", "장승 벌판", new[]
        {
            Z("4-1", "벌판 어귀", ZoneClear.Boss),
            Z("4-2", "냇물 앞", ZoneClear.Boss),
            Z("4-3", "벌판 안쪽", ZoneClear.Boss),
            Z("4-4", "가장 안쪽 장승", ZoneClear.Story)
        }),
        new(5, "돌문 아래 길", "궁궐 문", new[]
        {
            Z("5-1", "돌길", ZoneClear.Boss),
            Z("5-2", "궁궐 문", ZoneClear.Boss),
            Z("5-3", "두루마리", ZoneClear.Story)
        }),
        new(6, "꽃밭", "결계의 가장자리", new[]
        {
            Z("6-1", "꽃밭", ZoneClear.Boss),
            Z("6-2", "가장자리", ZoneClear.Boss)
        })
    };

    private static EnemyArchetype[] One(EnemyArchetype a) => new[] { a };

    // 【순서가 곧 구역 안의 순서다.】 한 구역에 보스가 둘이면 앞의 것을 쓰러뜨려야
    // 뒤의 것이 나온다 (3-2 · 5-1). 본문의 「싸움 끝에」 순서와 같다.
    private static readonly BossDefinition[] bosses =
    {
        new("yagwanggwi",   "야광귀",   "1-1", One(EnemyArchetype.Scav),    EnemyRarity.Unique, false),
        new("dalgyal",      "달걀귀신", "1-2", One(EnemyArchetype.Wraith),  EnemyRarity.Unique, false),
        new("hyeonmu",      "현무",     "1-3", One(EnemyArchetype.Crusher), EnemyRarity.Unique, true, "현무패"),
        new("eodukssini",   "어둑시니", "2-1", One(EnemyArchetype.Settled), EnemyRarity.Unique, false),
        new("gangcheori",   "강철이",   "2-3", One(EnemyArchetype.Lurker),  EnemyRarity.Unique, false),
        new("cheongnyong",  "청룡",     "2-4", One(EnemyArchetype.Dynamo),  EnemyRarity.Unique, true, "청룡패"),
        new("wongwi",       "처녀귀신 · 몽달귀신", "3-2",
            new[] { EnemyArchetype.Wraith, EnemyArchetype.Settled },          EnemyRarity.Unique, false),
        new("dueoksini",    "두억시니", "3-2", One(EnemyArchetype.Chemic),  EnemyRarity.Unique, false),
        new("jujak",        "주작",     "3-3", One(EnemyArchetype.Sentry),  EnemyRarity.Unique, true, "주작패"),
        new("duduri",       "두두리",   "4-1", One(EnemyArchetype.Crusher), EnemyRarity.Unique, false),
        new("kkeomeoksari", "꺼먹살이", "4-2", One(EnemyArchetype.Scav),    EnemyRarity.Unique, false),
        new("baekho",       "백호",     "4-3", One(EnemyArchetype.Scav),    EnemyRarity.Unique, true, "백호패"),
        new("changgwi",     "창귀",     "5-1",
            new[] { EnemyArchetype.Settled, EnemyArchetype.Settled, EnemyArchetype.Settled }, EnemyRarity.Rare, false),
        new("sangun",       "산군",     "5-1", One(EnemyArchetype.Crusher), EnemyRarity.Unique, false),
        new("haetae",       "해태",     "5-2", One(EnemyArchetype.Sentry),  EnemyRarity.Unique, true),
        new("samjogo",      "삼족오",   "6-1", One(EnemyArchetype.Dynamo),  EnemyRarity.Unique, false),
        new("gumiho",       "구미호",   "6-2", One(EnemyArchetype.Settled), EnemyRarity.Unique, true)
    };

    public const string Elder = "elder";
    public const string Debtor = "debtor";
    public const string Chambong = "chambong";
    public const string Gildal = "gildal";

    private static readonly MerchantDefinition[] merchants =
    {
        new(Elder,    "영감",   0, string.Empty, null),
        new(Debtor,   "빚쟁이", 1, BuildingTable.ArmourShop, new[] { ("arm_body_t1", 1) }),
        new(Chambong, "참봉",   1, BuildingTable.GeneralStore,
            new[] { ("con_medkit_small", 2), ("con_soda", 1) }),
        // 본문의 보상은 「화살 한 묶음」이다. 화살(탄약)이 아직 없다 [미구현] — 비워 둔다.
        new(Gildal,   "길달",   2, BuildingTable.WeaponShop, null)
    };

    private static readonly PieceDefinition[] pieces =
    {
        new("piece_1", 1, "1-1"), new("piece_2", 2, "1-2"), new("piece_3", 3, "2-1"),
        new("piece_4", 4, "2-2"), new("piece_5", 5, "3-1"), new("piece_6", 6, "3-2"),
        new("piece_7", 7, "3-3"), new("piece_8", 8, "4-1"), new("piece_9", 9, "5-1")
    };

    // 방의 글은 게임 규칙을 그대로 알려 준다 — 「이름이 기능을 거짓말하지 않는다」.
    // 글은 새로 지은 것이다 [조어 — 사용자 확인 대기].
    private static readonly NoticeDefinition[] notices =
    {
        new("notice_1", "0-1", "붉은 빛이 번뜩이면 그때 몸을 날려라. 늦으면 맞고, 이르면 다시 맞는다."),
        new("notice_2", "0-2", "길목은 몇 곳뿐이다. 돌아갈 길목을 먼저 알아 두어라."),
        new("notice_3", "1-1", "체를 처마에 걸어 두면 야광귀는 구멍을 세느라 꼼짝 못 한다."),
        new("notice_4", "2-1", "어둑시니는 올려다볼수록 커진다. 발끝을 보아라."),
        new("notice_5", "2-3", "큰물이 진 밤에는 번개가 두 배로 아프다."),
        new("notice_6", "3-2", "독안개 낀 밤에는 왕지네가 몰려든다."),
        new("notice_7", "4-2", "허깨비에게는 칼이 잘 들지 않는다. 불을 써라."),
        new("notice_8", "5-1", "그믐에는 순라귀가 돌지 않는다.")
    };

    public static IReadOnlyList<ChapterDefinition> Chapters => chapters;
    public static IReadOnlyList<BossDefinition> Bosses => bosses;
    public static IReadOnlyList<MerchantDefinition> Merchants => merchants;
    public static IReadOnlyList<PieceDefinition> Pieces => pieces;
    public static IReadOnlyList<NoticeDefinition> Notices => notices;

    public static ChapterDefinition Chapter(int number)
        => number >= 0 && number < chapters.Length ? chapters[number] : null;

    public static ZoneDefinition Zone(string id)
    {
        foreach (ChapterDefinition c in chapters)
            foreach (ZoneDefinition z in c.Zones)
                if (z.Id == id)
                    return z;

        return null;
    }

    public static BossDefinition Boss(string id)
    {
        foreach (BossDefinition b in bosses)
            if (b.Id == id)
                return b;

        return null;
    }

    public static MerchantDefinition Merchant(string id)
    {
        foreach (MerchantDefinition m in merchants)
            if (m.Id == id)
                return m;

        return null;
    }

    /// <summary>이 건물을 열어 주는 상인. 없으면 null — 처음부터 지을 수 있다.</summary>
    public static MerchantDefinition MerchantFor(string buildingId)
    {
        foreach (MerchantDefinition m in merchants)
            if (!string.IsNullOrEmpty(m.BuildingId) && m.BuildingId == buildingId)
                return m;

        return null;
    }

    /// <summary>구역의 보스들, 나오는 순서대로.</summary>
    public static List<BossDefinition> BossesIn(string zoneId)
    {
        var list = new List<BossDefinition>();

        foreach (BossDefinition b in bosses)
            if (b.ZoneId == zoneId)
                list.Add(b);

        return list;
    }

    public static PieceDefinition PieceIn(string zoneId)
    {
        foreach (PieceDefinition p in pieces)
            if (p.ZoneId == zoneId)
                return p;

        return null;
    }

    public static NoticeDefinition NoticeIn(string zoneId)
    {
        foreach (NoticeDefinition n in notices)
            if (n.ZoneId == zoneId)
                return n;

        return null;
    }

    public static PieceDefinition Piece(string id)
    {
        foreach (PieceDefinition p in pieces)
            if (p.Id == id)
                return p;

        return null;
    }

    public static NoticeDefinition Notice(string id)
    {
        foreach (NoticeDefinition n in notices)
            if (n.Id == id)
                return n;

        return null;
    }

    // ── 이야기 사건 id — StoryScriptParser가 같은 이름으로 만든다 ─────────

    public const string PrologueEvent = "prologue";

    public static string EnterEvent(string zoneId) => $"enter_{zoneId}";
    public static string BossEvent(string bossId) => $"boss_{bossId}";
    public static string NightEvent(int night) => $"night_{night}";
    public static string PieceEvent(string pieceId) => $"read_{pieceId}";
}
