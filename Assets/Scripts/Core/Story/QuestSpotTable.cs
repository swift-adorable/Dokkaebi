using System.Collections.Generic;

/// <summary>퀘스트 자리에서 하는 일 (결정 2-108).</summary>
public enum QuestSpotKind
{
    /// <summary>줍는다 — 봇짐 · 절구 · 약재 · 불씨 · 지방.</summary>
    Pickup = 0,

    /// <summary>말을 건다 · 건넨다 · 태운다 — 누르면 끝난다.</summary>
    Talk = 1,

    /// <summary>지킨다 — 가까이 가면 잡귀가 달라붙어 있고, 다 떼어 내면 끝난다(장승).</summary>
    Defend = 2
}

/// <summary>퀘스트 자리 하나 — 그 구역(또는 소굴)의 정해진 곳.</summary>
public sealed class QuestSpotDefinition
{
    public const string BunkerZone = "den";

    public readonly string Id;
    public readonly string QuestId;
    public readonly QuestSpotKind Kind;

    /// <summary>구역 id — 소굴이면 BunkerZone.</summary>
    public readonly string ZoneId;
    public readonly float X;
    public readonly float Z;

    /// <summary>버튼 글 — 본문의 물건 · 사람 이름.</summary>
    public readonly string Label;

    /// <summary>먼저 봐 둔 사건 · 쓰러뜨린 보스 — 다 채워야 이 자리가 나타난다.</summary>
    public readonly string[] RequiresEvents;
    public readonly string[] RequiresBosses;

    /// <summary>Defend — 달라붙은 잡귀 수.</summary>
    public readonly int Guards;

    public QuestSpotDefinition(string id, string questId, QuestSpotKind kind, string zoneId, float x, float z,
        string label, string[] requiresEvents = null, string[] requiresBosses = null, int guards = 0)
    {
        Id = id;
        QuestId = questId;
        Kind = kind;
        ZoneId = zoneId;
        X = x;
        Z = z;
        Label = label;
        RequiresEvents = requiresEvents ?? System.Array.Empty<string>();
        RequiresBosses = requiresBosses ?? System.Array.Empty<string>();
        Guards = guards;
    }

    /// <summary>이 자리를 끝냈다고 적는 사건 id.</summary>
    public string DoneEvent => QuestSpotTable.EventOf(Id);
}

/// <summary>
/// 【퀘스트를 본문 행동대로】 (결정 2-108) — 지역 사건 다섯 · 마지막 이야기의 넋 보내기를 자리에서 한다.
///
/// 진행은 이야기 진행(seen)에 「spot_자리」로 적는다 — 세이브에 따로 칸을 두지 않는다. 주운 퀘스트 물건은 가방에 들지 않고
/// 쓰러져도 잃지 않는다(기억의 조각과 같다). 자리는 맵 표지(ZoneMapTable Landmark)의 본문 자리를 쓴다 · 수 · 거리는 [임시값].
///
///   봇짐 되찾기   1-1 야광귀를 쓰러뜨리면 처마 밑(그것의 발치)에 봇짐 — 줍는다
///   멈춘 물레방아 2-1 방앗간 늙은이에게 안부 → 2-2 절구 줍기 → 방앗간에 가져오기 → 강철이 → 소굴의 영감에게 소식
///   총명탕       3-1 부엌 선반의 백복신 · 원지 · 석창포 → 3-2 아궁이 다섯의 불씨(처녀 · 몽달 · 두억시니를 지나야 셋) → 3-1 부엌에서 달이기 → 소굴의 빚쟁이에게
///   장승 지키기   4-1 장승 셋에 달라붙은 잡귀를 떼어 낸다 → 꺼먹살이
///   지방 보내기   5-1 떠도는 지방 다섯을 모아 담장 화로에 태운다
///   마지막 이야기 6-2 구미호를 물리친 뒤 결계의 가장자리에서 넋을 보낸다
/// </summary>
public static class QuestSpotTable
{
    public static string EventOf(string spotId) => $"spot_{spotId}";

    private const string Den = QuestSpotDefinition.BunkerZone;

    // 퀘스트 id (QuestTable과 같다)
    public const string Bundle = "sub_bundle";
    public const string Watermill = "sub_watermill";
    public const string Chongmyeong = "sub_chongmyeongtang";
    public const string Jangseung = "sub_jangseung";
    public const string Jibang = "sub_jibang";

    public static readonly string[] Herbs = { "herb_baekboksin", "herb_wonji", "herb_seokchangpo" };
    public static readonly string[] Embers = { "ember_maiden", "ember_bachelor", "ember_west", "ember_east", "ember_last" };
    public static readonly string[] Jangseungs = { "jangseung_wood", "jangseung_row", "jangseung_rope" };
    public static readonly string[] Jibangs = { "jibang_1", "jibang_2", "jibang_3", "jibang_4", "jibang_5" };

    public const string BundleBack = "bundle_back";
    public const string MillGreet = "mill_greet";
    public const string Mortar = "mortar";
    public const string MillMortar = "mill_mortar";
    public const string ElderMillNews = "elder_mill_news";
    public const string KitchenBrew = "kitchen_brew";
    public const string DebtorTonic = "debtor_tonic";
    public const string JibangBurn = "jibang_burn";
    public const string SendSoul = "send_soul";

    private static string[] E(params string[] spots)
    {
        var r = new string[spots.Length];
        for (int i = 0; i < spots.Length; i++)
            r[i] = EventOf(spots[i]);
        return r;
    }

    private static string[] B(params string[] bosses) => bosses;

    private static readonly QuestSpotDefinition[] spots =
    {
        // ── 봇짐 되찾기 (본문 211 ~ 219 · 부록 1175) ───────────────────────
        new(BundleBack, Bundle, QuestSpotKind.Pickup, "1-1", 0f, -29.5f, "봇짐", requiresBosses: B("yagwanggwi")),

        // ── 멈춘 물레방아 (본문 360 · 399 · 부록 1176) ────────────────────
        new(MillGreet, Watermill, QuestSpotKind.Talk, "2-1", -20f, 0.5f, "방앗간 늙은이 — 영감의 안부"),
        new(Mortar, Watermill, QuestSpotKind.Pickup, "2-2", 14f, -41f, "절구", requiresEvents: E(MillGreet)),
        new(MillMortar, Watermill, QuestSpotKind.Talk, "2-1", -20f, 3.5f, "방앗간 늙은이 — 절구", requiresEvents: E(Mortar)),
        new(ElderMillNews, Watermill, QuestSpotKind.Talk, Den, -2f, 2.5f, "영감 — 물레방아 소식",
            requiresEvents: E(MillMortar), requiresBosses: B("gangcheori")),

        // ── 총명탕 (본문 506 · 549 ~ 597 · 부록 1177) ─────────────────────
        new(Herbs[0], Chongmyeong, QuestSpotKind.Pickup, "3-1", 0f, 49.5f, "백복신"),
        new(Herbs[1], Chongmyeong, QuestSpotKind.Pickup, "3-1", 4f, 50.5f, "원지"),
        new(Herbs[2], Chongmyeong, QuestSpotKind.Pickup, "3-1", 8f, 49.5f, "석창포"),
        new(Embers[0], Chongmyeong, QuestSpotKind.Pickup, "3-2", -14f, -2.5f, "아궁이 불씨", requiresBosses: B("wongwi")),
        new(Embers[1], Chongmyeong, QuestSpotKind.Pickup, "3-2", 14f, -9.5f, "아궁이 불씨", requiresBosses: B("wongwi")),
        new(Embers[2], Chongmyeong, QuestSpotKind.Pickup, "3-2", -40f, -2.5f, "꺼져 가는 아궁이 불씨"),
        new(Embers[3], Chongmyeong, QuestSpotKind.Pickup, "3-2", 40f, -9.5f, "꺼져 가는 아궁이 불씨"),
        new(Embers[4], Chongmyeong, QuestSpotKind.Pickup, "3-2", -36f, -29f, "마지막 아궁이 불씨", requiresBosses: B("dueoksini")),
        new(KitchenBrew, Chongmyeong, QuestSpotKind.Talk, "3-1", 4f, 46f, "부엌 아궁이 — 총명탕 달이기",
            requiresEvents: Concat(E(Herbs), E(Embers))),
        new(DebtorTonic, Chongmyeong, QuestSpotKind.Talk, Den, 2f, 2.5f, "빚쟁이 — 총명탕", requiresEvents: E(KitchenBrew)),

        // ── 장승 지키기 (본문 731 · 733 · 부록 1178) ──────────────────────
        new(Jangseungs[0], Jangseung, QuestSpotKind.Defend, "4-1", 34f, 20f, "나무 장승", guards: 4),
        new(Jangseungs[1], Jangseung, QuestSpotKind.Defend, "4-1", 88f, 22f, "늘어선 장승", guards: 4),
        new(Jangseungs[2], Jangseung, QuestSpotKind.Defend, "4-1", 70f, -50f, "금줄을 붙든 장승", guards: 4),

        // ── 지방 보내기 (본문 870 ~ 876 · 부록 1179) ──────────────────────
        new(Jibangs[0], Jibang, QuestSpotKind.Pickup, "5-1", -26f, -56f, "떠도는 지방"),
        new(Jibangs[1], Jibang, QuestSpotKind.Pickup, "5-1", 6f, -54f, "떠도는 지방"),
        new(Jibangs[2], Jibang, QuestSpotKind.Pickup, "5-1", -24f, -36f, "떠도는 지방"),
        new(Jibangs[3], Jibang, QuestSpotKind.Pickup, "5-1", 4f, -22f, "떠도는 지방"),
        new(Jibangs[4], Jibang, QuestSpotKind.Pickup, "5-1", 16f, -2f, "떠도는 지방"),
        new(JibangBurn, Jibang, QuestSpotKind.Talk, "5-1", -30f, -60.5f, "담장 화로 — 지방 태우기", requiresEvents: E(Jibangs)),

        // ── 마지막 이야기 — 넋을 보낸다 (본문 1079 ~ 1103) ─────────────────
        new(SendSoul, QuestTable.MainLastStory, QuestSpotKind.Talk, "6-2", 0f, 55f, "결계의 가장자리 — 할머니의 넋",
            requiresBosses: B("gumiho")),
    };

    private static string[] Concat(string[] a, string[] b)
    {
        var r = new string[a.Length + b.Length];
        a.CopyTo(r, 0);
        b.CopyTo(r, a.Length);
        return r;
    }

    public static IReadOnlyList<QuestSpotDefinition> Spots => spots;

    public static QuestSpotDefinition Spot(string id)
    {
        foreach (QuestSpotDefinition s in spots)
            if (s.Id == id)
                return s;
        return null;
    }

    /// <summary>
    /// 지금 이 자리가 나타나는가 — 그 퀘스트를 받았고 · 아직 끝내지 않았고 · 이 자리를 아직 하지 않았고 · 먼저 할 일을 다 했다.
    /// </summary>
    public static bool IsAvailable(QuestSpotDefinition spot, StoryProgress progress, QuestTracker tracker)
    {
        if (spot == null || progress == null || tracker == null)
            return false;

        if (progress.HasSeen(spot.DoneEvent))
            return false;

        QuestDefinition quest = QuestTable.Quest(spot.QuestId);
        if (quest == null || tracker.StateOf(quest) != QuestState.Active)
            return false;

        foreach (string e in spot.RequiresEvents)
            if (!progress.HasSeen(e))
                return false;

        foreach (string b in spot.RequiresBosses)
            if (!progress.HasDefeated(b))
                return false;

        return true;
    }

    /// <summary>그 구역(또는 소굴)의 자리들.</summary>
    public static List<QuestSpotDefinition> In(string zoneId)
    {
        var list = new List<QuestSpotDefinition>();
        foreach (QuestSpotDefinition s in spots)
            if (s.ZoneId == zoneId)
                list.Add(s);
        return list;
    }
}
