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
    public static float ConeDegrees = 120f;   // 적 기본(EnemyArchetypeTable 18m · 100~150°)과 맞춤 — 「내가 보면 쟤도 날 본다」 (덕코프 조사 ~/Dokkaebi-art/ref_duckov_fov_notes.md)
    /// <summary>부채꼴 거리(m).</summary>
    public static float ViewDistance = 18f;
    /// <summary>둘레 작은 원 반지름(m) — 등 뒤도 이만큼은 보인다.</summary>
    public static float NearRadius = 2.5f;   // 덕코프 감지 원 약 3m · 벽은 넘지 않음
    /// <summary>보이지 않는 곳의 밝기 · 빛 빠짐.</summary>
    public static float Dark = 0.38f, Gray = 0.55f;

    private const int Rays = 180;
    private const int Res = 128;
    private const float EyeHeight = 1.0f;
    private const float WallBleed = 0.6f;
    private const float SoftEdge = 1.2f;       // 경계가 흐려지는 폭(m)
    private const float ConeSoftDegrees = 10f; // 부채꼴 옆 가장자리가 흐려지는 각도

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

    private readonly Dictionary<EnemyController, (Renderer[] renderers, bool shown)> enemies = new();
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
        mask = new Texture2D(Res, Res, TextureFormat.R8, false, true) { name = "VisionMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        pixels = new byte[Res * Res];
        Shader.SetGlobalTexture(MaskId, mask);
        Rebuild();
    }

    /// <summary>거리 · 해상도가 바뀌면 칸 표를 다시 만든다.</summary>
    private void Rebuild()
    {
        range = ViewDistance + 3f;
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
        if (!Mathf.Approximately(range, ViewDistance + 3f)) Rebuild();

        // 가운데를 칸 눈금에 맞춘다 — 움직일 때 경계가 미끄러지듯 떨리지 않게
        Vector3 p = player.position;
        float cx = Mathf.Round(p.x / texel) * texel, cz = Mathf.Round(p.z / texel) * texel;
        eye = new Vector3(cx, p.y + EyeHeight, cz);

        Vector3 facing = player.TryGetComponent(out PlayerAiming aim) ? aim.FacingDirection : player.forward;
        facing.y = 0f;
        float face = facing.sqrMagnitude > 0.001f ? Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg : 0f;

        float half = ConeDegrees * 0.5f;
        for (int i = 0; i <= Rays; i++)
        {
            float a = i * 360f / Rays;
            float delta = Mathf.Abs(Mathf.DeltaAngle(a, face));
            float t = Mathf.InverseLerp(half + ConeSoftDegrees * 0.5f, half - ConeSoftDegrees * 0.5f, delta);
            float len = Mathf.Lerp(NearRadius, ViewDistance, t);
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
        IReadOnlyList<EnemyController> list = EnemyManager.Instance.ActiveEnemies;
        for (int i = 0; i < list.Count; i++)
        {
            EnemyController e = list[i];
            if (e == null) continue;
            if (!enemies.TryGetValue(e, out var entry))
            {
                var rs = new List<Renderer>(e.GetComponentsInChildren<Renderer>(true));
                rs.RemoveAll(r => r.gameObject == e.gameObject);   // 몸은 EnemySprite 몫
                entry = (rs.ToArray(), true);
            }
            bool show = Sees(e.transform.position);
            if (show != entry.shown || entry.renderers.Length == 0)
            {
                foreach (Renderer r in entry.renderers) if (r != null) r.forceRenderingOff = !show;
                entry.shown = show;
            }
            enemies[e] = entry;
        }
        // 풀로 돌아간 적은 다시 보이게 해 두고 잊는다
        gone.Clear();
        foreach (var kv in enemies) if (kv.Key == null || !kv.Key.isActiveAndEnabled) gone.Add(kv.Key);
        foreach (EnemyController e in gone)
        {
            if (e != null) foreach (Renderer r in enemies[e].renderers) if (r != null) r.forceRenderingOff = false;
            enemies.Remove(e);
        }
    }

    private void ShowAll()
    {
        foreach (var kv in enemies)
            if (kv.Key != null) foreach (Renderer r in kv.Value.renderers) if (r != null) r.forceRenderingOff = false;
        enemies.Clear();
    }

    private void OnDestroy() => Shader.SetGlobalVector(RectId, Vector4.zero);
}
