using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【맵 상자 파밍】 (결정 2-140 · 2026-11-18 사용자 「덕코프는 사물함 · 박스 같은 것도 파밍의 종류인데 하나도 없네」)
/// 덕코프처럼 맵에 상자가 놓이고, 종류마다 나오는 것이 다르다. 판마다 새로 채운다(판 안에서는 다시 차지 않는다).
///   · 장독 — 먹을거리 · 재료        · 반닫이 궤 — 재료 · 탄 · 열쇠
///   · 약단지 — 소모품              · 화살 궤 — 탄 · 무기 · 방어구
///   · 광주리 — 이것저것(구슬 · 각인 빼고)
///   · 물건 등급(tier)은 그 장 + 1까지. 낮은 등급이 더 잦다. 패시브 「희귀 드롭」 · 달(LootScale)이 더 나오게 한다
///   · 앞에 서서 누르면 전리품 창으로 연다(봇짐 · 시체와 같은 창). 비어도 상자는 남는다
///   · 자리: 구역 안 집 둘레 · 길가 · 수풀가 — 출발 자리 8m · 길목 5m 안은 비운다. 넓이에 비례해 대략 10개
/// 그림은 nanasha 「Samurai Tales」(Resources/Wild) — 화풍을 PixelLab 으로 맞출 때 다시 그린다.
/// [임시값] 수 · 확률은 플레이해 보며 맞춘다.
/// </summary>
public class LootBox : MonoBehaviour
{
    public enum Type { Jar, Chest, Medicine, Arms, Basket }

    private static readonly (Type type, string label, string sprite, ItemKind[] kinds, int minRolls, int maxRolls)[] Types =
    {
        (Type.Jar,      "장독",     "set_of_earthenware_pickling_jars",               new[] { ItemKind.Consumable, ItemKind.Material }, 1, 3),
        (Type.Chest,    "반닫이 궤", "wooden_blanket_chest_with_iron_handles",         new[] { ItemKind.Material, ItemKind.Ammo, ItemKind.Key }, 2, 4),
        (Type.Medicine, "약단지",   "lidded_clay_pot_rattling_with_something_inside", new[] { ItemKind.Consumable }, 1, 2),
        (Type.Arms,     "화살 궤",  "crate_of_arrows_with_a_lid",                     new[] { ItemKind.Ammo, ItemKind.Weapon, ItemKind.Armour }, 1, 3),
        (Type.Basket,   "광주리",   "big_woven_basket_with_a_lid_tied_shut",          new[] { ItemKind.Material, ItemKind.Consumable, ItemKind.Ammo, ItemKind.Armour, ItemKind.Key }, 1, 3),
    };

    /// <summary>상자 수 기준 — 0-1 넓이에 10개.</summary>
    public const int BaseCount = 10;
    /// <summary>한 구역 바깥 상자 최대 (예전 18 — 맵 2배 뒤 30). 열린 집 안 상자는 따로.</summary>
    public const int MaxCount = 40;   // 결정 2-150 — 덕코프 상자 자리는 우리의 3배(1천㎡당 2.9 · 우리 1.0). 집 안 가구가 들어오면 더 는다
    public const float PixelsPerMeter = 23.5f;

    private static readonly Dictionary<string, Texture2D> textures = new();
    private LootContainer contents;
    private int typeIndex;

    /// <summary>【잠긴 궤】 (결정 2-143) — 쇠지레로 비틀어 연다. 바깥 반닫이 궤의 일부 [임시값 18%].</summary>
    private bool locked;
    private bool prying;
    public const float LockedChance = 0.18f;
    public const float PrySeconds = 1.5f;
    public const float PryNoise = 14f;

    /// <summary>이 판의 열쇠가 바깥 상자 어딘가에 들어 있을 확률 (잠긴 곳 하나마다) [임시값].</summary>
    public const float KeyChance = 0.45f;

    public string Label => locked ? $"잠긴 {Types[typeIndex].label}"
        : contents != null && contents.IsEmpty ? $"{Types[typeIndex].label} (빈)" : Types[typeIndex].label;

    public LootContainer Contents => contents;
    public static readonly List<LootBox> Active = new();

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    public void Open()
    {
        if (locked)
        {
            TryPry();
            return;
        }

        if (contents == null || contents.IsEmpty)
        {
            StoryDialogueUI.ShowBanner($"{Types[typeIndex].label} — 비었다.", 1.2f);
            return;
        }
        ExchangeWindowUI.EnsureInstance().Open(contents, Types[typeIndex].label, ExchangeWindowUI.Mode.Loot);
    }

    /// <summary>쇠지레로 비튼다 — 쇠지레가 한 번 닳고, 쇳소리가 난다 (결정 2-143).</summary>
    private void TryPry()
    {
        if (prying)
            return;

        ItemStack bar = LockedSite.FindInBag(KeyAssetIds.CrowbarId);
        if (bar == null)
        {
            StoryDialogueUI.ShowBanner($"잠긴 {Types[typeIndex].label} — 쇠지레가 있으면 비틀어 열 수 있다.", 2f);
            return;
        }

        StartCoroutine(Pry(bar));
    }

    private IEnumerator Pry(ItemStack bar)
    {
        prying = true;
        StoryDialogueUI.ShowBanner("쇠지레로 자물쇠를 비트는 중…", PrySeconds);
        yield return new WaitForSeconds(PrySeconds);

        bar.Damage(1);
        if (bar.IsBroken)
        {
            PlayerInventory.EnsureInstance().Bag.RemoveStack(bar);
            StoryDialogueUI.ShowBanner("자물쇠가 떨어졌다 — 쇠지레가 부러졌다.", 1.8f);
        }
        PlayerInventory.EnsureInstance().RefreshCapacity();

        var noise = FindAnyObjectByType<PlayerNoise>();
        if (noise != null)
            noise.Emit(PryNoise);

        locked = false;
        prying = false;
        Open();
    }

    // ── 판 열기 ──────────────────────────────────────────────────────

    /// <summary>이번 구역에 상자를 놓는다. 맵이 없으면 아무것도 하지 않는다.</summary>
    public static void SpawnForRaid(Vector3 start)
    {
        ChapterMap map = ZoneMap.Current != null ? ZoneMap.Current.Map : null;
        ZoneArea? area = map?.AreaOf(ZoneMap.Zone) ?? map?.AreaOf(map.ZoneAt(start));
        if (map == null || !area.HasValue)
            return;

        Rect r = area.Value.Bounds;
        int chapter = ZoneDataTable.ChapterOfZone(StoryManager.TargetZone)?.Chapter ?? 0;
        int target = Mathf.Clamp(Mathf.RoundToInt(BaseCount * Mathf.Pow(r.width * r.height / PopulationTable.ReferenceArea, PopulationTable.AreaPower)), 5, MaxCount);   // 결정 2-149 — 넓이 0.75제곱
        var rng = new System.Random();
        var placed = new List<Vector3>();
        var outside = new List<LootBox>();
        List<Vector3> extractions = ZoneMap.Anchors(MapAnchorKind.Extraction);

        // 후보: 집 둘레(앞마당 · 옆구리) · 길가 · 그 밖 빈 땅. 집이 있으면 거기부터.
        var candidates = new List<Vector3>();
        foreach (MapBlock b in map.Blocks)
        {
            var c = new Vector3(b.Center.x, 0f, b.Center.y);
            if (b.Kind is MapBlockKind.Building or MapBlockKind.Stall or MapBlockKind.Well)
                for (int k = 0; k < 4; k++)
                    candidates.Add(c + new Vector3((float)(rng.NextDouble() * 2 - 1) * (b.Size.x * 0.5f + 1.5f), 0f, -b.Size.z * 0.5f - 1.2f - (float)rng.NextDouble() * 2f));
            else if (b.Kind is MapBlockKind.Bamboo or MapBlockKind.Rock or MapBlockKind.Bush)
                candidates.Add(c + new Vector3((float)(rng.NextDouble() * 2 - 1) * b.Size.x * 0.5f, 0f, -b.Size.z * 0.5f - 1.4f));
        }
        // 덕코프처럼 — 마을 · 야영지에 몰고 길가에 몇 개씩 (결정 2-150 · SpawnBias)
        for (int k = 0; k < 40 + target * 6; k++)
            candidates.Add(SpawnBias.Sample(map, area.Value.ZoneId, r, rng, SpawnBias.Boxes));

        foreach (Vector3 p in candidates)
        {
            if (placed.Count >= target)
                break;
            if (!area.Value.Contains(p) || map.IsBlocked(p, 0.9f) || Flat(p - start) < 8f || LockedSite.Occupies(p))
                continue;
            bool near = false;
            foreach (Vector3 e in extractions) near |= Flat(p - e) < 5f;
            foreach (Vector3 q in placed) near |= Flat(p - q) < 7f;
            if (near)
                continue;
            placed.Add(p);
            LootBox box = Create(p, PickType(rng), chapter, rng);
            if (box != null)
            {
                outside.Add(box);
                // 반닫이 궤의 일부는 잠겨 있다 — 쇠지레로 (결정 2-143).
                if (box.typeIndex == (int)Type.Chest && rng.NextDouble() < LockedChance)
                    box.locked = true;
            }
        }

        // 이 판 잠긴 곳의 열쇠 — 바깥 상자 하나에 들어 있을 수 있다 (결정 2-143).
        SeedKeys(outside, rng);
        GameLogger.Log($"[LootBox] 상자 {placed.Count}개 (목표 {target})");
    }

    private static void SeedKeys(List<LootBox> boxes, System.Random rng)
    {
        ItemCatalog catalog = ItemCatalog.Load();
        if (catalog == null || boxes.Count == 0)
            return;

        foreach (LockedSite site in LockedSite.Active)
        {
            if (site == null || KeyRing.IsRegistered(site.Site.KeyId) || rng.NextDouble() >= KeyChance)
                continue;
            ItemDefinition key = catalog.Find(site.Site.KeyId);
            if (key != null)
                boxes[rng.Next(boxes.Count)].contents.TryPut(key, 1);
        }
    }

    /// <summary>잠긴 곳 안의 상자 — 궤 · 화살 궤 · 약단지가 더 잦다.</summary>
    public static int PickTypeFor(System.Random rng, bool rich)
    {
        if (!rich)
            return PickType(rng);
        double roll = rng.NextDouble();
        return roll < 0.4 ? (int)Type.Chest : roll < 0.65 ? (int)Type.Arms : roll < 0.85 ? (int)Type.Medicine : (int)Type.Jar;
    }

    private static int PickType(System.Random rng)
    {
        // 장독 · 광주리가 흔하고, 화살 궤 · 약단지는 드물다.
        double roll = rng.NextDouble();
        return roll < 0.30 ? 0 : roll < 0.52 ? 4 : roll < 0.74 ? 1 : roll < 0.88 ? 2 : 3;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    public static LootBox Create(Vector3 at, int type, int chapter, System.Random rng, bool rich = false)
    {
        var root = new GameObject($"LootBox_{Types[type].label}");
        root.transform.position = at;
        var box = root.AddComponent<LootBox>();
        box.typeIndex = type;
        box.BuildCard();
        box.Fill(chapter, rng, rich);
        root.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.LootBox, 1.8f);
        return box;
    }

    private void BuildCard()
    {
        string sprite = Types[typeIndex].sprite;
        if (!textures.TryGetValue(sprite, out Texture2D tex) || tex == null)
        {
            tex = Resources.Load<Texture2D>($"Wild/{sprite}");   // 상자 그림은 아직 받은 팩 (장 꾸밈은 World 로 바뀜 · 결정 2-148)
            textures[sprite] = tex;   // 정적으로 붙잡아 둔다 (BuildingSprite 와 같은 이유)
        }
        if (tex == null)
            return;
        var card = new GameObject("Card");
        card.transform.SetParent(transform, false);
        card.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f));
        card.transform.localScale = new Vector3(tex.width / PixelsPerMeter, tex.height / PixelsPerMeter, 1f);
        card.AddComponent<MeshFilter>().sharedMesh = BuildingSprite.Quad();
        var renderer = card.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = BuildingSprite.Material();
        renderer.shadowCastingMode = ShadowCastingMode.On;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>
    /// 채운다. 【rich】(잠긴 곳 안) — 등급이 한 장 위까지 열리고, 한 번 더 뽑고, 파랑(좋음) 이상 하나를 보장한다 (결정 2-143).
    /// </summary>
    private void Fill(int chapter, System.Random rng, bool rich = false)
    {
        if (rich)
            chapter += 1;
        contents = new LootContainer(LootContainer.DefaultCapacity);
        ItemCatalog catalog = ItemCatalog.Load();
        if (catalog == null)
            return;

        var t = Types[typeIndex];
        var pool = new List<ItemDefinition>();
        foreach (ItemDefinition item in catalog.Items)
        {
            if (item == null || System.Array.IndexOf(t.kinds, item.Kind) < 0 || item.Tier > chapter + 1)
                continue;
            if (KeyRing.IsDoorKey(item))   // 문 열쇠는 SeedKeys만 넣는다 — 다른 구역 열쇠가 섞이지 않게
                continue;
            if (!IngredientTable.DropsIn(item.Id, chapter) || !AmmoTable.DropsIn(item.Id, chapter))
                continue;
            pool.Add(item);
        }
        if (pool.Count == 0)
            return;

        // 희귀 드롭 패시브 · 달(LootScale)이 한 번 더 뽑을 기회를 준다.
        float bonus = CorpseController.DropBonus * RaidManager.Current.LootScale;
        int rolls = rng.Next(t.minRolls, t.maxRolls + 1);
        if (rng.NextDouble() < bonus - 1f)
            rolls++;
        if (rich)
        {
            rolls++;
            var good = pool.FindAll(d => ItemGrade.Of(d) >= 2);
            if (good.Count > 0)
            {
                contents.TryPut(good[rng.Next(good.Count)], 1);
                rolls--;
            }
        }

        for (int i = 0; i < rolls; i++)
        {
            ItemDefinition pick = WeightedPick(pool, rng);
            int count = pick.IsStackable ? Mathf.Clamp(1 + rng.Next(0, Mathf.Max(1, pick.StackMax / 4)), 1, pick.StackMax) : 1;
            contents.TryPut(pick, count);
        }
    }

    /// <summary>낮은 등급이 잦다 — 무게 1 / (등급 + 1)².</summary>
    private static ItemDefinition WeightedPick(List<ItemDefinition> pool, System.Random rng)
    {
        float total = 0f;
        foreach (ItemDefinition item in pool)
            total += 1f / Mathf.Pow(item.Tier + 1, 2f);
        double roll = rng.NextDouble() * total;
        foreach (ItemDefinition item in pool)
        {
            roll -= 1f / Mathf.Pow(item.Tier + 1, 2f);
            if (roll <= 0)
                return item;
        }
        return pool[pool.Count - 1];
    }
}
