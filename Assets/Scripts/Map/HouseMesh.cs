using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【덕코프처럼 들어가는 집 — 3D 단면 집】 (결정 2-151 · 2026-11-19 사용자 「건물들이 사진을 합성한 것처럼 붙인 것 같아 · 덕코프처럼 자연스럽게」)
/// 집 그림 카드 한 장은 바닥 · 빛 · 그림자와 따로 놀았다. 덕코프 집은 벽 · 바닥 · 지붕이 진짜 입체라 같은 빛을 받고 그림자를 드리운다.
/// 그래서 벽 · 마루 · 지붕을 상자 · 판으로 세우고 Texture Pixel(CC0)을 도트로 줄인 텍스처(Resources/HouseTex, 1m = 23.5칸)를 입힌다.
///   · UV 는 월드 미터 ÷ 타일 크기 — 어떤 크기의 벽이든 도트 밀도가 바닥 · 캐릭터와 같다
///   · 재질은 URP Lit(점 필터) — 장 빛깔 · 해 · 그림자를 그대로 받는다
/// </summary>
public static class HouseMesh
{
    private static readonly Dictionary<string, Material> materials = new();
    private static readonly Dictionary<string, float> tileMeters = new();

    /// <summary>텍스처 이름 → 재질 (없으면 회색). 타일 한 장이 덮는 미터도 같이.</summary>
    public static Material Mat(string tex, out float tileM)
    {
        if (materials.TryGetValue(tex, out Material m))
        {
            tileM = tileMeters[tex];
            return m;
        }
        var t = Resources.Load<Texture2D>($"HouseTex/{tex}");
        tileM = t != null ? t.width / BuildingSprite.PixelsPerMeter : 2f;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(shader) { name = $"House_{tex}" };
        if (t != null)
        {
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Repeat;
            m.SetTexture("_BaseMap", t);
            m.mainTexture = t;
        }
        m.SetFloat("_Cull", 0f);   // 양면 — 면 방향을 따지지 않는다 (1차: 바닥 윗면이 뒤집혀 안 보였다)
        m.doubleSidedGI = true;
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (t != null)   // 텍스처가 아직 없을 때(가져오기 전) 만든 흰 재질은 붙잡아 두지 않는다
        {
            materials[tex] = m;
            tileMeters[tex] = tileM;
        }
        return m;
    }

    /// <summary>단색 상자 — 가구의 장식(이불 · 솥 · 장석)처럼 작은 것. 같은 Lit 재질에 색만.</summary>
    public static GameObject BoxColor(Transform parent, string name, Vector3 local, Vector3 size, Color color)
    {
        string key = $"#{ColorUtility.ToHtmlStringRGB(color)}";
        if (!materials.ContainsKey(key))
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"House_{key}" };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Cull", 0f);
            materials[key] = m;
            tileMeters[key] = 1f;
        }
        return Box(parent, name, local, size, key);
    }

    /// <summary>상자 하나 — 면마다 월드 미터 UV. local 은 바닥 가운데 기준(y = 아래면).</summary>
    public static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, string tex, bool shadows = true)
    {
        Material mat = Mat(tex, out float tm);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var mesh = new Mesh { name = name };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var tri = new List<int>();
        Vector3 h = new(size.x * 0.5f, 0f, size.z * 0.5f);
        Vector3 Wp(Vector3 p) => go.transform.parent.TransformPoint(local + p);   // UV 를 월드 기준으로 — 이웃 상자와 무늬가 이어진다
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int ua, int va)
        {
            int i0 = v.Count;
            foreach (Vector3 p in new[] { a, b, c, d })
            {
                v.Add(p);
                Vector3 w = Wp(p);
                uv.Add(new Vector2(w[ua] / tm, w[va] / tm));
                n.Add(normal);
            }
            tri.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }
        float y0 = 0f, y1 = size.y;
        // 앞(남 -z) · 뒤(북 +z) · 서(-x) · 동(+x) · 위
        Face(new(-h.x, y0, -h.z), new(-h.x, y1, -h.z), new(h.x, y1, -h.z), new(h.x, y0, -h.z), Vector3.back, 0, 1);
        Face(new(h.x, y0, h.z), new(h.x, y1, h.z), new(-h.x, y1, h.z), new(-h.x, y0, h.z), Vector3.forward, 0, 1);
        Face(new(-h.x, y0, h.z), new(-h.x, y1, h.z), new(-h.x, y1, -h.z), new(-h.x, y0, -h.z), Vector3.left, 2, 1);
        Face(new(h.x, y0, -h.z), new(h.x, y1, -h.z), new(h.x, y1, h.z), new(h.x, y0, h.z), Vector3.right, 2, 1);
        Face(new(-h.x, y1, -h.z), new(-h.x, y1, h.z), new(h.x, y1, h.z), new(h.x, y1, -h.z), Vector3.up, 0, 2);
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    /// <summary>
    /// 우진각 지붕 — 채(긴 네모) 하나를 덮는다. 용마루는 긴 변 방향, 네 면이 처마로 내려온다 (한옥 참고 사진의 채마다 따로 얹힌 지붕).
    /// r 은 덮을 바닥 네모(툇마루까지), overhang 만큼 처마가 나온다. 면마다 UV 는 처마를 따라 u · 비탈을 따라 v — 기와 줄이 처마와 나란하다.
    /// ridgeCap 이면 용마루에 기와 한 줄(초가는 둥근 이엉 띠).
    /// </summary>
    public static GameObject HipRoof(Transform parent, Rect r, float wallH, float rise, float overhang, string roofTex, bool ridgeCap)
    {
        var root = new GameObject("Roof");
        root.transform.SetParent(parent, false);
        Material roof = Mat(roofTex, out float tm);
        float x0 = r.xMin - overhang, x1 = r.xMax + overhang, z0 = r.yMin - overhang, z1 = r.yMax + overhang;
        bool alongX = x1 - x0 >= z1 - z0;
        float half = (alongX ? z1 - z0 : x1 - x0) * 0.5f;
        float eave = wallH - 0.25f, ridge = wallH + rise;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        Vector3 rA, rB;   // 용마루 양 끝
        if (alongX) { float h = Mathf.Min(half, (x1 - x0) * 0.5f - 0.01f); rA = new Vector3(x0 + h, ridge, cz); rB = new Vector3(x1 - h, ridge, cz); }
        else { float h = Mathf.Min(half, (z1 - z0) * 0.5f - 0.01f); rA = new Vector3(cx, ridge, z0 + h); rB = new Vector3(cx, ridge, z1 - h); }
        Vector3 SW = new(x0, eave, z0), SE = new(x1, eave, z0), NE = new(x1, eave, z1), NW = new(x0, eave, z1);

        void Face(string name, Vector3[] pts)   // pts[0] → pts[1] 이 처마
        {
            Vector3 along = (pts[1] - pts[0]).normalized;
            Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[pts.Length - 1] - pts[0]).normalized;
            if (n.y < 0f) n = -n;
            Vector3 up = Vector3.Cross(n, along).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = new Mesh();
            var uv = new List<Vector2>();
            var nn = new List<Vector3>();
            foreach (Vector3 p in pts) { Vector3 d = p - pts[0]; uv.Add(new Vector2(Vector3.Dot(d, along) / tm, Vector3.Dot(d, up) / tm)); nn.Add(n); }
            mesh.SetVertices(new List<Vector3>(pts));
            mesh.SetUVs(0, uv);
            mesh.SetNormals(nn);
            var tri = new List<int>();
            for (int i = 1; i + 1 < pts.Length; i++) tri.AddRange(new[] { 0, i, i + 1, 0, i + 1, i });   // 양면
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = roof;
            mr.shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        if (alongX)
        {
            Face("RoofS", new[] { SW, SE, rB, rA });
            Face("RoofN", new[] { NE, NW, rA, rB });
            Face("RoofW", new[] { NW, SW, rA });
            Face("RoofE", new[] { SE, NE, rB });
        }
        else
        {
            Face("RoofW", new[] { NW, SW, rA, rB });
            Face("RoofE", new[] { SE, NE, rB, rA });
            Face("RoofS", new[] { SW, SE, rA });
            Face("RoofN", new[] { NE, NW, rB });
        }
        float len = (rB - rA).magnitude;
        if (ridgeCap && len > 0.1f)
            Box(root.transform, "Ridge", (rA + rB) * 0.5f - Vector3.up * 0.1f, alongX ? new Vector3(len + 0.4f, 0.3f, 0.42f) : new Vector3(0.42f, 0.3f, len + 0.4f), roofTex);
        else if (len > 0.1f)
            Box(root.transform, "Ridge", (rA + rB) * 0.5f - Vector3.up * 0.15f, alongX ? new Vector3(len + 0.6f, 0.3f, 0.7f) : new Vector3(0.7f, 0.3f, len + 0.6f), roofTex);
        return root;
    }

    /// <summary>
    /// 맞배지붕 — 용마루가 x 방향. 처마가 벽 밖으로 overhang m 나온다. 두 비탈 + 양 끝 박공(벽 텍스처).
    /// </summary>
    public static GameObject GableRoof(Transform parent, float hw, float hd, float wallH, float rise, float overhang, string roofTex, string gableTex)
    {
        var root = new GameObject("Roof");
        root.transform.SetParent(parent, false);
        Material roof = Mat(roofTex, out float tm);
        float x0 = -hw - overhang, x1 = hw + overhang, z0 = -hd - overhang, z1 = hd + overhang;
        float eave = wallH - overhang * (rise / hd) * 0.5f;   // 처마는 벽 위보다 살짝 낮게 처진다
        float ridge = wallH + rise;
        float slope = Mathf.Sqrt((z1) * (z1) + (ridge - eave) * (ridge - eave));

        void Plane(string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = new Mesh();
            mesh.SetVertices(new List<Vector3> { a, b, c, d });
            float lx = (b - a).magnitude, ly = (d - a).magnitude;
            mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(lx / tm, 0), new(lx / tm, ly / tm), new(0, ly / tm) });
            mesh.SetNormals(new List<Vector3> { normal, normal, normal, normal });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }, 0);   // 양면 — 처마 밑에서도 보이게
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = roof;
            r.shadowCastingMode = ShadowCastingMode.TwoSided;
        }
        Vector3 nS = new Vector3(0f, z1, -(ridge - eave)).normalized, nN = new Vector3(0f, z1, ridge - eave).normalized;
        // 앞 비탈(남) · 뒤 비탈(북) — 아래 처마 → 용마루
        Plane("RoofS", new(x0, eave, z0), new(x1, eave, z0), new(x1, ridge, 0f), new(x0, ridge, 0f), nS);
        Plane("RoofN", new(x1, eave, z1), new(x0, eave, z1), new(x0, ridge, 0f), new(x1, ridge, 0f), nN);
        // 용마루 — 짙은 기와 한 줄
        Box(root.transform, "Ridge", new Vector3(0f, ridge - 0.12f, 0f), new Vector3(x1 - x0 + 0.2f, 0.32f, 0.5f), roofTex);
        // 박공 — 양 끝 세모(벽 텍스처)
        Material g = Mat(gableTex, out float gm);
        foreach (float sx in new[] { -hw, hw })
        {
            var go = new GameObject("Gable");
            go.transform.SetParent(root.transform, false);
            var mesh = new Mesh();
            mesh.SetVertices(new List<Vector3> { new(sx, wallH, -hd), new(sx, wallH, hd), new(sx, ridge - 0.05f, 0f) });
            mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(2f * hd / gm, 0), new(hd / gm, rise / gm) });
            Vector3 nn = sx < 0 ? Vector3.left : Vector3.right;
            mesh.SetNormals(new List<Vector3> { nn, nn, nn });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 1 }, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = g;
        }
        return root;
    }
}
