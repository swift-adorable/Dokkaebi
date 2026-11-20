using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// 【잠긴 곳 — 담 두른 마당 · 들어가는 집】 (결정 2-143 · 2026-11-18 사용자 「A와 B 둘 다 적용」)
/// 자리는 LockSiteTable이 정한다(판마다 같은 곳). 판이 열리면 런타임이 세운다 — 씬을 다시 굽지 않는다.
///
///   · 마당(A) — 사방이 담(PixelLab 담 · 대문 그림). 남쪽 대문은 그 열쇠로 연다. 북쪽 담 한 칸은 금이 가 화약 뭉치로 무너뜨린다.
///     마당 안에 좋은 상자 셋(등급 하나 위 · 파랑 이상 하나 보장).
///   · 집(B) — 벽 · 지붕 그림(PixelLab 기와집 · 초가). 남쪽 방문을 열고 들어서면 【지붕이 걷혀】 방(마루 · 낮은 벽)과 상자 둘이 보인다.
///   · 열쇠 — 열쇠고리에 걸어 둔 것이면 그냥 열린다. 아니면 가방의 그 열쇠를 쓰고 한 번 닳는다(세 번 쓰면 부러진다).
///   · 여는 데 1.2초 — 그동안 문 앞을 떠나면 그만둔다. 열쇠는 조용하다(작은 소리), 화약은 온 동네가 듣는다.
///   · 담 · 문은 걸음과 적 길찾기(NavMesh 파내기)를 막는다. 적 · 상자는 잠긴 곳 안에 나오지 않는다(Occupies).
/// 그림은 PixelLab(Resources/Locks) — 벽 옆면 · 방 안은 단색 상자 [임시 그림].
/// </summary>
public class LockedSite : MonoBehaviour
{
    public const float OpenSeconds = 1.2f;
    public const float FuseSeconds = 2.5f;
    public const float KeyNoise = 5f;
    public const float PowderNoise = 40f;
    public const float SeenRange = 24f;

    private const float WallThickness = 0.5f;
    private const float WallHeight = 2.4f;
    private const float GateGap = 3f;
    private const float RoomDoorGap = 1.6f;
    private const float OpenDoorGap = 2.4f;   // 열린 집 — 둘이 지나갈 만큼 (결정 2-149)
    private const float CrackWidth = 3f;

    public static readonly List<LockedSite> Active = new();
    /// <summary>에디터 맵 사진용 — 상자 · 문 자리를 만들지 않고 생김새만.</summary>
    public static bool PreviewOnly;

    public LockSite Site { get; private set; }
    public bool IsOpen { get; private set; }
    public bool Breached { get; private set; }
    public bool Seen { get; private set; }

    private GameObject doorBlocker;
    private GameObject gateCard;
    private GameObject openGate;
    private GameObject crackedWall;
    private Renderer roofCard;
    private GameObject roof3D;
    private GameObject interior;
    private BunkerStation doorStation;
    private bool busy;
    private bool inside;

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    // ── 판 열기 ──────────────────────────────────────────────────────

    /// <summary>이번 구역의 잠긴 곳을 세운다. 맵이 없으면 아무것도 하지 않는다.</summary>
    public static void SpawnForRaid(Vector3 start)
    {
        ChapterMap map = ZoneMap.Current != null ? ZoneMap.Current.Map : null;
        if (map == null)
            return;

        string zone = map.AreaOf(ZoneMap.Zone).HasValue ? ZoneMap.Zone : map.ZoneAt(start);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<LockSite> sites = LockSiteTable.For(map, zone);
        foreach (LockSite site in sites)
            Create(site);
        IReadOnlyList<LockSite> open = LockSiteTable.OpenHouses(map, zone);
        foreach (LockSite site in open)
            Create(site);

        ClosedHanok.SpawnFor(map);   // 들어가지 못하는 3D 한옥 (헛간 · 방앗간)
        GameLogger.Log($"[LockedSite] {zone} — 잠긴 곳 {sites.Count} · 열린 집 {open.Count} · {watch.ElapsedMilliseconds}ms");
    }

    /// <summary>이 점이 잠긴 곳(담 안 · 담 위 · 문 앞) 자리인가 — 적 · 상자를 놓을 때 피한다.</summary>
    public static bool Occupies(Vector3 p, float margin = 1.5f)
    {
        foreach (LockedSite s in Active)
        {
            if (s == null)
                continue;
            Rect r = s.Site.Footprint;
            if (p.x >= r.xMin - margin && p.x <= r.xMax + margin && p.z >= r.yMin - margin - LockSiteTable.DoorClear && p.z <= r.yMax + margin)
                return true;
        }
        return false;
    }

    public static LockedSite Create(LockSite site)
    {
        var root = new GameObject($"LockedSite_{site.Id}");
        root.transform.position = new Vector3(site.Center.x, 0f, site.Center.y);
        var locked = root.AddComponent<LockedSite>();
        locked.Site = site;
        locked.Build();
        return locked;
    }

    // ── 세우기 ───────────────────────────────────────────────────────

    private void Build()
    {
        float hw = Site.Size.x * 0.5f, hd = Site.Size.y * 0.5f;
        bool compound = Site.Kind == LockSiteKind.Compound;
        bool open = Site.Kind == LockSiteKind.OpenHouse;
        float gap = compound ? GateGap : open ? OpenDoorGap : RoomDoorGap;
        var rng = new System.Random(LockSiteTable.StableSeed(Site.Id));

        // 벽 — 남쪽은 문 자리를 비운다. 마당이면 북쪽 가운데 한 칸은 금 간 담.
        float southSide = hw - gap * 0.5f;
        if (!open) {
        Wall("WallSW", new Vector3(-hw + southSide * 0.5f, 0f, -hd), new Vector3(southSide, WallHeight, WallThickness), compound);
        Wall("WallSE", new Vector3(hw - southSide * 0.5f, 0f, -hd), new Vector3(southSide, WallHeight, WallThickness), compound);
        Wall("WallE", new Vector3(hw, 0f, 0f), new Vector3(WallThickness, WallHeight, Site.Size.y), compound, side: true);
        Wall("WallW", new Vector3(-hw, 0f, 0f), new Vector3(WallThickness, WallHeight, Site.Size.y), compound, side: true);
        }   // 열린 집(3D)은 방마다 벽이 걸음을 막는다 — 네모 막이를 두면 ㄱ자 집의 마당이 막힌다

        if (compound)
        {
            float northSide = hw - CrackWidth * 0.5f;
            Wall("WallNW", new Vector3(-hw + northSide * 0.5f, 0f, hd), new Vector3(northSide, WallHeight, WallThickness), true);
            Wall("WallNE", new Vector3(hw - northSide * 0.5f, 0f, hd), new Vector3(northSide, WallHeight, WallThickness), true);
            crackedWall = Wall("CrackedWall", new Vector3(0f, 0f, hd), new Vector3(CrackWidth, WallHeight, WallThickness), true, tint: 0.62f);
            var crack = new GameObject("CrackStation");
            crack.transform.SetParent(crackedWall.transform.parent, false);
            crack.transform.localPosition = new Vector3(0f, 0f, hd + 1.2f);
            crack.AddComponent<LockedSiteStation>().Owner = this;
            crack.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.CrackedWall, 2.2f);

            gateCard = Card("Gate", "lock_gate", new Vector3(0f, 0f, -hd - 0.05f), 5.6f);
            openGate = new GameObject("OpenGate");
            openGate.transform.SetParent(transform, false);
            Card("GatePostL", "lock_wall", new Vector3(-gap * 0.5f - 0.6f, 0f, -hd - 0.06f), 1.2f, parent: openGate.transform);
            Card("GatePostR", "lock_wall", new Vector3(gap * 0.5f + 0.6f, 0f, -hd - 0.06f), 1.2f, parent: openGate.transform);
            openGate.SetActive(false);
        }
        else if (open)
        {
            BuildHouse3D(hw, hd, rng);
        }
        else
        {
            Wall("WallN", new Vector3(0f, 0f, hd), new Vector3(Site.Size.x, WallHeight, WallThickness), false);
            string[] houses = { "lock_sarangchae", "lock_choga", "lock_gotgan" };
            string art = open && Site.Art != null && Resources.Load<Texture2D>($"Locks/{Site.Art}") != null ? Site.Art : houses[rng.Next(houses.Length)];
            GameObject roof = Card("Roof", art, new Vector3(0f, 0f, -hd - 0.3f), Site.Size.x + 1.6f);
            roofCard = roof != null ? roof.GetComponent<Renderer>() : null;
            BuildInterior(hw, hd);
        }

        if (open)
        {
            // 열린 집 — 문이 없다. 상자 2 ~ 3(보통 상자 · 덕코프: 아무 건물이나 뒤질 거리가 있다).
            IsOpen = true;
            if (PreviewOnly) return;
            // 뒤지는 자리 = 가구 (덕코프: 선반 · 장 · 침대 곁이 뒤지는 자리). 상자 그림은 감추고 3D 가구가 그 노릇을 한다.
            // 집터가 커져 가구가 많다 — 다 뒤지는 자리로 두면 상자가 넘친다. 판마다 같은 씨앗으로 몇 곳만 (3 ~ 7).
            for (int i = lootSpots.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (lootSpots[i], lootSpots[j]) = (lootSpots[j], lootSpots[i]); }
            int cap = Mathf.Clamp(1 + lootSpots.Count / 6, 1, 3);   // v8 · 「상자 너무 과하게 많아」 — 3 ~ 7 → 1 ~ 3 (큰 집터일수록 조금 더)
            if (lootSpots.Count > cap) lootSpots.RemoveRange(cap, lootSpots.Count - cap);
            foreach (Vector3 spot in lootSpots)
            {
                LootBox box = LootBox.Create(transform.position + spot, LootBox.PickTypeFor(rng, rich: false), Site.Chapter, new System.Random(rng.Next()), rich: false);
                Transform card = box.transform.Find("Card");
                if (card != null) card.gameObject.SetActive(false);
            }
            return;
        }

        // 문 — 막는 판 + 여는 자리(문 바깥).
        doorBlocker = Blocker("Door", new Vector3(0f, 0f, -hd), new Vector3(gap, WallHeight, WallThickness));
        var station = new GameObject("DoorStation");
        station.transform.SetParent(transform, false);
        station.transform.localPosition = new Vector3(0f, 0f, -hd - 1.1f);
        station.AddComponent<LockedSiteStation>().Owner = this;
        doorStation = station.AddComponent<BunkerStation>();
        doorStation.Setup(BunkerStation.Kind.LockedDoor, 2.2f);

        // 안의 상자 — 마당 셋 · 집 둘. 문 앞 길은 비운다.
        int boxes = compound ? 3 : 2;
        for (int i = 0; i < boxes; i++)
        {
            float x = Mathf.Lerp(-hw + 1.8f, hw - 1.8f, (i + 0.5f) / boxes) + (float)(rng.NextDouble() - 0.5) * 1.2f;
            float z = Mathf.Lerp(0f, hd - 1.6f, (float)rng.NextDouble());
            LootBox.Create(transform.position + new Vector3(x, 0f, z), LootBox.PickTypeFor(rng, rich: true), Site.Chapter, new System.Random(rng.Next()), rich: true);
        }
    }

    private const float House3DWall = 2.6f;
    /// <summary>이 거리 안에 들어오면 3D 집 지붕을 걷는다 (m). 집터가 커져(최대 36m) 넓혔다.</summary>
    public const float RoofLiftRange = 28f;
    /// <summary>에디터 맵 사진용 — 3D 집 지붕을 처음부터 걷는다(안을 보려고).</summary>
    public static bool PreviewNoRoof;
    /// <summary>【측정용】 예전처럼 무겁게(합치기 · 막이 잇기 끔) — 전후 비교 때만.</summary>
    public static bool HeavyForCompare => Application.isEditor && PlayerPrefs.GetInt("Debug.HeavyHouses", 0) == 1;   // 플레이 들어갈 때 정적 값이 지워져 PlayerPrefs 로
    /// <summary>카메라 쪽 벽을 자르는 높이 — 지붕이 걷히면 이 위가 사라져 방 안이 보인다 (덕코프 단면 집).</summary>
    private const float CutHeight = 1.1f;
    /// <summary>
    /// 카메라 쪽 벽을 자르는가 — v8(사용자가 고른 시점 C · 대각선 원근)부터 끈다: 덕코프처럼 지붕만 걷고 벽은 제 높이로 선다.
    /// </summary>
    private const bool CutWalls = false;

    /// <summary>가구 앞(뒤지는 자리) — BuildHouse3D 가 채우고 Build 가 상자를 놓는다.</summary>
    private readonly List<Vector3> lootSpots = new();
    private Transform cutaway;
    /// <summary>3D 한옥 짜임 (점검용 — 방 자리 · 문).</summary>
    public HanokPlan Plan { get; private set; }
    /// <summary>지금 지붕이 보이는가 (점검용).</summary>
    public bool RoofShown
    {
        get
        {
            if (roof3D == null || !roof3D.activeSelf) return false;
            foreach (var (roof, _) in buildingRoofs) if (roof != null && !roof.activeSelf) return false;
            return true;
        }
    }

    /// <summary>
    /// 채마다 지붕 · 그 채의 방 네모들 (v8c · 사용자 「건물 근처에서는 뚜껑을 보여 주다가 캐릭터가 입구로 들어갈 때 뚜껑을 없애고 내부를 보여줘」).
    /// 지붕 규칙 (v8e): 집터(담 안)에 들어서면 이 집 지붕을 전부 걷고, 담 밖 RoofFar 넘게 나가야 다시 덮는다. RoofNear 는 옛 규칙(채 1m) — 점검용으로만 남김.
    /// </summary>
    private readonly List<(GameObject roof, List<Rect> rooms)> buildingRoofs = new();
    /// <summary>지붕이 걷히는 거리 — 그 채 벽에서 캐릭터 한 명 남짓(사용자 v8c 「건물 근처 · 거리는 한 캐릭터 정도」).</summary>
    public const float RoofNear = 1.0f;
    /// <summary>걷힌 지붕을 다시 덮는 거리 — 걷는 거리보다 멀게 해 경계에서 깜빡이지 않게.</summary>
    public const float RoofFar = 2.5f;
    private bool roofsLifted;
    /// <summary>카메라 쪽 벽 윗부분을 보이거나 감춘다 (시점 비교용).</summary>
    public void ShowCutaway(bool show) { if (cutaway != null) cutaway.gameObject.SetActive(show); }
    /// <summary>이 집에 놓은 뒤지는 자리 수 (점검용).</summary>
    public int LootCount => lootSpots.Count;

    private static float FloorHeight(HanokRoom k) => k switch
    {
        HanokRoom.Kitchen or HanokRoom.Stable => 0.06f,   // 부엌 · 외양간은 흙바닥 — 방보다 낮다
        HanokRoom.Storage or HanokRoom.Gate => 0.1f,
        HanokRoom.Pavilion => 0.5f,                       // 누마루 — 띄워 올린 마루
        _ => 0.3f,
    };

    private static string FloorTex(HanokRoom k) => k switch
    {
        HanokRoom.Kitchen or HanokRoom.Storage or HanokRoom.Stable => "floor_earth",
        HanokRoom.Gate => "floor_stone",
        HanokRoom.Room => "floor_ondol",
        _ => "floor_wood",
    };

    /// <summary>방 성격마다 가구 — 판마다 같은 씨앗으로 몇 개를 고른다. 사랑채 방은 서안 · 문갑 · 책 선반.</summary>
    private static string[] FurnitureOf(HanokRoom kind, string building, System.Random rng)
    {
        string[] Pick(string[] pool, int n) { var l = new List<string>(pool); var o = new List<string>(); for (int i = 0; i < n && l.Count > 0; i++) { int k = rng.Next(l.Count); o.Add(l[k]); l.RemoveAt(k); } return o.ToArray(); }
        bool sarang = building != null && (building.Contains("사랑") || building.Contains("밖거리"));
        return kind switch
        {
            HanokRoom.Kitchen => new[] { "stove" }.Concat(Pick(new[] { "shelf", "jars", "waterjar" }, 1 + rng.Next(2))).ToArray(),
            HanokRoom.Room when sarang => Pick(new[] { "desk", "mungap", "shelf" }, 2),
            HanokRoom.Room => Pick(new[] { "quilt", "bandaji", "mungap", "chest" }, 1 + rng.Next(2)),
            HanokRoom.Hall => Pick(new[] { "dwiju", "bandaji", "soban" }, 1 + rng.Next(2)),
            HanokRoom.Jeongju => new[] { "soban", "dwiju" },
            HanokRoom.Storage => Pick(new[] { "sacks", "jars", "dwiju", "shelf", "sacks" }, 2 + rng.Next(2)),
            HanokRoom.Stable => new[] { "trough", "straw" },
            HanokRoom.Shrine => new[] { "altar" },
            _ => System.Array.Empty<string>(),
        };
    }

    /// <summary>
    /// 【3D 한옥 집터】 (결정 2-151 v6) — HanokPlan(담 · 여러 채 · 마당)대로 세운다.
    ///   방마다 기단 · 바닥(부엌 · 외양간은 낮은 흙바닥, 방은 온돌, 대청 · 정주간은 마루, 누마루는 띄움)
    ///   벽 줄마다 문 · 창 · 트인 곳 — 바닥에 닿는 토막만 걸음을 막는다
    ///   카메라 쪽 벽은 지붕이 걷히면 1.1m 위가 사라진다 (덕코프 단면) · 뒷벽은 그대로
    ///   채마다 우진각 지붕(툇마루까지 덮음) · 담(기와담 · 흙돌담 · 싸리울 · 돌담) · 마당 살림
    /// </summary>
    private void BuildHouse3D(float hw, float hd, System.Random rng)
    {
        var house = new GameObject("House3D").transform;
        house.SetParent(transform, false);
        HanokPlan plan = HanokPlan.Make(Site.Variant ?? "ㅡ자-초가", Site.Size, new System.Random(rng.Next()));
        house.name = $"House3D_{plan.Variant}";
        Plan = plan;
        cutaway = new GameObject("Cutaway").transform;
        cutaway.SetParent(house, false);
        roof3D = new GameObject("Roofs");
        roof3D.transform.SetParent(house, false);
        float gapRate = plan.Ruin ? 0.3f : 0f;
        // 안마당 — 흰 모래 (2026-11-20 노트 9번). 담 안 전체에 얇게 깔고 기단 · 바닥이 그 위에 선다. 폐가는 흙 그대로.
        if (!plan.Ruin)
            HouseMesh.Box(house, "Yard", Vector3.zero, new Vector3(Site.Size.x - 0.6f, 0.015f, Site.Size.y - 0.6f), "floor_yard", false);
        var posts = new HashSet<Vector2Int>();
        void PostOnce(Vector2 p, float h) { if (posts.Add(new Vector2Int(Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f)))) Post(house, new Vector3(p.x, 0f, p.y), h); }

        int gidanN = 0;
        // 방 — 기단 · 바닥 · 기둥 · 가구
        foreach (HanokRoomPlan room in plan.Rooms)
        {
            Rect r = room.R;
            HanokBuilding b = plan.Buildings[room.Building];
            float fh = FloorHeight(room.Kind);
            // 기단 — 벽 밖으로 0.6m 나온 장대석 단 (2026-11-20 마인크래프트 노트 3번). 높이는 방바닥에 맞춘다(바닥보다 높으면 바닥을 덮는다).
            HouseMesh.Box(house, "Gidan", new Vector3(r.center.x, 0f, r.center.y), new Vector3(r.width + 1.2f, Mathf.Max(0.05f, fh - 0.01f) - 0.004f * (gidanN++ % 6), r.height + 1.2f), "wall_base");   // 겹친 기단 윗면이 같은 높이면 깜빡인다 — 조금씩 엇갈리게
            HouseMesh.Box(house, "Floor", new Vector3(r.center.x, 0f, r.center.y), new Vector3(r.width - 0.1f, fh, r.height - 0.1f), FloorTex(room.Kind), false);
            if (room.Kind == HanokRoom.Stable)
                HouseMesh.BoxColor(house, "Straw", new Vector3(r.center.x, fh, r.center.y), new Vector3(r.width * 0.6f, 0.04f, r.height * 0.5f), new Color(0.62f, 0.52f, 0.3f));
            foreach (Vector2 c in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
                PostOnce(c, b.WallHeight);
            if (room.Kind is HanokRoom.Hall or HanokRoom.Pavilion or HanokRoom.Gate or HanokRoom.Stable)   // 트인 앞 — 칸 사이 기둥
            {
                Side(r, room.Facing, out Vector2 a, out Vector2 e);
                int n = Mathf.Max(1, Mathf.RoundToInt((e - a).magnitude / 2.6f));
                for (int i = 1; i < n; i++) PostOnce(Vector2.Lerp(a, e, i / (float)n), b.WallHeight);
            }
            if (room.Kind == HanokRoom.Gate)   // 대문 — 두 짝이 안으로 열려 있다
            {
                Side(r, room.Facing, out Vector2 a, out Vector2 e);
                Vector2 mid = (a + e) * 0.5f, along = (e - a).normalized, inward = -Out(room.Facing);
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector2 hinge = mid + along * sgn * ((e - a).magnitude * 0.42f);
                    Vector2 leaf = hinge + inward * 0.6f;
                    HouseMesh.Box(house, "GateLeaf", new Vector3(leaf.x, 0f, leaf.y), Mathf.Abs(along.x) > 0.5f ? new Vector3(0.1f, 2.3f, 1.1f) : new Vector3(1.1f, 2.3f, 0.1f), "wood_post");
                }
            }
            if (room.Kind == HanokRoom.Pavilion)   // 누마루 — 아래 기둥이 드러난다
                HouseMesh.BoxColor(house, "PavilionShade", new Vector3(r.center.x, 0.02f, r.center.y), new Vector3(r.width - 0.4f, 0.02f, r.height - 0.4f), new Color(0.18f, 0.15f, 0.12f));

            // 가구 — 마당 반대쪽(뒷벽)을 따라. 방 방향대로 돌린 받침에 놓는다
            var pivot = new GameObject("Furnish").transform;
            pivot.SetParent(house, false);
            pivot.localPosition = new Vector3(r.center.x, fh, r.center.y);
            pivot.localRotation = Quaternion.Euler(0f, room.Facing switch { 3 => 0f, 1 => 180f, 0 => -90f, _ => 90f }, 0f);
            float width = room.Facing is 1 or 3 ? r.width : r.height, depth = room.Facing is 1 or 3 ? r.height : r.width;
            float cursor = -width * 0.5f + 0.55f, end = width * 0.5f - 0.55f;
            int slot = 0;
            foreach (string kind in FurnitureOf(room.Kind, b.Name, rng))
            {
                float wdt = Furniture(pivot, kind, Vector3.zero, true);
                if (cursor + wdt <= end) { Furniture(pivot, kind, new Vector3(cursor + wdt * 0.5f, 0f, depth * 0.5f - 0.62f), false); cursor += wdt + 0.3f; }
                else Furniture(pivot, kind, new Vector3(((slot++ % 2) - 0.5f) * Mathf.Min(1.4f, width * 0.4f), 0f, -0.1f), false);
            }
        }

        // 툇마루 — 마당 쪽 앞에 마루 띠 + 바깥 기둥
        foreach (Rect p in plan.Porches)
        {
            HouseMesh.Box(house, "ToetGidan", new Vector3(p.center.x, 0f, p.center.y), new Vector3(p.width + 1.2f, 0.285f - 0.004f * (gidanN++ % 6), p.height + 1.2f), "wall_base");
            HouseMesh.Box(house, "Toet", new Vector3(p.center.x, 0f, p.center.y), new Vector3(p.width, 0.3f, p.height), "floor_wood");
        }

        // 벽 줄
        foreach (HanokRun run in plan.Runs)
        {
            Vector3 a = new(run.A.x, 0f, run.A.y), e = new(run.B.x, 0f, run.B.y);
            float len = run.Length;
            var openings = new (float at, float width, bool door)[run.Openings.Count];
            for (int i = 0; i < openings.Length; i++) openings[i] = (run.Openings[i].At, run.Openings[i].Width, run.Openings[i].Kind == HanokOpenKind.Door);
            switch (run.Kind)
            {
                case HanokRunKind.Outer:
                case HanokRunKind.Inner:
                {
                    float h = run.Building >= 0 ? plan.Buildings[run.Building].WallHeight : House3DWall;
                    if (run.Kind == HanokRunKind.Inner) h *= 0.94f;
                    WallRun(house, a, e, openings, run.Kind == HanokRunKind.Outer ? gapRate : 0f, rng, h, cut: CutWalls && run.AlongX && !run.BackWall,
                            inward: new Vector3(run.Inward.x, 0f, run.Inward.y), outer: run.Kind == HanokRunKind.Outer);
                    break;
                }
                case HanokRunKind.Rail:
                    Rail(house, a + Vector3.up * 0.5f, e + Vector3.up * 0.5f);
                    Blocker("RailBlock", (a + e) * 0.5f, run.AlongX ? new Vector3(len, 1.2f, 0.2f) : new Vector3(0.2f, 1.2f, len));
                    break;
                case HanokRunKind.Fence:
                    FenceRun(house, plan.Fence, plan.Ruin, a, e, openings, rng, cut: CutWalls && run.AlongX && run.A.y < 0f);
                    break;
            }
        }

        // 지붕 — 채마다 우진각, 툇마루까지 덮는다
        for (int i = 0; i < plan.Buildings.Count; i++)
        {
            HanokBuilding b = plan.Buildings[i];
            Rect cover = b.R;
            foreach (Rect p in plan.Porches)
            {
                Rect grown = Rect.MinMaxRect(b.R.xMin - 0.1f, b.R.yMin - 0.1f, b.R.xMax + 0.1f, b.R.yMax + 0.1f);
                if (grown.Overlaps(p) && OwnsPorch(plan, i, p))
                    cover = Rect.MinMaxRect(Mathf.Min(cover.xMin, p.xMin), Mathf.Min(cover.yMin, p.yMin), Mathf.Max(cover.xMax, p.xMax), Mathf.Max(cover.yMax, p.yMax));
            }
            float shortSide = Mathf.Min(cover.width, cover.height);
            GameObject roof = HouseMesh.HipRoof(roof3D.transform, cover, b.WallHeight + 0.15f, shortSide * (b.Thatch ? 0.36f : 0.46f), b.Thatch ? 0.55f : 1.1f, b.Thatch ? "roof_choga" : "roof_giwa", !b.Thatch);
            var rooms = new List<Rect>();
            foreach (HanokRoomPlan rp in plan.Rooms) if (rp.Building == i) rooms.Add(rp.R);
            buildingRoofs.Add((roof, rooms));
            if (plan.Ruin && i == 0) roof.transform.Find("RoofS")?.gameObject.SetActive(false);   // 폐가 — 앞 지붕이 내려앉았다
        }

        // 마당 살림
        int treeN = 0;
        foreach (HanokYard y in plan.Yard)
        {
            Vector3 at = new(y.At.x, 0f, y.At.y);
            switch (y.Kind)
            {
                case "jangdok":   // 장독대 — 돌 단 위 장독
                    // PixelLab 장독대 그림 (2026-11-20) — 돌 단 + 크고 작은 독 무리가 한 장에
                    WorldCard("jangdokdae", at, house);
                    Blocker("JangdokBlock", at, new Vector3(2.0f, 1f, 1.1f));
                    break;
                case "jars": WorldCard("jars", at, house); break;
                case "woodpile": WorldCard("woodpile", at, house); break;
                case "cart": WorldCard("cart", at, house); break;
                case "tree": WorldCard((treeN++ + Site.Id.Length) % 2 == 0 ? "tree_zelkova" : "tree_maple", at, house); break;
                case "well":
                    HouseMesh.Box(house, "WellRim", at, new Vector3(1.3f, 0.6f, 1.3f), "wall_base");
                    HouseMesh.BoxColor(house, "WellWater", at + Vector3.up * 0.58f, new Vector3(0.9f, 0.03f, 0.9f), new Color(0.12f, 0.2f, 0.26f));
                    HouseMesh.Box(house, "WellPost", at + new Vector3(-0.7f, 0f, 0f), new Vector3(0.14f, 1.6f, 0.14f), "wood_post");
                    HouseMesh.Box(house, "WellPost", at + new Vector3(0.7f, 0f, 0f), new Vector3(0.14f, 1.6f, 0.14f), "wood_post");
                    HouseMesh.Box(house, "WellBeam", at + new Vector3(0f, 1.55f, 0f), new Vector3(1.6f, 0.12f, 0.14f), "wood_post");
                    Blocker("WellBlock", at, new Vector3(1.3f, 1f, 1.3f));
                    break;
                case "pyeongsang":   // 평상
                    WorldCard("pyeongsang", at, house);   // PixelLab 평상 그림 (2026-11-20)
                    break;
                case "stepping":   // 박석 디딤돌 — 대문에서 대청 쪽으로
                    for (int i = -2; i <= 2; i++)
                        HouseMesh.Box(house, "Stepping", at + new Vector3((i % 2) * 0.15f, 0f, i * 0.9f), new Vector3(0.75f, 0.05f, 0.55f), "floor_flagstone", false);
                    break;
            }
        }

        // 가볍게 (v7 · 사용자 「0장으로 넘어갈 때 오래 걸리고 무거워진 느낌」) — 상자 수백 개를 재질마다 메시 하나로 합친다.
        // 늘 보이는 것 · 카메라 쪽 벽 윗부분 · 지붕은 따로 켜고 끄니 셋을 따로 합친다. 그림 카드(소품)는 그대로.
        if (!HeavyForCompare)
        {
            HouseMesh.Combine(house, cutaway, roof3D.transform);
            HouseMesh.Combine(cutaway);
            foreach (var (roof, _) in buildingRoofs) HouseMesh.Combine(roof.transform);   // 채마다 따로 합친다 — 채마다 따로 걷으니까
        }

        if (PreviewNoRoof) { roof3D.SetActive(false); cutaway.gameObject.SetActive(false); }
    }

    private static Vector2 Out(int facing) => facing switch { 0 => Vector2.right, 1 => Vector2.up, 2 => Vector2.left, _ => Vector2.down };

    private static void Side(Rect r, int s, out Vector2 a, out Vector2 b)
    {
        switch (s)
        {
            case 0: a = new Vector2(r.xMax, r.yMin); b = new Vector2(r.xMax, r.yMax); break;
            case 1: a = new Vector2(r.xMin, r.yMax); b = new Vector2(r.xMax, r.yMax); break;
            case 2: a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.xMin, r.yMax); break;
            default: a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.xMax, r.yMin); break;
        }
    }

    /// <summary>툇마루가 이 채의 방 앞에 붙었는가.</summary>
    private static bool OwnsPorch(HanokPlan plan, int building, Rect porch)
    {
        foreach (HanokRoomPlan r in plan.Rooms)
            if (r.Building == building)
            {
                Rect g = Rect.MinMaxRect(r.R.xMin - 0.05f, r.R.yMin - 0.05f, r.R.xMax + 0.05f, r.R.yMax + 0.05f);
                if (g.Overlaps(porch)) return true;
            }
        return false;
    }

    /// <summary>담 한 줄 — 기와담(회벽 몸 + 기와 덮개) · 흙돌담 · 싸리울 · 막돌 돌담. 대문 틈엔 기둥(기와담은 일각문 지붕).</summary>
    private void FenceRun(Transform parent, HanokFence kind, bool ruin, Vector3 a, Vector3 b, (float at, float width, bool door)[] gates, System.Random rng, bool cut)
    {
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = alongX ? b.x - a.x : b.z - a.z;
        Vector3 dir = alongX ? Vector3.right : Vector3.forward;
        float h = kind switch { HanokFence.Giwa => 1.8f, HanokFence.Earth => 1.5f, HanokFence.Stone => 1.3f, _ => 1.3f };
        float t = kind == HanokFence.Brush ? 0.18f : 0.5f;
        Vector3 Size(float l, float hh, float tt) => alongX ? new Vector3(l, hh, tt) : new Vector3(tt, hh, l);
        var solid = new List<(float, float)>();
        float step = ruin || kind == HanokFence.Stone || kind == HanokFence.Brush ? 2f : 1000f;   // 돌담은 높이가 들쭉날쭉 · 싸리울은 2m 마다 말뚝
        void Piece(float s0, float s1)
        {
            if (s1 - s0 < 0.05f) return;
            if (ruin && rng.NextDouble() < 0.35) return;   // 쓰러진 울
            Vector3 c = a + dir * ((s0 + s1) * 0.5f);
            float l = s1 - s0;
            float hh = kind == HanokFence.Stone ? h * (0.85f + (float)rng.NextDouble() * 0.3f) : h;
            float low = cut ? Mathf.Min(hh, CutHeight) : hh;
            switch (kind)
            {
                case HanokFence.Brush:   // 싸리울 — 엮은 가지 띠 + 말뚝
                    HouseMesh.BoxColor(parent, "Ssari", c, Size(l, low, t), new Color(0.46f, 0.35f, 0.22f));
                    if (cut && hh > low) HouseMesh.BoxColor(cutaway, "SsariTop", c + Vector3.up * low, Size(l, hh - low, t), new Color(0.46f, 0.35f, 0.22f));
                    HouseMesh.Box(parent, "FencePost", a + dir * s0, Size(0.12f, hh + 0.15f, 0.12f), "wood_post");
                    break;
                default:
                    string tex = kind == HanokFence.Giwa ? "wall_plaster" : "wall_base";
                    HouseMesh.Box(parent, "Dam", c, Size(l, low, t), tex);
                    if (cut && hh > low) HouseMesh.Box(cutaway, "DamTop", c + Vector3.up * low, Size(l, hh - low, t), tex);
                    if (kind == HanokFence.Giwa)   // 기와 갓 — 넓은 아래 단 + 좁은 윗단 + 흰 마루 한 줄 (노트 8번)
                    {
                        Transform capParent = cut ? cutaway : parent;
                        HouseMesh.Box(capParent, "DamCap", c + Vector3.up * hh, Size(l + 0.02f, 0.12f, t + 0.55f), "roof_giwa");
                        HouseMesh.Box(capParent, "DamCap2", c + Vector3.up * (hh + 0.12f), Size(l + 0.02f, 0.14f, t + 0.2f), "roof_giwa");
                        HouseMesh.BoxColor(capParent, "DamRidge", c + Vector3.up * (hh + 0.26f), Size(l + 0.02f, 0.06f, 0.16f), new Color(0.90f, 0.88f, 0.82f));
                    }
                    if (kind == HanokFence.Earth) HouseMesh.Box(cut ? cutaway : parent, "DamCap", c + Vector3.up * hh, Size(l + 0.02f, 0.25f, t + 0.25f), "roof_choga");
                    if (kind == HanokFence.Giwa) HouseMesh.Box(parent, "DamBase", c, Size(l, 0.5f, t + 0.04f), "wall_base");
                    break;
            }
            solid.Add((s0, s1));
        }
        var cuts = new List<(float s, float e)>();
        foreach (var g in gates) cuts.Add((len * 0.5f + g.at - g.width * 0.5f, len * 0.5f + g.at + g.width * 0.5f));
        cuts.Sort((p, q) => p.s.CompareTo(q.s));
        float cur = 0f;
        foreach (var (s, e) in cuts)
        {
            for (float s0 = cur; s0 < s - 0.01f; s0 += step) Piece(s0, Mathf.Min(s, s0 + step));
            foreach (float at in new[] { s, e })   // 대문 기둥
                HouseMesh.Box(parent, "GatePost", a + dir * at, Size(0.24f, h + 0.5f, 0.24f), "wood_post");
            if (kind == HanokFence.Giwa)   // 일각문 — 작은 기와 지붕
            {
                Vector3 c = a + dir * ((s + e) * 0.5f);
                Rect r = alongX ? new Rect(c.x - (e - s) * 0.5f, c.z - 0.5f, e - s, 1f) : new Rect(c.x - 0.5f, c.z - (e - s) * 0.5f, 1f, e - s);
                HouseMesh.HipRoof(roof3D.transform, r, h + 0.5f, 0.5f, 0.35f, "roof_giwa", true);
            }
            cur = e;
        }
        for (float s0 = cur; s0 < len - 0.01f; s0 += step) Piece(s0, Mathf.Min(len, s0 + step));
        foreach (var (m0, m1) in Merge(solid))
            Blocker("FenceBlock", a + dir * ((m0 + m1) * 0.5f), Size(m1 - m0, 1.6f, t));
    }

    /// <summary>기둥 + 주춧돌.</summary>
    private void Post(Transform parent, Vector3 at, float height = House3DWall)
    {
        HouseMesh.Box(parent, "Chucho", at, new Vector3(0.56f, 0.34f, 0.56f), "wall_base");
        // 기둥이 벽(0.35m)보다 굵어야 벽면 앞으로 드러난다 — 기둥 · 띠가 「나무 액자 + 흰 판」이 된다 (노트 7번)
        HouseMesh.Box(parent, "Post", at + Vector3.up * 0.18f, new Vector3(0.44f, height, 0.44f), "wood_post");
    }

    /// <summary>
    /// a → b 벽 한 줄 (축에 나란히). openings = (벽 가운데부터 거리, 폭, 문인가). 창은 0.9 ~ 1.8m 높이에 뚫리고 나무 틀이 둘린다.
    /// gaps = 무너진 벽 비율(그 몫만큼 토막을 빼먹는다).
    /// </summary>
    private void WallRun(Transform parent, Vector3 a, Vector3 b, (float at, float width, bool door)[] openings, float gaps, System.Random rng, float height = House3DWall, bool cut = false, Vector3 inward = default, bool outer = false)
    {
        const float t = 0.35f;
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = alongX ? b.x - a.x : b.z - a.z;
        Vector3 dir = alongX ? Vector3.right : Vector3.forward;
        Vector3 mid = (a + b) * 0.5f;
        var cuts = new List<(float s, float e, bool door)>();
        foreach (var o in openings) cuts.Add((len * 0.5f + o.at - o.width * 0.5f, len * 0.5f + o.at + o.width * 0.5f, o.door));
        cuts.Sort((p, q) => p.s.CompareTo(q.s));
        Vector3 Size(float l, float h) => alongX ? new Vector3(l, h, t) : new Vector3(t, h, l);
        var solid = new List<(float, float)>();
        float step = gaps > 0f || HeavyForCompare ? 2f : 1000f;   // 무너진 집만 2m 토막으로 (군데군데 빠지게) — 성한 벽은 한 덩어리 (v7 가볍게)
        void Piece(float s0, float s1, float y0, float y1, string tex = "wall_plaster")
        {
            if (s1 - s0 < 0.05f || y1 - y0 < 0.05f) return;
            if (gaps > 0f && tex == "wall_plaster" && rng.NextDouble() < gaps) { if (y0 < 0.1f) HouseMesh.Box(parent, "Rubble", a + dir * ((s0 + s1) * 0.5f), Size(s1 - s0, 0.35f), "wall_base"); return; }
            if (cut && y1 > CutHeight + 0.05f)   // 카메라 쪽 벽 — 1.1m 위는 지붕과 함께 걷힌다
            {
                if (y0 < CutHeight) HouseMesh.Box(parent, "Wall", a + dir * ((s0 + s1) * 0.5f) + Vector3.up * y0, Size(s1 - s0, CutHeight - y0), tex);
                float c0 = Mathf.Max(y0, CutHeight);
                HouseMesh.Box(cutaway != null ? cutaway : parent, "WallTop", a + dir * ((s0 + s1) * 0.5f) + Vector3.up * c0, Size(s1 - s0, y1 - c0), tex);
            }
            else HouseMesh.Box(parent, "Wall", a + dir * ((s0 + s1) * 0.5f) + Vector3.up * y0, Size(s1 - s0, y1 - y0), tex);
            if (y0 < 0.1f) solid.Add((s0, s1));   // 바닥에 닿는 토막만 걸음을 막는다(문틈 · 무너진 곳은 지나간다) — 막이는 끝에 이어 붙여 하나로
        }
        float cur = 0f;
        foreach (var c in cuts)
        {
            // 벽 토막은 2m 마다 끊어 무너진 집이 군데군데 빠질 수 있게
            for (float s0 = cur; s0 < c.s; s0 += step) Piece(s0, Mathf.Min(c.s, s0 + step), 0f, height);
            if (c.door)
            {
                // 문 (v8 · 사용자 「문도 좀 잘 보이게」 · 덕코프 사진: 문틈마다 짙은 문짝이 반쯤 열려 있다)
                //   짙은 문설주 · 인방 + 안으로 열린 띠살문 두 짝(짙은 살 · 밝은 창호지) + 바깥문 앞 디딤돌
                Piece(c.s, c.e, 2.1f, height);                                         // 문 위 벽
                Color frame = new(0.24f, 0.15f, 0.09f);
                foreach (float at in new[] { c.s, c.e })
                    HouseMesh.BoxColor(parent, "DoorJamb", a + dir * at, alongX ? new Vector3(0.2f, 2.2f, t + 0.12f) : new Vector3(t + 0.12f, 2.2f, 0.2f), frame);
                HouseMesh.BoxColor(parent, "DoorLintel", a + dir * ((c.s + c.e) * 0.5f) + Vector3.up * 2.1f, alongX ? new Vector3(c.e - c.s + 0.3f, 0.2f, t + 0.14f) : new Vector3(t + 0.14f, 0.2f, c.e - c.s + 0.3f), frame);
                Vector3 inn = inward.sqrMagnitude > 0.01f ? inward.normalized : (alongX ? Vector3.forward : Vector3.right);
                float lw = (c.e - c.s) * 0.5f - 0.08f;
                foreach (var (hinge, sgn) in new[] { (c.s + 0.12f, 1f), (c.e - 0.12f, -1f) })
                {
                    // 문짝이 문틀에 붙어 90° 안으로 열렸다 (가운데 길은 트여 있다)
                    Vector3 centre = a + dir * hinge + inn * (t * 0.5f + lw * 0.5f) + Vector3.up * 0.05f;
                    Vector3 leaf = alongX ? new Vector3(0.07f, 2.0f, lw) : new Vector3(lw, 2.0f, 0.07f);
                    HouseMesh.Box(parent, "DoorLeaf", centre, leaf, "lattice_door");   // 띠살문 그림 (2026-11-20 · 한지 + 짙은 살)
                }
                if (outer)   // 디딤돌
                    HouseMesh.Box(parent, "Didimdol", a + dir * ((c.s + c.e) * 0.5f) - inn * (t * 0.5f + 0.9f), alongX ? new Vector3(c.e - c.s, 0.16f, 0.6f) : new Vector3(0.6f, 0.16f, c.e - c.s), "floor_stone");
            }
            else
            {
                Piece(c.s, c.e, 0f, 0.9f);                                             // 창 아래
                Piece(c.s, c.e, 1.8f, height);                                         // 창 위
                // 창 — 짙은 틀 + 창호지 + 살 (v8 · 덕코프 사진: 벽마다 창이 또렷한 네모로 보인다)
                Vector3 wc = a + dir * ((c.s + c.e) * 0.5f);
                Color wf = new(0.24f, 0.15f, 0.09f);
                float ww = c.e - c.s;
                HouseMesh.Box(parent, "WinPaper", wc + Vector3.up * 0.9f, alongX ? new Vector3(ww, 0.9f, 0.08f) : new Vector3(0.08f, 0.9f, ww), "lattice");   // 정자살 그림
                HouseMesh.BoxColor(parent, "WinSill", wc + Vector3.up * 0.84f, alongX ? new Vector3(ww + 0.2f, 0.1f, t + 0.1f) : new Vector3(t + 0.1f, 0.1f, ww + 0.2f), wf);
                HouseMesh.BoxColor(parent, "WinHead", wc + Vector3.up * 1.78f, alongX ? new Vector3(ww + 0.2f, 0.1f, t + 0.1f) : new Vector3(t + 0.1f, 0.1f, ww + 0.2f), wf);
            }
            cur = c.e;
        }
        for (float s0 = cur; s0 < len - 0.01f; s0 += step) Piece(s0, Mathf.Min(len, s0 + step), 0f, height);
        // 【나무 띠 세 줄】 (2026-11-20 마인크래프트 노트 2번) — 하방(바닥 ~ 0.28) · 중방(0.84 ~ 0.96, 창 밑틀과 같은 줄) · 상방(벽 위 0.22).
        // 벽보다 0.04m 씩 앞뒤로 나와 기둥과 이어진다. 하방 · 중방은 문에서 끊기고 상방은 문 위로 지나간다. 무너진 벽엔 두지 않는다.
        if (gaps <= 0f && len > 0.3f)
        {
            float bt = t + 0.08f;
            Vector3 Band(float l, float h) => alongX ? new Vector3(l, h, bt) : new Vector3(bt, h, l);
            var runs = new List<(float, float)>();
            float c0 = 0f;
            foreach (var c in cuts) { if (!c.door) continue; if (c.s > c0 + 0.05f) runs.Add((c0, c.s)); c0 = c.e; }
            if (len > c0 + 0.05f) runs.Add((c0, len));
            foreach (var (b0, b1) in runs)
            {
                Vector3 bc = a + dir * ((b0 + b1) * 0.5f);
                HouseMesh.Box(parent, "Haban", bc, Band(b1 - b0, 0.28f), "wood_post");
                HouseMesh.Box(parent, "Jungban", bc + Vector3.up * 0.84f, Band(b1 - b0, 0.12f), "wood_post");
            }
            HouseMesh.Box(parent, "Sangban", a + dir * (len * 0.5f) + Vector3.up * (height - 0.22f), Band(len, 0.25f), "wood_post");   // 벽 위보다 3cm 높게 — 같은 높이면 두 윗면이 번갈아 그려져 반짝였다(폰 녹화 v8d)
        }
        foreach (var (m0, m1) in HeavyForCompare ? solid : Merge(solid))
            Blocker("WallBlock", a + dir * ((m0 + m1) * 0.5f), Size(m1 - m0, House3DWall));
    }

    /// <summary>벽 위 구간들을 이어 붙인다 — 창 아래 · 벽 토막이 붙어 있으면 막이 하나 (막이마다 길찾기 구멍을 새로 판다).</summary>
    private static List<(float, float)> Merge(List<(float s, float e)> parts)
    {
        parts.Sort((p, q) => p.s.CompareTo(q.s));
        var o = new List<(float, float)>();
        foreach (var (ps, pe) in parts)
        {
            if (o.Count > 0 && ps <= o[^1].Item2 + 0.05f) o[^1] = (o[^1].Item1, Mathf.Max(o[^1].Item2, pe));
            else o.Add((ps, pe));
        }
        return o;
    }

    /// <summary>툇마루 난간 — 낮은 나무 띠 + 동자 기둥.</summary>
    private void Rail(Transform parent, Vector3 a, Vector3 b)
    {
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = (b - a).magnitude;
        if (len < 0.2f) return;
        Vector3 mid = (a + b) * 0.5f;
        HouseMesh.Box(parent, "Rail", mid + Vector3.up * 0.62f, alongX ? new Vector3(len, 0.1f, 0.12f) : new Vector3(0.12f, 0.1f, len), "wood_post");
        int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.6f));
        for (int i = 0; i <= n; i++)
            HouseMesh.Box(parent, "Baluster", Vector3.Lerp(a, b, i / (float)n) + Vector3.up * 0.24f, new Vector3(0.08f, 0.4f, 0.08f), "wood_post");
    }

    /// <summary>
    /// 한옥 가구 (3D 상자 묶음) — 놓고 그 폭(x)을 돌려준다. measureOnly 면 놓지 않고 폭만.
    /// 반닫이 · 뒤주 · 이불장(quilt) · 선반 · 부뚜막(stove) · 소반 · 장독(jars, 그림). 반닫이 · 뒤주 · 이불장 · 선반은 뒤지는 자리.
    /// </summary>
    private float Furniture(Transform parent, string kind, Vector3 at, bool measureOnly)
    {
        switch (kind)
        {
            case "bandaji":
                if (measureOnly) return 1.1f;
                HouseMesh.Box(parent, "Bandaji", at, new Vector3(1.1f, 0.65f, 0.5f), "wood_post");
                HouseMesh.BoxColor(parent, "BandajiPlate", at + new Vector3(0f, 0.3f, -0.26f), new Vector3(0.5f, 0.22f, 0.03f), new Color(0.62f, 0.48f, 0.22f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.7f)));
                return 1.1f;
            case "dwiju":
                if (measureOnly) return 0.9f;
                foreach (var o in new[] { new Vector3(-0.35f, 0f, -0.25f), new Vector3(0.35f, 0f, -0.25f), new Vector3(-0.35f, 0f, 0.25f), new Vector3(0.35f, 0f, 0.25f) })
                    HouseMesh.Box(parent, "DwijuLeg", at + o, new Vector3(0.1f, 0.3f, 0.1f), "wood_post");
                HouseMesh.Box(parent, "Dwiju", at + Vector3.up * 0.3f, new Vector3(0.85f, 0.75f, 0.65f), "floor_wood");
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.8f)));
                return 0.9f;
            case "quilt":
                if (measureOnly) return 1.2f;
                HouseMesh.Box(parent, "Ijang", at, new Vector3(1.2f, 1.0f, 0.5f), "wood_post");
                HouseMesh.BoxColor(parent, "Quilt1", at + Vector3.up * 1.0f, new Vector3(1.05f, 0.18f, 0.45f), new Color(0.55f, 0.22f, 0.22f));
                HouseMesh.BoxColor(parent, "Quilt2", at + Vector3.up * 1.18f, new Vector3(1.0f, 0.16f, 0.42f), new Color(0.26f, 0.34f, 0.52f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.75f)));
                return 1.2f;
            case "shelf":
                if (measureOnly) return 1.6f;
                HouseMesh.Box(parent, "ShelfSideL", at + new Vector3(-0.75f, 0f, 0f), new Vector3(0.1f, 1.5f, 0.4f), "wood_post");
                HouseMesh.Box(parent, "ShelfSideR", at + new Vector3(0.75f, 0f, 0f), new Vector3(0.1f, 1.5f, 0.4f), "wood_post");
                for (float y = 0.1f; y < 1.5f; y += 0.45f)
                    HouseMesh.Box(parent, "ShelfBoard", at + Vector3.up * y, new Vector3(1.5f, 0.06f, 0.4f), "floor_wood");
                HouseMesh.BoxColor(parent, "ShelfJar", at + new Vector3(-0.4f, 0.56f, 0f), new Vector3(0.25f, 0.3f, 0.25f), new Color(0.42f, 0.28f, 0.18f));
                HouseMesh.BoxColor(parent, "ShelfBox", at + new Vector3(0.35f, 1.01f, 0f), new Vector3(0.4f, 0.22f, 0.3f), new Color(0.55f, 0.45f, 0.3f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.7f)));
                return 1.6f;
            case "stove":
                if (measureOnly) return 2.2f;
                HouseMesh.Box(parent, "Bumak", at, new Vector3(2.2f, 0.75f, 0.85f), "wall_base");
                HouseMesh.BoxColor(parent, "Sot", at + new Vector3(-0.5f, 0.75f, 0f), new Vector3(0.62f, 0.3f, 0.62f), new Color(0.12f, 0.12f, 0.13f));
                HouseMesh.BoxColor(parent, "Fire", at + new Vector3(-0.5f, 0.12f, -0.44f), new Vector3(0.35f, 0.25f, 0.03f), new Color(0.9f, 0.45f, 0.15f));
                return 2.2f;
            case "soban":
                if (measureOnly) return 0.8f;
                HouseMesh.Box(parent, "Soban", at + Vector3.up * 0.2f, new Vector3(0.7f, 0.08f, 0.7f), "floor_wood");
                HouseMesh.Box(parent, "SobanLeg", at, new Vector3(0.5f, 0.2f, 0.5f), "wood_post");
                HouseMesh.BoxColor(parent, "Bowl", at + new Vector3(0.1f, 0.28f, 0f), new Vector3(0.18f, 0.08f, 0.18f), new Color(0.82f, 0.8f, 0.72f));
                return 0.8f;
            case "mungap":   // 문갑 — 낮고 긴 장
                if (measureOnly) return 1.5f;
                HouseMesh.Box(parent, "Mungap", at, new Vector3(1.5f, 0.45f, 0.4f), "wood_post");
                HouseMesh.BoxColor(parent, "MungapDoor", at + new Vector3(-0.35f, 0.12f, -0.21f), new Vector3(0.5f, 0.24f, 0.02f), new Color(0.5f, 0.36f, 0.2f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.7f)));
                return 1.5f;
            case "chest":   // 머릿장 — 두 층 장
                if (measureOnly) return 1.0f;
                HouseMesh.Box(parent, "Chest", at, new Vector3(0.95f, 1.15f, 0.48f), "wood_post");
                HouseMesh.BoxColor(parent, "ChestPlate", at + new Vector3(0f, 0.75f, -0.25f), new Vector3(0.4f, 0.16f, 0.02f), new Color(0.66f, 0.52f, 0.24f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.75f)));
                return 1.0f;
            case "desk":   // 서안 — 사랑방 책상 + 방석
                if (measureOnly) return 1.2f;
                HouseMesh.Box(parent, "Seoan", at + Vector3.up * 0.3f, new Vector3(1.1f, 0.06f, 0.45f), "floor_wood");
                HouseMesh.Box(parent, "SeoanLegL", at + new Vector3(-0.48f, 0f, 0f), new Vector3(0.08f, 0.3f, 0.4f), "wood_post");
                HouseMesh.Box(parent, "SeoanLegR", at + new Vector3(0.48f, 0f, 0f), new Vector3(0.08f, 0.3f, 0.4f), "wood_post");
                HouseMesh.BoxColor(parent, "Book", at + new Vector3(0.25f, 0.36f, 0.05f), new Vector3(0.3f, 0.06f, 0.22f), new Color(0.75f, 0.68f, 0.5f));
                HouseMesh.BoxColor(parent, "Bangseok", at + new Vector3(0f, 0f, -0.75f), new Vector3(0.6f, 0.06f, 0.6f), new Color(0.48f, 0.2f, 0.18f));
                lootSpots.Add(Spot(parent, at + new Vector3(0.9f, 0f, -0.7f)));
                return 1.2f;
            case "waterjar":   // 물독
                if (measureOnly) return 0.8f;
                HouseMesh.BoxColor(parent, "Muldok", at, new Vector3(0.6f, 0.75f, 0.6f), new Color(0.36f, 0.24f, 0.16f));
                HouseMesh.Box(parent, "MuldokLid", at + Vector3.up * 0.75f, new Vector3(0.66f, 0.05f, 0.66f), "floor_wood");
                return 0.8f;
            case "sacks":   // 가마니 · 섬 더미
                if (measureOnly) return 1.4f;
                for (int i = 0; i < 3; i++)
                    HouseMesh.BoxColor(parent, "Gamani", at + new Vector3(-0.4f + i * 0.42f, i == 1 ? 0.38f : 0f, (i % 2) * 0.1f), new Vector3(0.6f, 0.4f, 0.5f), new Color(0.64f, 0.54f, 0.34f) * (0.9f + i * 0.05f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.75f)));
                return 1.4f;
            case "trough":   // 구유
                if (measureOnly) return 1.8f;
                HouseMesh.Box(parent, "Guyu", at, new Vector3(1.8f, 0.5f, 0.5f), "wood_post");
                HouseMesh.BoxColor(parent, "GuyuIn", at + Vector3.up * 0.46f, new Vector3(1.6f, 0.05f, 0.32f), new Color(0.3f, 0.25f, 0.14f));
                return 1.8f;
            case "straw":   // 짚단
                if (measureOnly) return 1.2f;
                HouseMesh.BoxColor(parent, "Jip", at, new Vector3(1.1f, 0.7f, 0.6f), new Color(0.72f, 0.62f, 0.36f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.7f)));
                return 1.2f;
            case "altar":   // 사당 제상 · 감실
                if (measureOnly) return 1.4f;
                HouseMesh.Box(parent, "Gamsil", at + new Vector3(0f, 0f, 0.15f), new Vector3(1.2f, 1.3f, 0.35f), "wood_post");
                HouseMesh.Box(parent, "Jesang", at + new Vector3(0f, 0f, -0.35f), new Vector3(1.0f, 0.55f, 0.5f), "floor_wood");
                HouseMesh.BoxColor(parent, "Candle", at + new Vector3(-0.3f, 0.55f, -0.35f), new Vector3(0.08f, 0.2f, 0.08f), new Color(0.9f, 0.85f, 0.7f));
                HouseMesh.BoxColor(parent, "Candle", at + new Vector3(0.3f, 0.55f, -0.35f), new Vector3(0.08f, 0.2f, 0.08f), new Color(0.9f, 0.85f, 0.7f));
                lootSpots.Add(Spot(parent, at + new Vector3(0f, 0f, -0.95f)));
                return 1.4f;
            case "jars":
                if (measureOnly) return 1.6f;
                WorldCard("jars", at + new Vector3(0f, 0f, 0.1f), parent);
                return 1.6f;
        }
        return 0f;
    }

    /// <summary>가구 받침(돌려 놓은 Furnish) 위 자리 → 이 집터 기준 자리.</summary>
    private Vector3 Spot(Transform parent, Vector3 local) => transform.InverseTransformPoint(parent.TransformPoint(local));

    /// <summary>World(새로 그린 소품) 그림 한 장을 세운다 — 그림 칸 크기 그대로(1m = 23.5칸).</summary>
    private void WorldCard(string sprite, Vector3 local, Transform parent)
    {
        var tex = Resources.Load<Texture2D>($"World/{sprite}");
        if (tex == null) return;
        var card = new GameObject($"Prop_{sprite}");
        card.transform.SetParent(parent, false);
        card.transform.localPosition = local;
        card.transform.rotation = ArtCatalog.CardTilt;
        float w = tex.width / BuildingSprite.PixelsPerMeter;
        card.transform.localScale = new Vector3(w, w * tex.height / tex.width, 1f);
        card.AddComponent<MeshFilter>().sharedMesh = BuildingSprite.Quad();
        var r = card.AddComponent<MeshRenderer>();
        r.sharedMaterial = BuildingSprite.Material();
        r.shadowCastingMode = ShadowCastingMode.On;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
        r.SetPropertyBlock(block);
    }

    /// <summary>방 안 — 지붕이 걷혔을 때만 보이는 마루 · 낮은 벽.</summary>
    private void BuildInterior(float hw, float hd)
    {
        interior = new GameObject("Interior");
        interior.transform.SetParent(transform, false);

        Box("Floor", new Vector3(0f, 0.01f, 0f), new Vector3(Site.Size.x, 0.02f, Site.Size.y), new Color(0.40f, 0.29f, 0.19f), interior.transform);
        Color low = new(0.62f, 0.52f, 0.40f);
        Box("LowN", new Vector3(0f, 0f, hd), new Vector3(Site.Size.x, 0.7f, 0.3f), low, interior.transform);
        Box("LowE", new Vector3(hw, 0f, 0f), new Vector3(0.3f, 0.7f, Site.Size.y), low, interior.transform);
        Box("LowW", new Vector3(-hw, 0f, 0f), new Vector3(0.3f, 0.7f, Site.Size.y), low, interior.transform);
        float side = hw - (Site.Kind == LockSiteKind.OpenHouse ? OpenDoorGap : RoomDoorGap) * 0.5f;
        Box("LowSW", new Vector3(-hw + side * 0.5f, 0f, -hd), new Vector3(side, 0.7f, 0.3f), low, interior.transform);
        Box("LowSE", new Vector3(hw - side * 0.5f, 0f, -hd), new Vector3(side, 0.7f, 0.3f), low, interior.transform);
        interior.SetActive(false);
    }

    private GameObject Wall(string name, Vector3 local, Vector3 size, bool visible, bool side = false, float tint = 1f)
    {
        GameObject blocker = Blocker(name, local, size);
        if (!visible)
            return blocker;

        if (side)
        {
            // 옆 담 — 위에서 내려다보면 띠로 보인다. 담 그림과 같은 돌 색 몸통 + 기와 덮개.
            var body = new Vector3(0.9f, 1.4f, size.z);
            Box($"{name}_Look", local, body, new Color(0.45f, 0.36f, 0.33f) * tint, blocker.transform.parent);
            Box($"{name}_Cap", local + Vector3.up * body.y, new Vector3(1.15f, 0.28f, size.z + 0.3f), new Color(0.20f, 0.21f, 0.27f), blocker.transform.parent);
            return blocker;
        }

        // 남 · 북 담은 PixelLab 담 그림을 4m씩 잇는다.
        int pieces = Mathf.Max(1, Mathf.RoundToInt(size.x / 4f));
        float width = size.x / pieces;
        for (int i = 0; i < pieces; i++)
        {
            float x = local.x - size.x * 0.5f + width * (i + 0.5f);
            GameObject card = Card($"{name}_Card{i}", "lock_wall", new Vector3(x, 0f, local.z - WallThickness * 0.5f), width + 0.05f,
                parent: blocker.transform.parent);
            if (card != null && tint < 1f)
                Tint(card.GetComponent<Renderer>(), tint);
            if (card != null && name == "CrackedWall")
                card.transform.SetParent(blocker.transform, true);
        }
        return blocker;
    }

    /// <summary>걸음 · 총알 · 적 길을 막는 보이지 않는 상자.</summary>
    private GameObject Blocker(string name, Vector3 local, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local + Vector3.up * size.y * 0.5f;
        go.layer = GameLayers.Obstacle;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = size;
        obstacle.carving = true;
        return go;
    }

    private GameObject Card(string name, string texture, Vector3 local, float width, Transform parent = null)
    {
        var source = Resources.Load<Texture2D>($"Locks/{texture}");
        if (source == null)
            return null;

        Texture2D tex = BuildingSprite.CardTexture(texture, source, width);
        var card = new GameObject(name);
        card.transform.SetParent(parent != null ? parent : transform, false);
        card.transform.localPosition = local;
        card.transform.rotation = ArtCatalog.CardTilt;
        card.transform.localScale = new Vector3(width, width * source.height / source.width, 1f);
        card.AddComponent<MeshFilter>().sharedMesh = BuildingSprite.Quad();
        var renderer = card.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = BuildingSprite.Material();
        renderer.shadowCastingMode = ShadowCastingMode.On;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
        renderer.SetPropertyBlock(block);
        return card;
    }

    private static void Tint(Renderer renderer, float k)
    {
        if (renderer == null)
            return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", new Color(k, k, k, 1f));
        renderer.SetPropertyBlock(block);
    }

    private static GameObject Box(string name, Vector3 local, Vector3 size, Color color, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
        else Object.DestroyImmediate(go.GetComponent<Collider>());   // 에디터 맵 사진(MapShots)용 미리보기
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local + Vector3.up * size.y * 0.5f;
        go.transform.localScale = size;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = BuildingSprite.Material();
        var block = new MaterialPropertyBlock();
        block.SetTexture("_BaseMap", Texture2D.whiteTexture);
        block.SetTexture("_MainTex", Texture2D.whiteTexture);
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        return go;
    }

    // ── 매 프레임 ────────────────────────────────────────────────────

    private void Update()
    {
        Transform player = QuestSpotDirector.Player;
        if (player == null)
            return;

        Vector3 p = player.position;
        if (!Seen && (new Vector2(p.x - Site.Center.x, p.z - Site.Center.y)).magnitude < SeenRange)
            Seen = true;

        if (Site.Kind == LockSiteKind.Compound)
            return;

        // 【지붕 걷기】 — 집 안에 들어서면 지붕 그림을 감추고 방을 보인다.
        // 3D 집(결정 2-151)은 덕코프처럼 가까이 오면(20m) 지붕을 걷어 방이 들여다보이게 — 들어가기 전에 안을 보고 고른다
        if (buildingRoofs.Count > 0)
        {
            // 3D 한옥 — 집터(담 안) 안에 있으면 지붕을 전부 걷는다 (v8e · 2026-11-20 폰 「방이 아니라 건물을 기준으로 건물 내에 있을 경우 지붕이 안 보이게」).
            // 담 안으로 들어서면 걷고, 담 밖으로 RoofFar 넘게 나가야 다시 덮는다 — 한 경계로 켜고 끄면 그 선에서 깜빡였다(v8d).
            var local = new Vector2(p.x - Site.Center.x, p.z - Site.Center.y);
            float margin = roofsLifted ? RoofFar : 0f;
            bool inside = Mathf.Abs(local.x) < Site.Size.x * 0.5f + margin && Mathf.Abs(local.y) < Site.Size.y * 0.5f + margin;
            if (inside != roofsLifted)
            {
                roofsLifted = inside;
                foreach (var (roof, _) in buildingRoofs) if (roof != null) roof.SetActive(!inside);
                if (roof3D != null) roof3D.SetActive(!inside);   // 대문 지붕까지
            }
            return;
        }
        bool now = Site.Inside(p, 0.15f);
        if (now == inside)
            return;
        inside = now;
        if (roofCard != null)
            roofCard.enabled = !inside;
        if (roof3D != null)
            roof3D.SetActive(!inside);   // 들어서면 지붕이 걷힌다 (덕코프처럼)
        if (cutaway != null)
            cutaway.gameObject.SetActive(!inside);   // 카메라 쪽 벽 윗부분도 같이
        if (interior != null)
            interior.SetActive(inside);
    }

    // ── 문 · 금 간 담 ────────────────────────────────────────────────

    public string DoorPrompt => IsOpen ? $"{Site.DoorName} (열림)" : KeyRing.IsRegistered(Site.KeyId) ? $"{Site.DoorName} — 열쇠고리로 열기" : $"{Site.DoorName} (잠김)";

    public string CrackPrompt => Breached ? "무너진 담" : "금 간 담";

    public void UseDoor()
    {
        if (IsOpen || busy)
            return;

        if (KeyRing.IsRegistered(Site.KeyId))
        {
            StartCoroutine(Opening("열쇠고리의 열쇠로 여는 중…", null));
            return;
        }

        ItemStack key = FindInBag(Site.KeyId);
        if (key == null)
        {
            StoryDialogueUI.ShowBanner($"{Site.DoorName}은 잠겼다 — 「{Site.DoorName} 열쇠」가 있어야 한다.", 2.2f);
            return;
        }

        StartCoroutine(Opening("열쇠를 돌리는 중…", key));
    }

    private IEnumerator Opening(string text, ItemStack key)
    {
        busy = true;
        StoryDialogueUI.ShowBanner(text, OpenSeconds);
        float until = Time.time + OpenSeconds;
        while (Time.time < until)
        {
            if (!PlayerNear(doorStation.transform.position, 3f))
            {
                busy = false;
                StoryDialogueUI.ShowBanner("문 앞을 떠나 그만두었다.", 1.2f);
                yield break;
            }
            yield return null;
        }

        if (key != null)
        {
            key.Damage(1);
            if (key.IsBroken)
            {
                PlayerInventory.EnsureInstance().Bag.RemoveStack(key);
                PlayerInventory.EnsureInstance().RefreshCapacity();
                StoryDialogueUI.ShowBanner($"{Site.DoorName}이 열렸다 — 열쇠가 부러졌다.", 2f);
            }
            else
            {
                StoryDialogueUI.ShowBanner($"{Site.DoorName}이 열렸다 — 열쇠가 닳았다 ({key.Durability}/{key.MaxDurability}). 소굴 열쇠고리에 걸면 닳지 않는다.", 2.6f);
            }
        }
        else
        {
            StoryDialogueUI.ShowBanner($"{Site.DoorName}이 열렸다.", 1.4f);
        }

        Noise(KeyNoise);
        Open();
        busy = false;
    }

    private void Open()
    {
        IsOpen = true;
        if (doorBlocker != null)
            doorBlocker.SetActive(false);
        if (gateCard != null)
            gateCard.SetActive(false);
        if (openGate != null)
            openGate.SetActive(true);
    }

    public void UseCrack()
    {
        if (Breached || busy)
            return;

        ItemStack powder = FindInBag(KeyAssetIds.PowderId);
        if (powder == null)
        {
            StoryDialogueUI.ShowBanner("금이 간 담이다 — 화약 뭉치가 있으면 무너뜨릴 수 있다.", 2f);
            return;
        }

        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        if (powder.Count > 1)
            powder.Take(1);
        else
            bag.RemoveStack(powder);
        PlayerInventory.EnsureInstance().RefreshCapacity();
        StartCoroutine(Fuse());
    }

    private IEnumerator Fuse()
    {
        busy = true;
        StoryDialogueUI.ShowBanner("심지에 불이 붙었다 — 물러서라!", FuseSeconds);
        yield return new WaitForSeconds(FuseSeconds);

        Breached = true;
        if (crackedWall != null)
            crackedWall.SetActive(false);
        Noise(PowderNoise);
        StoryDialogueUI.ShowBanner("쾅 — 담이 무너졌다. 온 동네가 들었다.", 2f);
        busy = false;
    }

    private static void Noise(float radius)
    {
        var noise = Object.FindAnyObjectByType<PlayerNoise>();
        if (noise != null)
            noise.Emit(radius);
    }

    private static bool PlayerNear(Vector3 at, float range)
    {
        Transform player = QuestSpotDirector.Player;
        if (player == null)
            return false;
        Vector3 d = player.position - at;
        d.y = 0f;
        return d.magnitude <= range;
    }

    public static ItemStack FindInBag(string itemId)
    {
        if (!PlayerInventory.HasInstance)
            return null;
        foreach (ItemStack s in PlayerInventory.Instance.Bag.Stacks)
            if (s?.Definition != null && s.Definition.Id == itemId && !s.IsBroken)
                return s;
        return null;
    }
}

/// <summary>문 · 금 간 담 자리가 주인(LockedSite)을 찾는 표.</summary>
public class LockedSiteStation : MonoBehaviour
{
    public LockedSite Owner;
}

/// <summary>연장 아이템 id — 에디터 생성기(KeyAssetGenerator)와 런타임이 함께 쓴다.</summary>
public static class KeyAssetIds
{
    public const string CrowbarId = "tool_crowbar";
    public const string PowderId = "tool_powder";
}
