using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 【장 꾸미기 — 임시 아트】 (결정 2-127) — 그레이박스 장 씬에 로우폴리 모습을 입힌다. 막는 상자(콜라이더)는 그대로 두고 보이는 것만 바꾼다.
///   · 바닥: 풀밭 · 흙마당 · 길 · 장 밖 숲 바닥을 그린 그림 한 장 (Chapter{n}_Ground.png)
///   · 덩어리: 종류마다 나무 덩이 · 대나무 · 바위 · 기와/초가집 · 울타리 · 좌판 · 우물 · 꽃밭 (LowPolyKit 부품)
///   · 장 밖: 빽빽한 짙은 숲 띠
///   · 금줄(Gate_): 새끼줄 · 기둥 · 한지 · 고추 · 숯 — 금줄 자식으로 달아 열리면 같이 사라진다
///   · 빛: 따뜻한 해 · 세 갈래 바탕빛 · 후처리(ArtProfile)
/// 부품은 24m 칸 · 빛깔마다 하나로 합쳐(Chapter{n}_Art.asset) 휴대폰에서 그리기 횟수를 줄인다.
/// 다시 돌리면 지우고 새로 만든다. 「장 맵 굽기」 뒤에는 이걸 다시 돌린다.
/// </summary>
public static class SceneArtDresser
{
    public const string GeneratedFolder = "Assets/Art/Generated";
    public const string ProfilePath = "Assets/Settings/ArtProfile.asset";
    private const int GroundPpm = 4;

    public static string RootName(ChapterMap map) => $"Art_Ch{map.Chapter}";
    public const string KitPath = "Assets/Resources/ArtKit.asset";

    /// <summary>
    /// 꾸러미(Resources/ArtKit) 채우기 — 부품 메시 · 로우폴리 빛깔 재질 · 쓸 수 있는 모든 그림(계절 나무 · 돌 색)의 재질.
    /// </summary>
    [MenuItem("Dokkaebi/Art/아트 꾸러미 다시 채우기")]
    public static ArtKit BuildKit()
    {
        var kit = AssetDatabase.LoadAssetAtPath<ArtKit>(KitPath);
        if (kit == null)
        {
            kit = ScriptableObject.CreateInstance<ArtKit>();
            AssetDatabase.CreateAsset(kit, KitPath);
        }

        kit.Meshes = new List<Mesh>();
        foreach (string n in new[] { "Cone", "Hex", "Box", "HipRoof", "Card", "Crown0", "Crown1", "Crown2", "Crown3", "Rock0", "Rock1", "Rock2" })
            kit.Meshes.Add(LowPolyKit.Get(n));

        kit.Entries = new List<ArtKit.Entry>();
        foreach (string key in LowPolyKit.Palette.Keys)
            kit.Entries.Add(new ArtKit.Entry { Key = key, Material = LowPolyKit.Mat(key), Aspect = 1f });

        var paths = new HashSet<string>();
        foreach (Season s in System.Enum.GetValues(typeof(Season)))
        {
            foreach (int[] set in new[] { ArtCatalog.Trees(s), ArtCatalog.Forest(s), ArtCatalog.Bushes(s), ArtCatalog.WaterTrees(s), new[] { ArtCatalog.OldTree(s) } })
                foreach (int n in set)
                    paths.Add(ArtCatalog.Tree(n));
            foreach (string[] set in new[] { ArtCatalog.BigRocks, ArtCatalog.MidRocks, ArtCatalog.SmallStones })
                foreach (string r in set)
                    paths.Add(ArtCatalog.Rock(r, ArtCatalog.RockColor(s)));
        }

        foreach (string path in paths)
            if (SpriteProps.Exists(path))
                kit.Entries.Add(new ArtKit.Entry { Key = "S:" + path, Material = SpriteProps.MaterialOf(path), Aspect = SpriteProps.AspectOf(path) });

        kit.Reindex();
        EditorUtility.SetDirty(kit);
        AssetDatabase.SaveAssets();
        return kit;
    }

    [MenuItem("Dokkaebi/Art/장 꾸미기 (전부)")]
    public static void DressAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string back = SceneManager.GetActiveScene().path;
        for (int chapter = 0; chapter <= 6; chapter++)
        {
            ChapterMap map = ZoneMapTable.Of(chapter);
            string path = $"Assets/Scenes/{ZoneMapTable.SceneFor(chapter)}.unity";
            if (map == null || !File.Exists(path))
                continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Dress(scene, map);
            EditorSceneManager.SaveScene(scene);
        }

        foreach (string other in new[] { "Assets/Scenes/Bunker.unity", "Assets/Scenes/Raid.unity" })
        {
            if (!File.Exists(other))
                continue;
            Scene scene = EditorSceneManager.OpenScene(other, OpenSceneMode.Single);
            ApplyLighting(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(back))
            EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        Debug.Log("[Art] 장 꾸미기 끝 — 0 ~ 6장 · 벙커 · Raid 빛");
    }

    [MenuItem("Dokkaebi/Art/지금 장 꾸미기")]
    public static void DressCurrent()
    {
        Scene scene = SceneManager.GetActiveScene();
        ChapterMap map = ZoneMapTable.IsMapScene(scene.name) ? ZoneMapTable.Of(ZoneMapTable.ChapterOfScene(scene.name)) : null;
        if (map == null)
            ApplyLighting(scene);
        else
            Dress(scene, map);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void Dress(Scene scene, ChapterMap map)
    {
        SceneManager.SetActiveScene(scene);
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == RootName(map))
                Object.DestroyImmediate(r);

        LowPolyKit.EnsureFolder(GeneratedFolder);
        AssetDatabase.DeleteAsset($"{GeneratedFolder}/Chapter{map.Chapter}_Art.asset");   // 옛 방식(합친 메시 저장)의 찌꺼기
        ArtKit kit = BuildKit();

        var root = new GameObject(RootName(map));
        SceneManager.MoveGameObjectToScene(root, scene);
        BuildGround(map, root.transform);
        root.AddComponent<ChapterArt>().Setup(map.Chapter);

        HideGreybox(scene, map);
        DressGates(scene);
        ApplyLighting(scene, ZoneDataTable.Chapter(map.Chapter)?.Season ?? Season.Spring);
        Debug.Log($"[Art] {map.Chapter}장 — 덩이 {root.GetComponent<ChapterArt>().Pieces}개 (런타임에 만든다)");
    }

    // ── 바닥 ──────────────────────────────────────────────────────────

    private static void BuildGround(ChapterMap map, Transform parent)
    {
        float halfX = map.HalfSize.x + ArtCatalog.Margin, halfZ = map.HalfSize.y + ArtCatalog.Margin;
        int w = Mathf.RoundToInt(halfX * 2f * GroundPpm), h = Mathf.RoundToInt(halfZ * 2f * GroundPpm);
        var pixels = new Color[w * h];
        List<(Vector2 a, Vector2 b)> roads = ArtCatalog.Roads(map);
        (Color grass, Color grassDark, Color dirtC, Color forest) = ArtCatalog.GroundColors(ZoneDataTable.Chapter(map.Chapter)?.Season ?? Season.Spring);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var p = new Vector3(-halfX + (x + 0.5f) / GroundPpm, 0f, -halfZ + (y + 0.5f) / GroundPpm);
            float n = Mathf.PerlinNoise(p.x * 0.07f + 31f, p.z * 0.07f + 17f);
            float fine = Mathf.PerlinNoise(p.x * 0.33f + 5f, p.z * 0.33f + 9f);
            float speck = Mathf.PerlinNoise(p.x * 1.7f + 3f, p.z * 1.7f + 7f);

            if (map.ZoneAt(p) == null)
            {
                pixels[y * w + x] = Color.Lerp(forest, forest * 1.25f, n);
                continue;
            }

            Color g = Color.Lerp(grassDark, grass, n * 0.7f + fine * 0.2f + speck * 0.1f) * (0.96f + speck * 0.08f);
            float dirt = Mathf.Max(ArtCatalog.YardAmount(map, p), ArtCatalog.RoadAmount(roads, p));
            dirt = Mathf.Clamp01(dirt + (fine - 0.5f) * 0.5f);
            pixels[y * w + x] = Color.Lerp(g, Color.Lerp(dirtC * 0.92f, dirtC, fine), Mathf.SmoothStep(0.35f, 0.65f, dirt));
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply(false);
        string texPath = $"{GeneratedFolder}/Chapter{map.Chapter}_Ground.png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
        {
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }

        string matPath = $"{GeneratedFolder}/Chapter{map.Chapter}_Ground.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        var groundTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        mat.SetTexture("_BaseMap", groundTex);
        mat.mainTexture = groundTex;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_SpecularHighlights", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(mat);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = "Ground";
        quad.transform.SetParent(parent, false);
        quad.transform.position = new Vector3(0f, 0.005f, 0f);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(halfX * 2f, halfZ * 2f, 1f);
        var r = quad.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        quad.isStatic = true;
    }

    // ── 그레이박스 감추기 · 금줄 ─────────────────────────────────────

    private static void HideGreybox(Scene scene, ChapterMap map)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            if (r.name == "Floor" && r.TryGetComponent(out MeshRenderer floor))
                floor.enabled = false;

            if (r.name != $"Map_Ch{map.Chapter}")
                continue;

            foreach (Transform child in r.transform)
                if (!child.name.StartsWith("Gate_") && child.TryGetComponent(out MeshRenderer mr))
                    mr.enabled = false;
        }
    }

    /// <summary>금줄 — 새끼줄 하나에 한지 · 고추 · 숯을 번갈아 끼우고, 양 끝에 기둥. 금줄 자식이라 구역이 열리면 같이 꺼진다.</summary>
    private static void DressGates(Scene scene)
    {
        var gates = new List<Transform>();
        foreach (GameObject r in scene.GetRootGameObjects())
            gates.AddRange(r.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Gate_")));

        foreach (Transform gate in gates)
        {

            Transform old = gate.Find("GateArt");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            if (gate.TryGetComponent(out MeshRenderer mr))
                mr.enabled = false;

            var art = new GameObject("GateArt");
            art.transform.SetParent(gate, false);
            Vector3 s = gate.lossyScale;
            art.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);   // 자식은 월드 크기로 놓는다
            art.transform.localPosition = new Vector3(0f, -0.5f, 0f);              // 금줄 상자 밑바닥

            bool alongX = s.x >= s.z;
            float length = alongX ? s.x : s.z;
            Vector3 dir = alongX ? Vector3.right : Vector3.forward;

            void Part(string key, Mesh mesh, Vector3 pos, Vector3 scale, Quaternion? rot = null)
            {
                var go = new GameObject(key);
                go.transform.SetParent(art.transform, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = rot ?? Quaternion.identity;
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = LowPolyKit.Mat(key);
            }

            Part("Wood", LowPolyKit.Hex, dir * (length * 0.5f), new Vector3(0.22f, 1.5f, 0.22f));
            Part("Wood", LowPolyKit.Hex, -dir * (length * 0.5f), new Vector3(0.22f, 1.5f, 0.22f));
            Vector3 ropeScale = alongX ? new Vector3(length, 0.07f, 0.07f) : new Vector3(0.07f, 0.07f, length);
            Part("Rope", LowPolyKit.Box, Vector3.up * 1.25f, ropeScale);

            int count = Mathf.Max(2, Mathf.FloorToInt(length / 0.7f));
            string[] hang = { "Paper", "Pepper", "Paper", "Charcoal" };
            for (int i = 1; i < count; i++)
            {
                Vector3 at = dir * (-length * 0.5f + length * i / count) + Vector3.up * 0.95f;
                string key = hang[i % hang.Length];
                Vector3 size = key == "Paper" ? new Vector3(0.18f, 0.3f, 0.04f) : new Vector3(0.12f, 0.24f, 0.12f);
                if (!alongX)
                    size = new Vector3(size.z, size.y, size.x);
                Part(key, key == "Pepper" ? LowPolyKit.Cone : LowPolyKit.Box, at, size,
                    key == "Pepper" ? Quaternion.Euler(180f, 0f, 0f) * Quaternion.identity : (Quaternion?)null);
            }
        }
    }

    // ── 빛 · 후처리 ──────────────────────────────────────────────────

    public static void ApplyLighting(Scene scene) => ApplyLighting(scene, Season.Spring);

    public static void ApplyLighting(Scene scene, Season season)
    {
        SceneManager.SetActiveScene(scene);

        foreach (GameObject r in scene.GetRootGameObjects())
        foreach (Light l in r.GetComponentsInChildren<Light>(true))
        {
            if (l.type != LightType.Directional)
                continue;
            l.color = season switch
            {
                Season.Winter => new Color(0.93f, 0.96f, 1f),
                Season.Autumn => new Color(1f, 0.90f, 0.76f),
                _ => new Color(1f, 0.95f, 0.86f),
            };
            l.intensity = 1.25f;
            l.shadows = LightShadows.Hard;
            l.shadowStrength = 0.62f;
            l.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            EditorUtility.SetDirty(l);
            EditorUtility.SetDirty(l.transform);
        }

        // 2.5D 카메라 — 원근 없이(정사영) Pitch만큼 내려다본다. 세운 그림이 늘 정면으로 보이도록 같은 각.
        foreach (GameObject r in scene.GetRootGameObjects())
        foreach (Camera cam in r.GetComponentsInChildren<Camera>(true))
        {
            if (!cam.CompareTag("MainCamera"))
                continue;
            // 주인공 찾기 — 「Player」 태그가 없어(Dokkaebi 는 Untagged) 예전엔 늘 못 찾고 카메라 앞 18m 를 초점으로 잡아,
            // 구울 때마다 카메라가 12m 씩 뒤로 물러났다(0장 12번 → 높이 197m · 그림 거리 120m 밖이라 화면이 회색). PlayerMovement 로 찾는다.
            GameObject player = null;
            foreach (GameObject rr in scene.GetRootGameObjects())
            {
                if (rr.CompareTag("Player")) { player = rr; break; }
                PlayerMovement pm = rr.GetComponentInChildren<PlayerMovement>(true);
                if (pm != null) { player = pm.gameObject; break; }
            }
            if (player == null)
            {
                Debug.LogWarning("[SceneArtDresser] 주인공을 못 찾아 카메라 자리는 그대로 둔다");
                continue;
            }
            Vector3 focus = player.transform.position;
            // 시점 C (v8 · 사용자 선택) — 원근 · 55° 내려다봄 · 35° 돌림. 정사영 반높이 9.5m 와 같은 넓이가 보이는 거리.
            cam.orthographic = false;
            cam.fieldOfView = ArtCatalog.Fov;
            cam.orthographicSize = ArtCatalog.OrthoSize;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 160f;
            cam.transform.rotation = Quaternion.Euler(ArtCatalog.Pitch, ArtCatalog.Yaw, 0f);
            cam.transform.position = focus - cam.transform.forward * ArtCatalog.CameraDistance;
            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(cam.transform);
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.66f, 0.72f, 0.80f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.55f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.22f);
        RenderSettings.ambientIntensity = 1f;
        DynamicGI.UpdateEnvironment();

        VolumeProfile profile = EnsureProfile();
        foreach (GameObject r in scene.GetRootGameObjects())
        foreach (Volume v in r.GetComponentsInChildren<Volume>(true))
        {
            if (!v.isGlobal)
                continue;
            v.sharedProfile = profile;
            EditorUtility.SetDirty(v);
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    /// <summary>후처리 — 중립 톤 · 약한 번짐 · 조금 짙은 대비와 채도 · 가장자리 어둡게. 움직임 흐림은 없다.</summary>
    public static VolumeProfile EnsureProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile != null)
            return profile;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.25f);
        bloom.threshold.Override(1.05f);

        var color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(8f);
        color.saturation.Override(10f);
        color.postExposure.Override(0.1f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.45f);

        foreach (VolumeComponent c in profile.components)
            AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }
}
