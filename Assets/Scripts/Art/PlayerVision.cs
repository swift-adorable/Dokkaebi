using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【덕코프식 시야】 (2026-11-23 · 오늘 할 일 1번) — 주인공이 보는 쪽 부채꼴 + 둘레 작은 원만 「보이는 곳」.
/// 벽 · 담 · 나무 밑동(Obstacle 층)이 시선을 막는다. 보이지 않는 땅 · 집 · 나무는 어둡고 빛 빠진 색(SeeThroughLit 의 ApplyVision),
/// 보이지 않는 적은 그리지 않는다.
///
///   · 매 프레임 주인공 눈높이에서 Rays 갈래 광선 → 갈래마다 보이는 거리
///   · 그 거리로 주인공 둘레 Range×2 m 를 Res×Res 칸 그림(_VisionMask)으로 — 칸마다 각도 · 거리는 미리 계산해 둔다(가볍게)
///   · 광선이 벽에 닿으면 WallBleed 만큼 더 — 나를 막은 벽의 앞면 · 윗면은 밝게 보인다
/// 소굴 · 메뉴(주인공 없음)에서는 끈다.
/// </summary>
public class PlayerVision : MonoBehaviour
{
    public static bool Enabled = true;

    /// <summary>부채꼴 전체 각도(°).</summary>
    public static float ConeDegrees = 120f;   // 적 기본 시야와 맞춤 (사용자 2026-11-23 「각도는 바꾸지마」)
    /// <summary>부채꼴 거리(m).</summary>
    public static float ViewDistance = 18f;
    /// <summary>둘레 작은 원 반지름(m) — 등 뒤도 이만큼은 보인다.</summary>
    public static float NearRadius = 2.5f;   // 덕코프 감지 원 약 3m · 벽은 넘지 않음
    /// <summary>보이지 않는 곳의 밝기 · 빛 빠짐.</summary>
    public static float Dark = 0.38f, Gray = 0.55f;

    private const int Rays = 360;
    /// <summary>시야 그림 한 변 칸 수 — 높을수록 경계가 또렷하다.</summary>
    public static int Res = 192;
    private const float EyeHeight = 1.0f;
    private const float WallBleed = 0.6f;
    /// <summary>경계가 흐려지는 폭(m). v2 1.2 → 0.35 (덕코프는 칼 같은 경계).</summary>
    public static float SoftEdge = 0.35f;
    /// <summary>부채꼴 옆 가장자리에서 거리가 바뀌는 각도 폭. 넓으면 가장자리가 둥글게 휘어 타원처럼 보였다(v1 10°).</summary>
    public static float ConeSoftDegrees = 1f;

    private static readonly int MaskId = Shader.PropertyToID("_VisionMask");
    private static readonly int RectId = Shader.PropertyToID("_VisionRect");
    private static readonly int DarkId = Shader.PropertyToID("_VisionDark");
    private static readonly int GrayId = Shader.PropertyToID("_VisionGray");

    private Texture2D mask;
    private byte[] pixels;
    private float[] pixelAngle;   // 칸마다 가운데에서 본 각도 (갈래 번호 단위, 0 ~ Rays)
    private float[] pixelDist;    // 칸마다 가운데에서의 거리(m)
    private readonly float[] rayLen = new float[Rays + 1];
    private float range;
    private float texel;

    /// <summary>적이 나타나는 · 사라지는 시간(초) — 덕코프 조사 추정 0.1 ~ 0.3.</summary>
    public static float FadeIn = 0.15f, FadeOut = 0.25f;
    /// <summary>밤에 플레이어 시야가 줄어드는 하한 (달 배율의 최솟값).</summary>
    public static float NightFloor = 0.7f;
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private MaterialPropertyBlock fadeBlock;
    private sealed class Seen { public Renderer[] renderers; public float fade = 1f; public bool hidden; }
    private readonly Dictionary<EnemyController, Seen> enemies = new();
    private readonly List<EnemyController> gone = new();

    /// <summary>지금 이 자리가 보이는가 (적 숨기기 · 점검용).</summary>
    public static bool CanSee(Vector3 world) => instance != null && instance.Sees(world);
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
        Rebuild();
        Shader.SetGlobalTexture(MaskId, mask);
    }

    /// <summary>거리 · 해상도가 바뀌면 칸 표를 다시 만든다.</summary>
    private void Rebuild()
    {
        if (pixels == null || pixels.Length != Res * Res)
        {
            if (mask != null) Destroy(mask);
            mask = new Texture2D(Res, Res, TextureFormat.R8, false, true) { name = "VisionMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            pixels = new byte[Res * Res];
        }
        range = ViewDistance * 1.3f + 3f;   // 보름(1.25배)까지 덮는다
        texel = range * 2f / Res;
        pixelAngle = new float[Res * Res];
        pixelDist = new float[Res * Res];
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                float dx = (x + 0.5f) * texel - range, dz = (y + 0.5f) * texel - range;
                float a = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;   // 북쪽 0 · 시계 방향
                if (a < 0f) a += 360f;
                pixelAngle[y * Res + x] = a / 360f * Rays;
                pixelDist[y * Res + x] = Mathf.Sqrt(dx * dx + dz * dz);
            }
    }

    private void LateUpdate()
    {
        Transform player = QuestSpotDirector.Player;
        bool on = Enabled && player != null && !SceneFlow.InBunker;
        if (!on)
        {
            if (active) { Shader.SetGlobalVector(RectId, Vector4.zero); ShowAll(); active = false; }
            return;
        }
        active = true;
        if (!Mathf.Approximately(range, ViewDistance * 1.3f + 3f) || pixels.Length != Res * Res) Rebuild();

        // 가운데를 칸 눈금에 맞춘다 — 움직일 때 경계가 미끄러지듯 떨리지 않게
        Vector3 p = player.position;
        float cx = Mathf.Round(p.x / texel) * texel, cz = Mathf.Round(p.z / texel) * texel;
        eye = new Vector3(cx, p.y + EyeHeight, cz);

        float face = FaceAngle(player);

        // 밤 — 적 시야와 같은 달 배율 (삭 0.45 · 보름 1.25). 「내가 보면 쟤도 날 본다」 가 밤에도 맞게.
        // 단, 플레이어 쪽은 NightFloor(0.7배 · 약 12.6m) 밑으로 줄이지 않는다 — 삭 0.45배(8m)는 너무 답답했다(에디터 확인).
        float view = ViewDistance * Mathf.Max(NightFloor, RaidManager.Current.EnemyVisionScale);
        float half = ConeDegrees * 0.5f;
        for (int i = 0; i <= Rays; i++)
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

        for (int k = 0; k < pixels.Length; k++)
        {
            float ai = pixelAngle[k];
            int i0 = (int)ai;
            float l = Mathf.Lerp(rayLen[i0], rayLen[Mathf.Min(Rays, i0 + 1)], ai - i0);
            float m = (l - pixelDist[k]) / SoftEdge + 0.5f;
            pixels[k] = (byte)(Mathf.Clamp01(m) * 255f);
        }
        mask.SetPixelData(pixels, 0);
        mask.Apply(false);

        Shader.SetGlobalTexture(MaskId, mask);
        Shader.SetGlobalVector(RectId, new Vector4(cx, cz, range * 2f, 1f));
        Shader.SetGlobalFloat(DarkId, Dark);
        Shader.SetGlobalFloat(GrayId, Gray);

        HideUnseenEnemies();
    }

    /// <summary>조준을 놓은 뒤 이동 방향으로 돌아가기까지(초).</summary>
    public static float AimHold = 0.6f;
    /// <summary>시야가 도는 빠르기(°/초).</summary>
    public static float TurnSpeed = 720f;
    private float visionFace;
    /// <summary>점검용 — 시야 그림을 PNG 로 (위 = 북쪽) · 갈래 길이 요약.</summary>
    public static string DebugDump(string path)
    {
        if (instance == null || instance.mask == null) return "시야 없음";
        System.IO.File.WriteAllBytes(path, ImageConversion.EncodeToPNG(instance.mask));
        float min = 999f, max = 0f; int full = 0;
        foreach (float l in instance.rayLen) { min = Mathf.Min(min, l); max = Mathf.Max(max, l); if (l >= ViewDistance - 0.01f) full++; }
        int shown = 0, fading = 0, hidden = 0;
        foreach (var kv in instance.enemies) { if (kv.Value.hidden) hidden++; else if (kv.Value.fade < 0.999f) fading++; else shown++; }
        return $"적 보임 {shown} · 바뀌는 중 {fading} · 숨김 {hidden} · 달 배율 {RaidManager.Current.EnemyVisionScale:0.00} · 쪽 {instance.visionFace:0}° · 갈래 길이 {min:0.0}~{max:0.0}m · 끝까지 닿은 갈래 {full}/{Rays + 1} · Res {Res} · soft {ConeSoftDegrees}° · 경계 {SoftEdge}m · 켜짐 {instance.active}";
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

    private void OnDestroy() => Shader.SetGlobalVector(RectId, Vector4.zero);
}
