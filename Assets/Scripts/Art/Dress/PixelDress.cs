using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【장 맵 도트 꾸미기】 (2026-11-17 · 결정 2-148 그림 교체) — 로우폴리 · 그림 나무 대신 도트 그림(Resources/World — 「도깨비의 세계」
/// 색감으로 새로 그린 우리 그림)으로 장 맵을 꾸민다. 지금은 0장(대숲 밤길 · 고목 뒤 수풀)만.
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
    /// <summary>
    /// 그림 폴더 (결정 2-148) — 「도깨비의 세계」 색감으로 새로 그린 소품(World: dkbpl 화풍 1.0 + dkbworld 색감 0.3, 우리 그림 · 커밋 가능).
    /// 받은 팩(Wild, nanasha)은 선 · 색이 참고 장면과 달라 더는 쓰지 않는다 — World 가 비어 있을 때만 예전 그림으로 돌아간다.
    /// </summary>
    public const string Folder = "World";
    public static bool Has(int chapter) => chapter == 0;

    // ── 그림 묶음 (World 파일 이름 — 「이름」 · 「이름_b」 변형이 있으면 섞는다) ─────────────
    // 받은 팩 60여 종을 참고 장면에 실제로 나오는 28종으로 줄였다 — 종류가 많으면 난잡해 보인다.
    private static readonly string[] ForestTrees = { "tree_pine", "tree_zelkova", "tree_fir" };
    private static readonly string[] SpookyTrees = { "tree_dead", "tree_maple" };
    private const string BambooClump = "bamboo";
    private static readonly string[] BigRocks = { "rock_big", "rock_pillar" };
    private static readonly string[] Stones = { "rock_pile", "rock_moss" };
    private static readonly string[] Under = { "bush", "fern" };                 // 나무 밑 · 숲 가장자리
    private static readonly string[] Bank = { "reeds", "reeds", "rock_moss" };
    private static readonly string[] Jars = { "jars" };
    private static readonly string[] Woodpile = { "woodpile" };
    private static readonly string[] Yard = { "cart", "basket" };

    /// <summary>
    /// 테 무리 (결정 2-148) — 참고 장면의 무리는 늘 「큰 것 하나 + 작은 것 둘셋」이고 한 무리에 종류는 둘까지.
    /// Hero = 무리의 중심(뒤쪽), Small = 그 앞 · 옆에 붙는 것.
    /// </summary>
    private static readonly (string[] Hero, string[] Small)[] RimSets =
    {
        (new[] { "rock_big" }, new[] { "rock_pile", "fern" }),            // 바위 무리
        (new[] { "tree_pine", "tree_zelkova" }, new[] { "bush", "fern" }),  // 나무 한 그루 + 덤불
        (new[] { "log" }, new[] { "mushrooms", "fern" }),                     // 쓰러진 통나무 (그루터기는 두 번 그려도 나무 통째로 나와 뺐다)
        (new[] { "silvergrass" }, new[] { "grass", "silvergrass" }),        // 억새
        (new[] { "bush" }, new[] { "flowers", "grass" }),                   // 꽃 덤불
    };
    private static readonly (string[] Hero, string[] Small) EerieSet = (new[] { "tree_dead" }, new[] { "lily", "mushrooms" });

    private const string OldTree = "tree_old";
    private const string Waymark = "jangseung";
    private const string Lantern = "lantern";

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

    private static Vector2[][] scaledPaths;

    /// <summary>그 장의 길 — 0장은 맵 배율(ZoneMapTable.Chapter0Scale)만큼 늘린 것.</summary>
    public static Vector2[][] Paths(int chapter)
    {
        if (chapter != 0)
            return System.Array.Empty<Vector2[]>();
        if (scaledPaths == null)
        {
            scaledPaths = new Vector2[Chapter0Paths.Length][];
            for (int i = 0; i < Chapter0Paths.Length; i++)
            {
                scaledPaths[i] = new Vector2[Chapter0Paths[i].Length];
                for (int j = 0; j < Chapter0Paths[i].Length; j++)
                    scaledPaths[i][j] = Chapter0Paths[i][j] * ZoneMapTable.Chapter0Scale;
            }
        }
        return scaledPaths;
    }

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
        DressPlan.ClearCache();
        foreach (MapBlock b in map.Blocks)
            Block(c, b);
        Landmarks(c);
        OuterForest(c);
        // 【배치 틀 v2】 (결정 2-148 · 사용자 「소품들 너무 난잡하게 마구잡이로 막 배치하지말고」)
        //   참고 장면: 장면을 감싸는 건 큰 덩어리(숲 · 바위 벽)이고, 소품은 그 덩어리에 붙은 무리로만. 풀밭에 낱개로 흩어진 것은 없다.
        //   ① 숲 — 노이즈로 짙기를 정해 제각각인 모양으로, 가장자리는 덤불 · 풀로 서서히 성겨지게 (v3)
        //   ② 공터 테 — 들어오는 길 반대편 반원에만, 「큰 것 하나 + 작은 것 둘셋」
        //   ③ 길가 — 굽이마다 작은 무리 하나 (예전: 9m 마다 좌우 번갈아)
        //   ④ 나머지 풀밭은 비운다
        int n0 = c.Cards.Count;
        ForestMasses(c);
        int n1 = c.Cards.Count;
        ClearingRims(c);
        int n2 = c.Cards.Count;
        PathBends(c);
        Debug.Log($"[PixelDress] 덩어리 · 표지 · 담 밖 {n0} · 숲 덩어리 {n1 - n0} · 공터 테 {n2 - n1} · 길 굽이 {c.Cards.Count - n2}");
        return Emit(c, parent);
    }

    /// <summary>
    /// 1m 에 몇 칸 — 모든 그림을 캐릭터 · 바닥 · 건물과 같은 칸 크기로 (2026-11-17 사용자 「이미지마다 느낌이 너무 달라 어색」).
    /// </summary>
    private static float PpmOf(string sprite) => BuildingSprite.PixelsPerMeter;

    /// <summary>이름 또는 그 변형(이름_b) 중 하나 — 같은 나무가 줄지어 서지 않게.</summary>
    private static string Variant(Ctx c, string name)
    {
        string b = name + "_b";
        return rects.ContainsKey(b) && c.R < 0.5f ? b : name;
    }

    private static void Add(Ctx c, string sprite, Vector3 p, float scale = 1f)
    {
        sprite = Variant(c, sprite);
        if (!rects.ContainsKey(sprite))
            return;
        if (LockSiteTable.InAnySite(c.Map, new Vector2(p.x, p.z), 1.2f))
            return;   // 집(잠긴 곳 · 열린 집) 자리 · 문 앞은 비운다 (결정 2-149)
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
                // 대숲은 빽빽하게 한 덩이, 가장자리는 고사리 · 풀만 (예전: 억새 · 덤불을 섞어 어수선)
                // 가장자리는 노이즈로 들쭉날쭉 — 네모난 대숲은 인공적이다 (v3)
                Fill(c, b, 1.7f, p => { if (Ragged(b, p, 3.5f)) Add(c, BambooClump, p); });
                Edge(c, b, 2.2f, 0.7f, p => { if (c.R < 0.55f) Add(c, c.R < 0.6f ? "fern" : "grass", p); });
                break;
            case MapBlockKind.Boundary:
                // 남쪽 담 앞 큰 나무는 북쪽 땅(걷는 곳)을 가린다 — 남쪽은 낮은 덤불만.
                bool south = b.Center.y < 0f && b.Size.x > b.Size.z;
                Fill(c, b, south ? 1.8f : 2.2f, p => Add(c, south ? c.Pick(Under) : c.Pick(ForestTrees), p));
                break;
            case MapBlockKind.Rock:
                // 바위 벽 — 큰 바위를 겹쳐 세우고 발치에만 잔돌 · 고사리
                Add(c, BigRocks[1], center + new Vector3(0f, 0f, b.Size.z * 0.15f));
                Fill(c, b, 2.2f, p => { if (Ragged(b, p, 3f) && Free(c, new Vector2(p.x, p.z), 1.6f)) Add(c, c.R < 0.7f ? "rock_big" : "rock_pillar", p); });
                Edge(c, b, 2.4f, 0.8f, p => { if (p.z < b.Center.y && c.R < 0.6f) Add(c, c.R < 0.5f ? c.Pick(Stones) : "fern", p); });
                break;
            case MapBlockKind.Water:
                // 물가 — 돌 테는 바닥이 그린다. 소품은 갈대 무리만 띄엄띄엄(길이 건너는 곳은 비운다).
                Edge(c, b, 3.0f, 0.6f, p =>
                {
                    if (PathDistance(c.Map.Chapter, new Vector2(p.x, p.z)) < 3.5f) return;
                    if (c.R < 0.3f) Group(c, p, 0.8f, c.Rnd.Next(2, 4), Bank);
                });
                break;
            case MapBlockKind.Tree:
                // 고목 덩이 — 고목을 앞에, 뒤 · 옆으로 굵은 나무를 둘러 한 덩이로.
                foreach (Vector3 off in new[] { new Vector3(-2.6f, 0f, 2.2f), new Vector3(2.4f, 0f, 2.6f), new Vector3(0f, 0f, 3.8f), new Vector3(-3.6f, 0f, -0.4f), new Vector3(3.4f, 0f, 0f) })
                    Add(c, off.z > 3f ? "tree_zelkova" : off.x < 0f ? "tree_pine" : "tree_fir", center + off);
                Add(c, OldTree, center + new Vector3(0f, 0f, -b.Size.z * 0.15f));
                // 덩어리가 크면(맵 2배) 나머지를 숲으로 채운다 — 가장자리는 들쭉날쭉
                Fill(c, b, 2.2f, p => { if (Ragged(b, p, 3f) && Free(c, new Vector2(p.x, p.z), 1.6f)) Add(c, c.R < 0.8f ? c.Pick(ForestTrees) : c.Pick(Under), p); });
                // 발치 — 앞쪽 반원에만 꽃무릇 · 덤불 두 무리
                Group(c, center + new Vector3(-b.Size.x * 0.4f, 0f, -b.Size.z * 0.5f), 0.8f, 3, new[] { "lily", "bush" });
                Group(c, center + new Vector3(b.Size.x * 0.4f, 0f, -b.Size.z * 0.5f), 0.8f, 2, new[] { "fern", "mushrooms" });
                break;
            case MapBlockKind.Fence:
                Fill(c, b, 1.6f, p => Add(c, "bush", p));
                break;
            case MapBlockKind.Bush:
                Fill(c, b, 1.5f, p => Add(c, c.R < 0.15f ? "silvergrass" : c.Pick(Under), p));
                break;
            case MapBlockKind.Building:
                // 집 둘레 살림 — 장독은 벽에 붙여 무리로, 장작은 문 옆, 수레 · 광주리는 마당 한쪽.
                Group(c, center + new Vector3(-b.Size.x * 0.5f - 1.0f, 0f, b.Size.z * 0.1f), 0.7f, c.Rnd.Next(2, 4), Jars);
                Group(c, center + new Vector3(b.Size.x * 0.5f + 1.1f, 0f, -b.Size.z * 0.35f), 0.6f, 2, Woodpile);
                Group(c, center + new Vector3(b.Size.x * 0.3f, 0f, -b.Size.z * 0.5f - 2.4f), 0.9f, 2, Yard);
                break;
        }
    }

    /// <summary>길가 표지 — 출발 자리 장승 · 길목(철수) 등롱 한 쌍 · 갈림길 장승.</summary>
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
        float k = ZoneMapTable.Chapter0Scale;
        Add(c, Waymark, new Vector3(-8f * k - 1.6f, 0f, 1.8f));   // 방앗간 갈림
        Add(c, Waymark, new Vector3(13f * k - 2f, 0f, 2.2f));     // 고목 앞 두 갈래
    }

    /// <summary>담 밖 숲 — 바닥 그림 밖 6m 띠까지 빽빽하게. 남쪽은 낮은 덤불(걷는 땅을 가리지 않게).</summary>
    private static void OuterForest(Ctx c)
    {
        Vector2 h = c.Map.HalfSize;
        for (float x = -h.x - 6f; x <= h.x + 6f; x += 2.3f)
        for (float z = -h.y - 6f; z <= h.y + 8f; z += 2.3f)
        {
            if (Mathf.Abs(x) < h.x + 1.5f && Mathf.Abs(z) < h.y + 1.5f)
                continue;
            var p = new Vector3(x + c.Range(-0.8f, 0.8f), 0f, z + c.Range(-0.8f, 0.8f));
            Add(c, p.z < -h.y ? c.Pick(Under) : c.Pick(ForestTrees), p);
        }
    }

    // ── 배치 틀 ─────────────────────────────────────────────────────

    /// <summary>한 자리에 n개를 뭉쳐 놓는다 — 반지름 spread 안에 겹치지 않게. 막힌 곳 · 공터 안 · 길 위는 건너뛴다.</summary>
    private static int Group(Ctx c, Vector3 at, float spread, int n, string[] set)
    {
        int placed = 0;
        for (int i = 0; i < n * 3 && placed < n; i++)
        {
            float a = c.Range(0f, Mathf.PI * 2f), r = c.Range(0f, spread);
            var p = at + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.7f);
            var q = new Vector2(p.x, p.z);
            DressPlan.Spot spot = DressPlan.Classify(c.Map, q);
            if (spot is DressPlan.Spot.Clearing or DressPlan.Spot.Path or DressPlan.Spot.Water || !Free(c, q, 0.55f))
                continue;
            Add(c, set[c.Rnd.Next(set.Length)], p);
            placed++;
        }
        return placed;
    }

    /// <summary>「큰 것 하나 + 작은 것 둘셋」 — 큰 것은 뒤(북쪽), 작은 것은 그 앞 · 옆 반원에.</summary>
    private static void HeroGroup(Ctx c, Vector3 at, (string[] Hero, string[] Small) set)
    {
        var q = new Vector2(at.x, at.z);
        if (DressPlan.Classify(c.Map, q) is DressPlan.Spot.Clearing or DressPlan.Spot.Path or DressPlan.Spot.Water)
            return;
        Add(c, set.Hero[c.Rnd.Next(set.Hero.Length)], at + new Vector3(0f, 0f, 0.4f));
        int n = c.Rnd.Next(2, 4);
        for (int i = 0, placed = 0; i < n * 4 && placed < n; i++)
        {
            float a = c.Range(Mathf.PI * 1.05f, Mathf.PI * 1.95f);   // 앞(남쪽) 반원
            float r = c.Range(0.8f, 1.5f);
            var p = at + new Vector3(Mathf.Cos(a) * r * 1.2f, 0f, Mathf.Sin(a) * r * 0.7f);
            var pq = new Vector2(p.x, p.z);
            if (DressPlan.Classify(c.Map, pq) is DressPlan.Spot.Clearing or DressPlan.Spot.Path or DressPlan.Spot.Water || !Free(c, pq, 0.6f))
                continue;
            Add(c, set.Small[c.Rnd.Next(set.Small.Length)], p);
            placed++;
        }
    }

    /// <summary>
    /// 공터 테 — 들어오는 길들의 반대편 반원에만 무리 2 ~ 3개(큰 공터는 4). 입구 쪽은 비워 시원하게.
    /// 이미 덩어리(대숲 · 바위 · 고목)가 둘러싼 쪽은 건너뛴다 — 덩어리가 테 노릇을 한다.
    /// </summary>
    private static void ClearingRims(Ctx c)
    {
        foreach (DressPlan.Clearing cl in DressPlan.Clearings(c.Map))
        {
            // 길이 들어오는 방향들의 평균 → 그 반대가 「등」
            Vector2 entry = Vector2.zero;
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (PathDistance(c.Map.Chapter, cl.Center + dir * (cl.Radius + 2f)) < DressPlan.PathHalfWidth + 0.5f)
                    entry += dir;
            }
            float back = entry.sqrMagnitude > 0.01f ? Mathf.Atan2(-entry.y, -entry.x) : Mathf.PI * 0.5f;   // 길이 없으면 북쪽이 등
            int groups = cl.Big ? 4 : c.Rnd.Next(2, 4);
            float ring = cl.Radius + DressPlan.RimWidth * 0.5f;
            for (int i = 0; i < groups; i++)
            {
                float a = back + (i - (groups - 1) * 0.5f) * (Mathf.PI * 0.85f / Mathf.Max(1, groups - 1)) + c.Range(-0.12f, 0.12f);
                var q = cl.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
                var p = new Vector3(q.x, 0f, q.y);
                if (PathDistance(c.Map.Chapter, q) < DressPlan.PathHalfWidth + 1.6f || c.Map.IsBlocked(p, 0.6f) || Near(c.Map, p, 1.5f) || !Free(c, q, 2.4f))
                    continue;
                HeroGroup(c, p, c.R < 0.12f ? EerieSet : RimSets[c.Rnd.Next(RimSets.Length)]);
            }
        }
    }

    /// <summary>길 굽이 — 꺾이는 점의 바깥쪽에 작은 무리 하나(풀 · 돌). 곧은 길가는 비운다.</summary>
    private static void PathBends(Ctx c)
    {
        foreach (Vector2[] line in Paths(c.Map.Chapter))
            for (int i = 1; i < line.Length - 1; i++)
            {
                Vector2 d0 = (line[i] - line[i - 1]).normalized, d1 = (line[i + 1] - line[i]).normalized;
                if (Vector2.Dot(d0, d1) > 0.94f || c.R < 0.35f)
                    continue;   // 거의 곧은 곳 · 셋에 하나는 비운다
                Vector2 outward = (d0 - d1).normalized;
                Vector2 q = line[i] + outward * c.Range(3.0f, 3.8f);
                var p = new Vector3(q.x, 0f, q.y);
                if (DressPlan.ClearingDistance(c.Map, q, out _) < DressPlan.RimWidth || !Free(c, q, 3f) || c.Map.IsBlocked(p, 0.6f) || Near(c.Map, p, 1.5f))
                    continue;
                HeroGroup(c, p, c.R < 0.5f ? RimSets[0] : RimSets[3]);
            }
    }

    /// <summary>
    /// 숲 (결정 2-148 v3 · 사용자 「조금만 더 자연스럽게」) — 격자에 덩어리를 박지 않고, 노이즈 두 겹으로 「숲 짙기」를 정한다.
    ///   짙기 높음 → 나무(겹쳐 서도 된다) · 가장자리(조금 낮음) → 덤불 · 고사리 · 그보다 낮음 → 풀포기가 드문드문 · 나머지 → 빈 풀밭.
    ///   그래서 숲 모양이 제각각이고, 숲에서 풀밭으로 서서히 성겨진다. 길 · 공터 · 물 · 덩어리 곁은 짙기를 깎는다.
    /// </summary>
    private static void ForestMasses(Ctx c)
    {
        ChapterMap map = c.Map;
        const float Step = 1.9f;
        foreach (ZoneArea area in map.Areas)
            for (float x = area.Bounds.xMin + 2f; x < area.Bounds.xMax - 1f; x += Step)
            for (float z = area.Bounds.yMin + 2f; z < area.Bounds.yMax - 1f; z += Step)
            {
                var q = new Vector2(x + c.Range(-0.8f, 0.8f), z + c.Range(-0.8f, 0.8f));
                var p = new Vector3(q.x, 0f, q.y);
                float dens = Mathf.PerlinNoise(q.x * 0.028f + 31f, q.y * 0.028f + 7f) * 0.7f + Mathf.PerlinNoise(q.x * 0.09f + 3f, q.y * 0.09f + 57f) * 0.3f;
                // 길 · 공터 · 물 · 자리 곁은 숲이 물러난다 (거리만큼 짙기를 깎는다)
                float road = PathDistance(map.Chapter, q), clear = DressPlan.ClearingDistance(map, q, out _), wet = DressPlan.WaterDistance(map, q, out _);
                dens -= Mathf.Clamp01((6f - road) / 6f) * 0.45f + Mathf.Clamp01((5f - clear) / 5f) * 0.45f + Mathf.Clamp01((3f - wet) / 3f) * 0.4f;
                if (road < DressPlan.PathHalfWidth + 0.8f || clear < 0.5f || wet < 1f || map.IsBlocked(p, 0.6f) || Near(map, p, 1.2f) || NearAnchor(map, p, 4f))
                    continue;

                if (dens > 0.55f)
                {
                    if (!Free(c, q, 1.25f)) continue;
                    bool spooky = Mathf.PerlinNoise(q.x * 0.05f + 90f, q.y * 0.05f + 12f) > 0.68f;
                    Add(c, spooky && c.R < 0.5f ? c.Pick(SpookyTrees) : c.Pick(ForestTrees), p);
                }
                else if (dens > 0.48f)
                {
                    if (c.R < 0.55f && Free(c, q, 0.9f))
                        Add(c, c.R < 0.75f ? c.Pick(Under) : "silvergrass", p);
                }
                else if (dens > 0.40f)
                {
                    if (c.R < 0.16f && Free(c, q, 1.2f))
                        Add(c, c.R < 0.7f ? "grass" : c.R < 0.5f ? "flowers" : "rock_moss", p);
                }
            }
    }

    /// <summary>덩어리 가장자리를 깎을지 — 가장자리에서 depth m 안쪽은 노이즈만큼만 남긴다.</summary>
    private static bool Ragged(MapBlock b, Vector3 p, float depth)
    {
        float edge = Mathf.Min(b.Size.x * 0.5f - Mathf.Abs(p.x - b.Center.x), b.Size.z * 0.5f - Mathf.Abs(p.z - b.Center.y));
        float n = Mathf.PerlinNoise(p.x * 0.16f + 5f, p.z * 0.16f + 41f);
        return edge > depth * n;
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
