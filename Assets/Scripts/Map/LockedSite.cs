using System.Collections;
using System.Collections.Generic;
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
        IReadOnlyList<LockSite> sites = LockSiteTable.For(map, zone);
        foreach (LockSite site in sites)
            Create(site);
        IReadOnlyList<LockSite> open = LockSiteTable.OpenHouses(map, zone);
        foreach (LockSite site in open)
            Create(site);

        GameLogger.Log($"[LockedSite] {zone} — 잠긴 곳 {sites.Count} · 열린 집 {open.Count}");
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
        Wall("WallSW", new Vector3(-hw + southSide * 0.5f, 0f, -hd), new Vector3(southSide, WallHeight, WallThickness), compound);
        Wall("WallSE", new Vector3(hw - southSide * 0.5f, 0f, -hd), new Vector3(southSide, WallHeight, WallThickness), compound);
        Wall("WallE", new Vector3(hw, 0f, 0f), new Vector3(WallThickness, WallHeight, Site.Size.y), compound, side: true);
        Wall("WallW", new Vector3(-hw, 0f, 0f), new Vector3(WallThickness, WallHeight, Site.Size.y), compound, side: true);

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
            int n = 2 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(-hw + 1.6f, hw - 1.6f, (i + 0.5f) / n) + (float)(rng.NextDouble() - 0.5) * 1.2f;
                float z = Mathf.Lerp(0f, hd - 1.4f, (float)rng.NextDouble());
                LootBox.Create(transform.position + new Vector3(x, 0f, z), LootBox.PickTypeFor(rng, rich: false), Site.Chapter, new System.Random(rng.Next()), rich: false);
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
        card.transform.rotation = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);
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
        bool now = Site.Inside(p, 0.15f);
        if (now == inside)
            return;
        inside = now;
        if (roofCard != null)
            roofCard.enabled = !inside;
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
