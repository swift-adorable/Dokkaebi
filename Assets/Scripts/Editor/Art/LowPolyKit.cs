using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【로우폴리 부품 · 빛깔】 (결정 2-127 — 임시 아트) — 면마다 각진 단순한 도형(Thronefall 같은 결)으로 장을 꾸민다.
/// 부품은 모두 「밑바닥이 y = 0, 가로 · 세로 · 높이 1」인 상자 안에 맞춰 두었다 — 놓을 때 크기를 그대로 배율로 준다.
///   Crown(둥근 잎 덩이) · Rock(바위) · Cone(솔잎 층) · Hex(육각 기둥) · Box · HipRoof(우진각 지붕)
/// 부품 메시는 Assets/Art/Models/LowPoly/LowPolyKit.asset 하나에, 재질은 빛깔마다 하나(LP_이름.mat).
/// </summary>
public static class LowPolyKit
{
    public const string ModelPath = "Assets/Art/Models/LowPoly/LowPolyKit.asset";
    public const string MaterialFolder = "Assets/Art/Meterials/LowPoly";

    public const int CrownVariants = 4;
    public const int RockVariants = 3;

    // ── 빛깔 (늦여름 시골 · 조금 가라앉힌 색) ─────────────────────────
    public static readonly Dictionary<string, Color> Palette = new()
    {
        ["Grass"] = new Color(0.47f, 0.60f, 0.31f),
        ["GrassDark"] = new Color(0.40f, 0.53f, 0.28f),
        ["Dirt"] = new Color(0.70f, 0.58f, 0.42f),
        ["ForestFloor"] = new Color(0.19f, 0.27f, 0.16f),
        ["Leaf0"] = new Color(0.33f, 0.50f, 0.23f),
        ["Leaf1"] = new Color(0.39f, 0.56f, 0.25f),
        ["Leaf2"] = new Color(0.45f, 0.60f, 0.27f),
        ["Leaf3"] = new Color(0.55f, 0.58f, 0.24f),
        ["Deep0"] = new Color(0.17f, 0.31f, 0.17f),
        ["Deep1"] = new Color(0.21f, 0.35f, 0.19f),
        ["Deep2"] = new Color(0.15f, 0.27f, 0.16f),
        ["Pine"] = new Color(0.16f, 0.33f, 0.22f),
        ["Trunk"] = new Color(0.36f, 0.25f, 0.17f),
        ["BambooStem"] = new Color(0.55f, 0.66f, 0.33f),
        ["BambooLeaf"] = new Color(0.36f, 0.56f, 0.26f),
        ["Rock"] = new Color(0.56f, 0.55f, 0.51f),
        ["RockDark"] = new Color(0.42f, 0.41f, 0.39f),
        ["Wood"] = new Color(0.45f, 0.31f, 0.19f),
        ["WoodDark"] = new Color(0.29f, 0.20f, 0.13f),
        ["Plaster"] = new Color(0.86f, 0.82f, 0.73f),
        ["Tile"] = new Color(0.29f, 0.30f, 0.33f),
        ["Thatch"] = new Color(0.72f, 0.60f, 0.38f),
        ["Cloth"] = new Color(0.86f, 0.83f, 0.74f),
        ["Snow"] = new Color(0.90f, 0.92f, 0.95f),
        ["Water"] = new Color(0.28f, 0.45f, 0.58f),
        ["Rope"] = new Color(0.86f, 0.76f, 0.50f),
        ["Paper"] = new Color(0.95f, 0.94f, 0.90f),
        ["Pepper"] = new Color(0.72f, 0.16f, 0.12f),
        ["Charcoal"] = new Color(0.12f, 0.12f, 0.13f),
        ["FlowerPink"] = new Color(0.88f, 0.48f, 0.62f),
        ["FlowerYellow"] = new Color(0.95f, 0.82f, 0.40f),
        ["FlowerWhite"] = new Color(0.93f, 0.93f, 0.88f),
    };

    private static Dictionary<string, Mesh> meshes;
    private static readonly Dictionary<string, Material> Materials = new();

    // ── 부품 꺼내기 ─────────────────────────────────────────────────

    public static Mesh Crown(int v) => Get($"Crown{Mathf.Abs(v) % CrownVariants}");
    public static Mesh Rock(int v) => Get($"Rock{Mathf.Abs(v) % RockVariants}");
    public static Mesh Cone => Get("Cone");
    public static Mesh Hex => Get("Hex");
    public static Mesh Box => Get("Box");
    public static Mesh HipRoof => Get("HipRoof");
    /// <summary>세운 그림판 — 밑 가운데가 원점, 가로 · 세로 1. -z를 본다(카메라 쪽).</summary>
    public static Mesh Card => Get("Card");

    public static Mesh Get(string name)
    {
        EnsureMeshes();
        return meshes[name];
    }

    public static Material Mat(string key)
    {
        if (Materials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/LP_{key}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        Color c = Palette.TryGetValue(key, out Color pc) ? pc : Color.magenta;
        m.SetColor("_BaseColor", c);
        m.color = c;
        m.SetFloat("_Smoothness", 0.04f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        Materials[key] = m;
        return m;
    }

    // ── 부품 만들기 ─────────────────────────────────────────────────

    private static void EnsureMeshes()
    {
        if (meshes != null && meshes.TryGetValue("Box", out Mesh box) && box != null)
            return;

        meshes = new Dictionary<string, Mesh>();
        Object[] stored = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
        foreach (Object o in stored)
            if (o is Mesh m)
                meshes[m.name] = m;

        bool complete = new[] { "Cone", "Hex", "Box", "HipRoof", "Card" }
            .Concat(Enumerable.Range(0, CrownVariants).Select(i => $"Crown{i}"))
            .Concat(Enumerable.Range(0, RockVariants).Select(i => $"Rock{i}"))
            .All(meshes.ContainsKey);
        if (complete)
            return;

        // 처음 — 만들어 한 파일에 담는다.
        EnsureFolder("Assets/Art/Models/LowPoly");
        var built = new List<Mesh>();
        for (int i = 0; i < CrownVariants; i++)
            built.Add(Ico($"Crown{i}", 1, 0.16f, 100 + i, 1f));
        for (int i = 0; i < RockVariants; i++)
            built.Add(Ico($"Rock{i}", 0, 0.28f, 200 + i, 0.45f));
        built.Add(Prism("Cone", 7, true));
        built.Add(Prism("Hex", 6, false));
        built.Add(BoxMesh());
        built.Add(Hip());
        built.Add(CardMesh());

        // 파일의 으뜸 물건은 파일 이름을 따라가므로 빈 메시를 으뜸으로 두고 부품은 모두 딸린 것으로.
        AssetDatabase.DeleteAsset(ModelPath);
        AssetDatabase.CreateAsset(new Mesh { name = "LowPolyKit" }, ModelPath);
        foreach (Mesh m in built)
            AssetDatabase.AddObjectToAsset(m, ModelPath);
        AssetDatabase.SaveAssets();

        meshes.Clear();
        foreach (Mesh m in built)
            meshes[m.name] = m;
    }

    /// <summary>각진 공 — 정이십면체를 subdiv번 나누고, 꼭짓점을 씨앗대로 흔든다. flatBottom &lt; 1이면 아래를 눌러 바위처럼.</summary>
    private static Mesh Ico(string name, int subdiv, float jitter, int seed, float flatBottom)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
            new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
            new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        var f = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        for (int i = 0; i < v.Count; i++)
            v[i] = v[i].normalized;

        for (int s = 0; s < subdiv; s++)
        {
            var mid = new Dictionary<long, int>();
            var nf = new List<int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i))
                    return i;
                v.Add(((v[a] + v[b]) * 0.5f).normalized);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            f = nf;
        }

        var rnd = new System.Random(seed);
        for (int i = 0; i < v.Count; i++)
        {
            Vector3 p = v[i] * (1f + ((float)rnd.NextDouble() * 2f - 1f) * jitter);
            if (p.y < 0f)
                p.y *= flatBottom;
            v[i] = p;
        }

        var tris = new List<(Vector3, Vector3, Vector3)>();
        for (int i = 0; i < f.Count; i += 3)
            tris.Add((v[f[i]], v[f[i + 1]], v[f[i + 2]]));
        return Flat(name, tris);
    }

    /// <summary>n각 기둥(cone이면 뿔) — 바닥 반지름 0.5 · 높이 1.</summary>
    private static Mesh Prism(string name, int sides, bool cone)
    {
        var tris = new List<(Vector3, Vector3, Vector3)>();
        var top = new Vector3(0, 1, 0);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var b0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.5f;
            var b1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.5f;
            tris.Add((Vector3.zero, b0, b1));   // 바닥
            if (cone)
            {
                tris.Add((b0, b1, top));
            }
            else
            {
                Vector3 t0 = b0 + Vector3.up, t1 = b1 + Vector3.up;
                tris.Add((b0, b1, t1));
                tris.Add((b0, t1, t0));
                tris.Add((top, t0, t1));
            }
        }
        return Flat(name, tris);
    }

    private static Mesh BoxMesh()
    {
        Vector3 P(float x, float y, float z) => new(x - 0.5f, y, z - 0.5f);
        Vector3[] c = { P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1), P(0, 1, 0), P(1, 1, 0), P(1, 1, 1), P(0, 1, 1) };
        var tris = new List<(Vector3, Vector3, Vector3)>();
        void Q(int a, int b, int cc, int d) { tris.Add((c[a], c[b], c[cc])); tris.Add((c[a], c[cc], c[d])); }
        Q(0, 1, 2, 3); Q(4, 5, 6, 7); Q(0, 1, 5, 4); Q(1, 2, 6, 5); Q(2, 3, 7, 6); Q(3, 0, 4, 7);
        return Flat("Box", tris);
    }

    /// <summary>우진각 지붕 — 바닥 1 × 1, 용마루는 x를 따라 반 길이, 높이 1. 처마 끝이 살짝 들린 맛은 놓을 때 낮은 판을 하나 더 깐다.</summary>
    private static Mesh Hip()
    {
        Vector3 b0 = new(-0.5f, 0, -0.5f), b1 = new(0.5f, 0, -0.5f), b2 = new(0.5f, 0, 0.5f), b3 = new(-0.5f, 0, 0.5f);
        Vector3 r0 = new(-0.25f, 1, 0), r1 = new(0.25f, 1, 0);
        var tris = new List<(Vector3, Vector3, Vector3)>
        {
            (b0, b1, r1), (b0, r1, r0), (b2, b3, r0), (b2, r0, r1), (b3, b0, r0), (b1, b2, r1), (b0, b2, b1), (b0, b3, b2),
        };
        return Flat("HipRoof", tris);
    }

    private static Mesh CardMesh()
    {
        var mesh = new Mesh { name = "Card" };
        mesh.SetVertices(new List<Vector3> { new(-0.5f, 0, 0), new(0.5f, 0, 0), new(0.5f, 1, 0), new(-0.5f, 1, 0) });
        mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
        mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>삼각형 목록 → 각진 메시. 꼭짓점을 나누지 않아 면마다 빛이 꺾인다. 바깥을 보도록 감는 방향을 맞추고, 밑 y = 0 · 1 × 1 × 1 상자에 맞춘다.</summary>
    private static Mesh Flat(string name, List<(Vector3 a, Vector3 b, Vector3 c)> tris)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var (a, b, c) in tris)
            foreach (Vector3 p in new[] { a, b, c })
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

        Vector3 size = max - min;
        Vector3 center = (min + max) * 0.5f;
        Vector3 Fit(Vector3 p) => new((p.x - center.x) / size.x, (p.y - min.y) / size.y, (p.z - center.z) / size.z);

        var verts = new List<Vector3>();
        var idx = new List<int>();
        var mid = new Vector3(0f, 0.5f, 0f);
        foreach (var (a0, b0, c0) in tris)
        {
            Vector3 a = Fit(a0), b = Fit(b0), c = Fit(c0);
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f)
                continue;
            if (Vector3.Dot(n, (a + b + c) / 3f - mid) < 0f)   // Cross(b - a, c - a)가 앞면 쪽 — 바깥을 보게
                (b, c) = (c, b);
            idx.Add(verts.Count); verts.Add(a);
            idx.Add(verts.Count); verts.Add(b);
            idx.Add(verts.Count); verts.Add(c);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
