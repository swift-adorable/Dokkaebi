using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【지도 사진 굽기】 (결정 2-121) — 장 맵 씬을 바로 위에서 내려다본 그림으로 굽는다. 전체 지도가 이 그림을 쓴다.
/// 참고 그림(사용자 2026-10-13)처럼 「실제 장면을 위에서 찍은 지도」다.
/// 【지금은 모형을 찍는다】 (결정 2-122) — 씬이 그레이박스라 그대로 찍으면 색 판뿐이라, 같은 표로 풀밭 · 흙길 · 나무 덩이 · 지붕 ·
/// 숲을 세운 모형(MapDiorama)을 찍는다. 아트가 들어오면 UseSceneArt를 켜고 씬을 찍는다.
///
///   · 화면 1m에 PixelsPerMeter 화소 · 북쪽이 위 · 맵 가운데가 월드 원점 (MapRaster와 같은 자리)
///   · 금줄(Gate_)은 끄고 찍는다 — 닫힌 금줄은 지도가 덮개로 따로 그린다(열리면 사라져야 하므로)
///   · UI 층 · 픽셀 인형 층은 찍지 않는다
///   · Resources/MapPhotos/Chapter{n}.png — 다시 구우면 덮어쓴다
/// </summary>
public static class MapPhotoBaker
{
    public const int PixelsPerMeter = 8;

    /// <summary>씬 자체를 찍는가 — 아트가 들어오면 켠다. 꺼져 있으면 표로 세운 모형을 찍는다.</summary>
    public static bool UseSceneArt = true;   // 결정 2-127 — 장 꾸미기(2.5D)가 들어가 씬을 찍는다
    private const int MaxSize = 2048;
    private const string Folder = "Assets/Resources/" + MapRuntime.PhotoFolder;

    [MenuItem("Dokkaebi/Map/지도 사진 굽기 (전부)")]
    public static void BakeAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[MapPhoto] 플레이 중에는 굽지 않는다.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string back = SceneManager.GetActiveScene().path;
        Directory.CreateDirectory(Folder);

        var written = new List<string>();

        foreach (ChapterMap map in ZoneMapTable.All)
        {
            string scenePath = $"Assets/Scenes/{map.SceneName}.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[MapPhoto] {scenePath} 없음 — 「장 맵 굽기」부터");
                continue;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string path = Bake(map);
            if (path != null)
                written.Add(path);
        }

        AssetDatabase.Refresh();

        foreach (string path in written)
            Configure(path);

        if (!string.IsNullOrEmpty(back))
            EditorSceneManager.OpenScene(back, OpenSceneMode.Single);

        Debug.Log($"[MapPhoto] {written.Count}장 구움 — {string.Join(", ", written)}");
    }

    private static string Bake(ChapterMap map)
    {
        int ppm = PixelsPerMeter;
        while (MapRaster.WidthOf(map, ppm) > MaxSize || MapRaster.HeightOf(map, ppm) > MaxSize)
            ppm--;

        int w = MapRaster.WidthOf(map, ppm), h = MapRaster.HeightOf(map, ppm);

        // 【집도 찍는다】 (2026-11-20) — 집터(3D 한옥)와 들어가지 못하는 한옥(헛간 · 방앗간)은 판이 시작될 때 세워져 씬에 없다.
        // 지도에는 지붕 덮인 모습으로 나와야 하니 구역마다 잠깐 세워 찍고 지운다. 상자는 세우지 않는다(PreviewOnly).
        var houses = new List<GameObject>();
        LockedSite.PreviewOnly = true;
        try
        {
            foreach (ZoneArea area in map.Areas)
                foreach (LockSite site in LockSiteTable.OpenHouses(map, area.ZoneId))
                    houses.Add(LockedSite.Create(site).gameObject);
            ClosedHanok.SpawnFor(map);
            GameObject closed = GameObject.Find("ClosedHanok");
            if (closed != null) houses.Add(closed);
        }
        finally { LockedSite.PreviewOnly = false; }
        Debug.Log($"[MapPhoto] {map.SceneName} 집 {houses.Count}채 세워 찍음");

        // 금줄은 끄고 찍는다.
        var hidden = new List<GameObject>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (t.name.StartsWith("Gate_"))
            {
                hidden.Add(t.gameObject);
                t.gameObject.SetActive(false);
            }

        // 【비스듬한 해】 — 바로 위에서만 비추면 덩어리가 납작한 색 판으로 찍힌다. 남동쪽으로 그림자를 떨어뜨려 높이가 읽히게.
        // 씬의 해는 잠시 끈다(밤 · 비 빛깔이 지도에 묻지 않게).
        var lights = new List<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            if (l.type == LightType.Directional && l.enabled)
            {
                lights.Add(l);
                l.enabled = false;
            }

        var sunObject = new GameObject("MapPhotoSun (bake)") { hideFlags = HideFlags.HideAndDontSave };
        var sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.7f;
        sunObject.transform.rotation = Quaternion.Euler(50f, 140f, 0f);   // 그림자는 오른쪽 아래(남동)로

        // 그림자 거리 — 렌더 설정의 값은 화면 둘레만 덮는다. 장 전체가 들어가게 잠시 늘린다(반사로 — Editor 어셈블리는 URP를 모른다).
        object pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        System.Reflection.PropertyInfo shadowProp = pipeline?.GetType().GetProperty("shadowDistance");
        object shadowBefore = shadowProp?.GetValue(pipeline);
        shadowProp?.SetValue(pipeline, Mathf.Max(map.HalfSize.x, map.HalfSize.y) * 1.6f + 60f);

        var go = new GameObject("MapPhotoCamera (bake)") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = map.HalfSize.y;
        cam.aspect = map.HalfSize.x / map.HalfSize.y;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = (Color)MapRaster.OutOfZone;
        // 낮게 — 그림자 거리(렌더 설정) 안에 땅이 들어오게. 덩어리는 이보다 낮다.
        go.transform.SetPositionAndRotation(new Vector3(0f, 40f, 0f), Quaternion.Euler(90f, 0f, 0f));

        int mask = ~0;
        int ui = LayerMask.NameToLayer("UI");
        int pixel = LayerMask.NameToLayer("PixelChar");
        if (ui >= 0) mask &= ~(1 << ui);
        if (pixel >= 0) mask &= ~(1 << pixel);
        mask &= ~(1 << MapDiorama.Layer);

        MapDiorama diorama = null;
        if (!UseSceneArt)
        {
            diorama = new MapDiorama(map);
            mask = 1 << MapDiorama.Layer;
            cam.backgroundColor = new Color(0.16f, 0.24f, 0.14f);
        }

        cam.cullingMask = mask;

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply(false);
        RenderTexture.active = previous;

        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(sunObject);
        diorama?.Dispose();

        if (shadowProp != null && shadowBefore != null)
            shadowProp.SetValue(pipeline, shadowBefore);

        foreach (Light l in lights)
            if (l != null)
                l.enabled = true;

        foreach (GameObject g in hidden)
            if (g != null)
                g.SetActive(true);

        foreach (GameObject g in houses)
            if (g != null)
                Object.DestroyImmediate(g);

        string path = $"{Folder}/{MapRuntime.PhotoName(map.Chapter)}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        Debug.Log($"[MapPhoto] {map.SceneName} → {path} ({w} × {h}, 1m {ppm}화소)");
        return path;
    }

    private static void Configure(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = MaxSize;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }
}
