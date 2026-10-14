using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 【프레임 지킴이】 (결정 2-125 · 11단계) — 휴대폰에서 60fps를 목표로 한다.
///   · 켜질 때: targetFrameRate 60 · vSync 끔 · 화면 꺼짐 막음
///   · 5초마다 평균 프레임 시간을 잰다. 두 번 잇달아 52fps보다 느리면 한 단계 내린다(다시 올리지 않는다 — 오르내림 반복을 막는다)
///       0단계 그대로 → 1단계 해상도 87.5% → 2단계 + 그림자 끔 → 3단계 해상도 75%
///   · 판 불러오기 같은 한 번의 긴 멈춤(0.25초 넘는 프레임)은 세지 않는다. 처음 3초도 세지 않는다
///   · 에디터에서는 재기만 하고 바꾸지 않는다 — 플레이 중 URP 자산을 고치면 파일에 남기 때문
///   · 개발 빌드(실기)에서는 위 가운데에 「fps · ms · 프레임당 GC 할당 · 단계」를 작게 띄운다
///     (에디터의 GC 값은 에디터 자체 할당이 대부분이라 — 우리 것을 모두 꺼도 200KB 남짓 — 실기에서 본다)
/// </summary>
public class PerformanceGovernor : MonoBehaviour
{
    public const int TargetFps = 60;
    public const int MaxTier = 3;
    public const float WindowSeconds = 5f;
    public const float WarmUpSeconds = 3f;
    public const float SlowFrameMs = 1000f / 52f;
    public const float HitchSeconds = 0.25f;
    public const int SlowWindowsToDrop = 2;

    private static readonly float[] ScaleOfTier = { 1f, 0.875f, 0.875f, 0.75f };
    private static readonly bool[] ShadowsOfTier = { true, true, false, false };

    public static PerformanceGovernor Instance { get; private set; }
    public static int Tier { get; private set; }
    public static float Fps { get; private set; }
    public static float FrameMs { get; private set; }

    private float baseScale = 1f;
    private float baseShadowDistance;
    private float started;
    private float sum;
    private int frames;
    private float windowStart;
    private int slowWindows;

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
    private Text readout;
    private float nextReadout;
    private Unity.Profiling.ProfilerRecorder gcRecorder;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        Tier = 0;
        Fps = 0f;
        FrameMs = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        Application.targetFrameRate = TargetFps;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (Instance != null)
            return;

        var go = new GameObject("PerformanceGovernor (Runtime)");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PerformanceGovernor>();
    }

    /// <summary>다음 단계 — 느린 창이 잇달아 SlowWindowsToDrop번이면 한 단계 내린다. 순수 계산(테스트용).</summary>
    public static int NextTier(int tier, float averageMs, ref int slowWindows)
    {
        if (averageMs > SlowFrameMs)
            slowWindows++;
        else
            slowWindows = 0;

        if (slowWindows < SlowWindowsToDrop || tier >= MaxTier)
            return tier;

        slowWindows = 0;
        return tier + 1;
    }

    /// <summary>그 단계의 해상도 배율(자산 값에 곱한다)과 그림자 켬.</summary>
    public static float ScaleOf(int tier) => ScaleOfTier[Mathf.Clamp(tier, 0, MaxTier)];
    public static bool ShadowsOf(int tier) => ShadowsOfTier[Mathf.Clamp(tier, 0, MaxTier)];

    private void Awake()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            baseScale = urp.renderScale;
            baseShadowDistance = urp.shadowDistance;
        }

        started = Time.unscaledTime;
        windowStart = started;

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        Canvas canvas = UIFactory.CreateCanvas("PerformanceReadout (Runtime)", 5000);
        canvas.transform.SetParent(transform, false);
        readout = UIFactory.CreateLabel(canvas.transform, "", 16, FontStyle.Bold,
            new Vector2(0.4f, 0.965f), new Vector2(0.6f, 0.995f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));
        readout.raycastTarget = false;
        readout.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        gcRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
#endif
    }

    private void OnDestroy()
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        gcRecorder.Dispose();
#endif
        Restore();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        if (now - started < WarmUpSeconds || dt > HitchSeconds)
        {
            windowStart = now;
            sum = 0f;
            frames = 0;
            return;
        }

        sum += dt;
        frames++;

        if (now - windowStart >= WindowSeconds && frames > 0)
        {
            float averageMs = sum / frames * 1000f;
            FrameMs = averageMs;
            Fps = 1000f / averageMs;

            int next = NextTier(Tier, averageMs, ref slowWindows);
            if (next != Tier)
            {
                Tier = next;
                Apply();
                GameLogger.Log($"[Perf] 평균 {averageMs:F1}ms → {Tier}단계 (해상도 {ScaleOf(Tier):P0} · 그림자 {(ShadowsOf(Tier) ? "켬" : "끔")})");
            }

            windowStart = now;
            sum = 0f;
            frames = 0;
        }

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (readout != null && now >= nextReadout)
        {
            nextReadout = now + 0.5f;
            float ms = dt * 1000f;
            readout.text = $"{1000f / Mathf.Max(ms, 0.01f):F0}fps · {ms:F1}ms · GC {gcRecorder.LastValue / 1024f:F1}KB · {Tier}단계";
        }
#endif
    }

    private void Apply()
    {
#if !UNITY_EDITOR
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.renderScale = baseScale * ScaleOf(Tier);
            urp.shadowDistance = ShadowsOf(Tier) ? baseShadowDistance : 0f;
        }
#endif
    }

    private void Restore()
    {
#if !UNITY_EDITOR
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.renderScale = baseScale;
            urp.shadowDistance = baseShadowDistance;
        }
#endif
    }
}
