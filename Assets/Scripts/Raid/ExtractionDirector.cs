using UnityEngine;

/// <summary>
/// 【임시】 철수 지점 2곳을 세우고, 원 안에서 5초 버티면 철수시킨다 (결정 2-78 · ExtractionTable).
///
///   · 구역 씬이 열릴 때 스포너가 한 번 부른다 — 열매 나무와 같은 자리
///   · 초록 기둥 + 바닥 원 · 화면 가장자리 화살표(ExtractionHudUI)로 가장 가까운 곳을 가리킨다
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
    private readonly ExtractionChannel channel = new();
    private Transform player;
    private Health playerHealth;

    public static ExtractionDirector Instance { get; private set; }
    public Vector3[] Points => points;

    /// <summary>이번 구역에 철수 지점을 세운다. 이미 있으면 그대로 둔다.</summary>
    public static ExtractionDirector SpawnForRaid(Vector3 start)
    {
        if (Instance != null)
            return Instance;

        var root = new GameObject("ExtractionPoints (Runtime)");
        var director = root.AddComponent<ExtractionDirector>();
        director.points = ExtractionTable.PickPoints(start, new System.Random());

        for (int i = 0; i < director.points.Length; i++)
            BuildMarker(root.transform, director.points[i], i);

        GameLogger.Log($"[Extraction] 철수 지점 {director.points.Length}곳");
        return director;
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

        int nearest = ExtractionTable.Nearest(player.position, points);
        ExtractionHudUI.EnsureInstance().Show(player.position, points[nearest], channel, dead);
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
