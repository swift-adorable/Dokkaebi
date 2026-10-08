using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장 맵 씬의 런타임 (결정 2-86 · ZoneMapTable). 「장 맵 굽기」가 맵 뿌리에 붙여 둔다.
///
///   · 들어오면 고른 구역의 출발 자리로 플레이어를 옮긴다 (다른 Start보다 먼저 — 길목 · 열매 나무가 그 자리를 본다)
///   · 아직 열리지 않은 구역의 금줄을 세운다 · 열린 구역의 금줄은 걷는다
///   · 길목 · 조각 · 열매 나무 · 봇짐 · 큰 요괴 자리를 묻는 곳 — 없으면(맵이 없는 씬) 부른 쪽이 예전처럼 플레이어 둘레에 놓는다
/// </summary>
[DefaultExecutionOrder(-500)]
public class ZoneMap : MonoBehaviour
{
    [SerializeField] private int chapter;

    public static ZoneMap Current { get; private set; }

    public ChapterMap Map => ZoneMapTable.Of(chapter);

    /// <summary>이번 판의 구역.</summary>
    public static string Zone => StoryManager.TargetZone;

    public void EditorSetChapter(int value) => chapter = value;

    private void Awake() => Current = this;

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    private void Start()
    {
        ChapterMap map = Map;
        if (map == null)
            return;

        PlacePlayer(map);
        MapRuntime.Attach(gameObject, chapter);   // 지도 · 미니맵 (결정 2-90)
        RefreshGates();
        SecretPassage.SpawnFor(chapter);   // 역행 비밀 통로 — 입구 구역을 끝냈을 때만 (결정 2-88)
    }

    private void PlacePlayer(ChapterMap map)
    {
        // 비밀 통로로 왔으면 출구에 선다 (결정 2-88). 아니면 고른 구역의 출발 자리.
        Vector3 at;
        if (!SceneFlow.TryTakeArrival(map.Chapter, out at))
        {
            List<MapAnchor> starts = map.AnchorsOf(MapAnchorKind.Start, Zone);
            if (starts.Count == 0)
                return;

            at = starts[0].Position;
        }

        var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        if (movement == null)
            return;

        at.y = movement.transform.position.y;

        if (movement.TryGetComponent(out Rigidbody body))
        {
            body.position = at;
            body.linearVelocity = Vector3.zero;
        }

        movement.transform.position = at;
    }

    /// <summary>금줄 — 열리지 않은 구역으로 가는 길만 막는다. 이름은 「Gate_구역」 (굽기가 만든다).</summary>
    public void RefreshGates()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.StartsWith("Gate_"))
                continue;

            string zone = child.name.Substring("Gate_".Length);
            int cut = zone.IndexOf('#');
            if (cut >= 0)
                zone = zone.Substring(0, cut);

            child.gameObject.SetActive(!StoryManager.Progress.IsZoneOpen(zone));
        }

        // 지도의 금줄 · 닫힌 구역도 같이 (결정 2-90).
        if (TryGetComponent(out MapRuntime runtime))
            runtime.RedrawMap();
    }

    // ── 자리 묻기 ───────────────────────────────────────────────────

    /// <summary>이번 구역의 그 종류 자리들. 맵이 없으면 빈 목록.</summary>
    public static List<Vector3> Anchors(MapAnchorKind kind)
    {
        var list = new List<Vector3>();
        ChapterMap map = Current != null ? Current.Map : null;

        if (map == null)
            return list;

        foreach (MapAnchor a in map.AnchorsOf(kind, Zone))
            list.Add(a.Position);

        return list;
    }

    /// <summary>적이 나와도 되는 자리인가. 맵이 없으면 언제나 된다.</summary>
    public static bool CanSpawnAt(Vector3 p)
    {
        ChapterMap map = Current != null ? Current.Map : null;
        return map == null || map.CanStand(p, StoryManager.Progress.IsZoneOpen);
    }
}
