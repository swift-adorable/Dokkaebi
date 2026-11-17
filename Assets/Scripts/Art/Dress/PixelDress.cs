using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【장 맵 도트 꾸미기】 (2026-11-17) — 로우폴리 · 그림 나무 대신 도트 그림(Resources/Wild — nanasha 「Samurai Tales」 무료 팩,
/// 상업용 가능 · AI 학습 금지라 학습에는 안 씀)으로 장 맵을 꾸민다. 지금은 0장(대숲 밤길 · 고목 뒤 수풀)만.
///   · 바닥: 에디터 「0장 도트 바닥 굽기」가 구운 그림(Chapter0_PixelGround) — 길 · 냇물 · 진흙 둑 · 바위 땅 · 풀 (같은 길을 Paths 에서 읽는다)
///   · 덩어리: 대숲 → 대나무, 바위 → 이끼 바위 · 절벽, 냇물 → 둑의 갈대 · 버드나무, 고목 → 녹나무 고목, 울타리 → 가시 덤불,
///     수풀 → 덤불 · 고사리, 담 → 숲. 집(헛간 · 방앗간)은 아직 로우폴리 그대로(도트 그림이 없다)
///   · 빈 땅: 고사리 · 풀 · 버섯 · 돌 · 그루터기를 흩고, 길가에 이정표 · 등롱, 집 둘레에 허수아비 · 장작 같은 살림
///   · 그림은 한 장(아틀라스)으로 묶고 32m 칸마다 메쉬 하나로 합친다 — 휴대폰 그리기 횟수를 줄인다
///   · 막히는 것(콜라이더)은 장 맵 굽기가 만든 그레이박스 그대로 — 여기는 보이는 것만
/// 같은 장은 늘 같은 모습(씨앗 고정).
/// </summary>
public static class PixelDress
{
    public const string Folder = "Wild";
    public static bool Has(int chapter) => chapter == 0;

    // ── 그림 묶음 (파일 이름) ───────────────────────────────────────
    private static readonly string[] ForestTrees =
    {
        "lone_tall_cedar_with_a_thick_trunk", "tall_fir_tree", "tall_gnarled_mountain_pine", "tall_japanese_yew_tree",
        "tall_red_pine_with_a_bent_trunk", "tall_slim_cypress_tree", "tall_zelkova_tree", "tall_oak_tree_with_a_thick_trunk",
        "tall_katsura_tree_with_round_green_leaves", "tall_crooked_pine_on_a_rock", "tall_dead_pine_with_bare_twisted_branches",
        "tall_red_pine_with_a_crooked_trunk",
    };
    private static readonly string[] SpookyTrees =
    {
        "tall_dead_tree_with_a_hanging_cocoon_of_spider_silk", "tall_leafless_old_tree", "tall_hollow_tree_trunk_with_moss",
        "tall_dead_tree_with_shelf_fungus_on_its_trunk", "tall_maple_tree_with_dark_red_night_leaves",
    };
    private static readonly string[] Bamboo = { "tall_clump_of_black_bamboo_bent_by_the_wind", "tall_stand_of_bamboo_with_yellowing_leaves" };
    private static readonly string[] BigRocks = { "tall_mossy_rock_pillar", "tall_rock_cliff_with_moss_and_ferns", "tall_weathered_rock_with_a_small_pine_on_top" };
    private static readonly string[] Stones = { "moss_covered_stone", "mossy_stone_with_claw_marks", "pile_of_river_stones", "slimy_river_rock_with_a_puddle" };
    private static readonly string[] Bushes = { "thorny_bush", "forest_fern_clump", "clump_of_dry_autumn_grass", "small_sapling_with_green_leaves", "low_tuft_of_silver_grass" };
    private static readonly string[] Ground =
    {
        "forest_fern_clump", "clump_of_dry_autumn_grass", "low_tuft_of_silver_grass", "pile_of_pine_needles", "fallen_pine_branch_with_cones",
        "patch_of_wild_flowers", "cluster_of_tiny_white_mushrooms", "moss_covered_stone", "pile_of_river_stones", "shelf_fungus_on_a_log",
        "mossy_tree_stump_with_brown_mushrooms", "small_sapling_with_green_leaves", "thorny_bush", "set_of_deer_tracks_in_the_mud",
    };
    private static readonly string[] Eerie =
    {
        "ring_of_pale_mushrooms", "patch_of_red_spider_lilies", "hollow_log_with_glowing_eyes_inside", "spider_web_stretched_between_two_sticks",
        "tree_stump_split_by_lightning", "fallen_log_with_orange_shelf_fungus", "hollow_tree_stump_with_a_fox_fire_inside",
    };
    private static readonly string[] Bank = { "clump_of_tall_night_reeds_with_fireflies", "brown_cattail_on_a_long_stalk", "slimy_river_rock_with_a_puddle", "low_tuft_of_silver_grass", "forest_fern_clump" };
    private static readonly string[] Farm =
    {
        "tall_straw_scarecrow_in_a_farmers_coat", "wheelbarrow_of_cut_reeds", "tall_stack_of_firewood_under_a_little_roof",
        "stump_chopping_block_with_an_axe", "big_covered_clay_storage_jar", "hollowed_log_full_of_rainwater", "moss_covered_old_cart_wheel",
        "heap_of_cut_bamboo_poles",
    };
    private const string OldTree = "gnarled_old_camphor_tree_with_a_hollow";
    private const string Hedge = "tall_thorny_hedge_with_torn_cloth_caught_on_it";
    private const string Willow = "tall_weeping_willow_with_long_drooping_branches";
    private const string Pampas = "tall_pampas_grass_clump_with_white_plumes";
    private const string Waymark = "wooden_waymarker_post_with_a_small_roof";
    private const string Lantern = "low_wooden_lantern_stand";
    private const string Boat = "rotten_wooden_boat_half_in_the_reeds";

    // ── 길 (바닥 굽기와 함께 쓴다) ──────────────────────────────────
    /// <summary>
    /// 0장 길 — 본문 순서: 헛간(출발) → 봇짐 → 첫 대숲 아래로 동쪽 → 언덕 남쪽 → 냇물 건너기 → 방앗간 갈림 → 고목 양옆 두 갈래 → 0-2 수풀.
    /// </summary>
    public static readonly Vector2[][] Chapter0Paths =
    {
        new Vector2[] { new(-72f, -36f), new(-70f, -30f), new(-63f, -27f), new(-54f, -24f), new(-46f, -19f), new(-38f, -9f), new(-29f, -4f), new(-21f, -2f), new(-12f, -1f), new(-2f, 0f), new(8f, 0f), new(13f, 0f) },
        new Vector2[] { new(-8f, -1f), new(-7f, 8f), new(-6f, 17f) },                       // 방앗간 갈림
        new Vector2[] { new(-2f, 0f), new(4f, -14f), new(8f, -30f), new(8f, -36f) },        // 남쪽 길목
        new Vector2[] { new(-6f, 17f), new(4f, 18f), new(10f, 18f) },                       // 북쪽 길목
        new Vector2[] { new(13f, 0f), new(17f, -11f), new(24f, -11f), new(32f, 0f) },       // 고목 남쪽 갈래
        new Vector2[] { new(13f, 0f), new(17f, 11f), new(24f, 11f), new(32f, 0f) },         // 고목 북쪽 갈래
        new Vector2[] { new(32f, 0f), new(44f, -6f), new(56f, -12f), new(64f, -24f), new(74f, -38f) },
        new Vector2[] { new(32f, 0f), new(46f, 12f), new(60f, 12f), new(68f, 24f), new(74f, 36f) },
    };

    public static Vector2[][] Paths(int chapter) => chapter == 0 ? Chapter0Paths : System.Array.Empty<Vector2[]>();

    /// <summary>길까지의 거리 (m).</summary>
    public static float PathDistance(int chapter, Vector2 q)
    {
        float best = float.MaxValue;
        foreach (Vector2[] line in Paths(chapter))
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = line[i - 1], ab = line[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
            }
        return best;
    }

    // ── 만들기 ───────────────────────────────────────────────────
    private static Texture2D atlas;
    private static Material material;
    private static readonly Dictionary<string, (Rect uv, int w, int h)> rects = new();

    private sealed class Ctx
    {
        public ChapterMap Map;
        public System.Random Rnd;
        public readonly List<(string sprite, Vector3 pos, float ppm, bool flip)> Cards = new();
        public readonly List<Vector2> Taken = new();
        public float R => (float)Rnd.NextDouble();
        public float Range(float a, float b) => a + (b - a) * R;
        public string Pick(string[] set) => set[Rnd.Next(set.Length)];
    }

    /// <summary>그 장의 도트 꾸밈을 parent 아래에 「~px_칸」 자식들로 단다. 만든 조각 수.</summary>
    public static int Build(int chapter, Transform parent)
    {
        ChapterMap map = ZoneMapTable.Of(chapter);
        if (map == null || !Has(chapter) || !EnsureAtlas())
            return 0;

        var c = new Ctx { Map = map, Rnd = new System.Random(9100 + chapter) };
        foreach (MapBlock b in map.Blocks)
            Block(c, b);
        Landmarks(c);
        OuterForest(c);
        Scatter(c);
        return Emit(c, parent);
    }

    /// <summary>
    /// 1m 에 몇 칸 — 모든 그림을 캐릭터 · 바닥 · 건물과 같은 칸 크기로 (2026-11-17 사용자 「이미지마다 느낌이 너무 달라 어색」).
    /// 전에는 나무 16 · 고목 10 · 소품 24~26 칸이라 도트 굵기가 제각각이었다. 크기는 그림이 정한다(나무 3.5~4m).
    /// </summary>
    private static float PpmOf(string sprite) => BuildingSprite.PixelsPerMeter;

    private static void Add(Ctx c, string sprite, Vector3 p, float scale = 1f)
    {
        if (!rects.ContainsKey(sprite))
            return;
        c.Cards.Add((sprite, p, PpmOf(sprite), c.R < 0.5f));   // scale 은 쓰지 않는다 — 크기를 흔들면 도트 굵기가 다시 제각각이 된다
        c.Taken.Add(new Vector2(p.x, p.z));
    }

    private static bool Free(Ctx c, Vector2 p, float gap)
    {
        foreach (Vector2 q in c.Taken)
            if ((q - p).sqrMagnitude < gap * gap)
                return false;
        return true;
    }

    private static void Block(Ctx c, MapBlock b)
    {
        var center = new Vector3(b.Center.x, 0f, b.Center.y);
        switch (b.Kind)
        {
            case MapBlockKind.Bamboo:
                Fill(c, b, 1.9f, p => Add(c, c.R < 0.7f ? Bamboo[0] : Bamboo[1], p, c.Range(0.9f, 1.15f)));
                Edge(c, b, 2.2f, 0.8f, p => { if (c.R < 0.45f) Add(c, c.R < 0.5f ? Pampas : c.Pick(Bushes), p); });
                break;
            case MapBlockKind.Boundary:
                // 남쪽 담 앞 큰 나무는 북쪽 땅(걷는 곳)을 가린다 — 남쪽은 낮은 덤불만.
                bool south = b.Center.y < 0f && b.Size.x > b.Size.z;
                Fill(c, b, south ? 2f : 2.6f, p => Add(c, south ? c.Pick(Bushes) : c.Pick(ForestTrees), p, south ? 1f : c.Range(0.9f, 1.2f)));
                break;
            case MapBlockKind.Rock:
                Add(c, BigRocks[1], center + new Vector3(0f, 0f, b.Size.z * 0.15f), 1.2f);
                Fill(c, b, 2.6f, p => { if (Free(c, new Vector2(p.x, p.z), 1.8f)) Add(c, c.R < 0.5f ? c.Pick(BigRocks) : c.Pick(Stones), p); });
                Edge(c, b, 2f, 1f, p => { if (c.R < 0.5f) Add(c, c.R < 0.6f ? c.Pick(Stones) : c.Pick(Bushes), p); });
                break;
            case MapBlockKind.Water:
                Edge(c, b, 1.7f, 0.6f, p =>
                {
                    if (c.R > 0.6f) return;
                    float roll = c.R;
                    Add(c, roll < 0.035f ? Willow : c.Pick(Bank), p);
                });
                Add(c, Boat, center + new Vector3(-b.Size.x * 0.5f - 0.6f, 0f, 28f));
                break;
            case MapBlockKind.Tree:
                // 고목 덩이 — 녹나무(4m)를 앞에, 뒤 · 옆으로 굵은 나무를 둘러 한 덩이로 보이게 (그림 크기는 칸 크기 그대로).
                foreach (Vector3 off in new[] { new Vector3(-2.6f, 0f, 2.2f), new Vector3(2.4f, 0f, 2.6f), new Vector3(0f, 0f, 3.8f), new Vector3(-3.6f, 0f, -0.4f), new Vector3(3.4f, 0f, 0f) })
                    Add(c, off.z > 3f ? "tall_zelkova_tree" : off.x < 0f ? "tall_oak_tree_with_a_thick_trunk" : "lone_tall_cedar_with_a_thick_trunk", center + off);
                Add(c, OldTree, center + new Vector3(0f, 0f, -b.Size.z * 0.15f));
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2f + c.Range(-0.2f, 0.2f);
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * b.Size.x * 0.62f, 0f, Mathf.Sin(a) * b.Size.z * 0.62f);
                    Add(c, i % 3 == 0 ? "patch_of_red_spider_lilies" : i == 4 ? "hollow_tree_stump_with_a_fox_fire_inside" : c.Pick(Bushes), p);
                }
                break;
            case MapBlockKind.Fence:
                Fill(c, b, 2.3f, p => Add(c, c.R < 0.75f ? Hedge : c.Pick(Bushes), p));
                break;
            case MapBlockKind.Bush:
                Fill(c, b, 1.6f, p => Add(c, c.R < 0.12f ? Pampas : c.Pick(Bushes), p, c.Range(1f, 1.3f)));
                break;
            case MapBlockKind.Building:
                // 집 둘레 살림 — 앞(남쪽) 마당 양옆 · 옆구리에.
                foreach (Vector3 off in new[]
                         {
                             new Vector3(-b.Size.x * 0.5f - 1.2f, 0f, -b.Size.z * 0.2f), new Vector3(b.Size.x * 0.5f + 1.3f, 0f, b.Size.z * 0.1f),
                             new Vector3(-b.Size.x * 0.35f, 0f, -b.Size.z * 0.5f - 1.6f), new Vector3(b.Size.x * 0.4f, 0f, -b.Size.z * 0.5f - 2.2f),
                             new Vector3(b.Size.x * 0.5f + 1.1f, 0f, -b.Size.z * 0.4f),
                         })
                    Add(c, c.Pick(Farm), center + off + new Vector3(c.Range(-0.4f, 0.4f), 0f, c.Range(-0.4f, 0.4f)));
                break;
        }
    }

    /// <summary>길가 표지 — 출발 자리 이정표 · 길목(철수) 등롱 · 갈림길 이정표.</summary>
    private static void Landmarks(Ctx c)
    {
        foreach (MapAnchor a in c.Map.Anchors)
        {
            if (a.Kind == MapAnchorKind.Start)
                Add(c, Waymark, a.Position + new Vector3(2.4f, 0f, 2f));
            else if (a.Kind == MapAnchorKind.Extraction)
            {
                Add(c, Lantern, a.Position + new Vector3(-2f, 0f, 1.2f));
                Add(c, Lantern, a.Position + new Vector3(2f, 0f, 1.2f));
            }
        }
        Add(c, Waymark, new Vector3(-9.6f, 0f, 1.8f));   // 방앗간 갈림
        Add(c, Waymark, new Vector3(11f, 0f, 2.2f));     // 고목 앞 두 갈래
    }

    /// <summary>담 밖 숲 — 바닥 그림 밖 6m 띠까지. 남쪽은 낮은 덤불(걷는 땅을 가리지 않게).</summary>
    private static void OuterForest(Ctx c)
    {
        Vector2 h = c.Map.HalfSize;
        for (float x = -h.x - 6f; x <= h.x + 6f; x += 2.8f)
        for (float z = -h.y - 6f; z <= h.y + 8f; z += 2.8f)
        {
            if (Mathf.Abs(x) < h.x + 1.5f && Mathf.Abs(z) < h.y + 1.5f)
                continue;
            var p = new Vector3(x + c.Range(-1f, 1f), 0f, z + c.Range(-1f, 1f));
            Add(c, p.z < -h.y ? c.Pick(Bushes) : c.Pick(ForestTrees), p, p.z < -h.y ? 1f : c.Range(0.9f, 1.25f));
        }
    }

    /// <summary>빈 땅 흩뿌림 — 길 · 덩어리 · 자리(출발 · 길목 · 조각 · 열매 나무) 둘레는 비운다. 나무는 구역 가장자리 쪽에만.</summary>
    private static void Scatter(Ctx c)
    {
        ChapterMap map = c.Map;
        foreach (ZoneArea area in map.Areas)
        for (int tries = 0; tries < (int)(area.Bounds.width * area.Bounds.height / 9f); tries++)
        {
            var q = new Vector2(c.Range(area.Bounds.xMin + 1.5f, area.Bounds.xMax - 1.5f), c.Range(area.Bounds.yMin + 1.5f, area.Bounds.yMax - 1.5f));
            var p = new Vector3(q.x, 0f, q.y);
            float road = PathDistance(map.Chapter, q);
            if (road < 3.2f || !Free(c, q, 3.2f) || Near(map, p, 2f) || NearAnchor(map, p, 3.5f))
                continue;

            float edge = Mathf.Min(Mathf.Min(q.x - area.Bounds.xMin, area.Bounds.xMax - q.x), Mathf.Min(q.y - area.Bounds.yMin, area.Bounds.yMax - q.y));
            float roll = c.R;
            if (roll < 0.06f && road > 7f)
                Add(c, c.Pick(Eerie), p);
            else if (roll < 0.16f && road > 6f && edge > 6f && q.y > area.Bounds.yMin + 10f)
                Add(c, c.R < 0.3f ? c.Pick(SpookyTrees) : c.Pick(ForestTrees), p, c.Range(0.85f, 1.1f));
            else
                Add(c, c.Pick(Ground), p);
        }
    }

    private static bool Near(ChapterMap map, Vector3 p, float r)
    {
        foreach (MapBlock b in map.Blocks)
            if (b.Kind != MapBlockKind.Boundary && b.Contains(p, r))
                return true;
        return false;
    }

    private static bool NearAnchor(ChapterMap map, Vector3 p, float r)
    {
        foreach (MapAnchor a in map.Anchors)
            if ((a.Position - p).sqrMagnitude < r * r)
                return true;
        return false;
    }

    private static void Fill(Ctx c, MapBlock b, float spacing, System.Action<Vector3> place)
    {
        int nx = Mathf.Max(1, Mathf.RoundToInt(b.Size.x / spacing)), nz = Mathf.Max(1, Mathf.RoundToInt(b.Size.z / spacing));
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
            place(new Vector3(b.Center.x - b.Size.x * 0.5f + (ix + 0.5f) * b.Size.x / nx + c.Range(-0.35f, 0.35f) * spacing, 0f,
                b.Center.y - b.Size.z * 0.5f + (iz + 0.5f) * b.Size.z / nz + c.Range(-0.35f, 0.35f) * spacing));
    }

    /// <summary>덩어리 둘레(out m 바깥)를 spacing 간격으로.</summary>
    private static void Edge(Ctx c, MapBlock b, float spacing, float outward, System.Action<Vector3> place)
    {
        float hx = b.Size.x * 0.5f + outward, hz = b.Size.z * 0.5f + outward;
        for (float x = -hx; x <= hx; x += spacing)
        {
            place(new Vector3(b.Center.x + x + c.Range(-0.4f, 0.4f), 0f, b.Center.y - hz + c.Range(-0.4f, 0.4f)));
            place(new Vector3(b.Center.x + x + c.Range(-0.4f, 0.4f), 0f, b.Center.y + hz + c.Range(-0.4f, 0.4f)));
        }
        for (float z = -hz + spacing; z < hz; z += spacing)
        {
            place(new Vector3(b.Center.x - hx + c.Range(-0.4f, 0.4f), 0f, b.Center.y + z + c.Range(-0.4f, 0.4f)));
            place(new Vector3(b.Center.x + hx + c.Range(-0.4f, 0.4f), 0f, b.Center.y + z + c.Range(-0.4f, 0.4f)));
        }
    }

    // ── 아틀라스 · 메쉬 ─────────────────────────────────────────────

    private static bool EnsureAtlas()
    {
        if (atlas != null && material != null)
            return true;
        Texture2D[] all = Resources.LoadAll<Texture2D>(Folder);
        if (all.Length == 0)
            return false;
        atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "WildAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Rect[] uv = atlas.PackTextures(all, 2, 4096, false);
        atlas.filterMode = FilterMode.Point;
        rects.Clear();
        for (int i = 0; i < all.Length; i++)
            rects[all[i].name] = (uv[i], all[i].width, all[i].height);
        atlas.Apply(false, true);   // 다 묶었으니 CPU 쪽 사본은 버린다
        foreach (Texture2D t in all)
            Resources.UnloadAsset(t);

        material = new Material(BuildingSprite.Material()) { name = "WildAtlas" };
        material.SetTexture("_BaseMap", atlas);
        material.SetTexture("_MainTex", atlas);
        return true;
    }

    private static int Emit(Ctx c, Transform parent)
    {
        var groups = new Dictionary<(int, int), (List<Vector3> v, List<Vector2> uv, List<Vector3> n, List<int> t)>();
        Quaternion tilt = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);
        Vector3 right = tilt * Vector3.right, up = tilt * Vector3.up, normal = tilt * Vector3.back;

        foreach (var (sprite, pos, ppm, flip) in c.Cards)
        {
            (Rect uv, int w, int h) = rects[sprite];
            float W = w / ppm, H = h / ppm;
            var key = (Mathf.FloorToInt(pos.x / 32f), Mathf.FloorToInt(pos.z / 32f));
            if (!groups.TryGetValue(key, out var g))
                groups[key] = g = (new List<Vector3>(), new List<Vector2>(), new List<Vector3>(), new List<int>());
            int i0 = g.v.Count;
            g.v.Add(pos - right * (W * 0.5f));
            g.v.Add(pos + right * (W * 0.5f));
            g.v.Add(pos - right * (W * 0.5f) + up * H);
            g.v.Add(pos + right * (W * 0.5f) + up * H);
            float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax;
            g.uv.Add(new Vector2(u0, uv.yMin)); g.uv.Add(new Vector2(u1, uv.yMin));
            g.uv.Add(new Vector2(u0, uv.yMax)); g.uv.Add(new Vector2(u1, uv.yMax));
            for (int k = 0; k < 4; k++) g.n.Add(normal);
            g.t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 2, i0 + 3, i0 + 1 });
        }

        HideFlags meshFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
        HideFlags objectFlags = Application.isPlaying ? HideFlags.NotEditable : HideFlags.DontSave | HideFlags.NotEditable;
        foreach (var pair in groups)
        {
            var mesh = new Mesh { name = $"~px_{pair.Key.Item1}_{pair.Key.Item2}", indexFormat = IndexFormat.UInt32, hideFlags = meshFlags };
            mesh.SetVertices(pair.Value.v);
            mesh.SetUVs(0, pair.Value.uv);
            mesh.SetNormals(pair.Value.n);
            mesh.SetTriangles(pair.Value.t, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            var go = new GameObject(mesh.name) { hideFlags = objectFlags };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
        return c.Cards.Count;
    }
}
