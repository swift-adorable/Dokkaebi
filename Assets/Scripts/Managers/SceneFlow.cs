using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 벙커 ↔ 파밍 구역을 오간다. (로드맵 8-J · docs/Dokkaebi_Bunker_System.md 0절)
///
/// 【건너는 길은 세이브 하나다.】 떠나기 전에 저장하고, 도착한 씬에서
/// SaveManager가 다시 읽는다. 매니저를 DontDestroyOnLoad로 끌고 다니지 않는다 —
/// 씬 오브젝트를 가리키는 매니저가 다른 씬으로 넘어가면 끊어진 참조가 남는다.
///
/// 【파밍 중에는 저장하지 않는다】는 규칙은 그대로다. 저장은 셋뿐이다 —
/// 파밍 출발(들고 가는 것까지) · 사망 · 철수.
/// </summary>
public static class SceneFlow
{
    /// <summary>벙커 씬 이름. 「Dokkaebi/Bunker/벙커 씬 생성」이 만든다.</summary>
    public const string BunkerScene = "Bunker";

    /// <summary>파밍 구역 씬 이름. 6장 구조(9단계)가 생기면 구역마다 나뉜다.</summary>
    public const string RaidScene = "SampleScene";

    /// <summary>
    /// 이 구역을 도는 씬 (결정 2-86) — 그 장의 맵 씬(「Chapter0」…)이 빌드에 있으면 그것, 아니면 옛 평지(SampleScene).
    /// </summary>
    public static string RaidSceneFor(string zoneId)
    {
        ChapterData chapter = ZoneDataTable.ChapterOfZone(zoneId);
        string map = chapter != null ? ZoneMapTable.SceneFor(chapter.Chapter) : null;

        return map != null && ZoneMapTable.Of(chapter.Chapter) != null && Application.CanStreamedLevelBeLoaded(map)
            ? map
            : RaidScene;
    }

    /// <summary>파밍 구역 씬인가 — 옛 평지이거나 장 맵.</summary>
    public static bool IsRaidScene(string sceneName)
        => sceneName == RaidScene || ZoneMapTable.IsMapScene(sceneName);

    /// <summary>지금 벙커에 있는가. 벙커에서는 쏘지 않고 수분·에너지가 줄지 않는다.</summary>
    public static bool InBunker => SceneManager.GetActiveScene().name == BunkerScene;

    /// <summary>빌드 설정에 벙커 씬이 들어 있는가. 없으면 사망 후 구역을 다시 연다.</summary>
    public static bool HasBunker => Application.CanStreamedLevelBeLoaded(BunkerScene);

    private static bool loading;

    /// <summary>비밀 통로로 넘어갈 때 설 자리 (장 · 위치). 도착한 맵이 한 번 읽고 지운다.</summary>
    private static int arrivalChapter = -1;
    private static Vector3 arrivalPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loading = false;
        arrivalChapter = -1;
    }

    /// <summary>
    /// 【역행 비밀 통로】 (결정 2-88) — 들고 있는 그대로 이전 장 맵의 그 구역으로 넘어간다.
    /// 파밍 출발처럼 저장하고 넘어간다(소켓의 구슬은 그대로 · 철수가 아니다). 도착한 맵은 출발 자리 대신 출구에 세운다.
    /// </summary>
    public static void TakePassage(int toChapter, string toZone, Vector3 at)
    {
        if (loading || InBunker)
            return;

        StoryManager.TargetZone = toZone;
        arrivalChapter = toChapter;
        arrivalPosition = at;

        SaveManager.Commit(SecretPassageRule.SaveReason);
        Load(RaidSceneFor(toZone));
    }

    /// <summary>비밀 통로로 이 장에 왔으면 설 자리를 꺼낸다(한 번만).</summary>
    public static bool TryTakeArrival(int chapter, out Vector3 at)
    {
        at = arrivalPosition;

        if (arrivalChapter != chapter)
            return false;

        arrivalChapter = -1;
        return true;
    }

    /// <summary>
    /// 【파밍 출발】 — 벙커의 출발 지점이 부른다. 들고 가는 것까지 저장한 뒤 구역으로 간다.
    /// 이 저장이 있어서 파밍 도중에 꺼도 들고 들어간 그대로 돌아온다.
    /// </summary>
    public static void Depart()
    {
        if (loading || !InBunker)
            return;

        SaveManager.Commit("파밍 출발");
        Load(RaidSceneFor(StoryManager.TargetZone));
    }

    /// <summary>
    /// 【철수】 — 가방과 장비를 그대로 들고 벙커로 돌아간다.
    /// 소켓의 젬은 가방으로 돌아온다 (SkillManager.ResetRun). 젬은 칸을 먹지 않으므로
    /// 가방이 가득 차 있어도 잃지 않는다.
    ///
    /// 【임시】 철수 지점(ExtractionDirector · 결정 2-78)의 원 안에서 5초 버티면 이 길로 온다.
    /// 디버그 「즉시 철수」도 같은 길이다. 구역 맵(9단계)이 생기면 지점 자리만 맵이 정한다.
    /// </summary>
    public static void Extract()
    {
        if (loading || InBunker)
            return;

        if (SkillManager.HasInstance)
            SkillManager.Instance.ResetRun();

        ShopManager.RestockAfterRun();
        SpringManager.RefillAfterRun();

        // 튜토리얼 마지막 단계 — 0-2에서 철수했다 (결정 2-85). 저장 전에 적어야 남는다.
        if (StoryManager.TargetZone == ChapterZeroTable.GiftZone)
            StoryManager.Progress.See(TutorialTable.Extracted02Event);

        SaveManager.Commit("철수");

        ReturnToBunker();
    }

    /// <summary>
    /// 【새 게임으로 다시 시작】 (디버그 「세이브 지우기」 · 결정 2-82) — 세이브를 지운 뒤 부른다.
    /// 씬 밖에 사는 진행(이야기 · 난이도 · 건물 · 가게 · 샘)을 비우고 첫 씬(소굴)을 다시 연다.
    /// 씬 안의 매니저는 새 씬에서 새로 만들어지고, 세이브가 없으니 처음 상태로 시작한다.
    /// </summary>
    public static void RestartNewGame()
    {
        StoryManager.ResetForNewGame();
        DifficultyManager.Reset();
        BuildingManager.Reset();
        ShopManager.Reset();
        SpringManager.Reset();

        loading = false;
        Load(HasBunker ? BunkerScene : RaidScene);
    }

    /// <summary>
    /// 벙커로 돌아간다. 사망 화면의 버튼이 부른다 — 사망 저장은 이미 끝났다.
    /// 벙커 씬이 빌드에 없으면 구역을 다시 연다.
    /// </summary>
    public static void ReturnToBunker()
    {
        if (loading)
            return;

        Load(HasBunker ? BunkerScene : RaidScene);
    }

    private static void Load(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            GameLogger.Error($"[SceneFlow] 빌드 설정에 「{scene}」 씬이 없습니다. "
                             + "「Dokkaebi/Bunker/벙커 씬 생성」을 실행하십시오.");
            return;
        }

        loading = true;

        // 사망 화면은 시간을 멈춘 채다. 멈춘 채 넘어가면 새 씬이 움직이지 않는다.
        Time.timeScale = 1f;

        ItemActionMenu.Close();

        GameLogger.Log($"[SceneFlow] → {scene}");

        // 불러오기는 다음 프레임에 끝난다. 그 사이의 두 번째 누름을 막는다.
        SceneManager.sceneLoaded -= HandleLoaded;
        SceneManager.sceneLoaded += HandleLoaded;

        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }

    private static void HandleLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleLoaded;
        loading = false;
    }
}
