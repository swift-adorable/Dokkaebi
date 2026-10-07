using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 벙커 씬에 지은 건물을 세운다 · 배치 모드를 돌린다. (로드맵 8-K)
///
/// 【덕코프의 건설 순서를 따른다】 [확인됨 — 위키 가이드]
///   1. 스폰 지점 오른쪽의 설계도 테이블에서 건물 목록을 연다
///   2. 재료가 충분하면 항목을 눌러 배치 모드로 들어간다
///   3. 회전하고 옮겨서 놓는다
///   4. 옮기고 싶으면 재활용한다 — 손실 없이 목록으로 돌아간다
///
/// 【모바일이라 바꾼 것】 덕코프는 마우스로 자리를 고르고 Q/E로 돌린다.
/// Dokkaebi에는 마우스가 없으므로 【건물 그림자가 플레이어 앞에 붙어 다닌다】 —
/// 조이스틱으로 걸어가 자리를 잡고, 화면 아래 「회전 · 배치 · 취소」를 누른다.
/// </summary>
public class BunkerBuildings : MonoBehaviour
{
    /// <summary>그림자가 플레이어 앞으로 떨어진 거리 (m). 화면 위쪽이 +Z다.</summary>
    private const float GhostDistance = 2.5f;

    private static BunkerBuildings instance;

    private readonly List<GameObject> spawned = new();

    private BuildingDefinition placing;
    private int placingTurns;
    private GameObject ghost;
    private Material ghostMaterial;
    private Transform player;
    private bool canPlaceNow;

    public static bool IsPlacing => instance != null && instance.placing != null;

    public static BunkerBuildings Instance => instance;

    private void OnEnable()
    {
        instance = this;
        BuildingManager.OnChanged += Rebuild;
        EnsureSpring();
        Rebuild();
    }

    private GameObject spring;

    /// <summary>
    /// 고목 뿌리 샘 (결정 2-73). 짓는 것이 아니라 처음부터 있다 — 씬 생성기를 다시 돌리지 않도록 여기서 세운다.
    /// 고정 자리라 건물을 그 위에 놓을 수 없다(Occupied).
    /// </summary>
    private void EnsureSpring()
    {
        if (spring != null)
            return;

        spring = new GameObject("Spring");
        spring.transform.SetParent(transform, false);
        spring.transform.position = BunkerLayout.SpringPosition;

        GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pool.name = "Body";
        pool.transform.SetParent(spring.transform, false);
        pool.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pool.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
        Tint(pool, new Color(0.30f, 0.55f, 0.80f));

        AddLabel(spring.transform, SpringTable.Name);
        spring.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.Spring, 1.8f);
    }

    private void OnDisable()
    {
        BuildingManager.OnChanged -= Rebuild;

        if (instance == this)
            instance = null;
    }

    // ────────────────────────────────── 세우기

    private void Rebuild()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
                Destroy(go);
        }

        spawned.Clear();

        foreach (KeyValuePair<string, BuildingPose> pair in BuildingManager.State.Placed)
        {
            BuildingDefinition definition = BuildingTable.Find(pair.Key);

            if (definition != null)
                spawned.Add(Spawn(definition, pair.Value));
        }
    }

    /// <summary>잡화 가게 자리의 오른쪽 끝에 붙는 부뚜막의 폭(m).</summary>
    private const float HearthWidth = 1.2f;

    private GameObject Spawn(BuildingDefinition definition, BuildingPose pose)
    {
        var root = new GameObject($"Building_{definition.Id}");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(pose.X, 0f, pose.Z);
        root.transform.rotation = Quaternion.Euler(0f, pose.Turns * 90f, 0f);

        // 잡화 가게는 자리의 왼쪽에 좌판, 오른쪽 끝에 부뚜막이 붙는다 (결정 2-71) — 가게를 놓거나 옮기면 함께 간다.
        // 부뚜막은 가게의 자리(Width) 안에 있어 다른 건물과 겹치지 않는다.
        bool withHearth = definition.Id == BuildingTable.GeneralStore;
        float storeWidth = withHearth ? definition.Width - HearthWidth : definition.Width;
        float storeX = withHearth ? -HearthWidth * 0.5f : 0f;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(storeX, 0.6f, 0f);
        body.transform.localScale = new Vector3(storeWidth, 1.2f, definition.Depth);
        Tint(body, ColorOf(definition.Opens));

        var storeSpot = new GameObject("Station");
        storeSpot.transform.SetParent(root.transform, false);
        storeSpot.transform.localPosition = new Vector3(storeX, 0f, 0f);
        AddLabel(storeSpot.transform, definition.Name);

        if (definition.Opens != BunkerStation.Kind.None)
            storeSpot.AddComponent<BunkerStation>().Setup(definition.Opens, 2.6f);

        if (withHearth)
        {
            var hearth = new GameObject("Hearth");
            hearth.transform.SetParent(root.transform, false);
            hearth.transform.localPosition = new Vector3((definition.Width - HearthWidth) * 0.5f, 0f, 0f);

            GameObject stove = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stove.name = "Body";
            stove.transform.SetParent(hearth.transform, false);
            stove.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            stove.transform.localScale = new Vector3(HearthWidth * 0.9f, 0.8f, definition.Depth * 0.8f);
            Tint(stove, new Color(0.45f, 0.30f, 0.22f));

            AddLabel(hearth.transform, CookingTable.HearthName);
            hearth.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.Cooking, 1.6f);
        }

        return root;
    }

    private static Color ColorOf(BunkerStation.Kind kind)
    {
        switch (kind)
        {
            case BunkerStation.Kind.GeneralStore: return new Color(0.90f, 0.70f, 0.25f);
            case BunkerStation.Kind.Smithy:       return new Color(0.85f, 0.35f, 0.30f);
            case BunkerStation.Kind.Apothecary:   return new Color(0.40f, 0.70f, 0.50f);
            default:                               return new Color(0.55f, 0.50f, 0.42f);
        }
    }

    private static void Tint(GameObject target, Color color)
    {
        var renderer = target.GetComponent<Renderer>();

        if (renderer == null)
            return;

        // 인스턴스 머티리얼 — 공유 머티리얼을 칠하면 모든 상자가 같은 색이 된다.
        Material material = renderer.material;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
    }

    /// <summary>이름표. 카메라를 향해 눕힌다 — 위에서 내려다보는 시점이다.</summary>
    private static void AddLabel(Transform parent, string text)
    {
        var tag = new GameObject("Label");
        tag.transform.SetParent(parent, false);
        tag.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        Camera camera = Camera.main;
        tag.transform.rotation = camera != null ? camera.transform.rotation : Quaternion.Euler(60f, 0f, 0f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var mesh = tag.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = 64;
        mesh.characterSize = 0.06f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.white;

        tag.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    // ────────────────────────────────── 배치 모드

    /// <summary>배치 모드로 들어간다. 【가지고 있어야 한다】 — 값은 이미 치렀다.</summary>
    public void BeginPlacing(BuildingDefinition definition)
    {
        if (definition == null || !BuildingManager.State.Owns(definition.Id))
            return;

        EndPlacing();

        placing = definition;
        placingTurns = 0;

        ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "BuildingGhost";

        // 그림자는 부딪치지 않는다 — 플레이어가 밀려나면 자리를 잡을 수 없다.
        Destroy(ghost.GetComponent<Collider>());

        ghostMaterial = ghost.GetComponent<Renderer>().material;
        ghost.transform.localScale = new Vector3(definition.Width, 1.2f, definition.Depth);

        PlacementHudUI.Show(definition.Name);
    }

    public void Rotate()
    {
        if (placing != null)
            placingTurns = (placingTurns + 1) % 4;
    }

    /// <summary>지금 자리에 놓는다. 놓을 수 없는 자리면 알리고 그대로 둔다.</summary>
    public bool Confirm()
    {
        if (placing == null)
            return false;

        if (!canPlaceNow)
        {
            PlacementHudUI.Flash("여기에는 놓을 수 없습니다.");
            return false;
        }

        BuildingPose pose = CurrentPose();
        string id = placing.Id;

        EndPlacing();

        BuildingManager.Place(id, pose);
        SaveManager.Commit("건설");

        return true;
    }

    /// <summary>
    /// 그만둔다. 【지은 건물은 그대로 가진다】 — 덕코프의 재활용과 같다.
    /// 설계도 테이블 목록에서 다시 「배치」를 누르면 된다.
    /// </summary>
    public void EndPlacing()
    {
        placing = null;

        if (ghost != null)
            Destroy(ghost);

        ghost = null;
        PlacementHudUI.Hide();
    }

    private BuildingPose CurrentPose()
    {
        Vector3 anchor = player != null ? player.position : BunkerLayout.Spawn;

        return new BuildingPose(
            BunkerLayout.SnapValue(anchor.x),
            BunkerLayout.SnapValue(anchor.z + GhostDistance),
            placingTurns);
    }

    private void Update()
    {
        if (placing == null || ghost == null)
            return;

        if (player == null)
        {
            var dokkaebi = FindAnyObjectByType<DokkaebiController>(FindObjectsInactive.Exclude);
            player = dokkaebi != null ? dokkaebi.transform : null;
        }

        BuildingPose pose = CurrentPose();

        ghost.transform.position = new Vector3(pose.X, 0.6f, pose.Z);
        ghost.transform.rotation = Quaternion.Euler(0f, pose.Turns * 90f, 0f);

        canPlaceNow = BunkerLayout.CanPlace(BunkerLayout.Footprint(placing, pose), Occupied(placing.Id));

        Color color = canPlaceNow ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.90f, 0.35f, 0.30f);
        ghostMaterial.color = color;

        if (ghostMaterial.HasProperty("_BaseColor"))
            ghostMaterial.SetColor("_BaseColor", color);
    }

    /// <summary>
    /// 이미 차 있는 바닥 — 고정 자리 셋 · 놓인 건물 · 플레이어가 선 자리.
    /// 플레이어 위에 놓으면 몸이 건물 안에 갇힌다.
    /// </summary>
    private IEnumerable<Rect> Occupied(string exceptId)
    {
        yield return BunkerLayout.FixedRect(BunkerLayout.StashPosition);
        yield return BunkerLayout.FixedRect(BunkerLayout.DeparturePosition);
        yield return BunkerLayout.FixedRect(BunkerLayout.BlueprintPosition);
        yield return BunkerLayout.FixedRect(BunkerLayout.SpringPosition);

        if (player != null)
            yield return new Rect(player.position.x - 0.6f, player.position.z - 0.6f, 1.2f, 1.2f);

        foreach (KeyValuePair<string, BuildingPose> pair in BuildingManager.State.Placed)
        {
            if (pair.Key == exceptId)
                continue;

            BuildingDefinition other = BuildingTable.Find(pair.Key);

            if (other != null)
                yield return BunkerLayout.Footprint(other, pair.Value);
        }
    }
}
