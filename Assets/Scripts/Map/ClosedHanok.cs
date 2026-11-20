using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【들어가지 못하는 3D 한옥】 (2026-11-20 · 사용자 「맵에서 이미지로 넣은 한옥(건물)들도 이미지로 넣는 게 아니라 직접 만들어.
/// 단, 이렇게 만든 한옥들은 들어가지 못하고」).
/// 맵 덩어리(MapBlockKind.Building — 0장 헛간 · 방앗간)를 그림 카드 대신 집터와 같은 부품(기단 · 기둥 · 회벽 + 나무 띠 · 우진각 지붕)으로 세운다.
/// 문은 닫힌 띠살문, 지붕은 늘 덮여 있다. 걸음은 맵 굽기가 만든 덩어리 막이(Obstacle)가 그대로 막는다 — 여기서는 그리기만.
/// </summary>
public static class ClosedHanok
{
    private const float WallT = 0.35f;
    private static readonly Color Frame = new(0.24f, 0.15f, 0.09f);

    /// <summary>이 장의 건물 덩어리마다 한 채 — 0장만 (1 ~ 6장은 펼칠 때).</summary>
    public static void SpawnFor(ChapterMap map)
    {
        if (map == null || map.Chapter != 0) return;
        var root = new GameObject("ClosedHanok");
        int i = 0;
        foreach (MapBlock b in map.Blocks)
            if (b.Kind == MapBlockKind.Building)
                Build(root.transform, b, i++);
    }

    /// <summary>덩어리 하나 → 닫힌 한 채. 넓적하면 헛간(초가), 아니면 방앗간(기와).</summary>
    public static GameObject Build(Transform parent, MapBlock b, int index)
    {
        bool barn = b.Size.x > b.Size.z * 1.5f;
        var house = new GameObject(barn ? $"ClosedHanok_헛간_{index}" : $"ClosedHanok_방앗간_{index}").transform;
        house.SetParent(parent, false);
        house.position = new Vector3(b.Center.x, 0f, b.Center.y);

        // 벽 네모 — 덩어리보다 0.3m 안쪽 (기단이 0.6m 나와 덩어리 테두리쯤에 닿는다)
        float hx = b.Size.x * 0.5f - 0.3f, hz = b.Size.z * 0.5f - 0.3f;
        float wallH = barn ? 2.4f : 2.7f;
        bool thatch = barn;

        HouseMesh.Box(house, "Gidan", Vector3.zero, new Vector3(hx * 2f + 1.2f, 0.3f, hz * 2f + 1.2f), "wall_base");
        HouseMesh.Box(house, "Floor", Vector3.zero, new Vector3(hx * 2f - 0.1f, 0.3f, hz * 2f - 0.1f), "floor_wood", false);

        // 기둥 — 네 귀 + 2.6m 마다
        var posts = new HashSet<Vector2Int>();
        void Post(float x, float z)
        {
            if (!posts.Add(new Vector2Int(Mathf.RoundToInt(x * 10f), Mathf.RoundToInt(z * 10f)))) return;
            HouseMesh.Box(house, "Chucho", new Vector3(x, 0f, z), new Vector3(0.56f, 0.34f, 0.56f), "wall_base");
            HouseMesh.Box(house, "Post", new Vector3(x, 0.18f, z), new Vector3(0.44f, wallH, 0.44f), "wood_post");
        }
        foreach (var (a, e) in new[] { (new Vector2(-hx, -hz), new Vector2(hx, -hz)), (new Vector2(-hx, hz), new Vector2(hx, hz)), (new Vector2(-hx, -hz), new Vector2(-hx, hz)), (new Vector2(hx, -hz), new Vector2(hx, hz)) })
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((e - a).magnitude / 2.6f));
            for (int k = 0; k <= n; k++) { Vector2 p = Vector2.Lerp(a, e, k / (float)n); Post(p.x, p.y); }
        }

        // 벽 — 앞(남)에 닫힌 문 하나 · 옆과 앞에 창
        float doorW = barn ? 2.2f : 1.6f;
        Wall(house, new Vector3(-hx, 0.3f, -hz), new Vector3(hx, 0.3f, -hz), wallH, new[] { (0f, doorW, true), (-hx * 0.6f, 1.2f, false), (hx * 0.6f, 1.2f, false) });
        Wall(house, new Vector3(-hx, 0.3f, hz), new Vector3(hx, 0.3f, hz), wallH, new (float, float, bool)[0]);
        Wall(house, new Vector3(-hx, 0.3f, -hz), new Vector3(-hx, 0.3f, hz), wallH, hz > 2.5f ? new[] { (0f, 1.2f, false) } : new (float, float, bool)[0]);
        Wall(house, new Vector3(hx, 0.3f, -hz), new Vector3(hx, 0.3f, hz), wallH, hz > 2.5f ? new[] { (0f, 1.2f, false) } : new (float, float, bool)[0]);
        HouseMesh.Box(house, "Didimdol", new Vector3(0f, 0f, -hz - 0.9f), new Vector3(doorW, 0.16f, 0.6f), "floor_stone");

        // 지붕 — 늘 덮여 있다 (들어가지 못하는 집)
        var cover = Rect.MinMaxRect(-hx, -hz, hx, hz);
        float shortSide = Mathf.Min(cover.width, cover.height);
        HouseMesh.HipRoof(house, cover, wallH + 0.45f, shortSide * (thatch ? 0.36f : 0.46f), thatch ? 0.55f : 1.1f, thatch ? "roof_choga" : "roof_giwa", !thatch);

        HouseMesh.Combine(house);
        return house.gameObject;
    }

    /// <summary>a → b 벽 한 줄 (y = 기단 위). openings = (가운데부터 거리, 폭, 문인가). 문은 닫힌 띠살문 두 짝.</summary>
    private static void Wall(Transform parent, Vector3 a, Vector3 b, float height, (float at, float width, bool door)[] openings)
    {
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = alongX ? b.x - a.x : b.z - a.z;
        Vector3 dir = alongX ? Vector3.right : Vector3.forward;
        Vector3 Size(float l, float h, float t) => alongX ? new Vector3(l, h, t) : new Vector3(t, h, l);
        void Piece(float s0, float s1, float y0, float y1)
        {
            if (s1 - s0 < 0.05f || y1 - y0 < 0.05f) return;
            HouseMesh.Box(parent, "Wall", a + dir * ((s0 + s1) * 0.5f) + Vector3.up * y0, Size(s1 - s0, y1 - y0, WallT), "wall_plaster");
        }
        var cuts = new List<(float s, float e, bool door)>();
        foreach (var o in openings) cuts.Add((len * 0.5f + o.at - o.width * 0.5f, len * 0.5f + o.at + o.width * 0.5f, o.door));
        cuts.Sort((p, q) => p.s.CompareTo(q.s));
        float cur = 0f;
        foreach (var c in cuts)
        {
            Piece(cur, c.s, 0f, height);
            Vector3 mid = a + dir * ((c.s + c.e) * 0.5f);
            float w = c.e - c.s;
            if (c.door)
            {
                Piece(c.s, c.e, 2.1f, height);
                foreach (float at in new[] { c.s, c.e })
                    HouseMesh.BoxColor(parent, "DoorJamb", a + dir * at, Size(0.2f, 2.2f, WallT + 0.12f), Frame);
                HouseMesh.BoxColor(parent, "DoorLintel", mid + Vector3.up * 2.1f, Size(w + 0.3f, 0.2f, WallT + 0.14f), Frame);
                // 닫힌 두 짝 — 문틀 안을 꽉 채운다
                HouseMesh.Box(parent, "DoorLeaf", mid, Size(w, 2.1f, 0.14f), "lattice_door");   // 닫힌 띠살문 두 짝
                HouseMesh.BoxColor(parent, "DoorSeam", mid, Size(0.06f, 2.1f, 0.2f), Frame);
            }
            else
            {
                Piece(c.s, c.e, 0f, 0.9f);
                Piece(c.s, c.e, 1.8f, height);
                HouseMesh.Box(parent, "WinPaper", mid + Vector3.up * 0.9f, Size(w, 0.9f, 0.08f), "lattice");
                HouseMesh.BoxColor(parent, "WinSill", mid + Vector3.up * 0.84f, Size(w + 0.2f, 0.1f, WallT + 0.1f), Frame);
                HouseMesh.BoxColor(parent, "WinHead", mid + Vector3.up * 1.78f, Size(w + 0.2f, 0.1f, WallT + 0.1f), Frame);
            }
            cur = c.e;
        }
        Piece(cur, len, 0f, height);

        // 나무 띠 — 하방 · 중방(문에서 끊김) · 상방 (집터 벽과 같은 규칙)
        float bt = WallT + 0.08f;
        float c0 = 0f;
        var runs = new List<(float, float)>();
        foreach (var c in cuts) { if (!c.door) continue; if (c.s > c0 + 0.05f) runs.Add((c0, c.s)); c0 = c.e; }
        if (len > c0 + 0.05f) runs.Add((c0, len));
        foreach (var (b0, b1) in runs)
        {
            Vector3 bc = a + dir * ((b0 + b1) * 0.5f);
            HouseMesh.Box(parent, "Haban", bc, Size(b1 - b0, 0.28f, bt), "wood_post");
            HouseMesh.Box(parent, "Jungban", bc + Vector3.up * 0.84f, Size(b1 - b0, 0.12f, bt), "wood_post");
        }
        HouseMesh.Box(parent, "Sangban", a + dir * (len * 0.5f) + Vector3.up * (height - 0.22f), Size(len, 0.25f, bt), "wood_post");   // 벽 위보다 3cm 높게 (같은 높이면 반짝인다)
    }
}
