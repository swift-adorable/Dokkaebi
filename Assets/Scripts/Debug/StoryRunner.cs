#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【한 판 끝까지 자동으로 돌리기】 (결정 2-128 · 개발용) — 새 게임부터 엔딩(넋 보내기)까지 이야기 흐름을 기계가 대신 밟는다.
///   · 세이브는 따로(Logs/playthrough/save.json) — 원래 세이브는 건드리지 않는다
///   · 대화 · 팝업 · 난이도(보통)는 단추를 대신 누르고, 플레이어는 쓰러지지 않게 채운다
///   · 소굴: 열린 설계도의 건물을 지어 놓고(재료는 준다) → 아직 덜 끝낸 첫 구역으로 출발
///   · 구역: 기억의 조각 · 퀘스트 자리 · 봇짐을 차례로 밟고 → 이야기 보스를 쓰러뜨리고 → 길목으로 철수
///   · 장면마다 Logs/playthrough/ 에 화면을 찍고, 일지(log.md)에 구역 · 보스 · 조각 · 사건 · 오류를 적는다
/// 이야기 흐름이 끊기는 곳(구역이 끝나지 않음 · 다음 구역이 안 열림 · 오류)을 찾는 것이 목적이다. 전투 밸런스는 보지 않는다.
/// </summary>
public class StoryRunner : MonoBehaviour
{
    public const string Folder = "Logs/playthrough";   // Temp는 Unity를 다시 열면 지워진다
    private const float ZoneTimeout = 150f;

    public static StoryRunner Instance { get; private set; }
    public static bool Running => Instance != null;

    private readonly StringBuilder log = new();
    private readonly List<string> errors = new();
    private readonly Dictionary<string, int> attempts = new();
    private int shot;
    private float started;
    private string lastScene;
    private bool sceneReady;

    public static void Begin()
    {
        if (Instance != null)
            return;

        Directory.CreateDirectory(Folder);
        foreach (string f in Directory.GetFiles(Folder))
            File.Delete(f);

        var go = new GameObject("StoryRunner (Debug)");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<StoryRunner>();
    }

    public static void Stop()
    {
        if (Instance == null)
            return;
        Instance.Finish("멈춤 (손으로)");
    }

    private string phase = "시작";
    private string lastAction = "";
    private float nextBeat;

    /// <summary>1초마다 지금 하는 일을 status.txt에 — 멈춘 곳을 밖에서 본다.</summary>
    private void Update()
    {
        if (Time.realtimeSinceStartup < nextBeat)
            return;
        nextBeat = Time.realtimeSinceStartup + 1f;
        Mark(null);
    }

    /// <summary>지금 하는 일을 곧바로 적는다 — Unity가 멈추면 마지막 줄이 범인 가까이다.</summary>
    private void Mark(string action)
    {
        if (action != null)
            lastAction = action;
        File.WriteAllText($"{Folder}/status.txt",
            $"{System.DateTime.Now:HH:mm:ss} · {phase} · {lastAction} · 씬 {SceneManager.GetActiveScene().name} · 멈춤 {GamePause.IsHeld} · timeScale {Time.timeScale} · 불러오는 중 {SceneFlow.IsLoading} · 구역 {StoryManager.TargetZone}");
    }

    private void OnEnable() => Application.logMessageReceived += OnLog;
    private void OnDisable() => Application.logMessageReceived -= OnLog;

    private void OnLog(string message, string stack, LogType type)
    {
        if (type is not (LogType.Error or LogType.Exception or LogType.Assert))
            return;
        if (message.Contains("com.gamelovers.mcp-unity"))
            return;
        // 첫 줄만 적으면 「어느 오브젝트가 남았는지」 같은 뒷줄을 놓친다 — 네 줄까지 붙인다.
        string first = string.Join(" / ", message.Split('\n').Where(l => l.Length > 0).Take(4));
        string where = stack?.Split('\n').FirstOrDefault(l => l.Contains("Assets/")) ?? string.Empty;
        errors.Add($"{Time.realtimeSinceStartup - started:F0}s {SceneManager.GetActiveScene().name} — {first} {where}");
        Write($"- ⚠️ 오류: {first} `{where.Trim()}`");
    }

    private IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        SaveManager.PathOverride = Path.GetFullPath($"{Folder}/save.json");
        SaveManager.Wipe();
        Write($"# 한 판 자동 진행 — {System.DateTime.Now:yyyy-MM-dd HH:mm}\n");
        Write("세이브: Logs/playthrough/save.json (원래 세이브는 그대로)\n");
        SceneFlow.RestartNewGame();
        SceneManager.sceneLoaded += (_, _) => sceneReady = false;

        yield return new WaitForSecondsRealtime(2f);

        int guard = 0;
        while (!StoryManager.Progress.IsEnded && guard++ < 200)
        {
            yield return WaitForScene();

            if (SceneFlow.InBunker)
                yield return Bunker();
            else if (SceneFlow.IsRaidScene(SceneManager.GetActiveScene().name))
                yield return Raid();
            else
                yield return new WaitForSecondsRealtime(1f);

            if (stopped)
                yield break;
        }

        yield return Settle(4f);
        Shot("ending");
        Finish(StoryManager.Progress.IsEnded ? "엔딩까지 왔다 (넋 보내기)" : "멈춤 — 반복 한도");
    }

    private bool stopped;

    // ── 장면 ──────────────────────────────────────────────────────────

    private IEnumerator WaitForScene()
    {
        phase = "씬 기다림";
        float t = 0f;
        while ((SceneFlow.IsLoading || SceneManager.GetActiveScene().name == lastScene && sceneReady) && t < 20f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        lastScene = SceneManager.GetActiveScene().name;
        sceneReady = true;
    }

    /// <summary>떠 있는 창(대화 · 팝업 · 난이도 …)을 닫으며 seconds 동안 기다린다.</summary>
    private IEnumerator Settle(float seconds)
    {
        string was = phase;
        phase = was + " · 창 닫기";
        float quiet = 0f, total = 0f, stuck = 0f;
        while (quiet < seconds && total < 120f)
        {
            bool clicked = ClickThrough();
            stuck = clicked || !GamePause.IsHeld ? 0f : stuck + 0.25f;
            if (stuck >= 8f)
            {
                // 아는 단추가 없는 창 — 보이는 단추 아무거나 하나 누르고 적어 둔다.
                Button any = FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                    .Where(b => b.interactable && b.GetComponentInChildren<Text>() != null && b.GetComponentInChildren<Text>().text.Trim().Length > 0)
                    .OrderByDescending(b => b.GetComponentInParent<Canvas>()?.sortingOrder ?? 0).FirstOrDefault();
                if (any != null)
                {
                    Write($"- ⚠️ 멈춘 창에서 모르는 단추 「{any.GetComponentInChildren<Text>().text.Trim()}」를 눌렀다 ({SceneManager.GetActiveScene().name})");
                    any.onClick.Invoke();
                }
                stuck = 0f;
            }
            quiet = clicked || GamePause.IsHeld ? 0f : quiet + 0.25f;
            total += 0.25f;
            KeepAlive();
            yield return new WaitForSecondsRealtime(0.25f);
        }
        phase = was;
    }

    private static readonly string[] Preferred = { "보통", "건너뛰기", "다음", "확인", "계속", "닫기", "돌아가기", "받기", "좋다", "✕" };

    /// <summary>멈춘 창이 있으면 그 안의 단추를 하나 누른다 — 위 순서대로 먼저 보이는 것.</summary>
    private bool ClickThrough()
    {
        if (!GamePause.IsHeld)
            return false;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude);
        foreach (string label in Preferred)
            foreach (Button b in buttons)
            {
                if (!b.interactable || !b.gameObject.activeInHierarchy)
                    continue;
                Text t = b.GetComponentInChildren<Text>();
                if (t == null || !t.text.Trim().StartsWith(label))
                    continue;
                Mark($"단추 「{label}」");
                b.onClick.Invoke();
                return true;
            }
        return false;
    }

    private static void KeepAlive()
    {
        var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        if (movement != null && movement.TryGetComponent(out Health h) && h.Current < h.Max)
            h.Heal(h.Max);
    }

    // ── 소굴 ──────────────────────────────────────────────────────────

    private IEnumerator Bunker()
    {
        phase = "소굴";
        yield return Settle(3f);

        // 【소굴에 있는 동안에만 소굴 일을 한다.】 새 판은 난이도를 고르자마자 게임이 스스로
        // 0-1(프롤로그)로 떠난다. 그걸 모르고 소굴 일을 이어 가면 구역 안에서 TargetZone을
        // 0-2로 덮어써, 0-1을 「0-2 첫 시도 실패」로 적었다 (결정 2-128).
        if (!StillInBunker())
            yield break;
        Shot("bunker");

        BuildOpenBlueprints();
        yield return Settle(2f);
        if (!StillInBunker())
            yield break;

        string next = NextZone();
        if (next == null)
        {
            Write($"- 소굴: 갈 구역이 없다 — 열린 구역이 모두 끝났거나 다음 구역이 열리지 않았다");
            Finish("멈춤 — 다음 구역 없음");
            yield break;
        }

        attempts.TryGetValue(next, out int n);
        attempts[next] = n + 1;
        if (n >= 3)
        {
            Write($"- ❌ {next}에 세 번 갔는데도 끝나지 않는다 — 여기서 멈춘다");
            Finish($"멈춤 — {next} 안 끝남");
            yield break;
        }

        StoryManager.TargetZone = next;
        Write($"\n## → {next} {StoryTable.Zone(next)?.Name} (시도 {n + 1}) · 밤 {Nights()} · {Elapsed()}");
        Mark($"출발 {next}");
        SceneFlow.Depart();
        yield return new WaitForSecondsRealtime(1f);
    }

    private static bool StillInBunker() => SceneFlow.InBunker && !SceneFlow.IsLoading;

    private void BuildOpenBlueprints()
    {
        var poses = new Dictionary<string, BuildingPose>
        {
            [BuildingTable.Workbench] = new BuildingPose(-7f, -1f, 0),
            [BuildingTable.GeneralStore] = new BuildingPose(7f, 3f, 0),
            [BuildingTable.Smithy] = new BuildingPose(7f, 0f, 0),
            [BuildingTable.Apothecary] = new BuildingPose(7f, -3f, 0),
            [BuildingTable.LedgerRoom] = new BuildingPose(-7f, 2.5f, 0),
        };

        foreach (BuildingDefinition d in BuildingTable.All)
        {
            if (BuildingManager.State.Owns(d.Id) || !StoryManager.Progress.IsBlueprintOpen(d.Id))
                continue;

            PlaytestActions.GiveBuildingMaterials();
            BuildError e = BuildingManager.Build(d);
            bool placed = e == BuildError.None && poses.TryGetValue(d.Id, out BuildingPose p) && BuildingManager.Place(d.Id, p);
            Write($"- 소굴: {d.Name} 짓기 — {(e == BuildError.None ? "지음" : BuildingState.Explain(e))}{(placed ? " · 놓음" : string.Empty)}");
        }
    }

    private static string NextZone()
    {
        StoryProgress p = StoryManager.Progress;
        foreach (ChapterDefinition c in StoryTable.Chapters)
            foreach (ZoneDefinition z in c.Zones)
                if (p.IsZoneOpen(z.Id) && !p.IsCleared(z.Id))
                    return z.Id;

        // 구역은 다 끝났는데 아직 할 퀘스트 자리가 남은 곳(넋 보내기 등).
        QuestTracker tracker = StoryManager.Quests;
        foreach (QuestSpotDefinition spot in QuestSpotTable.Spots)
            if (p.IsZoneOpen(spot.ZoneId) && QuestSpotTable.IsAvailable(spot, p, tracker))
                return spot.ZoneId;
        return null;
    }

    // ── 구역 ──────────────────────────────────────────────────────────

    private IEnumerator Raid()
    {
        phase = "구역";
        string zone = StoryManager.TargetZone;
        StoryProgress p = StoryManager.Progress;
        var before = new HashSet<string>(Snapshot());
        float zoneStart = Time.realtimeSinceStartup;

        PlaytestActions.RefillSurvival();
        yield return Settle(2f);
        Shot($"{zone}_enter");

        // 1) 조각 · 봇짐 · 퀘스트 자리를 밟는다.
        yield return VisitSpots();

        // 2) 이야기 보스 — 나올 때까지 기다렸다가 쓰러뜨린다(뒤 보스가 이어 나오면 그것도).
        float bossWait = 0f;
        bool Pending() => p.NextBossIn(zone) != null && !p.HasDefeated(p.NextBossIn(zone).Id);   // 장 보스는 쓰러뜨린 뒤에도 다시 나온다 — 처음만 센다
        while (Pending() && bossWait < 60f && !p.IsEnded)
        {
            Mark("보스 쓰러뜨리기");
            KillEnemiesNear(Vector3.zero, 9999f, bossesOnly: true);
            yield return Settle(0.5f);
            bossWait += 1f;
        }
        if (Pending())
            Write($"- ⚠️ {zone}: 보스 「{p.NextBossIn(zone).Name}」이 끝내 쓰러지지 않음");

        // 보스를 쓰러뜨리면 열리는 자리(넋 보내기 등)가 있다 — 한 번 더 돈다.
        yield return VisitSpots();
        yield return Settle(2f);
        Shot($"{zone}_done");

        // 3) 엔딩이면 끝. 아니면 길목으로 철수.
        if (p.IsEnded)
        {
            Write($"- 엔딩 — 넋 보내기를 보았다 · {Elapsed()}");
            yield break;
        }

        string newly = string.Join(", ", Snapshot().Where(s => !before.Contains(s)));
        Write($"- {zone}: {(p.IsCleared(zone) ? "✅ 끝냄" : "❌ 아직 안 끝남")} · 새로 생긴 것: {(newly.Length > 0 ? newly : "없음")} · {Time.realtimeSinceStartup - zoneStart:F0}초");

        if (SceneFlow.IsLoading || SceneFlow.InBunker)
            yield break;   // 0-1처럼 고목에 닿아 이미 넘어감

        if (ExtractionDirector.Instance != null && ExtractionDirector.Instance.Points.Length > 0)
        {
            Mark("길목으로");
            ExtractionDirector.Instance.RevealAll();
            Teleport(ExtractionDirector.Instance.Points[0]);
            float t = 0f;
            while (!SceneFlow.IsLoading && !SceneFlow.InBunker && t < 12f)
            {
                KillEnemiesNear(ExtractionDirector.Instance != null ? ExtractionDirector.Instance.Points[0] : Vector3.zero, 15f);
                ClickThrough();
                KeepAlive();
                t += 0.25f;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (!SceneFlow.IsLoading && !SceneFlow.InBunker)
            {
                Write($"- ⚠️ {zone}: 길목에 서 있어도 철수가 안 됨 → 바로 철수");
                SceneFlow.Extract();
            }
        }
        else
        {
            Write($"- ⚠️ {zone}: 길목이 없다 → 바로 철수");
            SceneFlow.Extract();
        }
        yield return new WaitForSecondsRealtime(1f);
    }

    /// <summary>조각 · 봇짐 · 퀘스트 자리를 밟는다(세 바퀴 — 하나를 하면 다음 것이 열리기도 한다).</summary>
    private IEnumerator VisitSpots()
    {
        for (int round = 0; round < 3; round++)
        {
            foreach (StoryPickup pick in FindObjectsByType<StoryPickup>(FindObjectsInactive.Exclude))
            {
                if (pick == null) continue;
                Mark($"조각으로 이동 {pick.name}");
                Teleport(pick.transform.position);
                yield return Settle(1.2f);
            }
            foreach (StoryBundle b in StoryBundle.Active.ToArray())
            {
                if (b == null) continue;
                Mark("봇짐 열기");
                Teleport(b.transform.position);
                b.Open();
                yield return Settle(1.2f);
            }
            foreach (QuestSpot q in FindObjectsByType<QuestSpot>(FindObjectsInactive.Exclude))
            {
                if (q == null) continue;
                Mark($"퀘스트 자리 {q.Definition?.Id}");
                Teleport(q.transform.position);
                yield return new WaitForSecondsRealtime(0.4f);
                if (q != null && q.Definition != null && q.Definition.Kind == QuestSpotKind.Defend)
                {
                    for (int i = 0; i < 20 && q != null; i++)
                    {
                        KillEnemiesNear(q.transform.position, 30f);
                        yield return Settle(0.6f);
                    }
                }
                else if (q != null)
                {
                    Mark($"퀘스트 자리 쓰기 {q.Definition?.Id}");
                    q.Use();
                }
                yield return Settle(1.2f);
            }
            yield return Settle(0.5f);
        }

    }

    private IEnumerable<string> Snapshot()
    {
        StoryProgress p = StoryManager.Progress;
        foreach (BossDefinition b in StoryTable.Bosses)
            if (p.HasDefeated(b.Id)) yield return "보스 " + b.Name;
        foreach (PieceDefinition piece in StoryTable.Pieces)
            if (p.HasPiece(piece.Id)) yield return "조각 " + piece.Id;
        foreach (ChapterDefinition c in StoryTable.Chapters)
            foreach (ZoneDefinition z in c.Zones)
                if (p.IsZoneOpen(z.Id)) yield return "열림 " + z.Id;
    }

    private static void Teleport(Vector3 at)
    {
        var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        if (movement == null)
            return;
        at.y = movement.transform.position.y;
        if (movement.TryGetComponent(out Rigidbody body))
        {
            body.position = at;
            body.linearVelocity = Vector3.zero;
        }
        movement.transform.position = at;
    }

    private static void KillEnemiesNear(Vector3 at, float radius, bool bossesOnly = false)
    {
        if (!EnemyManager.HasInstance)
            return;
        foreach (EnemyController e in EnemyManager.Instance.ActiveEnemies.ToArray())
        {
            if (e == null || !e.TryGetComponent(out Health h) || h.IsDead)
                continue;
            if (!bossesOnly && (e.transform.position - at).sqrMagnitude > radius * radius)
                continue;
            for (int i = 0; i < 3 && !h.IsDead; i++)
                h.TakeDamage(new DamageRequest { source = DamageSource.Player, baseDamage = 999999, bypassArmour = true });
        }
    }

    // ── 기록 ──────────────────────────────────────────────────────────

    private void Shot(string tag)
    {
        string path = $"{Folder}/{shot++:00}_{tag}.png";
        ScreenCapture.CaptureScreenshot(path);

        // 사진마다 주인공 · 카메라 자리와 주인공이 화면 어디에 있는지 적는다 — 「화면에 주인공이 안 보인다」를 사진만으로 가리기 어려웠다.
        var players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera cam = Camera.main;
        foreach (PlayerMovement p in players)
        {
            Vector3 v = cam != null ? cam.WorldToViewportPoint(p.transform.position) : Vector3.zero;
            bool dressed = p.TryGetComponent(out CharacterSprite sprite) && sprite.enabled;
            string card = dressed && sprite.CardRenderer != null
                ? $" · 판 {sprite.CardRenderer.bounds.center:F1} 켜짐 {sprite.CardRenderer.enabled} 보임 {sprite.CardRenderer.isVisible}"
                : string.Empty;
            Write($"  - 📷 {tag} · 주인공 {p.name} ({p.transform.position.x:F1}, {p.transform.position.y:F1}, {p.transform.position.z:F1}) · 화면 ({v.x:F2}, {v.y:F2}) · 그림 {(dressed ? "있음" : "없음")}{card} · 카메라 {(cam != null ? cam.transform.position.ToString("F1") : "없음")}");
        }
    }

    private int Nights()
    {
        int n = 0;
        for (int i = 1; i < 30; i++)
            if (StoryManager.Progress.HasNight(i)) n = i;
        return n;
    }

    private string Elapsed() => $"{(Time.realtimeSinceStartup - started) / 60f:F1}분";

    private void Write(string line)
    {
        log.AppendLine(line);
        try { File.WriteAllText($"{Folder}/log.md", log.ToString()); } catch (IOException) { }
        Debug.Log("[Runner] " + line.Trim());
    }

    private void Finish(string why)
    {
        stopped = true;
        Write($"\n## 끝 — {why} · {Elapsed()}");
        Write($"- 보스 {StoryTable.Bosses.Count(b => StoryManager.Progress.HasDefeated(b.Id))} / {StoryTable.Bosses.Count} · 조각 {StoryTable.Pieces.Count(x => StoryManager.Progress.HasPiece(x.Id))} / {StoryTable.Pieces.Count} · 밤 {Nights()} · 오류 {errors.Count}");
        if (errors.Count > 0)
        {
            Write("\n### 오류 목록");
            foreach (string e in errors.Distinct().Take(80))
                Write("- " + e);
        }
        SaveManager.PathOverride = null;
        Instance = null;
        Destroy(gameObject);
    }
}
#endif
