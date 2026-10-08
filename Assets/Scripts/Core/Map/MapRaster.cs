using System;
using UnityEngine;

/// <summary>
/// 【지도 그림】 (결정 2-90) — 장 맵 표(ZoneMapTable)를 화소로 그린다. 전체 지도 · 미니맵이 같은 그림을 쓴다.
/// 화소 (0, 0)이 월드 (-반폭, -반높이) — 왼쪽 아래. 1m에 MapTable.PixelsPerMeter 화소.
///
///   · 바닥 · 덩어리(키 큰 것은 짙게 · 낮은 것은 옅게) · 냇물 · 꽃밭 · 장 끝 금줄
///   · 닫힌 구역은 어둡게 · 아직 닫힌 금줄(MapGate)은 밝은 줄
///   · 적 · 보스 · 조각 · 방은 그리지 않는다 (Map_System 4절 — 지도가 적 위치를 알려 주지 않는다)
/// </summary>
public static class MapRaster
{
    public static readonly Color32 Ground = new(52, 56, 46, 255);
    public static readonly Color32 OutOfZone = new(22, 22, 26, 255);
    public static readonly Color32 GateColor = new(240, 200, 90, 255);

    /// <summary>닫힌 구역에 곱하는 밝기.</summary>
    public const float ClosedDim = 0.4f;

    public static Color32 ColorOf(MapBlockKind kind) => kind switch
    {
        MapBlockKind.Boundary => new Color32(18, 18, 20, 255),
        MapBlockKind.Bamboo => new Color32(46, 98, 54, 255),
        MapBlockKind.Rock => new Color32(110, 106, 98, 255),
        MapBlockKind.Building => new Color32(112, 80, 54, 255),
        MapBlockKind.Bush => new Color32(78, 104, 52, 255),
        MapBlockKind.Tree => new Color32(86, 60, 36, 255),
        MapBlockKind.Water => new Color32(62, 108, 156, 255),
        MapBlockKind.Fence => new Color32(96, 70, 44, 255),
        MapBlockKind.Stall => new Color32(150, 118, 76, 255),
        MapBlockKind.Well => new Color32(140, 140, 156, 255),
        MapBlockKind.Post => new Color32(184, 158, 112, 255),
        MapBlockKind.Flowerbed => new Color32(176, 96, 132, 255),
        MapBlockKind.Geumjul => new Color32(236, 220, 130, 255),
        _ => new Color32(128, 128, 128, 255),
    };

    public static int WidthOf(ChapterMap map) => Mathf.Max(1, Mathf.RoundToInt(map.HalfSize.x * 2f * MapTable.PixelsPerMeter));
    public static int HeightOf(ChapterMap map) => Mathf.Max(1, Mathf.RoundToInt(map.HalfSize.y * 2f * MapTable.PixelsPerMeter));

    /// <summary>그린다. isOpen이 null이면 다 열린 것으로 본다.</summary>
    public static Color32[] Render(ChapterMap map, Func<string, bool> isOpen)
    {
        int w = WidthOf(map), h = HeightOf(map);
        var pixels = new Color32[w * h];
        float ppm = MapTable.PixelsPerMeter;

        // 바닥 — 구역 땅이면 바닥, 아니면 끊긴 밤길(어둡게). 닫힌 구역은 어둡게.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector3 p = WorldOf(map, x, y);
            string zone = map.ZoneAt(p);
            pixels[y * w + x] = zone == null ? OutOfZone : Shade(Ground, zone, isOpen);
        }

        // 덩어리 — 낮은 것(꽃밭 · 냇물)을 먼저, 키 큰 것을 나중에.
        foreach (bool solidPass in new[] { false, true })
            foreach (MapBlock b in map.Blocks)
            {
                if (b.Solid != solidPass)
                    continue;

                Fill(map, pixels, w, h, b.Center, b.Size.x, b.Size.z, ColorOf(b.Kind), isOpen, ppm);
            }

        // 아직 닫힌 금줄 — 밝은 줄.
        foreach (MapGate g in map.Gates)
            if (isOpen != null && !isOpen(g.ZoneId))
                Fill(map, pixels, w, h, g.Block.Center, Mathf.Max(g.Block.Size.x, 1.5f), Mathf.Max(g.Block.Size.z, 1.5f),
                    GateColor, null, ppm);

        return pixels;
    }

    /// <summary>화소 가운데의 월드 자리.</summary>
    public static Vector3 WorldOf(ChapterMap map, int x, int y)
        => new(-map.HalfSize.x + (x + 0.5f) / MapTable.PixelsPerMeter, 0f, -map.HalfSize.y + (y + 0.5f) / MapTable.PixelsPerMeter);

    /// <summary>월드 자리 → 그림의 0 ~ 1 좌표 (u, v).</summary>
    public static Vector2 UvOf(ChapterMap map, Vector3 world)
        => new((world.x + map.HalfSize.x) / (map.HalfSize.x * 2f), (world.z + map.HalfSize.y) / (map.HalfSize.y * 2f));

    /// <summary>그림의 0 ~ 1 좌표 → 월드 자리.</summary>
    public static Vector3 WorldOfUv(ChapterMap map, Vector2 uv)
        => new(uv.x * map.HalfSize.x * 2f - map.HalfSize.x, 0f, uv.y * map.HalfSize.y * 2f - map.HalfSize.y);

    private static void Fill(ChapterMap map, Color32[] pixels, int w, int h, Vector2 center, float sx, float sz,
        Color32 color, Func<string, bool> isOpen, float ppm)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - sx * 0.5f + map.HalfSize.x) * ppm));
        int x1 = Mathf.Min(w - 1, Mathf.CeilToInt((center.x + sx * 0.5f + map.HalfSize.x) * ppm) - 1);
        int y0 = Mathf.Max(0, Mathf.FloorToInt((center.y - sz * 0.5f + map.HalfSize.y) * ppm));
        int y1 = Mathf.Min(h - 1, Mathf.CeilToInt((center.y + sz * 0.5f + map.HalfSize.y) * ppm) - 1);

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            string zone = isOpen != null ? map.ZoneAt(WorldOf(map, x, y)) : null;
            pixels[y * w + x] = zone == null ? color : Shade(color, zone, isOpen);
        }
    }

    private static Color32 Shade(Color32 c, string zone, Func<string, bool> isOpen)
    {
        if (isOpen == null || isOpen(zone))
            return c;

        return new Color32((byte)(c.r * ClosedDim), (byte)(c.g * ClosedDim), (byte)(c.b * ClosedDim), 255);
    }
}
