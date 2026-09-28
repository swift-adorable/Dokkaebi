using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 벙커 씬을 만든다. (로드맵 8-J · docs/Blob_Bunker_System.md)
///
/// 【파밍 구역 씬에서 플레이어 · 카메라 · 조명 · 매니저를 복사해 온다.】
/// 두 씬이 같은 플레이어 설정을 써야 들고 다니는 것이 같게 느껴진다. 손으로 두 벌을
/// 맞추면 한쪽만 고치는 일이 반드시 생긴다 — 구역 씬을 고친 뒤 이 메뉴를 다시 누른다.
///
/// 【장면 파일(YAML)을 직접 고치지 않는다.】 열려 있는 씬을 밖에서 고치면 Unity가
/// 다시 읽을지 묻는 창을 띄운다. 에디터 API로 새 씬을 만들어 저장한다.
///
/// 복사한 것들 사이의 참조(카메라 → 플레이어 등)는 복사본끼리로 다시 잇는다.
/// 구역 씬에만 있는 것(스포너 · 바닥)을 가리키던 참조는 비운다.
/// </summary>
public static class BunkerSceneGenerator
{
    private const string RaidPath = "Assets/Scenes/SampleScene.unity";
    private const string BunkerPath = "Assets/Scenes/Bunker.unity";
    private const string MaterialFolder = "Assets/Data/Materials";

    /// <summary>구역 씬에서 가져오는 뿌리 오브젝트.</summary>
    private static readonly string[] CopiedRoots =
    {
        "Main Camera", "Directional Light", "Global Volume", "Blob",
        "Debug Object", "Canvas", "EventSystem", "Game Manager Object"
    };

    // 방 크기 (m) — 배치 모드와 같은 값을 쓴다 (BunkerLayout).
    private const float RoomWidth = BunkerLayout.RoomWidth;
    private const float RoomDepth = BunkerLayout.RoomDepth;
    private const float WallHeight = 2f;
    private const float WallThickness = 0.5f;

    private static readonly Vector3 SpawnPoint = BunkerLayout.Spawn;

    [MenuItem("Blob/Bunker/벙커 씬 생성")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Bunker] 플레이 중에는 만들 수 없습니다.");
            return;
        }

        Scene active = SceneManager.GetActiveScene();

        if (active.path == BunkerPath)
        {
            Debug.LogError("[Bunker] 벙커 씬이 열려 있습니다. 구역 씬(SampleScene)을 연 채로 실행하십시오.");
            return;
        }

        Scene raid = SceneManager.GetSceneByPath(RaidPath);
        bool openedRaid = false;

        if (!raid.isLoaded)
        {
            raid = EditorSceneManager.OpenScene(RaidPath, OpenSceneMode.Additive);
            openedRaid = true;
        }

        Scene bunker = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(bunker);

        try
        {
            Build(raid, bunker);

            EditorSceneManager.SaveScene(bunker, BunkerPath);
            Debug.Log($"[Bunker] {BunkerPath} 저장.");
        }
        finally
        {
            SceneManager.SetActiveScene(active.IsValid() && active.isLoaded ? active : raid);
            EditorSceneManager.CloseScene(bunker, true);

            if (openedRaid)
                EditorSceneManager.CloseScene(raid, true);
        }

        RegisterInBuild();
    }

    // ────────────────────────────────── 만들기

    private static void Build(Scene raid, Scene bunker)
    {
        var map = new Dictionary<Object, Object>();
        var copies = new Dictionary<string, GameObject>();

        foreach (GameObject root in raid.GetRootGameObjects())
        {
            if (System.Array.IndexOf(CopiedRoots, root.name) < 0)
                continue;

            GameObject copy = Object.Instantiate(root);
            copy.name = root.name;

            SceneManager.MoveGameObjectToScene(copy, bunker);

            MapHierarchy(root.transform, copy.transform, map);
            copies[root.name] = copy;
        }

        foreach (string name in CopiedRoots)
        {
            if (!copies.ContainsKey(name))
                Debug.LogWarning($"[Bunker] 구역 씬에 「{name}」이 없어 복사하지 못했습니다.");
        }

        RemapReferences(copies.Values, map, raid);

        PlacePlayer(copies);

        Material floorMaterial = MaterialFor("Bunker_Floor", new Color(0.23f, 0.25f, 0.27f));
        Material wallMaterial = MaterialFor("Bunker_Wall", new Color(0.36f, 0.38f, 0.40f));

        BuildRoom(floorMaterial, wallMaterial);

        // 고정 자리 셋 — 창고 · 파밍 출발 · 설계도 테이블.
        // 【상점은 고정 자리가 아니다】 — 덕코프처럼 지어야 생긴다 (8-K).
        // 설계도 테이블은 덕코프처럼 스폰 지점 오른쪽이다 [확인됨 — 위키 가이드].
        Camera camera = copies.TryGetValue("Main Camera", out GameObject cam) ? cam.GetComponent<Camera>() : null;

        CreateStation(BunkerStation.Kind.Stash, BunkerLayout.StashPosition,
            new Color(0.30f, 0.55f, 0.85f), camera);
        CreateStation(BunkerStation.Kind.Departure, BunkerLayout.DeparturePosition,
            new Color(0.35f, 0.85f, 0.45f), camera);
        CreateStation(BunkerStation.Kind.Blueprint, BunkerLayout.BlueprintPosition,
            new Color(0.75f, 0.75f, 0.80f), camera);

        // 지은 건물을 세우는 자리. 건물은 세이브에서 오므로 씬에는 이것만 둔다.
        new GameObject("Bunker Buildings").AddComponent<BunkerBuildings>();
    }

    /// <summary>원본과 복사본의 같은 자리에 있는 오브젝트 · 컴포넌트를 짝짓는다.</summary>
    private static void MapHierarchy(Transform original, Transform copy, Dictionary<Object, Object> map)
    {
        map[original.gameObject] = copy.gameObject;

        Component[] a = original.GetComponents<Component>();
        Component[] b = copy.GetComponents<Component>();

        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            if (a[i] != null && b[i] != null)
                map[a[i]] = b[i];
        }

        for (int i = 0; i < original.childCount && i < copy.childCount; i++)
            MapHierarchy(original.GetChild(i), copy.GetChild(i), map);
    }

    /// <summary>
    /// 복사본이 원본 씬을 가리키는 참조를 복사본으로 바꾼다.
    /// 짝이 없으면(스포너 · 바닥 등 가져오지 않은 것) 비운다 — 씬을 건너는 참조는 저장되지 않는다.
    /// </summary>
    private static void RemapReferences(IEnumerable<GameObject> roots, Dictionary<Object, Object> map, Scene raid)
    {
        foreach (GameObject root in roots)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;

                var so = new SerializedObject(component);
                SerializedProperty property = so.GetIterator();
                bool changed = false;

                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    Object target = property.objectReferenceValue;

                    if (target == null || !BelongsTo(target, raid))
                        continue;

                    if (map.TryGetValue(target, out Object replacement))
                    {
                        property.objectReferenceValue = replacement;
                    }
                    else
                    {
                        Debug.Log($"[Bunker] {component.GetType().Name}.{property.propertyPath} → " +
                                  $"「{target.name}」은 벙커에 없어 비웁니다.");
                        property.objectReferenceValue = null;
                    }

                    changed = true;
                }

                if (changed)
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static bool BelongsTo(Object target, Scene scene)
    {
        switch (target)
        {
            case GameObject go: return go.scene == scene;
            case Component c:   return c.gameObject.scene == scene;
            default:            return false;
        }
    }

    /// <summary>플레이어를 시작 자리로 옮긴다. 카메라도 같은 만큼 옮긴다 — 둘의 간격이 곧 시점이다.</summary>
    private static void PlacePlayer(Dictionary<string, GameObject> copies)
    {
        if (!copies.TryGetValue("Blob", out GameObject blob))
            return;

        Vector3 from = blob.transform.position;
        Vector3 to = new(SpawnPoint.x, from.y, SpawnPoint.z);
        Vector3 delta = to - from;

        blob.transform.position = to;

        if (copies.TryGetValue("Main Camera", out GameObject camera))
            camera.transform.position += delta;
    }

    private static void BuildRoom(Material floorMaterial, Material wallMaterial)
    {
        GameObject room = new("Bunker Room");

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(room.transform);
        floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        floor.transform.localScale = new Vector3(RoomWidth, 0.5f, RoomDepth);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

        float halfW = RoomWidth * 0.5f;
        float halfD = RoomDepth * 0.5f;

        Wall(room, "Wall_North", new Vector3(0f, WallHeight * 0.5f, halfD), new Vector3(RoomWidth + WallThickness, WallHeight, WallThickness), wallMaterial);
        Wall(room, "Wall_South", new Vector3(0f, WallHeight * 0.5f, -halfD), new Vector3(RoomWidth + WallThickness, WallHeight, WallThickness), wallMaterial);
        Wall(room, "Wall_East", new Vector3(halfW, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, RoomDepth), wallMaterial);
        Wall(room, "Wall_West", new Vector3(-halfW, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, RoomDepth), wallMaterial);
    }

    private static void Wall(GameObject room, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(room.transform);
        wall.transform.localPosition = position;
        wall.transform.localScale = size;
        wall.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreateStation(BunkerStation.Kind kind, Vector3 position, Color color, Camera camera)
    {
        GameObject station = new($"Station_{kind}");
        station.transform.position = position;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(station.transform);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        body.transform.localScale = new Vector3(1.6f, 1.2f, 1.2f);
        body.GetComponent<Renderer>().sharedMaterial = MaterialFor($"Bunker_{kind}", color);

        station.AddComponent<BunkerStation>().Setup(kind, 2.6f);

        // 이름표 — 멀리서도 무엇인지 보이게. 카메라를 향해 눕힌다.
        GameObject tag = new("Label");
        tag.transform.SetParent(station.transform);
        tag.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        tag.transform.rotation = camera != null ? camera.transform.rotation : Quaternion.Euler(60f, 0f, 0f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var text = tag.AddComponent<TextMesh>();
        text.text = BunkerStation.LabelOf(kind);
        text.font = font;
        text.fontSize = 64;
        text.characterSize = 0.06f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;

        tag.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    private static Material MaterialFor(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Materials");

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

    /// <summary>빌드 설정 맨 앞에 벙커를 둔다 — 게임은 벙커에서 시작한다.</summary>
    private static void RegisterInBuild()
    {
        var scenes = new List<EditorBuildSettingsScene> { new(BunkerPath, true) };

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path != BunkerPath)
                scenes.Add(scene);
        }

        if (!scenes.Exists(s => s.path == RaidPath) && File.Exists(RaidPath))
            scenes.Add(new EditorBuildSettingsScene(RaidPath, true));

        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log("[Bunker] 빌드 설정 — 0: Bunker · 1: SampleScene.");
    }
}
