using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【덕코프식 시야】 (2026-11-23 · 결정 2-154) — 주인공이 보는 쪽 부채꼴 + 둘레 작은 원만 「보이는 곳」.
/// 벽 · 담 · 나무 밑동(Obstacle 층)이 시선을 막는다. 보이지 않는 땅 · 집 · 나무는 어둡고 빛 빠진 색(SeeThroughLit 의 ApplyVision),
/// 보이지 않는 적은 서서히 사라진다.
///
///   · 매 프레임 주인공 눈높이에서 Rays 갈래 광선 → 갈래마다 보이는 거리
///   · v3: 그 거리 360개를 한 줄 그림(_VisionRays)으로 넘기고, 셰이더가 화면의 점마다 「주인공에게서 몇 도 · 몇 m」 를 계산해 견준다.
///     (v2 까지는 주인공 둘레를 192칸 그림으로 그려 경계가 계단처럼 깨졌다 — 사용자 「가장자리 선이 계단식으로 깨지듯이」)
///     경계는 화면 화소 폭(fwidth)으로 흐려 계단 없이 또렷하다. CPU 는 광선만 쏜다.
///   · 광선이 벽에 닿으면 WallBleed 만큼 더 — 나를 막은 벽의 앞면 · 윗면은 밝게 보인다
/// 소굴 · 메뉴(주인공 없음)에서는 끈다.
/// </summary>
public class PlayerVision : MonoBehaviour
{
    public static bool Enabled = true;

    /// <summary>부채꼴 전체 각도(°).</summary>
    public static float ConeDegrees = 100f;   // 사용자 2026-11-23 「시야각 100도로 줄여줘」 (처음 120°)
    /// <summary>부채꼴 거리(m) — 화면 끝까지 닿게 (사용자 「시야는 그냥 화면 끝까지 닿게」). 적 시야(18m)와는 따로.</summary>
    public static float ViewDistance = 40f;
    /// <summary>둘레 작은 원 반지름(m) — 등 뒤도 이만큼은 보인다.</summary>
    public static float NearRadius = 2.5f;   // 덕코프 감지 원 약 3m · 벽은 넘지 않음
    /// <summary>보이는 곳 밝기 배율 · 보이지 않는 곳 밝기 · 빛 빠짐 (v3 사용자 「시야가 닿는 부분은 좀 더 밝게, 시야 밖도 조금 더 밝게」).</summary>
    public static float Lit = 1.25f, Dark = 0.5f, Gray = 0.4f;
    /// <summary>부채꼴 옆 가장자리에서 거리가 바뀌는 각도 폭.</summary>
    public static float ConeSoftDegrees = 1f;

    private const int Rays = 360;
    private const float EyeHeight = 1.0f;
    private const float WallBleed = 0.6f;

    private static readonly int RaysId = Shader.PropertyToID("_VisionRays");
    private static readonly int CenterId = Shader.PropertyToID("_VisionCenter");
    private static readonly int DarkId = Shader.PropertyToID("_VisionDark");
    private static readonly int GrayId = Shader.PropertyToID("_VisionGray");
    private static readonly int LitId = Shader.PropertyToID("_VisionLit");

    private Texture2D raysTex;
    private readonly float[] rayLen = new float[Rays + 1];   // [Rays] = [0] (한 바퀴)

    /// <summary>적이 나타나는 · 사라지는 시간(초) — 덕코프 조사 추정 0.1 ~ 0.3.</summary>
    public static float FadeIn = 0.15f, FadeOut = 0.25f;
    /// <summary>밤에 플레이어 시야가 줄어드는 하한 (달 배율의 최솟값). 1 = 밤에도 줄지 않음 — 사용자 「화면 끝까지」.</summary>
    public static float NightFloor = 1f;
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private MaterialPropertyBlock fadeBlock;
    private sealed class Seen { public Renderer[] renderers; public float fade = 1f; public bool hidden; }
    private readonly Dictionary<EnemyController, Seen> enemies = new();
    private readonly List<EnemyController> gone = new();

    /// <summary>지금 이 자리가 보이는가 (적 숨기기 · 점검용).</summary>
    public static bool CanSee(Vector3 world) => instance != null && instance.Sees(world);
    /// <summary>시야가 보는 쪽(°, 북쪽 0 · 시계 방향) — 미니맵 화살표도 이걸 쓴다. 시야가 없으면 NaN.</summary>
    public static float FaceDegrees => instance != null && instance.active ? instance.visionFace : float.NaN;
    private static PlayerVision instance;
    private Vector3 eye;
    private bool active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("PlayerVision") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PlayerVision>();
    }

    private void Awake()
    {
        raysTex = new Texture2D(Rays, 1, TextureFormat.RFloat, false, true) { name = "VisionRays", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        Shader.SetGlobalTexture(RaysId, raysTex);
    }

    private void LateUpdate()
    {
        Transform player = QuestSpotDirector.Player;
        bool on = Enabled && player != null && !SceneFlow.InBunker;
        if (!on)
        {
            if (active) { Shader.SetGlobalVector(CenterId, Vector4.zero); ShowAll(); active = false; }
            return;
        }
        active = true;

        Vector3 p = player.position;
        eye = new Vector3(p.x, p.y + EyeHeight, p.z);
        float face = FaceAngle(player);

        // 밤 — 달 배율 (NightFloor 밑으로는 줄지 않는다)
        float view = ViewDistance * Mathf.Max(NightFloor, RaidManager.Current.EnemyVisionScale);
        float half = ConeDegrees * 0.5f;
        for (int i = 0; i < Rays; i++)
        {
            float a = i * 360f / Rays;
            float delta = Mathf.Abs(Mathf.DeltaAngle(a, face));
            float t = Mathf.InverseLerp(half + ConeSoftDegrees * 0.5f, half - ConeSoftDegrees * 0.5f, delta);
            float len = Mathf.Lerp(NearRadius, view, t);
            var dir = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad));
            if (Physics.Raycast(eye, dir, out RaycastHit hit, len, GameLayers.ShotBlockMask, QueryTriggerInteraction.Ignore))
                len = Mathf.Min(len, hit.distance + WallBleed);
            rayLen[i] = len;
        }
        rayLen[Rays] = rayLen[0];
        raysTex.SetPixelData(rayLen, 0);   // 앞 Rays 개만 쓰인다 (Repeat 로 한 바퀴 이어진다)
        raysTex.Apply(false);

        Shader.SetGlobalTexture(RaysId, raysTex);
        Shader.SetGlobalVector(CenterId, new Vector4(eye.x, eye.z, 1f, Rays));
        Shader.SetGlobalFloat(DarkId, Dark);
        Shader.SetGlobalFloat(GrayId, Gray);
        Shader.SetGlobalFloat(LitId, Lit);

        HideUnseenEnemies();
    }

    /// <summary>조준을 놓은 뒤 이동 방향으로 돌아가기까지(초).</summary>
    public static float AimHold = 0.6f;
    /// <summary>시야가 도는 빠르기(°/초).</summary>
    public static float TurnSpeed = 720f;
    private float visionFace;
    /// <summary>점검용 — 갈래 길이 요약 · 적 상태 (path 는 예전 호환용, 쓰지 않는다).</summary>
    public static string DebugDump(string path)
    {
        if (instance == null) return "시야 없음";
        float min = 999f, max = 0f; int full = 0;
        for (int i = 0; i < Rays; i++) { float l = instance.rayLen[i]; min = Mathf.Min(min, l); max = Mathf.Max(max, l); if (l >= ViewDistance - 0.01f) full++; }
        int shown = 0, fading = 0, hidden = 0;
        foreach (var kv in instance.enemies) { if (kv.Value.hidden) hidden++; else if (kv.Value.fade < 0.999f) fading++; else shown++; }
        return $"적 보임 {shown} · 바뀌는 중 {fading} · 숨김 {hidden} · 달 배율 {RaidManager.Current.EnemyVisionScale:0.00} · 쪽 {instance.visionFace:0}° · 갈래 길이 {min:0.0}~{max:0.0}m · 끝까지 닿은 갈래 {full}/{Rays} · 켜짐 {instance.active}";
    }
    /// <summary>점검용 — 지금 시야가 보는 쪽(°, 북쪽 0 · 시계 방향).</summary>
    public static float DebugFace => instance != null ? instance.visionFace : float.NaN;
    private bool faceInit;

    /// <summary>
    /// 시야가 보는 쪽 (v2 · 사용자 「발사 방향에 따라서는 움직이는데 좌측 움직임에 의해서는 바뀌지 않아」).
    /// 조준 중(마지막 조준에서 AimHold 안)이면 조준 방향, 아니면 움직이는 방향, 멈춰 있으면 그대로. 돌 때는 TurnSpeed 로 부드럽게.
    /// </summary>
    private float FaceAngle(Transform player)
    {
        Vector3 want = Vector3.zero;
        bool aiming = player.TryGetComponent(out PlayerAiming aim) && Time.time - aim.LastAimTime < AimHold;
        if (aiming) want = aim.FacingDirection;
        else if (player.TryGetComponent(out PlayerMovement move) && move.MoveDirection.sqrMagnitude > 0.01f) want = move.MoveDirection;
        else if (!faceInit) want = aim != null ? aim.FacingDirection : player.forward;
        want.y = 0f;
        if (want.sqrMagnitude > 0.0001f)
        {
            float target = Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg;
            visionFace = faceInit ? Mathf.MoveTowardsAngle(visionFace, target, TurnSpeed * Time.deltaTime) : target;
            faceInit = true;
        }
        return visionFace;
    }

    private bool Sees(Vector3 world)
    {
        if (!active) return true;
        float dx = world.x - eye.x, dz = world.z - eye.z;
        float a = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        if (a < 0f) a += 360f;
        float ai = a / 360f * Rays;
        int i0 = Mathf.Clamp((int)ai, 0, Rays);
        float l = Mathf.Lerp(rayLen[i0], rayLen[Mathf.Min(Rays, i0 + 1)], ai - i0);
        return Mathf.Sqrt(dx * dx + dz * dz) <= l - WallBleed * 0.5f;
    }

    /// <summary>보이지 않는 적은 그리지 않는다 (그림 · 머리 위 글자). 몸(뿌리의 MeshRenderer)은 EnemySprite 가 따로 끈다.</summary>
    private void HideUnseenEnemies()
    {
        if (!EnemyManager.HasInstance) return;
        fadeBlock ??= new MaterialPropertyBlock();
        IReadOnlyList<EnemyController> list = EnemyManager.Instance.ActiveEnemies;
        for (int i = 0; i < list.Count; i++)
        {
            EnemyController e = list[i];
            if (e == null) continue;
            if (!enemies.TryGetValue(e, out Seen seen) || seen.renderers.Length == 0)
            {
                var rs = new List<Renderer>(e.GetComponentsInChildren<Renderer>(true));
                rs.RemoveAll(r => r.gameObject == e.gameObject);   // 몸은 EnemySprite 몫
                seen ??= new Seen();
                seen.renderers = rs.ToArray();
                enemies[e] = seen;
            }
            float target = Sees(e.transform.position) ? 1f : 0f;
            if (Mathf.Approximately(seen.fade, target) && seen.hidden == (target == 0f)) continue;
            seen.fade = Mathf.MoveTowards(seen.fade, target, Time.deltaTime / (target > seen.fade ? FadeIn : FadeOut));
            bool hide = seen.fade <= 0.001f;
            foreach (Renderer r in seen.renderers)
            {
                if (r == null) continue;
                r.forceRenderingOff = hide;
                if (hide) continue;
                r.GetPropertyBlock(fadeBlock);   // EnemySprite 가 같은 블록에 그림 칸을 넣는다 — 읽고 덧붙인다
                fadeBlock.SetFloat(FadeId, seen.fade);
                r.SetPropertyBlock(fadeBlock);
            }
            seen.hidden = hide;
        }
        // 풀로 돌아간 적은 다시 보이게 해 두고 잊는다
        gone.Clear();
        foreach (var kv in enemies) if (kv.Key == null || !kv.Key.isActiveAndEnabled) gone.Add(kv.Key);
        foreach (EnemyController e in gone)
        {
            if (e != null) Reset(enemies[e]);
            enemies.Remove(e);
        }
    }

    private void Reset(Seen seen)
    {
        fadeBlock ??= new MaterialPropertyBlock();
        foreach (Renderer r in seen.renderers)
        {
            if (r == null) continue;
            r.forceRenderingOff = false;
            r.GetPropertyBlock(fadeBlock); fadeBlock.SetFloat(FadeId, 1f); r.SetPropertyBlock(fadeBlock);
        }
    }

    private void ShowAll()
    {
        foreach (var kv in enemies) if (kv.Key != null) Reset(kv.Value);
        enemies.Clear();
    }

    private void OnDestroy() => Shader.SetGlobalVector(CenterId, Vector4.zero);
}
