using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬이 뜨면 이번에 보여 줄 이야기를 고른다. (로드맵 3단계)
///
///   소굴(벙커)  처음이면 프롤로그 → 바로 0-1 (결정 2-79) · 0-1 뒤 처음 오면 「고목 아래」 → 지나지 않은 밤이 있으면 그 밤 (상인이 온다)
///   파밍 구역   그 구역에 처음 들어왔으면 구역 이야기 → 보스 · 조각 · 방을 놓는다
///
/// 세이브가 씬 로드 때 먼저 읽히므로(SaveManager) 이 컴포넌트의 Start에서는
/// 진행이 이미 돌아와 있다.
/// </summary>
public class StoryDirector : MonoBehaviour
{
    private const string TestScenePrefix = "InitTestScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Spawn(SceneManager.GetActiveScene());

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            Spawn(scene);
    }

    private static void Spawn(Scene scene)
    {
        if (scene.name.StartsWith(TestScenePrefix))
            return;

        if (FindAnyObjectByType<StoryDirector>() != null)
            return;

        var go = new GameObject("StoryDirector (Runtime)");
        go.AddComponent<StoryDirector>();

        // 【임시】 0장 튜토리얼 가이드 (결정 2-85) — 끝났으면 스스로 꺼진다.
        go.AddComponent<TutorialDirector>();
    }

    private void Start()
    {
        if (SceneFlow.InBunker)
            PlayBunker();
        else if (SceneManager.GetActiveScene().name == SceneFlow.RaidScene)
            PlayRaid();
    }

    // ── 소굴 ─────────────────────────────────────────────────────────

    private void PlayBunker()
    {
        // 【프롤로그 앞에서】 난이도를 한 번 고른다 (결정 2-77 — 2-69의 「프롤로그 뒤」를 바꿈). 이미 골랐으면 그냥 지나간다.
        DifficultyPickerUI.ShowIfNeeded(() => StoryManager.PlayOnce(StoryTable.PrologueEvent, AfterPrologue));
    }

    /// <summary>
    /// 본문 순서 (결정 2-79) — 새 게임은 소굴을 거치지 않고 0-1 대숲 밤길로 간다.
    /// 0-1을 지나 처음 소굴에 오면 「고목 아래」(영감 안내 · 창고 · 쓰러짐)를 보여 준다.
    /// </summary>
    private void AfterPrologue()
    {
        StoryProgress progress = StoryManager.Progress;

        if (ChapterZeroTable.ShouldStartInZeroOne(progress))
        {
            StoryManager.TargetZone = ChapterZeroTable.StartZone;
            SceneFlow.Depart();
            return;
        }

        if (ChapterZeroTable.ShouldPlayArrival(progress))
            StoryManager.PlayOnce(ChapterZeroTable.ArrivalEvent, PlayNextNight);
        else
            PlayNextNight();
    }

    private void PlayNextNight()
    {
        int night = StoryManager.Progress.PendingNight();

        if (night == 0)
        {
            SaveManager.Commit("이야기");
            return;
        }

        StoryManager.PlayNight(night, PlayNextNight);
    }

    // ── 파밍 구역 ────────────────────────────────────────────────────

    private void PlayRaid()
    {
        string zone = StoryManager.TargetZone;
        ZoneDefinition definition = StoryTable.Zone(zone);

        if (definition != null)
            StoryDialogueUI.ShowBanner(definition.Title, 2.5f);

        // 소굴을 거치지 않고 구역 씬부터 켰다면(에디터) 프롤로그부터 본다.
        DifficultyPickerUI.ShowIfNeeded(() =>
            StoryManager.PlayOnce(StoryTable.PrologueEvent, () =>
                StoryManager.PlayOnce(StoryTable.EnterEvent(zone), () =>
                    gameObject.AddComponent<StoryRaidDirector>().Begin(zone))));
    }
}
