using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【장 맵 굽기】 (결정 2-86) — ZoneMapTable의 그레이박스를 씬으로 만든다.
///
///   1) 옛 파밍 씬(SampleScene)을 「Chapter0.unity」로 복사한다 — 플레이어 · 카메라 · 매니저 · 스포너가 그대로 온다
///   2) 바닥을 맵 크기로 늘리고, 덩어리 · 금줄을 단색 블록으로 얹는다 (맵 뿌리 「Map_Ch0」 + ZoneMap)
///   3) 빌드 설정에 넣는다 — SceneFlow가 그 장의 구역을 이 씬에서 돌린다
/// 【다시 구우면 덮어쓴다】 손으로 고친 것은 사라진다 — 자리는 표(ZoneMapTable)에서 고친다.
/// </summary>
public static class ZoneMapBaker
{
    private const string RaidPath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialFolder = "Assets/Data/Materials/Map";

    [MenuItem("Dokkaebi/Map/장 맵 굽기 (전부)")]
    public static void BakeAll()
    {
        foreach (ChapterMap map in ZoneMapTable.All)
            Bake(map);

        // 빌드 설정(ProjectSettings)을 디스크에 쓴다 — 안 쓰면 다음 실행까지 빌드에 없는 것으로 남는다.
        AssetDatabase.SaveAssets();

        Debug.Log($"[ZoneMap] 장 맵 {ZoneMapTable.All.Count}개를 구웠습니다.");
    }

    public static string PathFor(ChapterMap map) => $"Assets/Scenes/{map.SceneName}.unity";

    public static void Bake(ChapterMap map)
    {
        string path = PathFor(map);

        Scene existing = SceneManager.GetSceneByPath(path);
        if (existing.IsValid() && existing.isLoaded)
            EditorSceneManager.CloseScene(existing, true);

        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        if (!AssetDatabase.CopyAsset(RaidPath, path))
        {
            Debug.LogError($"[ZoneMap] {RaidPath} → {path} 복사에 실패했습니다.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            Build(scene, map);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        RegisterInBuild(path);
        Debug.Log($"[ZoneMap] {map.SceneName} — 덩어리 {map.Blocks.Length} · 금줄 {map.Gates.Length} · 자리 {map.Anchors.Length}");
    }

    private static void Build(Scene scene, ChapterMap map)
    {
        GameObject floor = null;

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Floor")
                floor = root;

        // 바닥 — Plane은 10 × 10이라 배율 = 크기 / 10.
        if (floor != null)
        {
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(map.HalfSize.x * 2f / 10f, 1f, map.HalfSize.y * 2f / 10f);
        }
        else
        {
            Debug.LogWarning("[ZoneMap] 복사한 씬에 Floor가 없습니다.");
        }

        var rootObject = new GameObject($"Map_Ch{map.Chapter}");
        SceneManager.MoveGameObjectToScene(rootObject, scene);

        var zoneMap = rootObject.AddComponent<ZoneMap>();
        zoneMap.EditorSetChapter(map.Chapter);
        EditorUtility.SetDirty(zoneMap);

        var counts = new Dictionary<MapBlockKind, int>();

        foreach (MapBlock block in map.Blocks)
        {
            counts.TryGetValue(block.Kind, out int n);
            counts[block.Kind] = n + 1;

            GameObject go = Block(block, $"{block.Kind}_{n}", rootObject.transform);

            if (!block.Solid)
                Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        for (int i = 0; i < map.Gates.Length; i++)
        {
            MapGate gate = map.Gates[i];
            GameObject go = Block(gate.Block, $"Gate_{gate.ZoneId}#{i}", rootObject.transform);
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor("Map_Gate", new Color(0.92f, 0.86f, 0.70f));
        }

        // 자리 표시 — 보이지 않는 빈 오브젝트. 씬에서 어디인지 보려고 둔다(런타임은 표를 읽는다).
        var anchors = new GameObject("Anchors");
        anchors.transform.SetParent(rootObject.transform, false);

        foreach (MapAnchor a in map.Anchors)
        {
            var marker = new GameObject($"{a.Kind}_{a.ZoneId}");
            marker.transform.SetParent(anchors.transform, false);
            marker.transform.position = a.Position;
        }
    }

    private static GameObject Block(MapBlock block, string name, Transform parent)
    {
        PrimitiveType shape = block.Kind == MapBlockKind.Tree ? PrimitiveType.Cylinder : PrimitiveType.Cube;
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);

        float height = Mathf.Max(0.02f, block.Size.y);
        go.transform.position = new Vector3(block.Center.x, height * 0.5f, block.Center.y);

        // 원기둥은 높이 2가 기본 — 배율 y는 반으로.
        go.transform.localScale = shape == PrimitiveType.Cylinder
            ? new Vector3(block.Size.x, height * 0.5f, block.Size.z)
            : new Vector3(block.Size.x, height, block.Size.z);

        go.GetComponent<Renderer>().sharedMaterial = MaterialFor($"Map_{block.Kind}", ColorOf(block.Kind));
        go.isStatic = true;
        return go;
    }

    private static Color ColorOf(MapBlockKind kind) => kind switch
    {
        MapBlockKind.Boundary => new Color(0.22f, 0.22f, 0.24f),
        MapBlockKind.Bamboo => new Color(0.24f, 0.46f, 0.26f),
        MapBlockKind.Rock => new Color(0.46f, 0.44f, 0.40f),
        MapBlockKind.Building => new Color(0.42f, 0.30f, 0.20f),
        MapBlockKind.Bush => new Color(0.30f, 0.40f, 0.20f),
        MapBlockKind.Tree => new Color(0.30f, 0.22f, 0.14f),
        MapBlockKind.Water => new Color(0.24f, 0.42f, 0.60f),
        MapBlockKind.Fence => new Color(0.36f, 0.26f, 0.16f),
        _ => Color.gray,
    };

    private static Material MaterialFor(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Data/Materials", "Map");

        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>빌드 설정에 장 맵을 넣는다 — 벙커(0) · SampleScene 뒤에.</summary>
    private static void RegisterInBuild(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        if (!scenes.Exists(s => s.path == path))
            scenes.Add(new EditorBuildSettingsScene(path, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
