using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지도 · 미니맵에 더 그리는 점들 (결정 2-93) — 쓰러진 자리(◎) · 「옛 지도」의 전리품(봇짐 ■ · 열매 나무 ● · 내가 쓰러뜨린 적의 시체 ×).
/// 미니맵 · 전체 지도가 같은 목록을 읽는다. 조각 · 방 · 적 · 보스는 여전히 그리지 않는다 (Map_System 4절).
/// </summary>
public static class MapLootMarks
{
    public enum Kind { Fallen, Bundle, Berry, Corpse }

    public struct Mark
    {
        public Kind kind;
        public Vector3 position;
    }

    public static readonly Color FallenColor = new(1f, 0.42f, 0.32f, 1f);
    public static readonly Color BundleColor = new(0.86f, 0.72f, 0.45f, 1f);
    public static readonly Color BerryColor = new(0.85f, 0.35f, 0.75f, 1f);
    public static readonly Color CorpseColor = new(0.85f, 0.88f, 0.92f, 1f);

    public static string Glyph(Kind kind) => kind switch
    {
        Kind.Fallen => "◎",
        Kind.Bundle => "■",
        Kind.Berry => "●",
        _ => "×",
    };

    public static Color ColorOf(Kind kind) => kind switch
    {
        Kind.Fallen => FallenColor,
        Kind.Bundle => BundleColor,
        Kind.Berry => BerryColor,
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
