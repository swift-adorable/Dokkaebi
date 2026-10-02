using System.Collections.Generic;

/// <summary>
/// 이야기가 어디까지 왔는가. MonoBehaviour 없는 순수 클래스다. (로드맵 3단계)
///
/// 【담는 것은 다섯뿐이다】 — 쓰러뜨린 보스 · 주운 조각 · 읽은 방 · 본 이야기 · 지난 밤.
/// 열린 장 · 열린 구역 · 사신패 · 와 있는 상인은 전부 이 다섯에서 계산한다.
/// 같은 사실을 두 곳에 담으면 둘이 어긋나는 날이 온다.
///
/// 【규칙】 (docs/Dokkaebi_Story.md 2절 · Progression 3절)
///   · 0장 0-1은 처음부터 열려 있다
///   · 구역은 앞 구역을 끝내면 열린다 — 끝내는 조건은 구역마다 다르다 (ZoneClear)
///   · 1~4장은 앞 장의 마지막 구역을 끝내면 열린다 (사방길은 이야기 순서대로)
///   · 5장은 사신패 넷이 있어야 열린다. 한 번 열면 계속 열려 있다 (결정 2-44)
///   · 6장은 5장을 끝내면 열린다
///   · n장을 끝내면 「n번째 밤」이 온다. 그 밤에 오는 상인이 소굴에 온다
/// </summary>
public class StoryProgress
{
    private readonly HashSet<string> defeated = new();
    private readonly HashSet<string> pieces = new();
    private readonly HashSet<string> notices = new();
    private readonly HashSet<string> seen = new();
    private readonly HashSet<int> nights = new();

    public IEnumerable<string> DefeatedBosses => defeated;
    public IEnumerable<string> Pieces => pieces;
    public IEnumerable<string> Notices => notices;
    public IEnumerable<string> SeenEvents => seen;
    public IEnumerable<int> Nights => nights;

    public bool HasDefeated(string bossId) => bossId != null && defeated.Contains(bossId);
    public bool HasPiece(string pieceId) => pieceId != null && pieces.Contains(pieceId);
    public bool HasNotice(string noticeId) => noticeId != null && notices.Contains(noticeId);
    public bool HasSeen(string eventId) => eventId != null && seen.Contains(eventId);
    public bool HasNight(int night) => nights.Contains(night);

    public int PieceCount => pieces.Count;

    // ── 바꾸기 ───────────────────────────────────────────────────────

    /// <summary>보스를 쓰러뜨렸다. 처음이면 true.</summary>
    public bool Defeat(string bossId) => StoryTable.Boss(bossId) != null && defeated.Add(bossId);

    public bool CollectPiece(string pieceId) => StoryTable.Piece(pieceId) != null && pieces.Add(pieceId);

    public bool CollectNotice(string noticeId) => StoryTable.Notice(noticeId) != null && notices.Add(noticeId);

    public bool See(string eventId) => !string.IsNullOrEmpty(eventId) && seen.Add(eventId);

    /// <summary>밤을 지났다. 그 밤의 상인이 와 있게 된다.</summary>
    public bool PassNight(int night) => night >= 1 && nights.Add(night);

    public void Clear()
    {
        defeated.Clear();
        pieces.Clear();
        notices.Clear();
        seen.Clear();
        nights.Clear();
    }

    // ── 계산 ─────────────────────────────────────────────────────────

    /// <summary>구역을 끝냈는가.</summary>
    public bool IsCleared(string zoneId)
    {
        ZoneDefinition zone = StoryTable.Zone(zoneId);

        if (zone == null)
            return false;

        switch (zone.ClearBy)
        {
            case ZoneClear.Boss:
            {
                List<BossDefinition> list = StoryTable.BossesIn(zoneId);
                return list.Count > 0 && HasDefeated(list[list.Count - 1].Id);
            }

            case ZoneClear.Piece:
            {
                PieceDefinition piece = StoryTable.PieceIn(zoneId);
                return piece != null && HasPiece(piece.Id);
            }

            default:
                return HasSeen(StoryTable.EnterEvent(zoneId));
        }
    }

    /// <summary>장을 끝냈는가 — 마지막 구역을 끝냈다.</summary>
    public bool IsChapterComplete(int chapter)
    {
        ChapterDefinition c = StoryTable.Chapter(chapter);

        return c != null && IsCleared(c.Zones[c.Zones.Length - 1].Id);
    }

    /// <summary>가진 사신패 — 1~4장 보스가 하나씩 내준다.</summary>
    public List<string> Tablets
    {
        get
        {
            var list = new List<string>();

            foreach (BossDefinition b in StoryTable.Bosses)
                if (!string.IsNullOrEmpty(b.Tablet) && HasDefeated(b.Id))
                    list.Add(b.Tablet);

            return list;
        }
    }

    public bool IsChapterOpen(int chapter)
    {
        if (chapter == 0)
            return true;

        if (chapter < 0 || chapter > StoryTable.LastChapter)
            return false;

        if (chapter == 5)
            return Tablets.Count >= StoryTable.TabletsForGate;

        return IsChapterComplete(chapter - 1);
    }

    public bool IsZoneOpen(string zoneId)
    {
        ZoneDefinition zone = StoryTable.Zone(zoneId);

        if (zone == null || !IsChapterOpen(zone.Chapter))
            return false;

        ZoneDefinition[] zones = StoryTable.Chapter(zone.Chapter).Zones;

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Id != zoneId)
                continue;

            return i == 0 || IsCleared(zones[i - 1].Id);
        }

        return false;
    }

    /// <summary>열린 구역 전부, 장 · 구역 순서대로.</summary>
    public List<ZoneDefinition> OpenZones()
    {
        var list = new List<ZoneDefinition>();

        foreach (ChapterDefinition c in StoryTable.Chapters)
            foreach (ZoneDefinition z in c.Zones)
                if (IsZoneOpen(z.Id))
                    list.Add(z);

        return list;
    }

    /// <summary>
    /// 이 구역에 이번에 나올 이야기 보스 — 아직 쓰러뜨리지 않은 것 중 맨 앞.
    /// 다 쓰러뜨렸으면 장 보스는 다시 나오고(덕코프처럼), 중간 보스는 null이다.
    /// </summary>
    public BossDefinition NextBossIn(string zoneId)
    {
        List<BossDefinition> list = StoryTable.BossesIn(zoneId);

        foreach (BossDefinition b in list)
            if (!HasDefeated(b.Id))
                return b;

        foreach (BossDefinition b in list)
            if (b.IsChapterBoss)
                return b;

        return null;
    }

    /// <summary>이 장에서 이미 만난 중간 보스 — 맵 어디서든 무작위로 다시 나온다.</summary>
    public List<BossDefinition> MetMidBosses(int chapter)
    {
        var list = new List<BossDefinition>();

        foreach (BossDefinition b in StoryTable.Bosses)
        {
            if (b.IsChapterBoss || !HasDefeated(b.Id))
                continue;

            ZoneDefinition z = StoryTable.Zone(b.ZoneId);

            if (z != null && z.Chapter == chapter)
                list.Add(b);
        }

        return list;
    }

    /// <summary>아직 지나지 않은 밤 — 끝낸 장 가운데 가장 이른 것. 없으면 0.</summary>
    public int PendingNight()
    {
        for (int n = 1; n <= 5; n++)
            if (IsChapterComplete(n) && !HasNight(n))
                return n;

        return 0;
    }

    /// <summary>소굴에 와 있는가. 영감은 처음부터 있다.</summary>
    public bool HasMerchant(string merchantId)
    {
        MerchantDefinition m = StoryTable.Merchant(merchantId);

        return m != null && (m.Night == 0 || HasNight(m.Night));
    }

    /// <summary>이 건물의 설계도가 열렸는가 — 열어 주는 상인이 와 있다.</summary>
    public bool IsBlueprintOpen(string buildingId)
    {
        MerchantDefinition m = StoryTable.MerchantFor(buildingId);

        return m == null || HasMerchant(m.Id);
    }

    /// <summary>엔딩 — 구미호를 쓰러뜨렸다. 그 뒤로도 계속 다시 들어갈 수 있다.</summary>
    public bool IsEnded => HasDefeated("gumiho");

    // ── 세이브 ───────────────────────────────────────────────────────

    public SavedStory Capture()
    {
        return new SavedStory
        {
            bosses = new List<string>(defeated),
            pieces = new List<string>(pieces),
            notices = new List<string>(notices),
            seen = new List<string>(seen),
            nights = new List<int>(nights)
        };
    }

    /// <summary>되살린다. 【표에 없는 id는 버린다】 — 보스나 조각을 지우거나 이름을 바꾸면 생긴다.</summary>
    public void Restore(SavedStory saved)
    {
        Clear();

        if (saved == null)
            return;

        foreach (string id in saved.bosses ?? new List<string>()) Defeat(id);
        foreach (string id in saved.pieces ?? new List<string>()) CollectPiece(id);
        foreach (string id in saved.notices ?? new List<string>()) CollectNotice(id);
        foreach (string id in saved.seen ?? new List<string>()) See(id);
        foreach (int n in saved.nights ?? new List<int>()) PassNight(n);
    }
}
