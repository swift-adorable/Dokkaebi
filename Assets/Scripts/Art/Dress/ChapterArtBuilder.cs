using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【장 꾸미기 — 만드는 쪽】 (결정 2-127) — 장 맵 표(ZoneMapTable)를 읽어 2.5D 모습(세운 나무 · 돌 그림, 담장 친 작은 집, 대숲 …)을 부품 목록으로 만들고,
/// 칸 · 재질마다 합쳐 단다. 같은 장은 늘 같은 모습(씨앗 = 장 번호). 런타임에 만들어 씬 파일에 담지 않는다.
/// </summary>
public sealed class ChapterArtBuilder
{
    private readonly ChapterMap map;
    private readonly ArtKit kit;
    private readonly System.Random rnd;
    private readonly List<(Mesh mesh, Matrix4x4 m, string key)> parts = new();
    private readonly Season season;
    private readonly string rockColor;

    public int Count => parts.Count;

    public ChapterArtBuilder(ChapterMap map, ArtKit kit)
    {
        this.map = map;
        this.kit = kit;
        rnd = new System.Random(7000 + map.Chapter);
        season = ZoneDataTable.Chapter(map.Chapter)?.Season ?? Season.Spring;
        rockColor = ArtCatalog.RockColor(season);
    }

    /// <summary>세운 그림 하나 — 밑 가운데가 pos, 높이 height(가로는 그림 비율대로). 카메라 기울기만큼 눕힌다.</summary>
    private void Card(string path, Vector3 pos, float height)
    {
        if (!kit.Has("S:" + path))
            return;
        float w = height * kit.Aspect("S:" + path);
        parts.Add((kit.Mesh("Card"), Matrix4x4.TRS(pos, Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f), new Vector3(w, height, 1f)), "S:" + path));
    }

    private string TreeOf(int[] set) => ArtCatalog.Tree(set[rnd.Next(set.Length)]);
    private string RockOf(string[] set) => ArtCatalog.Rock(set[rnd.Next(set.Length)], rockColor);

    private float R => (float)rnd.NextDouble();
    private float Range(float a, float b) => a + (b - a) * R;
    private float Jit(float a) => (R * 2f - 1f) * a;

    private void Put(Mesh mesh, string key, Vector3 pos, Vector3 scale, float yaw = 0f, float tilt = 0f)
        => parts.Add((mesh, Matrix4x4.TRS(pos, Quaternion.Euler(Jit(tilt), yaw, Jit(tilt)), scale), key));

    private string Pick(params string[] keys) => keys[rnd.Next(keys.Length)];

    public void BuildAll()
    {
        foreach (MapBlock b in map.Blocks)
            Block(b);
        OuterForest();
        Scatter();
    }

    private void Block(MapBlock b)
    {
        var c = new Vector3(b.Center.x, 0f, b.Center.y);
        float h = Mathf.Max(0.1f, b.Size.y);

        switch (b.Kind)
        {
            case MapBlockKind.Boundary:
                Woods(b, 2.3f, 4.5f, 7f, deep: true);
                break;
            case MapBlockKind.Bamboo:
                Bamboo(b, Mathf.Clamp(h, 4f, 7f));
                break;
            case MapBlockKind.Bush:
                Fill(b, 1.6f, (x, z) => Card(TreeOf(ArtCatalog.Bushes(season)), new Vector3(x, 0f, z), Range(1.5f, 2.1f)));
                break;
            case MapBlockKind.Tree:
                OldTree(b, c);
                break;
            case MapBlockKind.Building:
                Compound(b, c);
                break;
            case MapBlockKind.Rock:
                Rocks(b, c, h);
                break;
            case MapBlockKind.Water:
                Put(kit.Mesh("Box"), "Water", c + Vector3.up * 0.01f, new Vector3(b.Size.x, 0.05f, b.Size.z));
                Edge(b, 2.4f, (p) =>
                {
                    if (R < 0.12f)
                        Card(TreeOf(ArtCatalog.WaterTrees(season)), p, Range(4.5f, 6f));
                    else
                        Card(RockOf(ArtCatalog.SmallStones), p, Range(0.8f, 1.1f));
                });
                break;
            case MapBlockKind.Fence:
                Fence(b, h);
                break;
            case MapBlockKind.Stall:
                Stall(b, c, h);
                break;
            case MapBlockKind.Well:
                Put(kit.Mesh("Hex"), "Rock", c, new Vector3(b.Size.x, 0.8f, b.Size.z));
                Put(kit.Mesh("Hex"), "Water", c + Vector3.up * 0.8f, new Vector3(b.Size.x * 0.7f, 0.01f, b.Size.z * 0.7f));
                Put(kit.Mesh("Hex"), "Wood", c + new Vector3(b.Size.x * 0.45f, 0f, 0f), new Vector3(0.14f, 1.9f, 0.14f));
                Put(kit.Mesh("Hex"), "Wood", c - new Vector3(b.Size.x * 0.45f, 0f, 0f), new Vector3(0.14f, 1.9f, 0.14f));
                Put(kit.Mesh("HipRoof"), season == Season.Winter ? "Snow" : "Tile", c + Vector3.up * 1.9f, new Vector3(b.Size.x * 0.95f, 0.45f, b.Size.z * 0.6f));
                break;
            case MapBlockKind.Post:
            {
                // 장승 · 말뚝 — 길게 늘인 것(장승 줄 · 담장)은 1.5m마다 하나씩 세운다.
                float lo = Mathf.Min(b.Size.x, b.Size.z), hi = Mathf.Max(b.Size.x, b.Size.z);
                bool alongX = b.Size.x >= b.Size.z;
                int n = hi / Mathf.Max(0.1f, lo) > 2.5f ? Mathf.Max(2, Mathf.RoundToInt(hi / 1.5f)) : 1;
                float w = Mathf.Min(lo, 1.2f);
                for (int i = 0; i < n; i++)
                {
                    float t = n == 1 ? 0f : -hi * 0.5f + hi * (i + 0.5f) / n;
                    Vector3 at = c + (alongX ? new Vector3(t, 0f, 0f) : new Vector3(0f, 0f, t));
                    float ph = h * Range(0.85f, 1.1f);
                    Put(kit.Mesh("Hex"), "Wood", at, new Vector3(w, ph, w), R * 60f);
                    Put(kit.Mesh("Cone"), "WoodDark", at + Vector3.up * ph, new Vector3(w * 1.4f, 0.35f, w * 1.4f), R * 60f);
                }
                break;
            }
            case MapBlockKind.Flowerbed:
                Fill(b, 0.9f, (x, z) =>
                {
                    Put(kit.Crown(rnd.Next(4)), Pick("Leaf1", "Leaf2"), new Vector3(x, 0f, z), new Vector3(0.7f, 0.35f, 0.7f));
                    Put(kit.Crown(rnd.Next(4)), Pick("FlowerPink", "FlowerYellow", "FlowerWhite"),
                        new Vector3(x + Jit(0.2f), 0.25f, z + Jit(0.2f)), Vector3.one * Range(0.22f, 0.32f));
                });
                break;
            case MapBlockKind.Geumjul:
                Put(kit.Mesh("Box"), "Rope", c + Vector3.up * 1.2f, new Vector3(Mathf.Max(b.Size.x, 0.08f), 0.07f, Mathf.Max(b.Size.z, 0.08f)));
                break;
            default:
                Put(kit.Mesh("Box"), "Rock", c, new Vector3(b.Size.x, h, b.Size.z));
                break;
        }
    }

    /// <summary>덩어리 바닥을 spacing 간격 격자로 훑으며(흔들어) 하나씩 놓는다.</summary>
    private void Fill(MapBlock b, float spacing, System.Action<float, float> place)
    {
        int nx = Mathf.Max(1, Mathf.RoundToInt(b.Size.x / spacing));
        int nz = Mathf.Max(1, Mathf.RoundToInt(b.Size.z / spacing));
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
            place(b.Center.x - b.Size.x * 0.5f + (ix + 0.5f) * b.Size.x / nx + Jit(spacing * 0.3f),
                b.Center.y - b.Size.z * 0.5f + (iz + 0.5f) * b.Size.z / nz + Jit(spacing * 0.3f));
    }

    /// <summary>덩어리 가장자리를 따라 spacing 간격으로.</summary>
    private void Edge(MapBlock b, float spacing, System.Action<Vector3> place)
    {
        float hx = b.Size.x * 0.5f, hz = b.Size.z * 0.5f;
        for (float x = -hx; x <= hx; x += spacing)
        {
            if (R < 0.5f) place(new Vector3(b.Center.x + x, 0f, b.Center.y - hz));
            if (R < 0.5f) place(new Vector3(b.Center.x + x, 0f, b.Center.y + hz));
        }
        for (float z = -hz; z <= hz; z += spacing)
        {
            if (R < 0.5f) place(new Vector3(b.Center.x - hx, 0f, b.Center.y + z));
            if (R < 0.5f) place(new Vector3(b.Center.x + hx, 0f, b.Center.y + z));
        }
    }

    private void Woods(MapBlock b, float spacing, float minH, float maxH, bool deep)
        => Fill(b, spacing, (x, z) => TreeAt(new Vector3(x, 0f, z), Range(minH, maxH), deep));

    /// <summary>나무 하나 — 계절 그림에서 고른다. deep이면 장 밖 숲의 짙은 것.</summary>
    private void TreeAt(Vector3 p, float height, bool deep)
        => Card(TreeOf(deep ? ArtCatalog.Forest(season) : ArtCatalog.Trees(season)), p, height);

    private void Bamboo(MapBlock b, float height)
    {
        Fill(b, 0.9f, (x, z) =>
        {
            float hh = height * Range(0.8f, 1.15f);
            Put(kit.Mesh("Hex"), "BambooStem", new Vector3(x, 0f, z), new Vector3(0.16f, hh, 0.16f), R * 60f, 3f);
            if (R < 0.55f)
                Put(kit.Crown(rnd.Next(4)), "BambooLeaf", new Vector3(x + Jit(0.3f), hh * Range(0.6f, 0.85f), z + Jit(0.3f)),
                    new Vector3(Range(1.0f, 1.6f), Range(0.9f, 1.4f), Range(1.0f, 1.6f)), R * 360f);
        });
    }

    /// <summary>고목 — 장의 표지. 계절의 큰 나무 그림 하나를 크게.</summary>
    private void OldTree(MapBlock b, Vector3 c)
        => Card(ArtCatalog.Tree(ArtCatalog.OldTree(season)), c - Vector3.forward * (Mathf.Max(b.Size.x, b.Size.z) * 0.3f), 13f);

    /// <summary>집 — 돌 기단 · 흰 회벽 · 모서리 나무 기둥 · 우진각 지붕(기와 또는 초가) · 앞에 문.</summary>
    /// <summary>
    /// 집 덩어리(결정 2-127 — 사람 3~4배 크기로) — 막히는 자리는 그대로 두고, 둘레에 담장, 안에 작은 집 여러 채.
    /// 큰 덩어리일수록 긴 쪽을 따라 7m 남짓한 집으로 나눈다. 앞(남쪽)은 마당.
    /// </summary>
    private void Compound(MapBlock b, Vector3 c)
    {
        float sx = b.Size.x, sz = b.Size.z;
        const float wallT = 0.35f, wallH = 1.5f;
        void Wall(Vector3 at, Vector3 size)
        {
            Put(kit.Mesh("Box"), "Plaster", at, size);
            Put(kit.Mesh("Box"), "Tile", at + Vector3.up * size.y, new Vector3(size.x + 0.25f, 0.22f, size.z + 0.25f));
        }
        Wall(c + new Vector3(0f, 0f, sz * 0.5f - wallT * 0.5f), new Vector3(sx, wallH, wallT));
        Wall(c + new Vector3(-sx * 0.5f + wallT * 0.5f, 0f, 0f), new Vector3(wallT, wallH, sz));
        Wall(c + new Vector3(sx * 0.5f - wallT * 0.5f, 0f, 0f), new Vector3(wallT, wallH, sz));
        float gateW = Mathf.Min(2.4f, sx * 0.3f), side = (sx - gateW) * 0.5f;
        Wall(c + new Vector3(-sx * 0.5f + side * 0.5f, 0f, -sz * 0.5f + wallT * 0.5f), new Vector3(side, wallH, wallT));
        Wall(c + new Vector3(sx * 0.5f - side * 0.5f, 0f, -sz * 0.5f + wallT * 0.5f), new Vector3(side, wallH, wallT));
        Put(kit.Mesh("Box"), "WoodDark", c + new Vector3(0f, 0f, -sz * 0.5f + wallT * 0.5f), new Vector3(gateW, wallH * 1.15f, wallT * 0.6f));

        float innerX = sx - 1.6f, innerZ = sz - 1.6f;
        if (innerX < 3f || innerZ < 3f)
            return;

        bool alongX = innerX >= innerZ;
        float along = alongX ? innerX : innerZ, across = alongX ? innerZ : innerX;
        int n = Mathf.Max(1, Mathf.CeilToInt(along / 6f));
        float houseAlong = Mathf.Min(along / n - 1.4f, 5f);
        float houseAcross = Mathf.Min(across * 0.55f, 4f);
        for (int i = 0; i < n; i++)
        {
            float t = -along * 0.5f + along * (i + 0.5f) / n;
            // 집은 뒤(북쪽 · 동쪽)에 붙이고 앞은 마당.
            float back = across * 0.5f - houseAcross * 0.5f - 0.2f;
            Vector3 at = c + (alongX ? new Vector3(t, 0f, back) : new Vector3(back, 0f, t));
            float hx = alongX ? houseAlong : houseAcross, hz = alongX ? houseAcross : houseAlong;
            House(at, Mathf.Max(3f, hx), Mathf.Max(3f, hz), 3f);
        }

        if (R < 0.6f)
            Card(TreeOf(ArtCatalog.Trees(season)), c + new Vector3(Jit(innerX * 0.35f), 0f, -innerZ * 0.3f), Range(3.5f, 4.5f));
    }

    private void House(Vector3 c, float sx, float sz, float h)
    {
        float wallH = Mathf.Clamp(h * 0.7f, 2.0f, 2.6f);
        Put(kit.Mesh("Box"), "RockDark", c, new Vector3(sx, 0.35f, sz));
        Put(kit.Mesh("Box"), "Plaster", c + Vector3.up * 0.35f, new Vector3(sx * 0.9f, wallH, sz * 0.9f));
        Put(kit.Mesh("Box"), "Wood", c + Vector3.up * (0.35f + wallH - 0.25f), new Vector3(sx * 0.92f, 0.25f, sz * 0.92f));
        foreach (float px in new[] { -0.45f, 0.45f })
        foreach (float pz in new[] { -0.45f, 0.45f })
            Put(kit.Mesh("Box"), "Wood", c + new Vector3(sx * px, 0.35f, sz * pz), new Vector3(0.28f, wallH, 0.28f));

        bool alongX = sx >= sz;
        float yaw = alongX ? 0f : 90f;
        Vector3 roofSize = alongX ? new Vector3(sx * 1.12f, 0f, sz * 1.2f) : new Vector3(sz * 1.12f, 0f, sx * 1.2f);
        bool tile = R < 0.6f;
        string roofKey = season == Season.Winter ? "Snow" : tile ? "Tile" : "Thatch";
        float roofH = Mathf.Min(sx, sz) * (tile ? 0.42f : 0.55f);
        roofSize.y = roofH;
        float baseY = 0.35f + wallH;
        Put(kit.Mesh("Box"), roofKey, c + Vector3.up * baseY,
            alongX ? new Vector3(sx * 1.16f, 0.12f, sz * 1.24f) : new Vector3(sx * 1.24f, 0.12f, sz * 1.16f));
        Put(kit.Mesh("HipRoof"), roofKey, c + Vector3.up * (baseY + 0.1f), roofSize, yaw);
        if (tile)
            Put(kit.Mesh("Box"), "Plaster", c + Vector3.up * (baseY + 0.1f + roofH * 0.97f),
                alongX ? new Vector3(sx * 0.62f, 0.14f, 0.22f) : new Vector3(0.22f, 0.14f, sz * 0.62f));

        // 문 — 긴 벽 앞(남쪽 또는 서쪽).
        Vector3 door = alongX ? new Vector3(0f, 0.35f, -sz * 0.455f) : new Vector3(-sx * 0.455f, 0.35f, 0f);
        Put(kit.Mesh("Box"), "WoodDark", c + door, alongX ? new Vector3(1.1f, 1.8f, 0.08f) : new Vector3(0.08f, 1.8f, 1.1f));
    }

    /// <summary>바위 — 둥근 덩어리는 큰 바위 그림 하나 + 둘레 잔돌, 길게 늘인 것(둑 · 바위 담)은 바위 그림을 잇는다.</summary>
    private void Rocks(MapBlock b, Vector3 c, float h)
    {
        float lo = Mathf.Min(b.Size.x, b.Size.z), hi = Mathf.Max(b.Size.x, b.Size.z);
        if (hi / Mathf.Max(0.1f, lo) > 2.5f)
        {
            bool alongX = b.Size.x >= b.Size.z;
            int n = Mathf.Max(2, Mathf.RoundToInt(hi / Mathf.Max(1.4f, lo * 1.2f)));
            for (int i = 0; i < n; i++)
            {
                float t = -hi * 0.5f + hi * (i + 0.5f) / n;
                Vector3 p = c + (alongX ? new Vector3(t + Jit(0.3f), 0f, 0f) : new Vector3(0f, 0f, t + Jit(0.3f)));
                Card(RockOf(ArtCatalog.MidRocks), p, Mathf.Clamp(Mathf.Max(lo, hi / n) * Range(1.0f, 1.25f), 1.2f, 3f));
            }
            return;
        }

        string big = RockOf(ArtCatalog.BigRocks);
        Card(big, c - Vector3.forward * b.Size.z * 0.2f, Mathf.Clamp(Mathf.Max(b.Size.x, h), 1.5f, 6f));
        for (int i = 0; i < 2; i++)
            Card(RockOf(ArtCatalog.SmallStones), c + new Vector3(Jit(b.Size.x * 0.7f), 0f, -b.Size.z * Range(0.4f, 0.6f)), Range(0.8f, 1.2f));
    }

    private void Fence(MapBlock b, float h)
    {
        bool alongX = b.Size.x >= b.Size.z;
        float length = alongX ? b.Size.x : b.Size.z;
        var c = new Vector3(b.Center.x, 0f, b.Center.y);
        Vector3 dir = alongX ? Vector3.right : Vector3.forward;
        int posts = Mathf.Max(2, Mathf.CeilToInt(length / 1.3f) + 1);
        for (int i = 0; i < posts; i++)
            Put(kit.Mesh("Hex"), "Wood", c + dir * (-length * 0.5f + length * i / (posts - 1)), new Vector3(0.18f, h * Range(0.95f, 1.1f), 0.18f), R * 60f);
        foreach (float y in new[] { h * 0.35f, h * 0.75f })
            Put(kit.Mesh("Box"), "WoodDark", c + Vector3.up * y, alongX ? new Vector3(length, 0.09f, 0.09f) : new Vector3(0.09f, 0.09f, length));
    }

    private void Stall(MapBlock b, Vector3 c, float h)
    {
        Put(kit.Mesh("Box"), "Wood", c, new Vector3(b.Size.x, h, b.Size.z));
        foreach (float px in new[] { -0.48f, 0.48f })
        foreach (float pz in new[] { -0.48f, 0.48f })
            Put(kit.Mesh("Hex"), "WoodDark", c + new Vector3(b.Size.x * px, 0f, b.Size.z * pz), new Vector3(0.12f, h + 1.5f, 0.12f));
        Put(kit.Mesh("Box"), "Cloth", c + Vector3.up * (h + 1.5f), new Vector3(b.Size.x * 1.15f, 0.08f, b.Size.z * 1.2f), 0f, 4f);
    }

    /// <summary>장 밖 숲 띠 — 맵 테두리 밖 ArtCatalog.Margin m, 그리고 맵 안이라도 구역에서 4m 넘게 떨어진 빈 땅.</summary>
    private void OuterForest()
    {
        const float spacing = 3.3f;
        float hx = map.HalfSize.x + ArtCatalog.Margin - 2f, hz = map.HalfSize.y + ArtCatalog.Margin - 2f;
        for (float x = -hx; x <= hx; x += spacing)
        for (float z = -hz; z <= hz; z += spacing)
        {
            var p = new Vector3(x + Jit(1.1f), 0f, z + Jit(1.1f));
            if (DistanceToZones(p) < 4f)
                continue;
            bool blocked = false;
            foreach (MapBlock b in map.Blocks)
                if (b.Contains(p, 0.5f))
                {
                    blocked = true;
                    break;
                }
            if (blocked)
                continue;

            // 【구역 남쪽 숲은 키를 줄인다】 (결정 2-130) — 카메라 쪽으로 세운 나무는 제 키의 1.3배만큼 뒤(북쪽) 땅을 가린다.
            // 구역 바로 남쪽의 큰 나무가 출발 자리의 주인공을 통째로 가렸다(1-1 등). 가리는 범위가 구역에 닿지 않게 키를 줄이고,
            // 너무 낮아지면 덤불로, 그것도 안 되면 비운다.
            float h = Range(5f, 7.5f);
            float limit = (RoomNorth(p) - 0.5f) * PitchSin;
            if (limit >= h)
                TreeAt(p, h, deep: true);
            else if (limit >= 2.6f)
                TreeAt(p, limit, deep: true);
            else if (limit >= 1.2f)
                Card(TreeOf(ArtCatalog.Bushes(season)), p, Mathf.Min(limit, Range(1.5f, 2.1f)));
        }
    }

    private static readonly float PitchSin = Mathf.Sin(ArtCatalog.Pitch * Mathf.Deg2Rad);

    /// <summary>p에서 북쪽(+z)으로 구역에 닿기까지의 거리. 나무 폭을 생각해 좌우 1.5m도 본다. 12m 안에 없으면 무한.</summary>
    private float RoomNorth(Vector3 p)
    {
        for (float s = 0f; s <= 12f; s += 0.5f)
            for (float dx = -1.5f; dx <= 1.5f; dx += 1.5f)
                if (DistanceToZones(new Vector3(p.x + dx, 0f, p.z + s)) <= 0f)
                    return s;
        return float.PositiveInfinity;
    }

    /// <summary>빈 땅에 잔돌 · 덤불 · 나무를 드문드문 — 바람의나라처럼 땅이 비어 보이지 않게. 길 · 마당 · 덩어리 가까이는 비운다.</summary>
    private void Scatter()
    {
        const float spacing = 4.5f;
        List<(Vector2 a, Vector2 b)> roads = ArtCatalog.Roads(map);
        foreach (ZoneArea a in map.Areas)
        for (float x = a.Bounds.xMin + 2f; x < a.Bounds.xMax - 2f; x += spacing)
        for (float z = a.Bounds.yMin + 2f; z < a.Bounds.yMax - 2f; z += spacing)
        {
            var p = new Vector3(x + Jit(1.8f), 0f, z + Jit(1.8f));
            float roll = R;
            if (roll > 0.42f || ArtCatalog.RoadAmount(roads, p) > 0.05f || ArtCatalog.YardAmount(map, p) > 0.05f)
                continue;
            bool near = false;
            foreach (MapBlock b in map.Blocks)
                if (b.Contains(p, 2.5f))
                {
                    near = true;
                    break;
                }
            if (near)
                continue;

            // 걷는 땅이라 나무는 구역 가장자리(3m 안)에만 — 한가운데 나무는 캐릭터 · 적을 가린다.
            float edge = Mathf.Min(Mathf.Min(p.x - a.Bounds.xMin, a.Bounds.xMax - p.x), Mathf.Min(p.z - a.Bounds.yMin, a.Bounds.yMax - p.z));
            if (roll < 0.26f)
                Card(RockOf(ArtCatalog.SmallStones), p, Range(0.8f, 1.2f));
            else if (roll < 0.34f)
                Card(TreeOf(ArtCatalog.Bushes(season)), p, Range(0.9f, 1.3f));
            else if (edge < 3f)
                TreeAt(p, Range(4f, 5.5f), deep: false);
        }
    }

    private float DistanceToZones(Vector3 p)
    {
        float best = float.MaxValue;
        foreach (ZoneArea a in map.Areas)
        {
            Rect r = a.Bounds;
            float dx = Mathf.Max(0f, Mathf.Max(r.xMin - p.x, p.x - r.xMax));
            float dz = Mathf.Max(0f, Mathf.Max(r.yMin - p.z, p.z - r.yMax));
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    /// <summary>칸(ArtCatalog.Cell) · 재질마다 하나로 합쳐 parent 아래에 단다. 저장하지 않는 물건(DontSave) — 씬 파일 · 저장소를 키우지 않는다.</summary>
    public int Emit(Transform parent)
    {
        int count = 0;
        var groups = new Dictionary<(int, int, string), List<CombineInstance>>();
        foreach ((Mesh mesh, Matrix4x4 m, string key) in parts)
        {
            if (mesh == null || kit.Mat(key) == null)
                continue;
            Vector3 pos = m.GetColumn(3);
            var k = (Mathf.FloorToInt(pos.x / ArtCatalog.Cell), Mathf.FloorToInt(pos.z / ArtCatalog.Cell), key);
            if (!groups.TryGetValue(k, out List<CombineInstance> list))
                groups[k] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = m });
        }

        // 【플레이 중에는 저장 안 함 표시를 달지 않는다】 (결정 2-128) — 에디터에서는 씬 파일에 들어가지 않게
        // DontSave를 달지만, 플레이 중에는 씬과 함께 사라지고 메쉬는 씬을 바꿀 때 안 쓰는 애셋 정리가 거둔다.
        HideFlags meshFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
        HideFlags objectFlags = Application.isPlaying ? HideFlags.NotEditable : HideFlags.DontSave | HideFlags.NotEditable;

        foreach (var g in groups)
        {
            var mesh = new Mesh { name = $"~{g.Key.Item1}_{g.Key.Item2}_{g.Key.Item3}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = meshFlags };
            mesh.CombineMeshes(g.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            var go = new GameObject(mesh.name) { hideFlags = objectFlags };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = kit.Mat(g.Key.Item3);
            r.shadowCastingMode = g.Key.Item3 == "Water" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            count++;
        }
        return count;
    }

    /// <summary>쓰인 열쇠 전부 — 꾸러미에 빠진 재질을 찾는 데.</summary>
    public IEnumerable<string> Keys
    {
        get
        {
            var seen = new HashSet<string>();
            foreach (var p in parts)
                if (seen.Add(p.key))
                    yield return p.key;
        }
    }
}
