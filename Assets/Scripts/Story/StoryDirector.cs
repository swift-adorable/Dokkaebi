using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬이 뜨면 이번에 보여 줄 이야기를 고른다. (로드맵 3단계)
///
///   소굴(벙커)  처음이면 프롤로그 → 지나지 않은 밤이 있으면 그 밤 (상인이 온다)
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

        new GameObject("StoryDirector (Runtime)").AddComponent<StoryDirector>();
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
        StoryManager.PlayOnce(StoryTable.PrologueEvent, PlayNextNight);
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
        StoryManager.PlayOnce(StoryTable.PrologueEvent, () =>
            StoryManager.PlayOnce(StoryTable.EnterEvent(zone), () =>
                gameObject.AddComponent<StoryRaidDirector>().Begin(zone)));
    }
}
