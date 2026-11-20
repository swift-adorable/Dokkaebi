using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2.5D 아트 목록】 (결정 2-127) — 장을 꾸밀 때 쓰는 그림 고르기 · 땅 빛 · 길 · 마당 계산. 에디터(꾸미기)와 런타임(ChapterArt)이 같이 쓴다.
///   · 나무: The Higalina Vault 「Trees — 100 Fantasy Trees」 · 돌: Kebap Guy 「Stone Rock Pack」 (사용자가 에셋 스토어에서 받음)
/// </summary>
public static class ArtCatalog
{
    /// <summary>정사영 카메라 반높이(m) — 화면 세로에 약 19m가 보인다.</summary>
    public const float OrthoSize = 9.5f;
    /// <summary>맵 밖으로 바닥 · 숲을 더 까는 폭(m).</summary>
    public const float Margin = 18f;
    /// <summary>합치는 칸(m).</summary>
    public const float Cell = 32f;

    /// <summary>카메라가 내려다보는 각 — 세운 그림도 이만큼 눕힌다. (v8 · 사용자가 고른 시점 C: 50 → 55)</summary>
    public const float Pitch = 55f;
    /// <summary>
    /// 카메라를 옆으로 돌린 각 (v8 · 사용자 「덕코프 건물은 완전 위가 아니라 약간 대각선 위에서 바라보는 모양」 → 시점 C).
    /// 건물 앞면과 옆면이 함께 보인다. 세운 그림도 이만큼 돌려 늘 카메라를 본다.
    /// </summary>
    public const float Yaw = 35f;
    /// <summary>원근 카메라 세로 화각(도) — 정사영 반높이 OrthoSize 와 같은 넓이가 보이게 거리를 잡는다.</summary>
    public const float Fov = 35f;
    public static float CameraDistance => OrthoSize / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);

    /// <summary>
    /// 세운 그림(캐릭터 · 소품 · 상자 · 나무)의 회전 — 플레이 중엔 지금 카메라를 따른다(굽기 전 옛 씬 · 소굴은 제 카메라대로),
    /// 에디터에서 구울 땐 Pitch · Yaw.
    /// </summary>
    public static Quaternion CardTilt
    {
        get
        {
            if (Application.isPlaying)
            {
                Camera c = Camera.main;
                if (c != null) { Vector3 e = c.transform.eulerAngles; return Quaternion.Euler(e.x, e.y, 0f); }
            }
            return Quaternion.Euler(Pitch, Yaw, 0f);
        }
    }

    public const string TreeFolder = "Assets/The Higalina Vault/Trees/Icons/without_shadow";
    public const string RockFolder = "Assets/KebapGuy/StoneRockPack/Sprites";

    public static string Tree(int n) => $"{TreeFolder}/tree_medium_{(n < 100 ? n.ToString("00") : n.ToString())}.png";

    // ── 계절마다 쓰는 나무 (번호는 묶음 안 그림 번호) ─────────────────
    // 소나무 1 3 5 · 눈 소나무 2 4 6 8 · 활엽 9 15 18 47 52 57 62 67 · 열매 48 63 64 69 · 덤불 36 38 40 37 39 41
    // 단풍 12 27 30 33 21 · 벗은 나무 10 13 16 19 29 34 · 눈 덮인 가지 11 14 17 20 32 35 46 51 61 · 버들 24 · 꽃 84
    public static int[] Trees(Season s) => s switch
    {
        Season.Spring => new[] { 1, 3, 5, 9, 15, 18, 47, 57, 62, 67, 84, 84, 48, 63 },
        Season.Summer => new[] { 1, 5, 9, 18, 47, 52, 57, 62, 67, 24 },
        Season.Autumn => new[] { 1, 5, 12, 27, 30, 33, 21, 9, 64, 69, 13 },
        Season.Winter => new[] { 2, 4, 6, 8, 11, 14, 17, 20, 32, 35, 46, 51, 61, 23 },
        _ => new[] { 1, 9, 18 },
    };

    /// <summary>장 밖 숲 — 짙은 것 위주.</summary>
    public static int[] Forest(Season s) => s switch
    {
        Season.Winter => new[] { 2, 4, 6, 2, 11, 17 },
        Season.Autumn => new[] { 1, 5, 52, 12, 33 },
        _ => new[] { 1, 5, 52, 18, 1, 5 },
    };

    public static int[] Bushes(Season s) => s switch
    {
        Season.Winter => new[] { 37, 39, 41 },
        Season.Autumn => new[] { 40, 36 },
        Season.Spring => new[] { 36, 38 },
        _ => new[] { 36 },
    };

    /// <summary>고목 — 장의 표지. 봄 · 여름은 크고 둥근 것, 가을은 단풍, 겨울은 눈 덮인 큰 가지.</summary>
    public static int OldTree(Season s) => s switch
    {
        Season.Winter => 17,
        Season.Autumn => 12,
        _ => 9,
    };

    public static int[] WaterTrees(Season s) => s == Season.Winter ? new[] { 26 } : s == Season.Autumn ? new[] { 25 } : new[] { 24 };

    // ── 돌 ────────────────────────────────────────────────────────────

    public static string RockColor(Season s) => s switch
    {
        Season.Winter => "Gray_Snow",
        Season.Autumn => "Brown",
        Season.Summer => "Moss",
        _ => "Gray",
    };

    public static readonly string[] BigRocks = { "rock_giant_01", "rock_giant_02", "rock_giant_03", "rock_giant_04" };
    public static readonly string[] MidRocks = { "rock_angular_01", "rock_angular_02", "rock_angular_03", "rock_angular_04", "rock_round_01", "rock_round_02", "rock_round_03", "rock_cracked_01", "rock_cracked_02", "rock_cluster_01", "rock_cluster_02" };
    public static readonly string[] SmallStones = { "stone_cluster_01", "stone_cluster_02", "stone_cluster_03", "stone_pebbles_01", "stone_pebbles_02", "stone_round_01", "stone_round_02", "stone_flat_01", "stone_pointy_01" };

    public static string Rock(string name, string color) => $"{RockFolder}/{color}/{name}_{color.ToLowerInvariant()}.png";

    /// <summary>계절마다 땅 빛 — 봄 연두 · 여름 짙은 초록 · 가을 누런 풀 · 겨울 눈(길은 녹은 흙).</summary>
    public static (Color grass, Color grassDark, Color dirt, Color forest) GroundColors(Season s) => s switch
    {
        Season.Winter => (new Color(0.90f, 0.92f, 0.95f), new Color(0.80f, 0.84f, 0.89f), new Color(0.60f, 0.55f, 0.50f), new Color(0.72f, 0.76f, 0.82f)),
        Season.Autumn => (new Color(0.66f, 0.58f, 0.32f), new Color(0.56f, 0.50f, 0.28f), new Color(0.68f, 0.55f, 0.38f), new Color(0.30f, 0.26f, 0.16f)),
        Season.Summer => (new Color(0.38f, 0.56f, 0.26f), new Color(0.30f, 0.47f, 0.22f), new Color(0.68f, 0.57f, 0.41f), new Color(0.16f, 0.25f, 0.14f)),
        _ => (new Color(0.50f, 0.66f, 0.33f), new Color(0.42f, 0.58f, 0.29f), new Color(0.72f, 0.60f, 0.43f), new Color(0.19f, 0.28f, 0.16f)),
    };

    public static float YardAmount(ChapterMap map, Vector3 p)
    {
        float best = 0f;
        foreach (MapBlock b in map.Blocks)
        {
            if (b.Kind is not (MapBlockKind.Building or MapBlockKind.Stall or MapBlockKind.Well or MapBlockKind.Post))
                continue;
            float dx = Mathf.Max(0f, Mathf.Abs(p.x - b.Center.x) - b.Size.x * 0.5f);
            float dz = Mathf.Max(0f, Mathf.Abs(p.z - b.Center.y) - b.Size.z * 0.5f);
            best = Mathf.Max(best, 1f - Mathf.InverseLerp(2.5f, 6f, Mathf.Sqrt(dx * dx + dz * dz)));
        }
        return best;
    }

    public static List<(Vector2, Vector2)> Roads(ChapterMap map)
    {
        var roads = new List<(Vector2, Vector2)>();
        foreach (MapGate gate in map.Gates)
        {
            Vector2 g = gate.Block.Center;
            foreach (ZoneArea area in map.Areas)
            {
                Rect r = area.Bounds;
                if (Rect.MinMaxRect(r.xMin - 3f, r.yMin - 3f, r.xMax + 3f, r.yMax + 3f).Contains(g))
                    roads.Add((g, r.center));
            }
        }
        if (roads.Count == 0)
            for (int i = 1; i < map.Areas.Length; i++)
                roads.Add((map.Areas[i - 1].Bounds.center, map.Areas[i].Bounds.center));
        return roads;
    }

    public static float RoadAmount(List<(Vector2 a, Vector2 b)> roads, Vector3 p)
    {
        var q = new Vector2(p.x, p.z);
        float best = 0f;
        foreach ((Vector2 a, Vector2 b) in roads)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Max(best, 1f - Mathf.InverseLerp(2.2f, 3.6f, Vector2.Distance(q, a + ab * t)));
        }
        return best;
    }

}
