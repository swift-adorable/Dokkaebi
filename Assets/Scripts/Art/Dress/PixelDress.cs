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
        // 【큰길】 (제로존의 포장길 자리) — 헛간 → 언덕 남쪽 → 냇물 → 고목 앞 갈림
        new Vector2[] { new(-126f, -70f), new(-104f, -62f), new(-80f, -52f), new(-62f, -30f), new(-48f, -8f), new(-30f, -2f), new(-14f, 0f), new(0f, 0f), new(20f, 0f) },
        // 고목 양옆 두 갈래 (금줄 자리) → 0-2 출발
        new Vector2[] { new(20f, 0f), new(26f, -22f), new(48f, -22f), new(64f, 0f) },
        new Vector2[] { new(20f, 0f), new(26f, 22f), new(48f, 22f), new(64f, 0f) },
        // 0-2 큰길 — 동남 길목 · 동북 길목(선물 자리를 지난다)
        new Vector2[] { new(64f, 0f), new(88f, -12f), new(112f, -24f), new(128f, -48f), new(150f, -82f) },
        new Vector2[] { new(64f, 0f), new(92f, 22f), new(120f, 24f), new(132f, 48f), new(150f, 82f) },
        // 방앗간 길 → 북쪽 길목 · 남쪽 길목
        new Vector2[] { new(-14f, 0f), new(-12f, 20f), new(-10f, 44f), new(4f, 78f), new(22f, 96f) },
        new Vector2[] { new(0f, 0f), new(6f, -30f), new(16f, -60f), new(18f, -100f) },
        // 흙길 그물 (제로존 숲길처럼 큰길에서 갈라져 마을 · 줍기 자리로)
        new Vector2[] { new(-104f, -62f), new(-118f, -30f), new(-104f, 0f), new(-96f, 40f), new(-110f, 56f), new(-130f, 70f) },
        new Vector2[] { new(-48f, -8f), new(-56f, 30f), new(-50f, 56f), new(-30f, 58f), new(-10f, 44f) },
        new Vector2[] { new(-80f, -52f), new(-76f, -78f), new(-56f, -80f), new(-24f, -84f), new(12f, -66f) },
        new Vector2[] { new(-126f, -70f), new(-140f, -56f), new(-146f, -30f) },
        new Vector2[] { new(88f, -12f), new(90f, -40f), new(104f, -62f), new(118f, -46f) },
        new Vector2[] { new(92f, 22f), new(84f, 48f), new(78f, 70f) },
        new Vector2[] { new(112f, -24f), new(140f, 4f) },
        new Vector2[] { new(-10f, 44f), new(12f, 50f), new(26f, 64f) },
    };

    /// <summary>길마다 반폭 (m) — 큰길은 넓게(제로존 포장길), 흙길은 기본(DressPlan.PathHalfWidth).</summary>
    public static readonly float[] Chapter0PathHalfWidths = { 3.4f, 2.6f, 2.6f, 3.0f, 3.0f, 2.6f, 2.6f, 1.9f, 1.9f, 1.9f, 1.9f, 1.9f, 1.9f, 1.9f, 1.9f };

    /// <summary>그 장의 길 (미터). 지금은 0장만.</summary>
    public static Vector2[][] Paths(int chapter) => chapter == 0 ? (smooth0 ??= AroundPlots(Smooth(Chapter0Paths), Chapter0PathHalfWidths, LockSiteTable.Plots(0))) : System.Array.Empty<Vector2[]>();

    /// <summary>
    /// 집터 자리(LockSiteTable.Plots) 안을 지나는 길 점을 가장 가까운 변 밖으로 민다 — 길이 집터 담을 따라 돌아간다 (v8 · 덕코프: 건물 자리가 먼저, 길은 그 둘레).
    /// 길 반폭 + 1.5m 를 띄운다. 밀린 점끼리 다시 한 번 부드럽게.
    /// </summary>
    private static Vector2[][] AroundPlots(Vector2[][] lines, float[] halfWidths, IEnumerable<Rect> plots)
    {
        var rects = new List<Rect>(plots);
        if (rects.Count == 0) return lines;
        var result = new Vector2[lines.Length][];
        for (int k = 0; k < lines.Length; k++)
        {
            float m = (k < halfWidths.Length ? halfWidths[k] : 2f) + 1.5f;
            // 점을 밀고, 앞 점과 다른 변으로 밀렸으면 그 사이에 네모 모서리를 끼운다 (모서리를 대각선으로 가로지르지 않게)
            var list = new List<Vector2>();
            int prevRect = -1, prevSide = -1;
            foreach (Vector2 q0 in lines[k])
            {
                Vector2 q = q0;
                int hitRect = -1, side = -1;
                for (int ri = 0; ri < rects.Count; ri++)
                {
                    Rect r0 = rects[ri];
                    Rect r = Rect.MinMaxRect(r0.xMin - m, r0.yMin - m - LockSiteTable.DoorClear, r0.xMax + m, r0.yMax + m);
                    if (!r.Contains(q)) continue;
                    float dl = q.x - r.xMin, dr = r.xMax - q.x, db = q.y - r.yMin, dt = r.yMax - q.y;
                    float best = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
                    side = best == dl ? 0 : best == dr ? 1 : best == db ? 2 : 3;
                    q = side == 0 ? new Vector2(r.xMin, q.y) : side == 1 ? new Vector2(r.xMax, q.y) : side == 2 ? new Vector2(q.x, r.yMin) : new Vector2(q.x, r.yMax);
                    hitRect = ri;
                    if (prevRect == ri && prevSide >= 0 && prevSide != side && (prevSide < 2) != (side < 2))
                    {
                        float cx = (side < 2 ? side : prevSide) == 0 ? r.xMin : r.xMax;
                        float cz = (side >= 2 ? side : prevSide) == 2 ? r.yMin : r.yMax;
                        list.Add(new Vector2(cx, cz));
                    }
                    break;
                }
                list.Add(q);
                prevRect = hitRect; prevSide = side;
            }
            var pts = list.ToArray();
            for (int pass = 0; pass < 2; pass++)   // 모서리를 둥글게
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    Vector2 avg = (pts[i - 1] + pts[i] * 2f + pts[i + 1]) * 0.25f;
                    bool inside = false;
                    foreach (Rect r0 in rects) if (Rect.MinMaxRect(r0.xMin - m + 0.5f, r0.yMin - m + 0.5f - LockSiteTable.DoorClear, r0.xMax + m - 0.5f, r0.yMax + m - 0.5f).Contains(avg)) inside = true;
                    if (!inside) pts[i] = avg;
                }
            result[k] = pts;
        }
        return result;
    }

    private static Vector2[][] smooth0;

    /// <summary>
    /// 꺾은선 길을 부드러운 곡선으로 (결정 2-150 v2 · 사용자 「각지게 만든 부분들을 좀 더 자연스럽게」) — 캐트멀-롬 곡선으로 2m 남짓 간격 점을 다시 찍는다.
    /// 끝점은 그대로라 갈림(Junctions) · 길목 자리는 바뀌지 않는다.
    /// </summary>
    private static Vector2[][] Smooth(Vector2[][] lines)
    {
        var result = new Vector2[lines.Length][];
        for (int k = 0; k < lines.Length; k++)
        {
            Vector2[] l = lines[k];
            var pts = new List<Vector2> { l[0] };
            for (int i = 0; i < l.Length - 1; i++)
            {
                Vector2 p0 = l[Mathf.Max(0, i - 1)], p1 = l[i], p2 = l[i + 1], p3 = l[Mathf.Min(l.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 2f));
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result[k] = pts.ToArray();
        }
        return result;
    }

    /// <summary>길까지의 거리 (m).</summary>
    public static float PathDistance(int chapter, Vector2 q)
    {
        float best = float.MaxValue;
        Vector2[][] paths = Paths(chapter);
        for (int k = 0; k < paths.Length; k++)
        {
            Vector2[] line = paths[k];
            // 넓은 길은 그만큼 가깝게 친다 — 쓰는 쪽은 모두 DressPlan.PathHalfWidth 를 기준으로 본다 (결정 2-150)
            float extra = chapter == 0 && k < Chapter0PathHalfWidths.Length ? Chapter0PathHalfWidths[k] - DressPlan.PathHalfWidth : 0f;
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = line[i - 1], ab = line[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t) - extra);
            }
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
        // 【막힌 곳 테두리】 (결정 2-150 · 사용자 「대나무 그냥 여러개 박아두고 접근 못하는게 너무 성의 없어 … 벽을 세우던가 길과 길이 아닌 곳을 한 눈에」)
        // 테두리를 먼저 세워 자리를 잡는다 — 덩어리 속 나무 · 대나무는 그 안쪽에만 선다.
        foreach (MapBlock b in map.Blocks)
            Border(c, b);
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
                // 안은 빽빽하게 (막힌 곳과 그림이 어긋나지 않게), 밖으로 4m 까지 성기게 번져 네모 윤곽을 지운다 (v3 · 사용자 「대나무 숲은 좀 더 자연스럽게」)
                Blob(c, b, 6f, 1.6f, new[] { BambooClump }, new[] { "fern", "grass" });
                break;
            case MapBlockKind.Boundary:
                // 남쪽 담 앞 큰 나무는 북쪽 땅(걷는 곳)을 가린다 — 남쪽은 낮은 덤불만.
                bool south = b.Center.y < 0f && b.Size.x > b.Size.z;
                Fill(c, b, south ? 1.8f : 2.2f, p => Add(c, south ? c.Pick(Under) : c.Pick(ForestTrees), p));
                break;
            case MapBlockKind.Rock:
                // 바위 벽 — 큰 바위를 겹쳐 세우고 발치에만 잔돌 · 고사리
                Add(c, BigRocks[1], center + new Vector3(0f, 0f, b.Size.z * 0.15f));
                Blob(c, b, 5f, 2.0f, new[] { "rock_big", "rock_big", "rock_pillar", "tree_pine" }, new[] { "rock_pile", "rock_moss", "fern" });
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
                Blob(c, b, 6f, 2.2f, ForestTrees, Under);   // 고목 숲도 노이즈 윤곽 (네모 · 위아래 울타리 → 자연스러운 숲)
                // 발치 — 앞쪽 반원에만 꽃무릇 · 덤불 두 무리
                Group(c, center + new Vector3(-b.Size.x * 0.4f, 0f, -b.Size.z * 0.5f), 0.8f, 3, new[] { "lily", "bush" });
                Group(c, center + new Vector3(b.Size.x * 0.4f, 0f, -b.Size.z * 0.5f), 0.8f, 2, new[] { "fern", "mushrooms" });
                break;
            case MapBlockKind.Fence:
                Fill(c, b, 1.6f, p => Add(c, "bush", p));
                break;
            case MapBlockKind.Forest:
                // 숲 덩어리 (결정 2-150 · 제로존) — 나무를 겹쳐 빽빽하게, 가장자리는 들쭉날쭉.
                // 앞(남) · 옆 가장자리는 덤불 · 고사리 한 줄 — 숲이 땅에 앉고, 여기부터 못 간다는 게 보이게.
                Blob(c, b, 7f, 2.0f, ForestTrees, Under);
                Edge(c, b, 1.5f, -0.6f, p =>
                {
                    if (p.z > b.Center.y + b.Size.z * 0.5f - 1.2f) return;   // 뒤(북) 변은 나무가 가린다
                    if (c.R < 0.8f && Free(c, new Vector2(p.x, p.z), 0.7f)) Add(c, c.R < 0.65f ? c.Pick(Under) : "fern", p);
                });
                break;
            case MapBlockKind.Bush:
                Fill(c, b, 1.5f, p => Add(c, c.R < 0.15f ? "silvergrass" : c.Pick(Under), p));
                break;
            case MapBlockKind.Building:
                // 집 그림 (결정 2-150 — 헛간 · 방앗간이 회색 상자로 남아 있었다). 넓적하면 헛간, 아니면 방앗간(물레방아).
                // 0장은 그림 대신 들어가지 못하는 3D 한옥(ClosedHanok)이 선다 (2026-11-20 사용자 「이미지로 넣은 한옥도 직접 만들어 · 들어가지 못하게」)
                if (c.Map.Chapter != 0)
                    AddFixed(c, b.Size.x > b.Size.z * 1.5f ? "story_barn" : "story_mill", new Vector3(b.Center.x, 0f, b.Center.y - b.Size.z * 0.5f - 0.2f));
                // 집 둘레 살림 — 장독은 벽에 붙여 무리로, 장작은 문 옆, 수레 · 광주리는 마당 한쪽.
                Group(c, center + new Vector3(-b.Size.x * 0.5f - 1.0f, 0f, b.Size.z * 0.1f), 0.7f, c.Rnd.Next(2, 4), Jars);
                Group(c, center + new Vector3(b.Size.x * 0.5f + 1.1f, 0f, -b.Size.z * 0.35f), 0.6f, 2, Woodpile);
                Group(c, center + new Vector3(b.Size.x * 0.3f, 0f, -b.Size.z * 0.5f - 2.4f), 0.9f, 2, Yard);
                break;
        }
    }

    /// <summary>
    /// 막힌 덩어리의 테두리 (결정 2-150) — 참고 장면은 걷지 못하는 곳을 늘 벽 · 울타리 · 절벽으로 두른다.
    ///   · 남 · 북 변(카메라와 마주 보는 변): 담 그림 토막을 끊김 없이 잇는다 — 대숲은 대울타리, 바위는 절벽, 고목 · 수풀은 싸리 울타리, 맵 가장자리는 돌담
    ///   · 동 · 서 변: 담 토막을 세우면 막대처럼 보여(결정 2-143 1차) 큰 바위 · 돌무더기를 촘촘히 줄 세운다
    ///   · 길이 덩어리 곁을 지나도 테두리는 덩어리 위에만 선다(덩어리는 원래 막혀 있다)
    /// </summary>
    private static void Border(Ctx c, MapBlock b)
    {
        string wall = b.Kind switch
        {
            MapBlockKind.Bamboo => null,   // 대숲은 울타리로 두르지 않는다 — 네모 액자처럼 보였다 (v3). 대가 덩어리 밖으로 번져 나간다(Spill)
            MapBlockKind.Rock => null,   // 바위 언덕도 네모 절벽 테 대신 노이즈 윤곽 (v4)
            MapBlockKind.Fence => "wall_brush",
            MapBlockKind.Boundary => null,   // v8 · 사용자 「하단 쪽에 벽 타일이 이상하게 배치」 — 맵 끝은 빽빽한 숲(OuterForest)이 감싼다. 돌담 줄은 뺀다
            _ => null,
        };
        if (wall == null || !rects.ContainsKey(wall))
            return;

        float hx = b.Size.x * 0.5f, hz = b.Size.z * 0.5f;
        float seg = rects[wall].w / PpmOf(wall);   // 무늬 주기가 토막 폭을 나눠 떨어져 그대로 이으면 이음매가 없다 (make_walls.py)
        bool horizontal = b.Size.x >= b.Size.z;
        if (b.Kind == MapBlockKind.Boundary)
        {
            // 맵 가장자리 담 — 긴 변 하나뿐. 남쪽 담은 카메라 앞이라 낮은 돌담, 나머지는 바위 줄.
            if (horizontal)
                Run(c, wall, new Vector2(b.Center.x - hx, b.Center.y), new Vector2(b.Center.x + hx, b.Center.y), seg);
            else
                Rocks(c, new Vector2(b.Center.x, b.Center.y - hz), new Vector2(b.Center.x, b.Center.y + hz));
            return;
        }
        if (b.Kind == MapBlockKind.Fence && !horizontal)
        {
            Rocks(c, new Vector2(b.Center.x, b.Center.y - hz), new Vector2(b.Center.x, b.Center.y + hz));
            return;
        }
        // 남 · 북 변은 담, 동 · 서 변은 바위 줄 — 담은 덩어리 안쪽으로 0.3m 들여 세운다
        Run(c, wall, new Vector2(b.Center.x - hx, b.Center.y - hz + 0.3f), new Vector2(b.Center.x + hx, b.Center.y - hz + 0.3f), seg);
        Run(c, wall, new Vector2(b.Center.x - hx, b.Center.y + hz - 0.3f), new Vector2(b.Center.x + hx, b.Center.y + hz - 0.3f), seg);
        if (b.Kind == MapBlockKind.Rock)   // 바위 덩어리만 옆면을 바위 줄로 — 대숲 · 수풀 옆면은 덩어리 안의 대 · 덤불이 가장자리까지 차서 충분하다 (v2: 점선처럼 보였다)
        {
            Rocks(c, new Vector2(b.Center.x - hx + 0.4f, b.Center.y - hz + 1.2f), new Vector2(b.Center.x - hx + 0.4f, b.Center.y + hz - 1.2f));
            Rocks(c, new Vector2(b.Center.x + hx - 0.4f, b.Center.y - hz + 1.2f), new Vector2(b.Center.x + hx - 0.4f, b.Center.y + hz - 1.2f));
        }
    }

    /// <summary>a → b 를 담 토막으로 잇는다 (양 끝은 토막 반만큼 안쪽에서 시작).</summary>
    private static void Run(Ctx c, string wall, Vector2 a, Vector2 b, float seg)
    {
        float len = Vector2.Distance(a, b);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / seg));
        for (int i = 0; i < n; i++)
        {
            Vector2 q = Vector2.Lerp(a, b, (i + 0.5f) / n);
            AddFixed(c, wall, new Vector3(q.x, 0f, q.y));
        }
    }

    /// <summary>a → b 를 바위 줄로 — 큰 바위와 돌무더기를 번갈아 1.3m 마다.</summary>
    private static void Rocks(Ctx c, Vector2 a, Vector2 b)
    {
        float len = Vector2.Distance(a, b);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 1.3f));
        for (int i = 0; i <= n; i++)
        {
            Vector2 q = Vector2.Lerp(a, b, i / (float)n) + new Vector2(c.Range(-0.25f, 0.25f), 0f);
            AddFixed(c, i % 3 == 2 ? "rock_pile" : "rock_big", new Vector3(q.x, 0f, q.y));
        }
    }

    /// <summary>집 자리 검사 없이 · 뒤집기 없이 세운다 — 담 토막은 이어져야 해서 좌우를 뒤집지 않는다.</summary>
    private static void AddFixed(Ctx c, string sprite, Vector3 p)
    {
        sprite = Variant(c, sprite);
        if (!rects.ContainsKey(sprite))
            return;
        c.Cards.Add((sprite, p, PpmOf(sprite), false));
        c.Taken.Add(new Vector2(p.x, p.z));
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
        Add(c, Waymark, new Vector3(-17.5f, 0f, 3.5f));   // 방앗간 갈림
        Add(c, Waymark, new Vector3(16f, 0f, 4.5f));      // 고목 앞 두 갈래
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
                // 【제로존처럼】 (결정 2-150) 길 · 마을에서 멀어질수록 숲이 짙어진다 — 길이 숲을 가르고 지나가는 모습. 12m 밖부터 차오른다
                dens += Mathf.Clamp01((PathDistance(map.Chapter, q) - 12f) / 14f) * 0.35f;
                // 길 · 공터 · 물 · 자리 곁은 숲이 물러난다 (거리만큼 짙기를 깎는다)
                float road = PathDistance(map.Chapter, q), clear = DressPlan.ClearingDistance(map, q, out _), wet = DressPlan.WaterDistance(map, q, out _);
                dens -= Mathf.Clamp01((6f - road) / 6f) * 0.45f + Mathf.Clamp01((5f - clear) / 5f) * 0.45f + Mathf.Clamp01((3f - wet) / 3f) * 0.4f;
                if (road < DressPlan.PathHalfWidth + 0.8f || clear < 0.5f || wet < 1f || map.IsBlocked(p, 0.6f) || Near(map, p, 1.2f) || NearAnchor(map, p, 4f))
                    continue;

                if (dens > 0.62f)
                {
                    if (!Free(c, q, 1.25f)) continue;
                    bool spooky = Mathf.PerlinNoise(q.x * 0.05f + 90f, q.y * 0.05f + 12f) > 0.68f;
                    Add(c, spooky && c.R < 0.5f ? c.Pick(SpookyTrees) : c.Pick(ForestTrees), p);
                }
                else if (dens > 0.55f)
                {
                    if (c.R < 0.55f && Free(c, q, 0.9f))
                        Add(c, c.R < 0.75f ? c.Pick(Under) : "silvergrass", p);
                }
                else if (dens > 0.47f)
                {
                    if (c.R < 0.16f && Free(c, q, 1.2f))
                        Add(c, c.R < 0.7f ? "grass" : c.R < 0.5f ? "flowers" : "rock_moss", p);
                }
            }
    }

    /// <summary>
    /// 【덩어리를 노이즈 윤곽으로】 (v4 · 사용자 「대나무 숲은 좀 더 자연스럽게」) — 네모 덩어리를 그대로 채우지 않고,
    /// 가장자리에서 안팎 reach m 를 노이즈가 정한 윤곽으로 채운다. 깊은 안쪽(2m 넘게)은 늘 채워 막힌 곳과 그림이 크게 어긋나지 않는다.
    /// 윤곽 바로 바깥 띠에는 덤불 · 풀(small)이 드문드문 — 숲이 땅에 스며든다.
    /// </summary>
    private static void Blob(Ctx c, MapBlock b, float reach, float spacing, string[] big, string[] small)
    {
        float hx = b.Size.x * 0.5f + reach, hz = b.Size.z * 0.5f + reach;
        for (float x = -hx; x <= hx; x += spacing)
        for (float z = -hz; z <= hz; z += spacing)
        {
            var p = new Vector3(b.Center.x + x + c.Range(-0.35f, 0.35f) * spacing, 0f, b.Center.y + z + c.Range(-0.35f, 0.35f) * spacing);
            float sd = Mathf.Max(Mathf.Abs(p.x - b.Center.x) - b.Size.x * 0.5f, Mathf.Abs(p.z - b.Center.y) - b.Size.z * 0.5f);   // 안이면 음수
            float n = Mathf.PerlinNoise(p.x * 0.09f + 23f, p.z * 0.09f + 61f) * 0.7f + Mathf.PerlinNoise(p.x * 0.3f + 2f, p.z * 0.3f + 8f) * 0.3f;
            float edge = (n - 0.5f) * reach * 3.4f;   // 윤곽이 바깥으로 · 안으로 몇 m 씩 흔들린다 (v4: ±2m 는 여전히 벽처럼 보여 ±4m 넘게)
            var q = new Vector2(p.x, p.z);
            if (sd < -2f || sd < edge)
            {
                if (sd >= 0f)   // 덩어리 밖으로 번진 대 — 길 · 공터 · 물 위엔 세우지 않는다
                {
                    DressPlan.Spot spot = DressPlan.Classify(c.Map, q);
                    if (spot is DressPlan.Spot.Clearing or DressPlan.Spot.Path or DressPlan.Spot.Water || c.Map.IsBlocked(p, 0.3f)) continue;
                }
                Add(c, big[c.Rnd.Next(big.Length)], p);
            }
            else if (sd < edge + 2.5f && c.R < 0.35f && Free(c, q, 0.9f))
            {
                DressPlan.Spot spot = DressPlan.Classify(c.Map, q);
                if (spot is DressPlan.Spot.Wild or DressPlan.Spot.Rim or DressPlan.Spot.PathEdge)
                    Add(c, small[c.Rnd.Next(small.Length)], p);
            }
        }
    }

    /// <summary>
    /// 덩어리 밖으로 번지기 — 가장자리에서 depth m 까지, 멀어질수록 · 노이즈가 낮을수록 드물게. 가까운 곳은 big, 먼 곳은 small(덤불 · 풀).
    /// 덩어리 자체는 반듯한 네모(길찾기 · 부딪힘)라도 그림의 윤곽은 들쭉날쭉해진다.
    /// </summary>
    private static void Spill(Ctx c, MapBlock b, float depth, float spacing, System.Action<Vector3> big, System.Action<Vector3> small, float strength = 0.4f)
    {
        float hx = b.Size.x * 0.5f + depth, hz = b.Size.z * 0.5f + depth;
        for (float x = -hx; x <= hx; x += spacing)
        for (float z = -hz; z <= hz; z += spacing)
        {
            var p = new Vector3(b.Center.x + x + c.Range(-0.4f, 0.4f) * spacing, 0f, b.Center.y + z + c.Range(-0.4f, 0.4f) * spacing);
            float dx = Mathf.Abs(p.x - b.Center.x) - b.Size.x * 0.5f, dz = Mathf.Abs(p.z - b.Center.y) - b.Size.z * 0.5f;
            float out_ = Mathf.Max(dx, dz);
            if (out_ <= 0f || out_ > depth) continue;
            var q = new Vector2(p.x, p.z);
            DressPlan.Spot spot = DressPlan.Classify(c.Map, q);
            if (spot is DressPlan.Spot.Clearing or DressPlan.Spot.Path or DressPlan.Spot.Water || c.Map.IsBlocked(p, 0.3f))
                continue;
            float n = Mathf.PerlinNoise(p.x * 0.13f + 17f, p.z * 0.13f + 29f);
            float keep = (1f - out_ / depth) * (strength + n);
            if (c.R > keep) continue;
            if (out_ < depth * 0.55f && Free(c, q, spacing * 0.6f)) big(p);
            else if (Free(c, q, 0.8f)) small(p);
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

    /// <summary>굽기용 — 지금 아틀라스 · 재질 (없으면 만든다).</summary>
    public static (Texture2D atlas, Material material) AtlasForBake() { EnsureAtlas(); return (atlas, material); }

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
        atlas.Apply(false, Application.isPlaying);   // 플레이 중엔 CPU 쪽 사본을 버린다 — 에디터에선 굽기가 PNG 로 저장한다
        foreach (Texture2D t in all)
            Resources.UnloadAsset(t);

        material = new Material(BuildingSprite.Material()) { name = "WildAtlas" };
        material.SetTexture("_BaseMap", atlas);
        material.SetTexture("_MainTex", atlas);
        return true;
    }

    /// <summary>마지막 Build 의 카드(그림 · 자리 · 칸 밀도) — 굽기가 나무 밑동 막이를 세운다.</summary>
    public static List<(string sprite, Vector3 pos, float ppm)> LastCards { get; } = new();

    /// <summary>걸음을 막는 그림 — 나무 · 대 · 큰 바위 · 장승 (v8 · 사용자 「갈 수 있는 곳과 없는 곳의 차이를 잘 모르겠다」 → 나무는 모두 막는다).</summary>
    public static bool Blocks(string sprite) => sprite.StartsWith("tree_") || sprite.StartsWith("bamboo") || sprite.StartsWith("rock_big") || sprite.StartsWith("rock_pillar") || sprite == "jangseung";

    /// <summary>그림 폭(m).</summary>
    public static float WidthOf(string sprite, float ppm) => rects.TryGetValue(sprite, out var r) ? r.w / ppm : 1f;

    private static int Emit(Ctx c, Transform parent)
    {
        LastCards.Clear();
        foreach (var (sprite, pos, ppm, _) in c.Cards) LastCards.Add((sprite, pos, ppm));
        var groups = new Dictionary<(int, int), (List<Vector3> v, List<Vector2> uv, List<Vector3> n, List<int> t)>();
        Quaternion tilt = ArtCatalog.CardTilt;
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
            if (Application.isPlaying) mesh.UploadMeshData(true);
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
