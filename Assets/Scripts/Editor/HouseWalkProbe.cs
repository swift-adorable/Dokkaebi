using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 【개발용】 결정 2-151 v7 — 플레이 중 열린 집터(3D 한옥)를 차례로 돌며 확인한다.
///   멀리(지붕 덮임) · 대문 앞(지붕 걷힘) · 안마당 사진 → Temp/screens/walk_{i}_*.png
///   방마다: 주인공 몸(반지름 0.35)이 방 가운데에 설 수 있는가 · 대문 밖에서 내비 길로 닿는가 · 뒤지는 자리 수
///   결과는 콘솔 [HouseWalk] 줄.
/// </summary>
public static class HouseWalkProbe
{
    private static int house, phase;
    private static double nextAt;
    private static readonly List<LockedSite> sites = new();
    private static readonly StringBuilder report = new();

    [MenuItem("Dokkaebi/Debug/집터 예전처럼 무겁게 (다음 판부터 · 측정용)")]
    public static void Heavy() { PlayerPrefs.SetInt("Debug.HeavyHouses", LockedSite.HeavyForCompare ? 0 : 1); PlayerPrefs.Save(); Debug.Log($"[HouseWalk] 무겁게 {LockedSite.HeavyForCompare}"); }

    [MenuItem("Dokkaebi/Debug/소굴에서 출발 (넘어가는 시간 재기)")]
    public static void DepartTimed() { if (EditorApplication.isPlaying) SceneFlow.Depart(); }

    [MenuItem("Dokkaebi/Debug/0-2로 출발 (소굴 · 0장 어디서나)")]
    public static void DepartTo02()
    {
        if (!EditorApplication.isPlaying) return;
        StoryManager.TargetZone = "0-2";
        Debug.Log($"[HouseWalk] 0-2 출발 → 정해진 구역 {StoryManager.TargetZone}");
        if (SceneFlow.InBunker) SceneFlow.Depart(); else ToChapter0Timed();
    }

    [MenuItem("Dokkaebi/Debug/0장으로 넘어가기 (넘어가는 시간 재기)")]
    public static void ToChapter0Timed()
    {
        if (!EditorApplication.isPlaying) return;
        var m = typeof(SceneFlow).GetMethod("Load", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        m?.Invoke(null, new object[] { "Chapter0" });
    }

    [MenuItem("Dokkaebi/Debug/시야 그림 꺼내기 (지금)")]
    public static void DumpVision()
    {
        System.IO.Directory.CreateDirectory("Temp/screens");
        Debug.Log("[HouseWalk] 시야 그림 " + PlayerVision.DebugDump("Temp/screens/vision_mask.png"));
        ScreenCapture.CaptureScreenshot("Temp/screens/vision_now.png");
    }

    [MenuItem("Dokkaebi/Debug/시야 모양 비교 (전 · 후)")]
    public static void ShootVision()
    {
        if (!EditorApplication.isPlaying) return;
        System.IO.Directory.CreateDirectory("Temp/screens");
        var aiming = Object.FindAnyObjectByType<PlayerAiming>();
        int step = 0;
        double at = EditorApplication.timeSinceStartup + 0.2;
        void Set(bool after)
        {
            PlayerVision.ConeSoftDegrees = after ? 1f : 10f;
            PlayerVision.SoftEdge = after ? 0.35f : 1.2f;
            PlayerVision.Res = after ? 192 : 128;
        }
        void Step()
        {
            if (EditorApplication.timeSinceStartup < at || !EditorApplication.isPlaying) return;
            if (aiming != null) aiming.SetAim(new Vector2(0.3f, -1f));   // 남쪽(집터 반대쪽 트인 땅)을 조준 (시야가 조준을 따른다)
            switch (step++)
            {
                case 0:
                {
                    Vector3 spot = new(-30f, 0f, -40f);   // 트인 곳 — 첫 열린 집터의 안마당
                    foreach (LockedSite ls in LockedSite.Active) if (ls != null && ls.Plan != null) { spot = new Vector3(ls.Site.Center.x, 0f, ls.Site.Center.y - ls.Site.Size.y * 0.5f - 4f); break; }   // 집터 대문 밖 4m — 트인 땅
                    MoveTo(spot); Set(false); at += 1.0; break;
                }
                case 1: ScreenCapture.CaptureScreenshot("Temp/screens/vision_before.png"); Debug.Log($"[HouseWalk] 시야 timeScale {Time.timeScale} · 시야 쪽 {PlayerVision.DebugFace:0} · 주인공 앞 {(aiming != null ? aiming.FacingDirection : Vector3.zero)}"); at += 0.3; break;
                case 2: Set(true); at += 0.6; break;
                case 3: ScreenCapture.CaptureScreenshot("Temp/screens/vision_after.png"); at += 0.3; break;
                default: EditorApplication.update -= Step; Debug.Log("[HouseWalk] 시야 비교 끝"); break;
            }
        }
        EditorApplication.update += Step;
    }

    [MenuItem("Dokkaebi/Debug/닫힌 한옥 찍기 (헛간 · 방앗간)")]
    public static void ShootClosed()
    {
        if (!EditorApplication.isPlaying) return;
        System.IO.Directory.CreateDirectory("Temp/screens");
        var list = new List<Transform>();
        GameObject root = GameObject.Find("ClosedHanok");
        if (root != null) foreach (Transform t in root.transform) list.Add(t);
        Debug.Log($"[HouseWalk] 닫힌 한옥 {list.Count}채");
        int i = 0, phase = 0;
        double at = EditorApplication.timeSinceStartup + 0.3;
        void Step()
        {
            if (EditorApplication.timeSinceStartup < at || !EditorApplication.isPlaying) return;
            if (i >= list.Count) { EditorApplication.update -= Step; Debug.Log("[HouseWalk] 닫힌 한옥 끝"); return; }
            Vector3 c = list[i].position;
            if (phase == 0) { MoveTo(c + new Vector3(0f, 0f, -7f)); at += 1.0; phase = 1; }
            else if (phase == 1) { ScreenCapture.CaptureScreenshot($"Temp/screens/closed_{i}_front.png"); at += 0.3; phase = 2; }
            else if (phase == 2) { MoveTo(c + new Vector3(0f, 0f, -1.2f)); at += 1.0; phase = 3; }   // 문 안쪽으로 걸어 들어가 보기 — 막혀야 한다
            else { var pm = Object.FindAnyObjectByType<PlayerMovement>(); Debug.Log($"[HouseWalk] 닫힌 한옥 {i} 안쪽 자리 막힘 {Physics.CheckCapsule(c + new Vector3(0f, 0.4f, -1.2f), c + new Vector3(0f, 1.4f, -1.2f), 0.35f, 1 << GameLayers.Obstacle, QueryTriggerInteraction.Ignore)}"); i++; phase = 0; at += 0.1; }
        }
        EditorApplication.update += Step;
    }

    [MenuItem("Dokkaebi/Debug/결정 2-151 집터 걸어 보기")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[HouseWalk] 플레이 중에만"); return; }
        System.IO.Directory.CreateDirectory("Temp/screens");
        sites.Clear();
        foreach (LockedSite s in LockedSite.Active)
            if (s != null && s.Site.Kind == LockSiteKind.OpenHouse && s.Plan != null) sites.Add(s);
        report.Clear();
        report.AppendLine($"[HouseWalk] 열린 집터 {sites.Count}곳");
        house = 0; phase = 0;
        nextAt = EditorApplication.timeSinceStartup + 0.3;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void MoveTo(Vector3 p)
    {
        var movement = Object.FindAnyObjectByType<PlayerMovement>();
        if (movement == null) return;
        p.y = movement.transform.position.y;
        if (movement.TryGetComponent(out Rigidbody body)) { body.position = p; body.linearVelocity = Vector3.zero; }
        movement.transform.position = p;
        foreach (Health h in Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude))   // 찍는 동안 다치지 않게
            if (h.gameObject == movement.gameObject) h.Heal(9999);
    }

    private static void Shot(string name) => ScreenCapture.CaptureScreenshot($"Temp/screens/walk_{house}_{name}.png");

    private static Vector3 Front(LockedSite s) => new(s.Site.Center.x, 0f, s.Site.Center.y - s.Site.Size.y * 0.5f - 3f);

    private static void Check(LockedSite s)
    {
        HanokPlan plan = s.Plan;
        Vector3 origin = s.transform.position;
        int blocked = 0, unreachable = 0;
        var bad = new List<string>();
        NavMesh.SamplePosition(Front(s), out NavMeshHit from, 6f, NavMesh.AllAreas);
        foreach (HanokRoomPlan r in plan.Rooms)
        {
            Vector3 c = origin + new Vector3(r.R.center.x, 0f, r.R.center.y);
            Collider[] hits = Physics.OverlapCapsule(c + Vector3.up * 0.4f, c + Vector3.up * 1.4f, 0.35f, 1 << GameLayers.Obstacle, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0)
            { blocked++; bad.Add($"막힘 {r.Kind}({hits[0].name}@{hits[0].transform.position.x - origin.x:0.0},{hits[0].transform.position.z - origin.z:0.0} 방 {r.R.center.x:0.0},{r.R.center.y:0.0})"); }
            var path = new NavMeshPath();
            bool ok = NavMesh.SamplePosition(c, out NavMeshHit to, 1.2f, NavMesh.AllAreas)
                      && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            if (!ok) { unreachable++; bad.Add($"길없음 {r.Kind}"); }
        }
        report.AppendLine($"  {house} {plan.Variant} ({s.Site.Size.x:0}×{s.Site.Size.y:0}m) 방 {plan.Rooms.Count} · 몸 막힘 {blocked} · 길 없음 {unreachable} · 뒤지는 자리 {s.LootCount} · 그리는 것 {s.GetComponentsInChildren<MeshRenderer>(true).Length} · 막이 {s.GetComponentsInChildren<Collider>(true).Length} · 물체 {s.GetComponentsInChildren<Transform>(true).Length} · 지붕(대문 앞) {(s.RoofShown ? "덮임" : "걷힘")} {string.Join(", ", bad)}");
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt || !EditorApplication.isPlaying) return;
        if (house >= sites.Count)
        {
            EditorApplication.update -= Tick;
            Debug.Log(report.ToString());
            Debug.Log("[HouseWalk] 끝");
            return;
        }
        LockedSite s = sites[house];
        Vector3 c = new(s.Site.Center.x, 0f, s.Site.Center.y);
        switch (phase++)
        {
            case 0: MoveTo(c + new Vector3(0f, 0f, -45f)); nextAt += 1.0; break;
            case 1: Shot("far"); report.AppendLine($"  {house} 멀리 지붕 {(s.RoofShown ? "덮임" : "걷힘")}"); nextAt += 0.3; break;
            case 2: MoveTo(Front(s)); nextAt += 0.8; break;
            case 3: Shot("front"); Check(s); nextAt += 0.3; break;
            case 4: MoveTo(c + new Vector3(0f, 0f, -1f)); nextAt += 0.8; break;
            case 5: Shot("yard"); nextAt += 0.3; break;
            case 6:   // 방 안 — 벽 뒤에 선 주인공 (가려진 둘레가 비쳐 보이는지)
            {
                HanokRoomPlan room = s.Plan.Rooms[0];
                foreach (HanokRoomPlan r in s.Plan.Rooms) if (r.R.center.y > room.R.center.y) room = r;
                MoveTo(new Vector3(s.Site.Center.x + room.R.center.x, 0f, s.Site.Center.y + room.R.center.y));
                nextAt += 0.8; break;
            }
            case 7: Shot("room"); report.AppendLine($"  {house} 방 안에서 지붕 {(s.RoofShown ? "덮임" : "걷힘")}"); nextAt += 0.3; break;
            case 8:   // 남쪽 벽 바로 뒤 — 벽이 주인공을 가린다
            {
                HanokRoomPlan room = s.Plan.Rooms[0];
                foreach (HanokRoomPlan r in s.Plan.Rooms) if (r.R.center.y > room.R.center.y) room = r;
                MoveTo(new Vector3(s.Site.Center.x + room.R.center.x, 0f, s.Site.Center.y + room.R.yMin + 0.9f));
                nextAt += 0.8; break;
            }
            case 9: Shot("behind"); nextAt += 0.3; break;
            case 10:   // 남쪽 벽 바로 앞(카메라 쪽) — 벽 앞에 선 주인공이 가려지거나 흐려지면 안 된다
            {
                HanokRoomPlan room = s.Plan.Rooms[0];
                foreach (HanokRoomPlan r in s.Plan.Rooms) if (r.R.center.y > room.R.center.y) room = r;
                MoveTo(new Vector3(s.Site.Center.x + room.R.center.x, 0f, s.Site.Center.y + room.R.yMin - 2.2f));
                nextAt += 0.8; break;
            }
            case 11: Shot("front_of_wall"); nextAt += 0.3; break;
            case 12: MoveTo(new Vector3(s.Site.Center.x - s.Site.Size.x * 0.3f, 0f, s.Site.Center.y - s.Site.Size.y * 0.5f - 0.7f)); nextAt += 0.8; break;   // 담 바로 바깥(카메라 쪽) — 담이 몸을 덮으면 안 된다
            case 13: Shot("outside_fence"); report.AppendLine($"  {house} 담 밖에서 지붕 {(s.RoofShown ? "덮임" : "걷힘")}"); nextAt += 0.3; break;
            default: house++; phase = 0; nextAt += 0.1; break;
        }
    }
}

/// <summary>
/// 【개발용】 시점 비교 (사용자 2026-11-19 「덕코프 건물은 완전 위가 아니라 대각선 위에서 본 모양」) —
/// 같은 집터 안마당에서 카메라만 바꿔 네 장: A 지금(정사영 50° · 앞벽 잘림) · B 원근 55° · 벽 그대로 ·
/// C 원근 55° · 35° 돌림 · 벽 그대로 · D 원근 60° · 35° 돌림. Temp/screens/view_*.png
/// </summary>
public static class CameraAngleProbe
{
    private static int step;
    private static double nextAt;
    private static LockedSite site;
    private static Vector3 focus;

    [MenuItem("Dokkaebi/Debug/시점 비교 네 장 (집터)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) return;
        site = null;
        foreach (LockedSite s in LockedSite.Active)
            if (s != null && s.Plan != null && s.Plan.Variant == "ㄷ자-안채") site = s;
        if (site == null) foreach (LockedSite s in LockedSite.Active) if (s != null && s.Plan != null) { site = s; break; }
        if (site == null) return;
        focus = new Vector3(site.Site.Center.x, 0f, site.Site.Center.y);
        var movement = Object.FindAnyObjectByType<PlayerMovement>();
        if (movement != null)
        {
            Vector3 p = focus + new Vector3(0f, movement.transform.position.y, -2f);
            if (movement.TryGetComponent(out Rigidbody b)) { b.position = p; b.linearVelocity = Vector3.zero; }
            movement.transform.position = p;
        }
        step = 0;
        nextAt = EditorApplication.timeSinceStartup + 1.0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Set(bool persp, float pitch, float yaw, bool fullWalls, float fov = 35f, float zoom = 1f)
    {
        Camera cam = Camera.main;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;
        site.ShowCutaway(fullWalls);
        cam.orthographic = !persp;
        cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        float half = 9.5f * zoom;
        if (persp) { cam.fieldOfView = fov; cam.transform.position = focus - cam.transform.forward * (half / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)); }
        else { cam.orthographicSize = half; cam.transform.position = focus - cam.transform.forward * 30f; }
        cam.farClipPlane = 200f;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt || !EditorApplication.isPlaying) return;
        nextAt = EditorApplication.timeSinceStartup + 0.6;
        switch (step++)
        {
            // v8 「카메라 각도가 살짝 이상하고 어색」 — 시점 C 를 바탕으로 원근 세기 · 거리 · 기울기만 바꿔 본다
            case 0: Set(true, 55f, 35f, true, 35f, 1f); break;
            case 1: ScreenCapture.CaptureScreenshot("Temp/screens/view2_A.png"); break;
            case 2: Set(true, 55f, 35f, true, 20f, 1f); break;
            case 3: ScreenCapture.CaptureScreenshot("Temp/screens/view2_B.png"); break;
            case 4: Set(true, 55f, 35f, true, 20f, 0.75f); break;
            case 5: ScreenCapture.CaptureScreenshot("Temp/screens/view2_C.png"); break;
            case 6: Set(true, 50f, 35f, true, 25f, 0.85f); break;
            case 7: ScreenCapture.CaptureScreenshot("Temp/screens/view2_D.png"); break;
            default:
                Set(true, 55f, 35f, true, 35f, 1f);
                var follow = Camera.main.GetComponent<CameraFollow>();
                if (follow != null) follow.enabled = true;
                EditorApplication.update -= Tick;
                Debug.Log("[ViewProbe] 끝");
                break;
        }
    }
}

/// <summary>【개발용】 0장 가장자리 · 숲 틈을 돌며 찍는다 — 갈 수 있는 곳 / 없는 곳이 보이는지 (v8 사용자 6번). Temp/screens/edge_*.png</summary>
public static class EdgeProbe
{
    private static readonly Vector3[] spots =
    {
        new(-60f, 0f, -96f), new(-130f, 0f, -92f), new(100f, 0f, -92f), new(0f, 0f, 96f),
        new(-150f, 0f, 30f), new(150f, 0f, 10f), new(-92f, 0f, -36f), new(70f, 0f, -40f),
    };
    private static int step;
    private static double nextAt;

    [MenuItem("Dokkaebi/Debug/가장자리 · 숲 틈 찍기 (0장)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) return;
        step = 0; nextAt = EditorApplication.timeSinceStartup + 0.3;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt || !EditorApplication.isPlaying) return;
        int i = step / 2;
        if (i >= spots.Length) { EditorApplication.update -= Tick; Debug.Log("[EdgeProbe] 끝"); return; }
        if (step % 2 == 0)
        {
            var movement = Object.FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
            {
                Vector3 p = spots[i]; p.y = movement.transform.position.y;
                if (movement.TryGetComponent(out Rigidbody b)) { b.position = p; b.linearVelocity = Vector3.zero; }
                movement.transform.position = p;
            }
            nextAt = EditorApplication.timeSinceStartup + 1.0;
        }
        else
        {
            ScreenCapture.CaptureScreenshot($"Temp/screens/edge_{i}.png");
            nextAt = EditorApplication.timeSinceStartup + 0.3;
        }
        step++;
    }
}

/// <summary>【개발용】 비쳐 보이기 셰이더 ↔ URP Lit 맞바꿔 찍기 — 밝기 · 검게 나오는 곳 비교 (v8).</summary>
public static class ShaderSwapProbe
{
    private static int step;
    private static double nextAt;
    private static readonly System.Collections.Generic.Dictionary<Material, Shader> saved = new();

    [MenuItem("Dokkaebi/Debug/셰이더 맞바꿔 찍기 (지금 자리)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) return;
        step = 0; nextAt = EditorApplication.timeSinceStartup + 0.2;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    private static void Swap(bool toLit)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit"), see = Shader.Find(SeeThrough.ShaderName);
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (toLit && m.shader == see) { saved[m] = see; m.shader = lit; m.SetFloat("_Cull", 0f); if (m.GetFloat("_Cutoff") > 0f) { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_AlphaClip", 1f); } }
                else if (!toLit && saved.ContainsKey(m)) m.shader = see;
            }
        if (!toLit) saved.Clear();
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt || !EditorApplication.isPlaying) return;
        nextAt = EditorApplication.timeSinceStartup + 0.6;
        switch (step++)
        {
            case 0: ScreenCapture.CaptureScreenshot("Temp/screens/swap_see.png"); break;
            case 1: Swap(true); break;
            case 2: ScreenCapture.CaptureScreenshot("Temp/screens/swap_lit.png"); break;
            default: Swap(false); EditorApplication.update -= Tick; Debug.Log("[ShaderSwap] 끝"); break;
        }
    }
}
