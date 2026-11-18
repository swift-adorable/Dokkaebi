using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지도 · 미니맵에 더 그리는 점들 (결정 2-93) — 쓰러진 자리(◎) · 「옛 지도」의 전리품(봇짐 ■ · 열매 나무 ● · 내가 쓰러뜨린 적의 시체 ×).
/// 미니맵 · 전체 지도가 같은 목록을 읽는다. 조각 · 적은 여전히 그리지 않는다 (Map_System 4절).
/// 퀘스트 자리(!)는 결정 2-142에서 더했다. 보스는 점이 아니라 「소문 영역」으로만(BossRumor).
/// </summary>
public static class MapLootMarks
{
    public enum Kind { Fallen, Bundle, Berry, Corpse, Quest, Lock, LockOpen }

    public struct Mark
    {
        public Kind kind;
        public Vector3 position;
    }

    public static readonly Color FallenColor = new(1f, 0.42f, 0.32f, 1f);
    public static readonly Color BundleColor = new(0.86f, 0.72f, 0.45f, 1f);
    public static readonly Color BerryColor = new(0.85f, 0.35f, 0.75f, 1f);
    public static readonly Color CorpseColor = new(0.85f, 0.88f, 0.92f, 1f);

    /// <summary>퀘스트 자리 — 전체 지도의 퀘스트 구역 색과 같다.</summary>
    public static readonly Color QuestColor = new(0.42f, 0.86f, 0.86f, 1f);

    /// <summary>잠긴 곳 — 잠김(금빛) · 열림(초록) (결정 2-143).</summary>
    public static readonly Color LockColor = new(1f, 0.80f, 0.32f, 1f);
    public static readonly Color LockOpenColor = new(0.50f, 0.88f, 0.50f, 1f);

    public static string Glyph(Kind kind) => kind switch
    {
        Kind.Fallen => "◎",
        Kind.Bundle => "■",
        Kind.Berry => "●",
        Kind.Quest => "!",
        Kind.Lock => "□",
        Kind.LockOpen => "□",
        _ => "×",
    };

    public static Color ColorOf(Kind kind) => kind switch
    {
        Kind.Fallen => FallenColor,
        Kind.Bundle => BundleColor,
        Kind.Berry => BerryColor,
        Kind.Quest => QuestColor,
        Kind.Lock => LockColor,
        Kind.LockOpen => LockOpenColor,
        _ => CorpseColor,
    };

    /// <summary>전체 지도에 붙이는 이름 — 쓰러진 자리만.</summary>
    public static string Label(Kind kind) => kind == Kind.Fallen ? $"{Glyph(kind)} {FallenStash.Name}" : Glyph(kind);

    private static readonly List<Mark> buffer = new();

    /// <summary>지금 그릴 점들. 쓰러진 자리는 늘, 전리품은 「옛 지도」를 배웠을 때만.</summary>
    public static List<Mark> Collect()
    {
        buffer.Clear();

        if (FallenStash.Current != null)
            buffer.Add(new Mark { kind = Kind.Fallen, position = FallenStash.Current.transform.position });

        // 【퀘스트 자리】 (결정 2-142 · 덕코프 모드 「퀘스트 위치 지도 표시」 147만) — 자리가 정해진 퀘스트(줍기 · 말 걸기 · 지키기)만.
        // 처치 퀘스트는 자리가 없으니 찍히지 않는다. 지금 나타난 자리(QuestSpotDirector가 세운 것)만이라 앞 단계를 해야 다음이 보인다.
        foreach (QuestSpot q in QuestSpot.Active)
            if (q != null)
                buffer.Add(new Mark { kind = Kind.Quest, position = q.transform.position });

        // 잠긴 곳 (결정 2-143) — 한 번 가까이 가 본 곳만. 덕코프 「Better Key Indicator」처럼 열 수 있는지가 색으로 보인다.
        foreach (LockedSite s in LockedSite.Active)
            if (s != null && s.Seen)
                buffer.Add(new Mark
                {
                    kind = s.IsOpen || s.Breached || KeyRing.IsRegistered(s.Site.KeyId) ? Kind.LockOpen : Kind.Lock,
                    position = s.transform.position,
                });

        if (!MapPassives.MapLoot)
            return buffer;

        foreach (StoryBundle b in StoryBundle.Active)
            if (b != null && b.Contents != null && !b.Contents.IsEmpty)
                buffer.Add(new Mark { kind = Kind.Bundle, position = b.transform.position });

        foreach (BerryTree t in BerryTree.Active)
            if (t != null && !t.Harvested)
                buffer.Add(new Mark { kind = Kind.Berry, position = t.transform.position });

        foreach (CorpseController c in CorpseController.Active)
            if (c != null && c.KilledByPlayer && c.HasLoot)
                buffer.Add(new Mark { kind = Kind.Corpse, position = c.transform.position });

        return buffer;
    }
}
