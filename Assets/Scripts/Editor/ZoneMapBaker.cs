using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 【장 맵 굽기】 (결정 2-86) — ZoneMapTable의 그레이박스를 씬으로 만든다.
///
///   1) 옛 파밍 씬(SampleScene)을 「Chapter0.unity」로 복사한다 — 플레이어 · 카메라 · 매니저 · 스포너가 그대로 온다
///   2) 바닥을 맵 크기로 늘리고, 덩어리 · 금줄을 단색 블록으로 얹는다 (맵 뿌리 「Map_Ch0」 + ZoneMap)
///   3) 빌드 설정에 넣는다 — SceneFlow가 그 장의 구역을 이 씬에서 돌린다
///   4) 덩어리에 레이어를 붙인다 — 키 큰 것 Obstacle(화살 · 시야를 막는다) · 낮은 것 LowCover (ObstacleRules · 결정 2-89)
///   5) NavMesh를 굽는다 — 적이 덩어리를 돌아간다. 금줄은 길을 파내는 장애물(열리면 걷힌다) · 덩어리 지붕은 못 걷는 땅
/// 【다시 구우면 덮어쓴다】 손으로 고친 것은 사라진다 — 자리는 표(ZoneMapTable)에서 고친다.
/// </summary>
public static class ZoneMapBaker
{
    private const string RaidPath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialFolder = "Assets/Data/Materials/Map";

    [MenuItem("Dokkaebi/Map/장 맵 굽기 (전부)")]
    public static void BakeAll()
    {
        EnsureLayers();

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

        // 이미 있으면 내용만 덮어쓴다 — .meta(GUID)를 지켜야 빌드 설정 · 참조가 끊기지 않는다.
        if (File.Exists(path))
        {
            File.Copy(RaidPath, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        else if (!AssetDatabase.CopyAsset(RaidPath, path))
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
            go.layer = ObstacleRules.LayerFor(block);

            if (!block.Solid)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                continue;
            }

            // 지붕 위는 걷는 땅이 아니다 — 덩어리는 길을 막기만 한다.
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NotWalkableArea;
        }

        for (int i = 0; i < map.Gates.Length; i++)
        {
            MapGate gate = map.Gates[i];
            GameObject go = Block(gate.Block, $"Gate_{gate.ZoneId}#{i}", rootObject.transform);
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor("Map_Gate", new Color(0.92f, 0.86f, 0.70f));
            go.layer = ObstacleRules.GateLayer;
            go.isStatic = false;   // 열리면 꺼진다

            // NavMesh에 굽지 않고, 서 있는 동안만 길을 파낸다 — 구역이 열려 꺼지면 길이 이어진다.
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;   // 배율이 곧 크기
            obstacle.carving = true;
        }

        BakeNavMesh(rootObject, map);

        // 자리 표시 — 보이지 않는 빈 오브젝트. 씬에서 어디인지 보려고 둔다(런타임은 표를 읽는다).
        var anchors = new GameObject("Anchors");
        anchors.transform.SetParent(rootObject.transform, false);

        foreach (MapAnchor a in map.Anchors)
        {
            // 조각 · 표지는 이야기 속 자리 이름을 붙인다 — 레이어 · 아트 작업 때 씬에서 찾는다 (결정 2-87).
            string name = string.IsNullOrEmpty(a.Label) ? $"{a.Kind}_{a.ZoneId}" : $"{a.Kind}_{a.ZoneId}_{a.Label}";
            var marker = new GameObject(name);
            marker.transform.SetParent(anchors.transform, false);
            marker.transform.position = a.Position;
        }
    }

    private const int NotWalkableArea = 1;
    private const string NavMeshFolder = "Assets/Data/NavMesh";

    /// <summary>
    /// NavMesh를 굽는다 (결정 2-89) — 맵 뿌리 아래 덩어리 콜라이더 + 맵 크기의 임시 바닥 상자로.
    /// 바닥(Floor)은 씬 뿌리에 따로 있고 플레이어 · 적 콜라이더도 씬에 있어서, 「아래 것만」 모으려고 임시 바닥을 둔다.
    /// 데이터는 Assets/Data/NavMesh/Chapter{n}_NavMesh.asset.
    /// </summary>
    private static void BakeNavMesh(GameObject root, ChapterMap map)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "NavGround (bake only)";
        ground.transform.SetParent(root.transform, false);
        ground.transform.position = new Vector3(0f, -0.05f, 0f);
        ground.transform.localScale = new Vector3(map.HalfSize.x * 2f, 0.1f, map.HalfSize.y * 2f);
        Object.DestroyImmediate(ground.GetComponent<Renderer>());

        var surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();

        Object.DestroyImmediate(ground);

        if (surface.navMeshData == null)
        {
            Debug.LogError($"[ZoneMap] {map.SceneName} NavMesh를 굽지 못했습니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(NavMeshFolder))
            AssetDatabase.CreateFolder("Assets/Data", "NavMesh");

        string path = $"{NavMeshFolder}/{map.SceneName}_NavMesh.asset";
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
    }

    /// <summary>맵 레이어 이름을 TagManager에 적는다 (GameLayers — 번호는 코드가 정한다).</summary>
    public static void EnsureLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        SetLayer(layers, GameLayers.Obstacle, GameLayers.ObstacleName);
        SetLayer(layers, GameLayers.LowCover, GameLayers.LowCoverName);

        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayer(SerializedProperty layers, int index, string name)
    {
        SerializedProperty slot = layers.GetArrayElementAtIndex(index);

        if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != name)
            Debug.LogWarning($"[ZoneMap] 레이어 {index}「{slot.stringValue}」를 「{name}」으로 바꿉니다.");

        slot.stringValue = name;
    }

    private static GameObject Block(MapBlock block, string name, Transform parent)
    {
        PrimitiveType shape = block.Kind is MapBlockKind.Tree or MapBlockKind.Well ? PrimitiveType.Cylinder : PrimitiveType.Cube;
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
        MapBlockKind.Stall => new Color(0.62f, 0.48f, 0.30f),
        MapBlockKind.Well => new Color(0.55f, 0.55f, 0.62f),
        MapBlockKind.Post => new Color(0.72f, 0.62f, 0.44f),
        MapBlockKind.Flowerbed => new Color(0.78f, 0.42f, 0.58f),
        MapBlockKind.Geumjul => new Color(0.94f, 0.88f, 0.52f),
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

        // 같은 경로가 있으면 새로 만든 항목으로 바꾼다 — GUID가 지금 .meta와 맞도록.
        int index = scenes.FindIndex(s => s.path == path);
        var entry = new EditorBuildSettingsScene(path, true);

        if (index >= 0)
            scenes[index] = entry;
        else
            scenes.Add(entry);

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
