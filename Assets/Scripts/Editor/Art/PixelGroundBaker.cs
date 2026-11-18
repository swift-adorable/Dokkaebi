using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 【장 맵 도트 바닥 굽기】 (2026-11-17) — nanasha 바닥 타일(32칸, 이음매 없음)을 그대로 이어 깔아 장 바닥 한 장을 굽는다.
///   · 1m 에 23.5칸 (캐릭터 · 소굴 바닥과 같은 도트 밀도). 타일 그림은 바꾸지 않고 어느 타일을 깔지만 정한다
///   · 길(PixelDress.Paths) → 솔잎 길, 냇물 → 검은 냇물, 냇가 → 갈대 진흙, 바위 둘레 → 이끼 바위 땅, 집 앞 → 다져진 흙,
///     나머지 → 밤 풀밭 · 이끼 숲 바닥 · 짙은 숲 바닥 (큰 얼룩 노이즈), 구역 밖 → 짙은 숲 바닥
///   · 경계는 2칸 덩이로 흩어(디더) 도트답게 섞는다
///   · 그 바깥(장 밖 띠)은 원래 바닥 판에 짙은 숲 바닥 타일을 되풀이해 깐다
/// </summary>
public static class PixelGroundBaker
{
    private const string Source = "Assets/Art/PixelSource/Floors";
    private const float Ppm = 23.5f;
    private const float Margin = 6f;

    [MenuItem("Dokkaebi/Art/0장 도트 바닥 굽기 (열린 0장 씬)")]
    public static void BakeOpen()
    {
        Scene scene = SceneManager.GetActiveScene();
        int chapter = ZoneMapTable.ChapterOfScene(scene.name);
        ChapterMap map = ZoneMapTable.Of(chapter);
        if (map == null || !PixelDress.Has(chapter))
        {
            Debug.LogWarning("[PixelGround] 도트 꾸밈 장 씬(Chapter0)을 열고 돌린다.");
            return;
        }
        Bake(scene, map);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>장마다의 바닥 타일 (결정 2-148) — Texture Pixel(lynocs, CC0)을 도트로 줄이고 장 색으로 물들인 것. 없으면 nanasha 타일.</summary>
    public static string ChapterFolder(int chapter) => $"Assets/Art/Floors/Chapter{chapter}";
    private static readonly string[] ChapterNames = { "dark", "grass", "moss", "trail", "stream", "mud", "rock", "dirt" };

    private static Color32[] Tile(string path, out int size)
    {
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(path));
        size = t.width;
        Color32[] px = t.GetPixels32();
        Object.DestroyImmediate(t);
        return px;
    }

    private static uint Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    public static void Bake(Scene scene, ChapterMap map)
    {
        string[] names = { "dark_forest_floor", "night_hill_grass", "mossy_forest_floor", "pine_needle_trail", "misty_black_stream", "marsh_reed_mud", "mossy_rock_ground", "trodden_camp_dirt" };
        const int Dark = 0, Grass = 1, Moss = 2, Trail = 3, Stream = 4, Mud = 5, RockG = 6, Dirt = 7;
        var tiles = new Color32[names.Length][];
        var sizes = new int[names.Length];
        string own = ChapterFolder(map.Chapter);
        bool chapterTiles = File.Exists($"{own}/{ChapterNames[0]}.png");
        for (int i = 0; i < names.Length; i++)
            tiles[i] = Tile(chapterTiles ? $"{own}/{ChapterNames[i]}.png" : $"{Source}/{names[i]}.png", out sizes[i]);

        float halfX = map.HalfSize.x + Margin, halfZ = map.HalfSize.y + Margin;
        int w = Mathf.RoundToInt(halfX * 2f * Ppm), h = Mathf.RoundToInt(halfZ * 2f * Ppm);
        var outPx = new Color32[w * h];
        var rocks = new List<MapBlock>();
        foreach (MapBlock b in map.Blocks)
            if (b.Kind == MapBlockKind.Rock) rocks.Add(b);

        // 배치 틀 (결정 2-147) — 공터 · 디딤돌은 DressPlan이 정한다. 디딤돌은 1m 칸으로 묶어 빨리 찾는다.
        DressPlan.ClearCache();
        var stoneGrid = new Dictionary<(int, int), List<Vector2>>();
        foreach (Vector2 st in DressPlan.SteppingStones(map))
        {
            var k = (Mathf.FloorToInt(st.x), Mathf.FloorToInt(st.y));
            if (!stoneGrid.TryGetValue(k, out var l)) stoneGrid[k] = l = new List<Vector2>();
            l.Add(st);
        }

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var p = new Vector3(-halfX + (x + 0.5f) / Ppm, 0f, -halfZ + (y + 0.5f) / Ppm);
            var q = new Vector2(p.x, p.z);
            // 2칸 덩이 디더 — 경계가 도트 계단처럼 흩어진다
            float d = (Hash(x >> 1, y >> 1) & 1023) / 1023f - 0.5f;
            float n = Mathf.PerlinNoise(p.x * 0.045f + 11f, p.z * 0.045f + 3f);
            float n2 = Mathf.PerlinNoise(p.x * 0.2f + 7f, p.z * 0.2f + 19f);

            int t;
            if (map.ZoneAt(p) == null && (Mathf.Abs(p.x) > map.HalfSize.x - 0.5f || Mathf.Abs(p.z) > map.HalfSize.y - 0.5f))
                t = Dark;
            else
            {
                float wet = DressPlan.WaterDistance(map, q, out bool inWater);
                float road = PixelDress.PathDistance(map.Chapter, q);
                float clear = DressPlan.ClearingDistance(map, q, out _);
                bool rocky = false;
                foreach (MapBlock b in rocks)
                    if (b.Contains(p, 1.6f + (n2 - 0.5f) * 2f + d))
                        rocky = true;
                bool stone = false;
                if (stoneGrid.TryGetValue((Mathf.FloorToInt(q.x), Mathf.FloorToInt(q.y)), out var near))
                    foreach (Vector2 st in near)
                        stone |= (st - q).sqrMagnitude < DressPlan.StoneRadius * DressPlan.StoneRadius;
                if (!stone)   // 칸 경계에 걸친 돌
                    for (int ox = -1; ox <= 1 && !stone; ox++)
                    for (int oz = -1; oz <= 1 && !stone; oz++)
                        if ((ox != 0 || oz != 0) && stoneGrid.TryGetValue((Mathf.FloorToInt(q.x) + ox, Mathf.FloorToInt(q.y) + oz), out var n3))
                            foreach (Vector2 st in n3)
                                stone |= (st - q).sqrMagnitude < DressPlan.StoneRadius * DressPlan.StoneRadius;

                float edgeOfMap = Mathf.Min(map.HalfSize.x - Mathf.Abs(p.x), map.HalfSize.y - Mathf.Abs(p.z));

                if (inWater) t = Stream;
                else if (wet < 0.9f + (n2 - 0.5f) * 0.6f + d * 0.4f) t = RockG;          // 물가 돌 테 (결정 2-147)
                else if (wet < 1.9f + (n2 - 0.5f) * 1.2f + d * 0.6f) t = Mud;
                else if (stone) t = RockG;                                                 // 디딤돌
                else if (clear < (n2 - 0.5f) * 1.6f + d * 0.6f) t = Dirt;                  // 둥근 흙 공터
                else if (road < DressPlan.PathHalfWidth + (n2 - 0.5f) * 1.0f + d * 0.6f) t = Trail;
                else if (ArtCatalog.YardAmount(map, p) > 0.55f + (n2 - 0.5f) * 0.4f + d * 0.25f) t = Dirt;
                else if (LockSiteTable.InAnySite(map, q, 1.4f + (n2 - 0.5f) * 1.6f + d * 0.6f)) t = Dirt;   // 집 마당 (결정 2-149)
                else if (rocky) t = RockG;
                else if (edgeOfMap < 7f + (n - 0.5f) * 4f) t = Dark;                        // 장 가장자리는 짙은 숲 바닥
                else
                {
                    float v = n + d * 0.08f;
                    t = v < 0.32f ? Dark : v < 0.58f ? Grass : Moss;   // 짙은 얼룩은 줄였다 (v3 — 그림자처럼 보였다)
                }
            }
            int ts = sizes[t];
            outPx[y * w + x] = tiles[t][(y % ts) * ts + (x % ts)];
        }

        // 조각으로 나눠 굽는다 (결정 2-149 — 맵을 2배로 늘려 한 장이 4096칸을 넘는다). 조각마다 그림 · 재질 · 판 하나.
        string folder = SceneArtDresser.GeneratedFolder;
        int nx = Mathf.CeilToInt(w / 4096f), nz = Mathf.CeilToInt(h / 4096f);
        int cw = Mathf.CeilToInt(w / (float)nx), ch = Mathf.CeilToInt(h / (float)nz);
        var chunks = new List<(string path, RectInt rect)>();
        foreach (string old in Directory.GetFiles(folder, $"Chapter{map.Chapter}_PixelGround*.png"))
            AssetDatabase.DeleteAsset(old);
        for (int j = 0; j < nz; j++)
        for (int i = 0; i < nx; i++)
        {
            var rect = new RectInt(i * cw, j * ch, Mathf.Min(cw, w - i * cw), Mathf.Min(ch, h - j * ch));
            var part = new Color32[rect.width * rect.height];
            for (int y = 0; y < rect.height; y++)
                System.Array.Copy(outPx, (rect.y + y) * w + rect.x, part, y * rect.width, rect.width);
            string texPath = $"{folder}/Chapter{map.Chapter}_PixelGround_{i}_{j}.png";
            var tex = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            tex.SetPixels32(part);
            tex.Apply(false);
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
            {
                ti.filterMode = FilterMode.Point;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;   // 보통 압축은 도트가 뭉갰다 — 고품질(BC7 · ASTC 4x4)
                ti.SaveAndReimport();
            }
            chunks.Add((texPath, rect));
        }

        // 바깥 띠 — 짙은 숲 바닥 타일 되풀이
        string outerSrc = chapterTiles ? $"{own}/{ChapterNames[0]}.png" : $"{Source}/dark_forest_floor.png";
        if (AssetImporter.GetAtPath(outerSrc) is TextureImporter oi)
        {
            oi.filterMode = FilterMode.Point;
            oi.wrapMode = TextureWrapMode.Repeat;
            oi.mipmapEnabled = false;
            oi.textureCompression = TextureImporterCompression.Uncompressed;
            oi.SaveAndReimport();
        }

        Transform root = null;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == SceneArtDresser.RootName(map))
                root = r.transform;
        if (root == null)
        {
            Debug.LogWarning("[PixelGround] Art_Ch 뿌리가 없다 — 먼저 「지금 장 꾸미기」.");
            return;
        }

        Transform ground = root.Find("Ground");
        if (ground != null)
        {
            Vector3 s = ground.localScale;
            float tileM = sizes[Dark] / Ppm;
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                Mat($"{folder}/Chapter{map.Chapter}_OuterGround.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(outerSrc), new Vector2(s.x / tileM, s.y / tileM));
        }

        for (int k = root.childCount - 1; k >= 0; k--)
            if (root.GetChild(k).name.StartsWith("PixelGround"))
                Object.DestroyImmediate(root.GetChild(k).gameObject);
        foreach (string old in Directory.GetFiles(folder, $"Chapter{map.Chapter}_PixelGround*.mat"))
            AssetDatabase.DeleteAsset(old);
        foreach (var (path, rect) in chunks)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            Material inner = Mat($"{folder}/{name}.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(path), Vector2.one);
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name.Replace($"Chapter{map.Chapter}_", "");
            quad.transform.SetParent(root, false);
            float x0 = -halfX + rect.x / Ppm, z0 = -halfZ + rect.y / Ppm;
            float sw = rect.width / Ppm, sh = rect.height / Ppm;
            quad.transform.position = new Vector3(x0 + sw * 0.5f, 0.012f, z0 + sh * 0.5f);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(sw, sh, 1f);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = inner;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            quad.isStatic = true;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[PixelGround] {map.Chapter}장 바닥 {w}×{h} ({nx}×{nz} 조각)");
    }

    private static Material Mat(string path, Texture2D tex, Vector2 tiling)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        mat.SetTextureScale("_BaseMap", tiling);
        mat.mainTextureScale = tiling;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_SpecularHighlights", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
