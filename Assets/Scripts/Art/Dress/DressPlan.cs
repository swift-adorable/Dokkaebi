using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【장 맵 배치 틀】 (결정 2-147 · 2026-11-18 사용자 「참고전용_도깨비의세계_맵 이미지를 참고해서 타일과 소품 배치 틀을 전부 다시」)
/// 「도깨비의 세계」 159장을 한 장씩 본 결과의 공통 배치를 규칙으로 옮겼다 — 바닥 굽기(PixelGroundBaker)와 소품(PixelDress)이 같은 틀을 읽는다.
///   ① 【가운데는 비운다】 싸우는 곳 · 모이는 곳(출발 · 길목 · 보스 · 조각 · 갈림길)은 둥근 흙 공터. 안에는 아무것도 두지 않는다
///   ② 【가장자리를 채운다】 공터 테두리 띠(Rim)에 소품을 2 ~ 6개 무리로. 잔소품(풀 · 꽃 · 버섯)은 가장자리에만
///   ③ 【길】 넓은 흙길, 공터로 드는 마지막 구간엔 디딤돌. 길가에는 띄엄띄엄 작은 무리만
///   ④ 【물가】 둥근 돌로 테를 두르고 갈대는 무리로
///   ⑤ 【나머지 들판】 나무는 2 ~ 3그루 덩이로 띄엄띄엄 — 고르게 흩뿌리지 않는다. 장 가장자리는 짙은 숲으로 감싼다
/// 같은 장은 늘 같은 모습(씨앗 고정). 순수 계산 — 테스트 대상.
/// </summary>
public static class DressPlan
{
    public enum Spot { Clearing, Rim, Path, PathEdge, Water, WaterRim, Wild }

    public readonly struct Clearing
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly bool Big;

        public Clearing(Vector2 center, float radius, bool big = false)
        {
            Center = center;
            Radius = radius;
            Big = big;
        }
    }

    /// <summary>공터 테두리 띠 폭 (m) — 소품 무리가 서는 곳.</summary>
    public const float RimWidth = 3.5f;
    /// <summary>길 반폭 (m) — 예전 1.5에서 넓혔다(참고: 넓은 흙길).</summary>
    public const float PathHalfWidth = 1.9f;
    /// <summary>길가 띠 (m) — 길에서 이만큼까지는 큰 것을 두지 않는다.</summary>
    public const float PathEdge = 4.5f;
    /// <summary>디딤돌을 까는 구간 — 공터 가장자리에서 이만큼 바깥까지 (m).</summary>
    public const float StoneApproach = 8f;
    public const float StoneStep = 2.3f;
    public const float StoneRadius = 0.62f;   // 1차 시험: 작은 점처럼 흩어져 지저분했다 — 크고 드물게

    private static readonly Dictionary<int, List<Clearing>> cache = new();

    /// <summary>그 장의 공터 — 자리(앵커) · 길 갈림에서 만든다.</summary>
    public static IReadOnlyList<Clearing> Clearings(ChapterMap map)
    {
        if (cache.TryGetValue(map.Chapter, out List<Clearing> found))
            return found;

        var list = new List<Clearing>();
        foreach (MapAnchor a in map.Anchors)
        {
            var c = new Vector2(a.Position.x, a.Position.z);
            switch (a.Kind)
            {
                case MapAnchorKind.Start: list.Add(new Clearing(c, 6f)); break;
                case MapAnchorKind.Extraction: list.Add(new Clearing(c, 5.5f)); break;
                case MapAnchorKind.Boss:
                case MapAnchorKind.Gift: list.Add(new Clearing(c, 9.5f, big: true)); break;
                case MapAnchorKind.Pickup:
                case MapAnchorKind.Piece: list.Add(new Clearing(c, 4.5f)); break;
                case MapAnchorKind.Bundle:
                case MapAnchorKind.SecretExit: list.Add(new Clearing(c, 3.5f)); break;
            }
        }

        // 길 갈림 — 세 갈래 이상이 만나는 점.
        foreach (Vector2 j in Junctions(PixelDress.Paths(map.Chapter)))
            list.Add(new Clearing(j, 4f));   // 갈림 공터 — 길이 많아져(결정 2-150) 작게

        // 막힌 덩어리 안에 박힌 공터는 버린다(보스 자리 등이 대숲 안이면 안 된다 — 표가 정한 자리라 대개 비어 있다).
        list.RemoveAll(c => map.IsBlocked(new Vector3(c.Center.x, 0f, c.Center.y), 0.5f));
        cache[map.Chapter] = list;
        return list;
    }

    public static void ClearCache() => cache.Clear();

    /// <summary>갈림 — 길 끝점이 다른 길(끝점이든 중간이든)에 1.5m 안으로 닿는 곳. 가까운 것끼리는 하나로.</summary>
    public static List<Vector2> Junctions(Vector2[][] paths)
    {
        var list = new List<Vector2>();
        for (int i = 0; i < paths.Length; i++)
        {
            Vector2[] line = paths[i];
            if (line.Length < 2) continue;
            foreach (Vector2 end in new[] { line[0], line[line.Length - 1] })
            {
                bool touches = false;
                for (int j = 0; j < paths.Length && !touches; j++)
                    if (j != i && SegmentDistance(paths[j], end) < 1.5f)
                        touches = true;
                if (!touches) continue;
                bool dup = false;
                foreach (Vector2 q in list)
                    dup |= (q - end).sqrMagnitude < 9f;
                if (!dup) list.Add(end);
            }
        }
        return list;
    }

    private static float SegmentDistance(Vector2[] line, Vector2 q)
    {
        float best = float.MaxValue;
        for (int i = 1; i < line.Length; i++)
        {
            Vector2 a = line[i - 1], ab = line[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
        }
        return best;
    }

    /// <summary>가장 가까운 공터 가장자리까지의 거리 (안이면 음수) · 그 공터.</summary>
    public static float ClearingDistance(ChapterMap map, Vector2 p, out Clearing nearest)
    {
        float best = float.MaxValue;
        nearest = default;
        foreach (Clearing c in Clearings(map))
        {
            // 둘레를 울퉁불퉁하게 (1차 시험: 완전한 원이라 인공적이었다) — 방향마다 반지름이 ±28% 흔들린다.
            Vector2 v = p - c.Center;
            float len = v.magnitude;
            float a = Mathf.Atan2(v.y, v.x);
            float wobble = Mathf.PerlinNoise(Mathf.Cos(a) * 1.4f + c.Center.x * 0.13f + 50f, Mathf.Sin(a) * 1.4f + c.Center.y * 0.13f + 50f);
            float d = len - c.Radius * (0.72f + 0.56f * wobble);
            if (d < best)
            {
                best = d;
                nearest = c;
            }
        }
        return best;
    }

    /// <summary>디딤돌 자리 — 공터로 드는 길 끝 구간에 StoneStep 간격으로(살짝 흔들어).</summary>
    public static List<Vector2> SteppingStones(ChapterMap map)
    {
        var stones = new List<Vector2>();
        var rnd = new System.Random(7300 + map.Chapter);
        foreach (Vector2[] line in PixelDress.Paths(map.Chapter))
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                float len = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / Mathf.Max(0.001f, len);
                Vector2 side = new(-dir.y, dir.x);
                for (float t = 0f; t < len; t += StoneStep)
                {
                    Vector2 q = a + dir * t;
                    float cd = ClearingDistance(map, q, out _);
                    if (cd < 0.5f || cd > StoneApproach)
                        continue;
                    stones.Add(q + side * ((float)rnd.NextDouble() - 0.5f) * 0.7f);
                }
            }
        return stones;
    }

    /// <summary>물 덩어리 가장자리까지의 거리 (안이면 0) · 안인지.</summary>
    public static float WaterDistance(ChapterMap map, Vector2 p, out bool inside)
    {
        inside = false;
        float best = float.MaxValue;
        foreach (MapBlock b in map.Blocks)
        {
            if (b.Kind != MapBlockKind.Water) continue;
            if (b.Contains(new Vector3(p.x, 0f, p.y))) inside = true;
            float dx = Mathf.Max(0f, Mathf.Abs(p.x - b.Center.x) - b.Size.x * 0.5f);
            float dz = Mathf.Max(0f, Mathf.Abs(p.y - b.Center.y) - b.Size.z * 0.5f);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    /// <summary>이 점이 어떤 자리인가 — 소품 배치가 쓴다(바닥은 노이즈를 섞어 따로 판정).</summary>
    public static Spot Classify(ChapterMap map, Vector2 p)
    {
        float wet = WaterDistance(map, p, out bool inWater);
        if (inWater) return Spot.Water;
        if (wet < 1.6f) return Spot.WaterRim;
        float cd = ClearingDistance(map, p, out _);
        if (cd < 0f) return Spot.Clearing;
        float road = PixelDress.PathDistance(map.Chapter, p);
        if (road < PathHalfWidth) return Spot.Path;
        if (cd < RimWidth) return Spot.Rim;
        if (road < PathEdge) return Spot.PathEdge;
        return Spot.Wild;
    }
}
