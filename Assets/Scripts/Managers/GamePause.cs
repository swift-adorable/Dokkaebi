using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【창이 뜨면 게임이 멈춘다】 (결정 2-92 · 사용자 결정 「전투 중 어떤 창이 뜨던 게임(적)을 멈추게하자」)
///
/// 창은 만들 때 자기 바탕(열리면 켜지고 닫히면 꺼지거나 사라지는 것)을 Register한다.
/// 하나라도 켜져 있으면 시간이 멈춘다 — 적 · 탄 · 상태이상 · 생존 · 날씨 · 철수 시간 모두.
/// 사망 화면(GameOver)처럼 GameManager가 멈춘 것도 같이 본다 — timeScale을 정하는 곳은 여기 하나다.
///
/// 덮는 창을 개발 메뉴로 잠시 꺼도(캔버스만 끈다) 창은 열린 것으로 친다 — 뒤를 보는 동안에도 멈춰 있다.
/// 개발용 디버그 창은 넣지 않는다 (시간이 흐르는 것을 보려고 연다).
/// </summary>
public static class GamePause
{
    private static readonly List<GameObject> windows = new();
    private static bool held;

    /// <summary>열린 창이 있어 멈춰 있는가.</summary>
    public static bool IsHeld => held;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        windows.Clear();
        held = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("GamePause (Runtime)");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Ticker>();
    }

    /// <summary>이 창의 바탕을 등록한다 — 켜져 있는 동안 게임이 멈춘다. 사라지면 저절로 빠진다.</summary>
    public static void Register(GameObject window)
    {
        if (window != null && !windows.Contains(window))
            windows.Add(window);

        Refresh();
    }

    /// <summary>열린 창을 다시 세고 시간을 맞춘다.</summary>
    public static void Refresh()
    {
        bool any = false;

        for (int i = windows.Count - 1; i >= 0; i--)
        {
            GameObject w = windows[i];

            if (w == null)
            {
                windows.RemoveAt(i);
                continue;
            }

            if (w.activeInHierarchy)
                any = true;
        }

        held = any;
        Apply();
    }

    /// <summary>게임이 흘러야 하는가 — 플레이 중이고 열린 창이 없을 때만.</summary>
    public static bool ShouldRun(bool playing, bool windowOpen) => playing && !windowOpen;

    /// <summary>timeScale을 정한다. GameManager의 상태가 바뀔 때도 여기를 부른다.</summary>
    public static void Apply()
    {
        bool playing = !GameManager.HasInstance || GameManager.Instance.IsPlaying;
        float want = ShouldRun(playing, held) ? 1f : 0f;

        if (!Mathf.Approximately(Time.timeScale, want))
            Time.timeScale = want;
    }

    /// <summary>창은 여러 방법으로 열리고 닫힌다(켜고 끄기 · 만들고 부수기) — 매 프레임 센다.</summary>
    private sealed class Ticker : MonoBehaviour
    {
        private void LateUpdate() => Refresh();
    }
}
