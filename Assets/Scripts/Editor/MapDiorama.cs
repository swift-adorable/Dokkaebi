using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【지도 사진용 모형】 (결정 2-122) — 장 맵 표(ZoneMapTable)로 작은 모형을 세워 위에서 찍는다.
/// 참고 그림(2026-10-13 사용자 · 위에서 본 3D 장면)처럼 보이게 — 풀밭 · 흙길 · 둥근 나무 덩이 · 지붕 · 냇물 · 장 밖의 빽빽한 숲.
/// 지금 씬은 그레이박스라 그대로 찍으면 색 판만 보인다. 아트가 들어오면 MapPhotoBaker가 이 모형 대신 씬을 찍게 바꾼다.
///
///   · 모두 전용 층(Layer)에만 — 카메라는 이 층만 찍는다. 씬의 것(플레이어 · 금줄 · UI)은 섞이지 않는다
///   · 같은 씨앗이면 늘 같은 그림 (장 번호가 씨앗)
///   · 다 찍으면 Dispose로 전부 지운다 — 씬에 남지 않는다
/// </summary>
public sealed class MapDiorama : System.IDisposable
{
    /// <summary>모형이 쓰는 층 — 프로젝트가 쓰지 않는 마지막 칸.</summary>
    public const int Layer = 31;

    // ── 빛깔 (참고 그림의 밝은 여름 숲) ────────────────────────────────
    private static readonly Color Grass = new(0.50f, 0.64f, 0.32f);
    private static readonly Color GrassDark = new(0.42f, 0.56f, 0.28f);
    private static readonly Color Dirt = new(0.72f, 0.60f, 0.44f);
    private static readonly Color ForestFloor = new(0.20f, 0.30f, 0.17f);
    private static readonly Color Water = new(0.27f, 0.42f, 0.60f);
    private static readonly Color Wall = new(0.62f, 0.52f, 0.40f);
    private static readonly Color[] Roofs = { new(0.46f, 0.30f, 0.22f), new(0.36f, 0.36f, 0.38f), new(0.55f, 0.42f, 0.26f) };
    private static readonly Color[] Leaves =
    {
        new(0.30f, 0.48f, 0.22f), new(0.36f, 0.55f, 0.25f), new(0.42f, 0.60f, 0.28f), new(0.26f, 0.42f, 0.20f),
    };
    private static readonly Color[] DeepLeaves = { new(0.18f, 0.32f, 0.16f), new(0.22f, 0.36f, 0.18f), new(0.16f, 0.28f, 0.15f) };
    private static readonly Color Bamboo = new(0.48f, 0.64f, 0.30f);
    private static readonly Color Rock = new(0.55f, 0.54f, 0.50f);
    private static readonly Color Wood = new(0.44f, 0.32f, 0.20f);
    private static readonly Color Cloth = new(0.70f, 0.58f, 0.40f);
    private static readonly Color[] Flowers = { new(0.88f, 0.48f, 0.62f), new(0.95f, 0.82f, 0.40f), new(0.92f, 0.92f, 0.86f) };
    private static readonly Color Rope = new(0.94f, 0.86f, 0.52f);

    private readonly ChapterMap map;
    private readonly System.Random random;
    private readonly GameObject root;
    private readonly Dictionary<Color, Material> materials = new();
    private readonly List<Object> owned = new();
    private readonly Mesh sphere;
    private readonly Mesh cube;
    private readonly Mesh cylinder;
    private readonly Mesh quad;

    public MapDiorama(ChapterMap map)
    {
        this.map = map;
        random = new System.Random(1000 + map.Chapter);
        root = new GameObject("MapDiorama (bake)") { hideFlags = HideFlags.HideAndDontSave };
        root.layer = Layer;

        sphere = MeshOf(PrimitiveType.Sphere);
        cube = MeshOf(PrimitiveType.Cube);
        cylinder = MeshOf(PrimitiveType.Cylinder);
        quad = MeshOf(PrimitiveType.Quad);

        BuildGround();
        foreach (MapBlock block in map.Blocks)
            BuildBlock(block);
        BuildOuterForest();
    }

    public void Dispose()
    {
        Object.DestroyImmediate(root);
        foreach (Object o in owned)
            if (o != null)
                Object.DestroyImmediate(o);
        foreach (Material m in materials.Values)
            if (m != null)
                Object.DestroyImmediate(m);
    }

    // ── 땅 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 바닥 그림 — 구역 땅은 풀밭(얼룩), 집 · 좌판 둘레와 구역을 잇는 길은 흙, 장 밖은 짙은 숲 바닥. 1m 2화소.
    /// </summary>
    private void BuildGround()
    {
        const int ppm = 2;
        int w = MapRaster.WidthOf(map, ppm), h = MapRaster.HeightOf(map, ppm);
        var pixels = new Color[w * h];
        List<(Vector2 a, Vector2 b)> roads = Roads();

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector3 p = MapRaster.WorldOf(map, x, y, ppm);
            float n = Mathf.PerlinNoise(p.x * 0.08f + 31f, p.z * 0.08f + 17f);
            float fine = Mathf.PerlinNoise(p.x * 0.35f + 5f, p.z * 0.35f + 9f);

            if (map.ZoneAt(p) == null)
            {
                pixels[y * w + x] = Color.Lerp(ForestFloor, ForestFloor * 1.25f, n);
                continue;
            }

            Color grass = Color.Lerp(GrassDark, Grass, n * 0.8f + fine * 0.2f);
            float dirt = Mathf.Max(YardAmount(p), RoadAmount(roads, p));
            dirt = Mathf.Clamp01(dirt + (fine - 0.5f) * 0.5f);
            Color dirtColor = Color.Lerp(Dirt * 0.92f, Dirt, fine);
            pixels[y * w + x] = Color.Lerp(grass, dirtColor, Mathf.SmoothStep(0.35f, 0.65f, dirt));
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.SetPixels(pixels);
        tex.Apply(false);
        owned.Add(tex);

        Material ground = NewMaterial(Color.white);
        ground.SetTexture("_BaseMap", tex);
        ground.mainTexture = tex;
        owned.Add(ground);

        GameObject plane = Piece("Ground", quad, ground, new Vector3(0f, 0f, 0f), new Vector3(map.HalfSize.x * 2f, map.HalfSize.y * 2f, 1f));
        plane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    /// <summary>집 · 좌판 · 우물 · 기둥 둘레의 흙마당 (0 ~ 1).</summary>
    private float YardAmount(Vector3 p)
    {
        float best = 0f;
        foreach (MapBlock b in map.Blocks)
        {
            if (b.Kind is not (MapBlockKind.Building or MapBlockKind.Stall or MapBlockKind.Well or MapBlockKind.Post))
                continue;

            float dx = Mathf.Max(0f, Mathf.Abs(p.x - b.Center.x) - b.Size.x * 0.5f);
            float dz = Mathf.Max(0f, Mathf.Abs(p.z - b.Center.y) - b.Size.z * 0.5f);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            best = Mathf.Max(best, 1f - Mathf.InverseLerp(2.5f, 6f, d));
        }

        return best;
    }

    /// <summary>길 — 구역 땅의 가운데와, 그 땅에 닿은 금줄(구역 사이 문)을 잇는다.</summary>
    private List<(Vector2, Vector2)> Roads()
    {
        var roads = new List<(Vector2, Vector2)>();

        foreach (MapGate gate in map.Gates)
        {
            Vector2 g = gate.Block.Center;
            foreach (ZoneArea area in map.Areas)
            {
                Rect r = area.Bounds;
                Rect grown = Rect.MinMaxRect(r.xMin - 3f, r.yMin - 3f, r.xMax + 3f, r.yMax + 3f);
                if (grown.Contains(g))
                    roads.Add((g, r.center));
            }
        }

        // 금줄이 없는 장은 땅들의 가운데를 차례로 잇는다.
        if (roads.Count == 0)
            for (int i = 1; i < map.Areas.Length; i++)
                roads.Add((map.Areas[i - 1].Bounds.center, map.Areas[i].Bounds.center));

        return roads;
    }

    private static float RoadAmount(List<(Vector2 a, Vector2 b)> roads, Vector3 p)
    {
        var q = new Vector2(p.x, p.z);
        float best = 0f;

        foreach ((Vector2 a, Vector2 b) in roads)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            float d = Vector2.Distance(q, a + ab * t);
            best = Mathf.Max(best, 1f - Mathf.InverseLerp(2.2f, 3.6f, d));
        }

        return best;
    }

    // ── 덩어리 ────────────────────────────────────────────────────────

    private void BuildBlock(MapBlock b)
    {
        var c = new Vector3(b.Center.x, 0f, b.Center.y);
        float h = Mathf.Max(0.1f, b.Size.y);

        switch (b.Kind)
        {
            case MapBlockKind.Bamboo:
                Scatter(b, 1.9f, 1.1f, 1.7f, Mathf.Min(h, 6f) * 0.7f, Leaves, Bamboo);
                break;

            case MapBlockKind.Bush:
                Scatter(b, 1.3f, 0.7f, 1.1f, 0.7f, Leaves, null);
                break;

            case MapBlockKind.Boundary:
                Scatter(b, 2.6f, 1.6f, 2.4f, 3f, DeepLeaves, null);
                break;

            case MapBlockKind.Tree:
            {
                float r = Mathf.Max(b.Size.x, b.Size.z) * 0.5f;
                Piece("Trunk", cylinder, Mat(Wood), c + Vector3.up * 2f, new Vector3(r * 0.5f, 2f, r * 0.5f));
                Piece("Crown", sphere, Mat(DeepLeaves[0]), c + Vector3.up * 5f, new Vector3(r * 4.2f, r * 2.6f, r * 4.2f));
                Piece("Crown2", sphere, Mat(Leaves[0]), c + new Vector3(r * 0.8f, 5.6f, r * 0.6f), new Vector3(r * 2.6f, r * 1.8f, r * 2.6f));
                break;
            }

            case MapBlockKind.Building:
                House(b, c, h);
                break;

            case MapBlockKind.Rock:
            {
                // 둥근 바위 · 언덕은 덩이로, 길게 늘인 것(둑 · 바위 담)은 모난 돌담으로.
                float aspect = Mathf.Max(b.Size.x, b.Size.z) / Mathf.Max(0.1f, Mathf.Min(b.Size.x, b.Size.z));
                if (aspect > 2.5f)
                    Piece("RockWall", cube, Mat(Rock * 0.9f), c + Vector3.up * Mathf.Min(h, 2f) * 0.5f,
                        new Vector3(b.Size.x, Mathf.Min(h, 2f), b.Size.z));
                else
                    Piece("Rock", sphere, Mat(Rock), c + Vector3.up * h * 0.35f, new Vector3(b.Size.x, h * 0.9f, b.Size.z));
                break;
            }

            case MapBlockKind.Water:
                Piece("Water", cube, Mat(Water), c + Vector3.up * 0.03f, new Vector3(b.Size.x, 0.06f, b.Size.z));
                break;

            case MapBlockKind.Fence:
                Piece("Fence", cube, Mat(Wood), c + Vector3.up * h * 0.5f, new Vector3(b.Size.x, h, b.Size.z));
                break;

            case MapBlockKind.Stall:
                Piece("Stall", cube, Mat(Wood), c + Vector3.up * h * 0.5f, new Vector3(b.Size.x, h, b.Size.z));
                Piece("Awning", cube, Mat(Cloth), c + Vector3.up * (h + 0.6f), new Vector3(b.Size.x * 1.1f, 0.12f, b.Size.z * 1.2f));
                break;

            case MapBlockKind.Well:
                Piece("Well", cylinder, Mat(Rock), c + Vector3.up * 0.4f, new Vector3(b.Size.x, 0.4f, b.Size.z));
                Piece("WellWater", cylinder, Mat(Water * 0.7f), c + Vector3.up * 0.82f, new Vector3(b.Size.x * 0.65f, 0.02f, b.Size.z * 0.65f));
                break;

            case MapBlockKind.Post:
                Piece("Post", cube, Mat(Wood * 1.2f), c + Vector3.up * h * 0.5f, new Vector3(b.Size.x, h, b.Size.z));
                break;

            case MapBlockKind.Flowerbed:
                Flowerbed(b);
                break;

            case MapBlockKind.Geumjul:
                Piece("Rope", cube, Mat(Rope), c + Vector3.up * 0.8f, new Vector3(Mathf.Max(b.Size.x, 0.3f), 0.15f, Mathf.Max(b.Size.z, 0.3f)));
                break;

            default:
                Piece(b.Kind.ToString(), cube, Mat(Rock), c + Vector3.up * h * 0.5f, new Vector3(b.Size.x, h, b.Size.z));
                break;
        }
    }

    /// <summary>집 — 벽 상자 위에 처마가 나온 지붕 · 가운데 용마루(밝은 줄). 지붕 빛은 집마다 셋 중 하나.</summary>
    private void House(MapBlock b, Vector3 c, float h)
    {
        float wallH = Mathf.Min(h, 3.2f);
        Piece("Wall", cube, Mat(Wall), c + Vector3.up * wallH * 0.5f, new Vector3(b.Size.x * 0.92f, wallH, b.Size.z * 0.92f));

        Color roof = Roofs[random.Next(Roofs.Length)];
        bool alongX = b.Size.x >= b.Size.z;
        Piece("Roof", cube, Mat(roof), c + Vector3.up * (wallH + 0.35f), new Vector3(b.Size.x * 1.04f, 0.7f, b.Size.z * 1.04f));

        Vector3 ridge = alongX ? new Vector3(b.Size.x * 1.04f, 0.3f, 0.5f) : new Vector3(0.5f, 0.3f, b.Size.z * 1.04f);
        Piece("Ridge", cube, Mat(roof * 1.35f), c + Vector3.up * (wallH + 0.85f), ridge);
    }

    private void Flowerbed(MapBlock b)
    {
        Piece("Bed", cube, Mat(Grass * 1.1f), new Vector3(b.Center.x, 0.02f, b.Center.y), new Vector3(b.Size.x, 0.04f, b.Size.z));

        int count = Mathf.Clamp(Mathf.RoundToInt(b.Size.x * b.Size.z * 0.8f), 4, 600);
        for (int i = 0; i < count; i++)
        {
            float x = b.Center.x + ((float)random.NextDouble() - 0.5f) * b.Size.x;
            float z = b.Center.y + ((float)random.NextDouble() - 0.5f) * b.Size.z;
            float r = 0.25f + (float)random.NextDouble() * 0.2f;
            Piece("Flower", sphere, Mat(Flowers[random.Next(Flowers.Length)]), new Vector3(x, 0.2f, z), new Vector3(r, r, r));
        }
    }

    /// <summary>둥근 나무 덩이를 덩어리 바닥에 흩뿌린다 — 간격 spacing, 반지름 minR ~ maxR.</summary>
    private void Scatter(MapBlock b, float spacing, float minR, float maxR, float height, Color[] palette, Color? stem)
    {
        int nx = Mathf.Max(1, Mathf.RoundToInt(b.Size.x / spacing));
        int nz = Mathf.Max(1, Mathf.RoundToInt(b.Size.z / spacing));

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            float x = b.Center.x - b.Size.x * 0.5f + (ix + 0.5f) * b.Size.x / nx + Jitter(spacing * 0.3f);
            float z = b.Center.y - b.Size.z * 0.5f + (iz + 0.5f) * b.Size.z / nz + Jitter(spacing * 0.3f);
            float r = Mathf.Lerp(minR, maxR, (float)random.NextDouble());
            float y = height * (0.75f + (float)random.NextDouble() * 0.5f);

            if (stem.HasValue)
                Piece("Stem", cylinder, Mat(stem.Value), new Vector3(x, y * 0.5f, z), new Vector3(0.25f, y * 0.5f, 0.25f));

            Piece("Leaf", sphere, Mat(palette[random.Next(palette.Length)]), new Vector3(x, y, z), new Vector3(r * 2f, r * 1.5f, r * 2f));
        }
    }

    /// <summary>장 밖(끊긴 밤길) — 빽빽한 짙은 숲. 참고 그림의 섬 둘레처럼.</summary>
    private void BuildOuterForest()
    {
        const float spacing = 3.2f;
        for (float x = -map.HalfSize.x; x <= map.HalfSize.x; x += spacing)
        for (float z = -map.HalfSize.y; z <= map.HalfSize.y; z += spacing)
        {
            var p = new Vector3(x + Jitter(1.2f), 0f, z + Jitter(1.2f));
            if (map.ZoneAt(p) != null)
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

            float r = 1.6f + (float)random.NextDouble() * 1.2f;
            Piece("Forest", sphere, Mat(DeepLeaves[random.Next(DeepLeaves.Length)]),
                new Vector3(p.x, 2.5f + (float)random.NextDouble() * 1.5f, p.z), new Vector3(r * 2f, r * 1.4f, r * 2f));
        }
    }

    // ── 도구 ──────────────────────────────────────────────────────────

    private float Jitter(float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;

    private GameObject Piece(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    private Material Mat(Color color)
    {
        color.a = 1f;
        if (materials.TryGetValue(color, out Material m))
            return m;

        m = NewMaterial(color);
        materials[color] = m;
        return m;
    }

    private static Material NewMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = color };
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", 0.05f);
        return m;
    }

    private Mesh MeshOf(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return mesh;
    }
}
