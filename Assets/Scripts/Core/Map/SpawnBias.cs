using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【덕코프처럼 몰아서 놓기】 (결정 2-150 · 2026-11-19 덕코프 지도 자료 ~/Dokkaebi-art/ref_duckov)
/// 덕코프 맵의 스폰 좌표를 지도 위에 찍어 보면 — 몹은 길을 따라 늘어서고 야영지 · 마을에 몰리며 숲 한가운데엔 거의 없다.
/// 상자는 건물 · 야영지에 빽빽하고 길가에 몇 개씩. 그래서 자리를 뽑을 때 「마을 둘레 · 길가 · 아무 데나」를 섞는다.
/// 마을 = 그 구역의 Landmark 자리, 길 = PixelDress.Paths. 둘 다 없으면 구역 안 아무 데나(예전과 같다).
/// </summary>
public static class SpawnBias
{
    /// <summary>몹 — 마을 둘레 45% · 길가 40% · 아무 데나 15%.</summary>
    public static readonly (float village, float road) Mobs = (0.45f, 0.40f);
    /// <summary>상자 — 마을 둘레 60% · 길가 25% · 아무 데나 15%.</summary>
    public static readonly (float village, float road) Boxes = (0.60f, 0.25f);

    public static Vector3 Sample(ChapterMap map, string zone, Rect area, System.Random rng, (float village, float road) mix)
    {
        float roll = (float)rng.NextDouble();
        List<MapAnchor> villages = map.Chapter == 0 ? map.AnchorsOf(MapAnchorKind.Landmark, zone) : null;
        if (villages != null && villages.Count > 0 && roll < mix.village)
        {
            MapAnchor v = villages[rng.Next(villages.Count)];
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 3f + (float)rng.NextDouble() * 20f;
            return v.Position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        Vector2[][] paths = PixelDress.Paths(map.Chapter);
        if (paths.Length > 0 && roll < mix.village + mix.road)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                Vector2[] line = paths[rng.Next(paths.Length)];
                int i = 1 + rng.Next(line.Length - 1);
                Vector2 q = Vector2.Lerp(line[i - 1], line[i], (float)rng.NextDouble());
                Vector2 dir = (line[i] - line[i - 1]).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x) * ((rng.Next(2) == 0 ? -1f : 1f) * (3f + (float)rng.NextDouble() * 4f));
                Vector2 p = q + side;
                if (area.Contains(p))
                    return new Vector3(p.x, 0f, p.y);
            }
        }

        return new Vector3(Mathf.Lerp(area.xMin + 3f, area.xMax - 3f, (float)rng.NextDouble()), 0f,
                           Mathf.Lerp(area.yMin + 3f, area.yMax - 3f, (float)rng.NextDouble()));
    }
}
