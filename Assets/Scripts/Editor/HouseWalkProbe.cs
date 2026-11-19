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
        report.AppendLine($"  {house} {plan.Variant} ({s.Site.Size.x:0}×{s.Site.Size.y:0}m) 방 {plan.Rooms.Count} · 몸 막힘 {blocked} · 길 없음 {unreachable} · 뒤지는 자리 {s.LootCount} · 지붕(대문 앞) {(s.RoofShown ? "덮임" : "걷힘")} {string.Join(", ", bad)}");
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
            case 0: MoveTo(c + new Vector3(0f, 0f, -45f)); nextAt += 0.8; break;
            case 1: Shot("far"); report.AppendLine($"  {house} 멀리 지붕 {(s.RoofShown ? "덮임" : "걷힘")}"); nextAt += 0.3; break;
            case 2: MoveTo(Front(s)); nextAt += 0.8; break;
            case 3: Shot("front"); Check(s); nextAt += 0.3; break;
            case 4: MoveTo(c + new Vector3(0f, 0f, -1f)); nextAt += 0.8; break;
            case 5: Shot("yard"); nextAt += 0.3; break;
            default: house++; phase = 0; nextAt += 0.1; break;
        }
    }
}
