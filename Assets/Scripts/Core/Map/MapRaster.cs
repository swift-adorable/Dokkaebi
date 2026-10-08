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

    public static int WidthOf(ChapterMap map, int ppm = MapTable.PixelsPerMeter) => Mathf.Max(1, Mathf.RoundToInt(map.HalfSize.x * 2f * ppm));
    public static int HeightOf(ChapterMap map, int ppm = MapTable.PixelsPerMeter) => Mathf.Max(1, Mathf.RoundToInt(map.HalfSize.y * 2f * ppm));

    /// <summary>그린다. isOpen이 null이면 다 열린 것으로 본다.</summary>
    public static Color32[] Render(ChapterMap map, Func<string, bool> isOpen, int pixelsPerMeter = MapTable.PixelsPerMeter,
                                   bool parchment = false)
    {
        Color32 ground = parchment ? ParchmentGround : Ground;
        Color32 outside = parchment ? ParchmentOutside : OutOfZone;
        Func<MapBlockKind, Color32> colorOf = parchment ? ParchmentColorOf : ColorOf;

        int w = WidthOf(map, pixelsPerMeter), h = HeightOf(map, pixelsPerMeter);
        var pixels = new Color32[w * h];
        float ppm = pixelsPerMeter;

        // 바닥 — 구역 땅이면 바닥, 아니면 끊긴 밤길(어둡게). 닫힌 구역은 어둡게.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector3 p = WorldOf(map, x, y, pixelsPerMeter);
            string zone = map.ZoneAt(p);
            pixels[y * w + x] = zone == null ? outside : Shade(ground, zone, isOpen);
        }

        // 덩어리 — 낮은 것(꽃밭 · 냇물)을 먼저, 키 큰 것을 나중에.
        foreach (bool solidPass in new[] { false, true })
            foreach (MapBlock b in map.Blocks)
            {
                if (b.Solid != solidPass)
                    continue;

                Fill(map, pixels, w, h, b.Center, b.Size.x, b.Size.z, colorOf(b.Kind), isOpen, ppm);
            }

        // 아직 닫힌 금줄 — 밝은 줄.
        foreach (MapGate g in map.Gates)
            if (isOpen != null && !isOpen(g.ZoneId))
                Fill(map, pixels, w, h, g.Block.Center, Mathf.Max(g.Block.Size.x, 1.5f), Mathf.Max(g.Block.Size.z, 1.5f),
                    GateColor, null, ppm);

        return pixels;
    }

    /// <summary>화소 가운데의 월드 자리.</summary>
    public static Vector3 WorldOf(ChapterMap map, int x, int y, int ppm = MapTable.PixelsPerMeter)
        => new(-map.HalfSize.x + (x + 0.5f) / ppm, 0f, -map.HalfSize.y + (y + 0.5f) / ppm);

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
            string zone = isOpen != null ? map.ZoneAt(WorldOf(map, x, y, Mathf.RoundToInt(ppm))) : null;
            pixels[y * w + x] = zone == null ? color : Shade(color, zone, isOpen);
        }
    }

    private static Color32 Shade(Color32 c, string zone, Func<string, bool> isOpen)
    {
        if (isOpen == null || isOpen(zone))
            return c;

        return new Color32((byte)(c.r * ClosedDim), (byte)(c.g * ClosedDim), (byte)(c.b * ClosedDim), 255);
    }

    // ── 양피지 (전체 지도 · 2026-10-08 사용자 참고 그림) ─────────────────────

    /// <summary>양피지 바닥(길 · 마당 — 밝다) · 장 밖(짙다) [임시 — 아트 때 그림으로 바꾼다].</summary>
    public static readonly Color32 ParchmentGround = new(228, 206, 160, 255);
    public static readonly Color32 ParchmentOutside = new(62, 50, 37, 255);

    /// <summary>양피지 빛 — 참고 그림처럼 길이 밝고 덩어리(건물 · 대숲 · 바위)가 짙다.</summary>
    public static Color32 ParchmentColorOf(MapBlockKind kind) => kind switch
    {
        MapBlockKind.Boundary => new Color32(46, 37, 27, 255),
        MapBlockKind.Bamboo => new Color32(104, 104, 70, 255),
        MapBlockKind.Rock => new Color32(128, 112, 88, 255),
        MapBlockKind.Building => new Color32(98, 76, 54, 255),
        MapBlockKind.Bush => new Color32(146, 136, 94, 255),
        MapBlockKind.Tree => new Color32(92, 72, 50, 255),
        MapBlockKind.Water => new Color32(112, 122, 116, 255),
        MapBlockKind.Fence => new Color32(110, 86, 60, 255),
        MapBlockKind.Stall => new Color32(162, 132, 94, 255),
        MapBlockKind.Well => new Color32(140, 130, 112, 255),
        MapBlockKind.Post => new Color32(120, 96, 66, 255),
        MapBlockKind.Flowerbed => new Color32(176, 132, 118, 255),
        MapBlockKind.Geumjul => new Color32(226, 200, 132, 255),
        _ => new Color32(140, 120, 92, 255),
    };

    /// <summary>
    /// 양피지 빛으로 그린 지도(Render parchment)를 손본다 — 덩어리 가장자리에 먹선을 긋고, 종이 결을 살짝 섞는다.
    /// 같은 그림이면 늘 같은 결과(결이 고정 씨앗).
    /// </summary>
    public static Color32[] Parchment(Color32[] source, int w, int h)
    {
        var result = new Color32[source.Length];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            Color32 c = source[i];

            float r = c.r, g = c.g, b = c.b;

            // 먹선 — 이웃과 빛이 다르면 가장자리다.
            if (IsEdge(source, w, h, x, y))
            {
                r *= 0.62f;
                g *= 0.58f;
                b *= 0.55f;
            }

            // 종이 결.
            float grain = (Hash(x, y) - 0.5f) * 14f;

            result[i] = new Color32(
                (byte)Mathf.Clamp(r + grain, 0f, 255f),
                (byte)Mathf.Clamp(g + grain, 0f, 255f),
                (byte)Mathf.Clamp(b + grain * 0.8f, 0f, 255f),
                255);
        }

        return result;
    }

    private static bool IsEdge(Color32[] px, int w, int h, int x, int y)
    {
        Color32 c = px[y * w + x];

        return Differs(c, px, w, h, x + 1, y) || Differs(c, px, w, h, x - 1, y)
            || Differs(c, px, w, h, x, y + 1) || Differs(c, px, w, h, x, y - 1);
    }

    private static bool Differs(Color32 c, Color32[] px, int w, int h, int x, int y)
    {
        if (x < 0 || y < 0 || x >= w || y >= h)
            return false;

        Color32 o = px[y * w + x];
        int d = Mathf.Abs(c.r - o.r) + Mathf.Abs(c.g - o.g) + Mathf.Abs(c.b - o.b);

        // 더 짙은 쪽(덩어리 안쪽)에만 선을 긋는다 — 선이 두 겹이 되지 않고 길은 깨끗하게 남는다.
        return d > 24 && (c.r + c.g + c.b) < (o.r + o.g + o.b);
    }

    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint n = (uint)(x * 374761393 + y * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
