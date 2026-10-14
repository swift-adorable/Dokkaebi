#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// 【개발용 성능 재기】 (결정 2-125) — 정한 시간 동안 프레임 시간 · 메인 스레드 · GC 할당 · 배치 · SetPass · 삼각형을 재서
/// 평균 · 가장 나쁜 값을 한 덩어리 글로 남긴다. 에디터 메뉴 「Dokkaebi/Debug/성능 재기 (10초)」가 부른다.
/// ProfilerRecorder는 에디터 · 개발 빌드에서만 값이 나온다(렌더 수는 에디터에선 게임 화면 기준).
/// </summary>
public class PerfProbe : MonoBehaviour
{
    public static string LastReport { get; private set; }
    public static bool Running => current != null;

    private static PerfProbe current;

    private ProfilerRecorder mainThread, gcAlloc, batches, setPass, triangles, drawCalls;
    private float seconds;
    private float started;
    private int frames;
    private double sumMs, sumMain, sumGc, sumBatches, sumSetPass, sumTris, sumDraw;
    private double worstMs, worstGc;
    private int framesWithGc;
    private string label;

    public static void Begin(float duration, string tag)
    {
        if (current != null)
            return;

        var go = new GameObject("PerfProbe (Debug)");
        DontDestroyOnLoad(go);
        current = go.AddComponent<PerfProbe>();
        current.seconds = duration;
        current.label = tag;
    }

    private void OnEnable()
    {
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        started = Time.unscaledTime;
    }

    private void OnDisable()
    {
        mainThread.Dispose();
        gcAlloc.Dispose();
        batches.Dispose();
        setPass.Dispose();
        triangles.Dispose();
        drawCalls.Dispose();
        if (current == this)
            current = null;
    }

    private void Update()
    {
        // 첫 1초는 버린다 — 켜진 순간의 할당 · 멈춤.
        if (Time.unscaledTime - started < 1f)
            return;

        double ms = Time.unscaledDeltaTime * 1000.0;
        double gc = gcAlloc.LastValue;

        frames++;
        sumMs += ms;
        sumMain += mainThread.LastValue * 1e-6;
        sumGc += gc;
        sumBatches += batches.LastValue;
        sumSetPass += setPass.LastValue;
        sumTris += triangles.LastValue;
        sumDraw += drawCalls.LastValue;
        if (gc > 0) framesWithGc++;
        if (ms > worstMs) worstMs = ms;
        if (gc > worstGc) worstGc = gc;

        if (Time.unscaledTime - started >= seconds + 1f)
            Finish();
    }

    private void Finish()
    {
        int n = Mathf.Max(1, frames);
        int enemies = Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude).Length;
        var sb = new StringBuilder();
        sb.AppendLine($"[Perf] {label} · {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} · {Screen.width}×{Screen.height} · {n}프레임");
        sb.AppendLine($"  프레임 {sumMs / n:F2}ms 평균 (가장 나쁨 {worstMs:F1}ms) · 메인 스레드 {sumMain / n:F2}ms");
        sb.AppendLine($"  GC 할당 {sumGc / n / 1024.0:F2}KB/프레임 · 할당한 프레임 {100.0 * framesWithGc / n:F0}% · 가장 큼 {worstGc / 1024.0:F1}KB");
        sb.AppendLine($"  배치 {sumBatches / n:F0} · SetPass {sumSetPass / n:F0} · 드로우콜 {sumDraw / n:F0} · 삼각형 {sumTris / n / 1000.0:F0}k");
        sb.AppendLine($"  체력 가진 것(플레이어 + 적) {enemies}");
        LastReport = sb.ToString();
        Debug.Log(LastReport);
        Destroy(gameObject);
    }
}
#endif
