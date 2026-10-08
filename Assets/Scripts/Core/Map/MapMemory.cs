using System;
using System.Collections.Generic;
using UnityEngine;

public enum MarkerError
{
    None = 0,
    Full = 1,
    NoMap = 2,
}

/// <summary>
/// 【장마다 기억하는 지도】 (결정 2-90) — 가 본 땅(FogGrid)과 플레이어가 찍은 마커(장마다 MaxMarkers).
/// 세이브에 담긴다(SaveData.maps). 마커는 지도 위에만 보이고 현장에는 없다.
/// 길목 발견은 여기 없다 — 판마다 새로 찾는다(ExtractionDirector).
/// </summary>
public static class MapMemory
{
    private static readonly Dictionary<int, FogGrid> fogs = new();
    private static readonly Dictionary<int, List<Vector2>> markers = new();

    /// <summary>마커가 바뀌었다 (장 번호).</summary>
    public static event Action<int> MarkersChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        fogs.Clear();
        markers.Clear();
        MarkersChanged = null;
    }

    /// <summary>그 장의 안개. 맵이 없는 장이면 null.</summary>
    public static FogGrid Fog(int chapter)
    {
        if (fogs.TryGetValue(chapter, out FogGrid fog))
            return fog;

        ChapterMap map = ZoneMapTable.Of(chapter);
        if (map == null)
            return null;

        fog = new FogGrid(map.HalfSize);
        fogs[chapter] = fog;
        return fog;
    }

    public static IReadOnlyList<Vector2> Markers(int chapter)
        => markers.TryGetValue(chapter, out List<Vector2> list) ? list : Array.Empty<Vector2>();

    /// <summary>마커를 찍는다 (x, z).</summary>
    public static MarkerError AddMarker(int chapter, Vector2 at)
    {
        if (ZoneMapTable.Of(chapter) == null)
            return MarkerError.NoMap;

        if (!markers.TryGetValue(chapter, out List<Vector2> list))
            markers[chapter] = list = new List<Vector2>();

        if (list.Count >= MapTable.MaxMarkers)
            return MarkerError.Full;

        list.Add(at);
        MarkersChanged?.Invoke(chapter);
        return MarkerError.None;
    }

    /// <summary>가장 가까운 마커를 지운다 — radius 안에 있을 때만. 지웠으면 true.</summary>
    public static bool RemoveMarkerNear(int chapter, Vector2 at, float radius)
    {
        if (!markers.TryGetValue(chapter, out List<Vector2> list))
            return false;

        int best = -1;
        float bestSq = radius * radius;

        for (int i = 0; i < list.Count; i++)
        {
            float sq = (list[i] - at).sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }

        if (best < 0)
            return false;

        list.RemoveAt(best);
        MarkersChanged?.Invoke(chapter);
        return true;
    }

    public static string Explain(MarkerError error) => error switch
    {
        MarkerError.Full => $"표시는 장마다 {MapTable.MaxMarkers}개까지다 — 하나를 지우고 찍는다.",
        MarkerError.NoMap => "이곳에는 지도가 없다.",
        _ => string.Empty,
    };

    // ── 세이브 ──────────────────────────────────────────────────────

    public static List<SavedMap> Capture()
    {
        var list = new List<SavedMap>();
        var chapters = new SortedSet<int>(fogs.Keys);
        chapters.UnionWith(markers.Keys);

        foreach (int chapter in chapters)
        {
            var saved = new SavedMap { chapter = chapter };

            if (fogs.TryGetValue(chapter, out FogGrid fog) && fog.RevealedCount > 0)
                saved.fog = fog.Export();

            if (markers.TryGetValue(chapter, out List<Vector2> points))
                foreach (Vector2 p in points)
                    saved.markers.Add(new SavedMarker { x = p.x, z = p.y });

            if (!string.IsNullOrEmpty(saved.fog) || saved.markers.Count > 0)
                list.Add(saved);
        }

        return list;
    }

    public static void Restore(List<SavedMap> saved)
    {
        Reset();

        if (saved == null)
            return;

        foreach (SavedMap s in saved)
        {
            FogGrid fog = Fog(s.chapter);
            if (fog == null)
                continue;

            fog.Import(s.fog);

            var list = new List<Vector2>();
            for (int i = 0; s.markers != null && i < s.markers.Count && list.Count < MapTable.MaxMarkers; i++)
                list.Add(new Vector2(s.markers[i].x, s.markers[i].z));

            if (list.Count > 0)
                markers[s.chapter] = list;
        }
    }

    public static void Reset()
    {
        fogs.Clear();
        markers.Clear();
    }
}
