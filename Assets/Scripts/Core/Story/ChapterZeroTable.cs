/// <summary>
/// 0장(튜토리얼) 한 판의 흐름 — 본문 순서대로 (결정 2-79).
///
///   새 게임   난이도 → 프롤로그 → 【바로 0-1】 (소굴을 거치지 않는다)
///   0-1       길가 봇짐 — 열면 환목궁 장착 · 소환단 · 식혜 · 미숫가루 (한 번만). 구슬 없이 활로 싸운다
///   소굴 도착 0-1에서 철수하거나 쓰러져 처음 소굴에 오면 「고목 아래」 이야기 (영감 안내 · 창고 · 쓰러짐)
///   0-2       처음 쓰러뜨린 큰 요괴가 불을 싣는 구슬(화염 핵심 구슬)을 반드시 떨어뜨린다 (한 번만)
///   안전판    핵심 구슬이 하나도 없을 때 주는 구슬은 그 선물을 받은 뒤(또는 1장부터)만 준다
///
/// 본문(Story Lock)은 바꾸지 않는다 — 어느 글을 어디서 보여 주는지만 정한다.
/// 봇짐의 수량 · 큰 요괴의 종류는 [임시값].
/// </summary>
public static class ChapterZeroTable
{
    public const string StartZone = "0-1";
    public const string GiftZone = "0-2";

    /// <summary>「고목 아래 (거점)」 — 본문에서 0-1 글 뒤에 이어지는 소굴 도착 장면.</summary>
    public const string ArrivalEvent = "arrive_den";
    public const string ArrivalHeader = "고목 아래 (거점)";
    public const string ArrivalTitle = "고목 아래";

    // ── 0-1 길가 봇짐 ──────────────────────────────────────────────────
    public const string BundleEvent = "bundle_0-1";
    public const string BundleName = "젖은 봇짐";
    public const string BowId = "wpn_t1_pipe";

    /// <summary>봇짐 안의 것 — 화살 한 줌 · 소환단 · 식혜 · 미숫가루. 화살은 들어서면 화살통으로 채워진다 (결정 2-80).</summary>
    public static readonly (string Id, int Count)[] BundleItems =
    {
        (AmmoTable.Arrow, 60),
        ("con_medkit_small", 3),
        ("con_soda", 2),
        ("con_ration", 2),
    };

    /// <summary>봇짐이 놓이는 거리 (m) — 들어서자마자 보이는 길가.</summary>
    public const float BundleDistance = 4f;

    // ── 0-2 첫 구슬 ────────────────────────────────────────────────────
    public const string GiftEvent = "gift_core_fire";
    public const string GiftSkill = "core_fire";
    public const EnemyArchetype GiftCarrier = EnemyArchetype.Crusher;
    public const EnemyRarity GiftRarity = EnemyRarity.Magic;

    /// <summary>들어온 뒤 큰 요괴가 나오기까지 (초).</summary>
    public const float GiftDelay = 10f;

    public const float GiftDistance = 14f;

    /// <summary>새 게임 — 프롤로그 뒤 소굴을 거치지 않고 0-1로 간다.</summary>
    public static bool ShouldStartInZeroOne(StoryProgress p)
        => !p.HasSeen(StoryTable.EnterEvent(StartZone));

    /// <summary>
    /// 소굴에 처음 왔다 — 0-1을 지났고 아직 「고목 아래」를 보지 않았다.
    /// 0-2를 이미 본 세이브(이 규칙 전 — 0-1 글에 고목 아래가 붙어 있었다)는 다시 보이지 않는다.
    /// </summary>
    public static bool ShouldPlayArrival(StoryProgress p)
        => p.HasSeen(StoryTable.EnterEvent(StartZone))
           && !p.HasSeen(ArrivalEvent)
           && !p.HasSeen(StoryTable.EnterEvent(GiftZone));

    public static bool ShouldPlaceBundle(StoryProgress p, string zone)
        => zone == StartZone && !p.HasSeen(BundleEvent);

    public static bool ShouldSendGiftCarrier(StoryProgress p, string zone)
        => zone == GiftZone && !p.HasSeen(GiftEvent);

    /// <summary>
    /// 튜토리얼이 끝났는가 (결정 2-84) — 지금은 「1장 첫 구역(1-1)에 들어섰다」.
    /// 튜토리얼 가이드가 생기면 그 끝으로 바꾼다. 끝난 뒤부터 주운 장비가 빈 자리에 바로 들어간다.
    /// 0장(0-1 봇짐의 환목궁)은 가이드를 따라 손으로 장착한다.
    /// </summary>
    public static bool TutorialDone(StoryProgress p)
        => p.HasSeen(StoryTable.EnterEvent("1-1"));

    /// <summary>핵심 구슬이 하나도 없을 때의 안전판을 켤까 — 0장에서는 선물을 받은 뒤부터.</summary>
    public static bool FirstCoreSafety(StoryProgress p, int chapter)
        => chapter >= 1 || p.HasSeen(GiftEvent);
}
