using UnityEngine;

/// <summary>
/// 【임시】 철수 지점 2곳을 세우고, 원 안에서 5초 버티면 철수시킨다 (결정 2-78 · ExtractionTable).
///
///   · 구역 씬이 열릴 때 스포너가 한 번 부른다 — 열매 나무와 같은 자리
///   · 초록 기둥 + 바닥 원 · 화면 가장자리 화살표(ExtractionHudUI)로 【찾은 곳 중】 가장 가까운 곳을 가리킨다
///   · 【찾기】 (결정 2-90) — 기둥이 화면에 들어오고 키 큰 덩어리가 가리지 않으면 찾은 것. 판마다 새로 찾는다.
///     지도 · 미니맵 · 화살표는 찾은 길목만 보여 준다
///   · 다 버티면 SceneFlow.Extract — 철수 저장 · 상점 재고 · 샘 채움까지 그쪽이 한다
/// 구역 맵(9단계)이 생기면 자리만 맵이 정한다.
/// </summary>
public class ExtractionDirector : MonoBehaviour
{
    /// <summary>화면 이름 — 기획 용어는 「철수 지점」, 화면 · 이야기에서는 「길목」 (결정 2-42).</summary>
    public const string Name = "길목";

    private static readonly Color PillarColor = new(0.35f, 0.95f, 0.45f);
    private static readonly Color CircleColor = new(0.18f, 0.55f, 0.26f);

    private Vector3[] points;
    private bool[] discovered;
    private float nextDiscover;
    private readonly ExtractionChannel channel = new();
    private Transform player;
    private Health playerHealth;

    public static ExtractionDirector Instance { get; private set; }
    public Vector3[] Points => points;

    /// <summary>이 길목을 이번 판에 찾았는가 (결정 2-90).</summary>
    public bool IsDiscovered(int index)
        => discovered != null && index >= 0 && index < discovered.Length && discovered[index];

    /// <summary>이번 구역에 철수 지점을 세운다. 이미 있으면 그대로 둔다.</summary>
    public static ExtractionDirector SpawnForRaid(Vector3 start)
    {
        if (Instance != null)
            return Instance;

        var root = new GameObject("ExtractionPoints (Runtime)");
        var director = root.AddComponent<ExtractionDirector>();
        // 장 맵이 있으면 그 구역의 길목 자리 (결정 2-86). 없으면 출발 자리에서 25~35m 무작위 두 곳.
        System.Collections.Generic.List<Vector3> anchors = ZoneMap.Anchors(MapAnchorKind.Extraction);
        director.points = anchors.Count > 0
            ? anchors.ToArray()
            : ExtractionTable.PickPoints(start, new System.Random());
        director.discovered = new bool[director.points.Length];

        // 「돌아갈 길목」 (결정 2-93) — 찾지 않아도 처음부터 보인다(지도 · 미니맵 · 화살표).
        if (MapPassives.ExtractMark)
            for (int i = 0; i < director.discovered.Length; i++)
                director.discovered[i] = true;

        for (int i = 0; i < director.points.Length; i++)
            BuildMarker(root.transform, director.points[i], i);

        GameLogger.Log($"[Extraction] 철수 지점 {director.points.Length}곳");
        return director;
    }

    /// <summary>길목을 모두 찾은 것으로 — 「돌아갈 길목」 · 검증 도구.</summary>
    public void RevealAll()
    {
        if (discovered == null)
            return;

        for (int i = 0; i < discovered.Length; i++)
            discovered[i] = true;
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        ExtractionHudUI.Clear();
    }

    private void Update()
    {
        if (points == null || points.Length == 0 || channel.Completed)
            return;

        if (player == null && !FindPlayer())
            return;

        bool dead = playerHealth != null && playerHealth.IsDead;
        bool inside = false;

        for (int i = 0; !dead && i < points.Length; i++)
            inside |= ExtractionTable.InCircle(player.position, points[i]);

        ExtractionStep step = channel.Tick(inside, Time.deltaTime);

        switch (step)
        {
            case ExtractionStep.Started:
                StoryDialogueUI.ShowBanner($"{Name} — {ExtractionTable.ChannelSeconds:0}초 버티면 철수한다.", 2f);
                break;
            case ExtractionStep.Cancelled:
                StoryDialogueUI.ShowBanner("철수가 끊겼다 — 원을 벗어났다.", 1.5f);
                break;
            case ExtractionStep.Completed:
                StoryDialogueUI.ShowBanner("철수한다.", 1.5f);
                ExtractionHudUI.Clear();
                SceneFlow.Extract();
                return;
        }

        // 원 안에 선 길목은 찾은 것이다.
        for (int i = 0; i < points.Length; i++)
            if (!discovered[i] && ExtractionTable.InCircle(player.position, points[i]))
                Discover(i);

        if (Time.time >= nextDiscover)
        {
            nextDiscover = Time.time + MapTable.DiscoverInterval;
            DiscoverVisible();
        }

        int nearest = NearestDiscovered(player.position);
        ExtractionHudUI.EnsureInstance().Show(player.position,
            nearest >= 0 ? points[nearest] : (Vector3?)null, channel, dead);
    }

    /// <summary>찾은 길목 중 가장 가까운 것. 하나도 없으면 -1.</summary>
    private int NearestDiscovered(Vector3 from)
    {
        int best = -1;
        float bestSq = float.MaxValue;

        for (int i = 0; i < points.Length; i++)
        {
            if (!discovered[i])
                continue;

            float dx = from.x - points[i].x, dz = from.z - points[i].z;
            float sq = dx * dx + dz * dz;

            if (sq < bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// 기둥 가운데(4m)가 화면 안이고, 카메라에서 그 점까지 키 큰 덩어리가 가리지 않으면 찾은 것 (결정 2-90).
    /// </summary>
    private void DiscoverVisible()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        for (int i = 0; i < points.Length; i++)
        {
            if (discovered[i])
                continue;

            Vector3 sight = points[i] + Vector3.up * MapTable.PillarSightHeight;
            Vector3 vp = cam.WorldToViewportPoint(sight);

            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                continue;

            if (Physics.Linecast(cam.transform.position, sight, GameLayers.ShotBlockMask, QueryTriggerInteraction.Ignore))
                continue;

            Discover(i);
        }
    }

    private void Discover(int index)
    {
        discovered[index] = true;
        StoryDialogueUI.ShowBanner($"{Name}을 찾았다.", 1.5f);
    }

    private bool FindPlayer()
    {
        var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        if (movement == null)
            return false;

        player = movement.transform;
        playerHealth = movement.GetComponent<Health>();
        return true;
    }

    private static void BuildMarker(Transform parent, Vector3 at, int index)
    {
        var marker = new GameObject($"ExtractionPoint_{index}");
        marker.transform.SetParent(parent, false);
        marker.transform.position = at;

        GameObject circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        circle.name = "Circle";
        circle.transform.SetParent(marker.transform, false);
        circle.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        circle.transform.localScale = new Vector3(ExtractionTable.Radius * 2f, 0.02f, ExtractionTable.Radius * 2f);
        Object.Destroy(circle.GetComponent<Collider>());
        Tint(circle, CircleColor);

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Pillar";
        pillar.transform.SetParent(marker.transform, false);
        pillar.transform.localPosition = new Vector3(0f, 4f, 0f);
        pillar.transform.localScale = new Vector3(0.35f, 4f, 0.35f);
        Object.Destroy(pillar.GetComponent<Collider>());
        Tint(pillar, PillarColor);
    }

    private static void Tint(GameObject target, Color color)
    {
        var renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            return;

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material m = renderer.material;
        m.color = color;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.6f);
        }
    }
}
