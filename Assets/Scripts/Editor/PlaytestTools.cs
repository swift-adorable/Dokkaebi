using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 검증용 도구. (로드맵 6-M)
///
/// 만든 이유 — 장비 82종을 만들고 착용·사격까지 연결했는데
/// 【게임 안에서 장비를 얻을 경로가 하나도 없었다.】
/// 전리품 표는 재료 5 + 소모품 3뿐이고 상점·제작은 8단계다.
/// 그래서 6-C부터 6-K까지를 플레이로 확인할 방법이 아예 없었다.
///
/// 이 메뉴는 【검증 전용】이다. 상점과 드랍이 들어오면 지운다.
/// </summary>
public static class PlaytestTools
{
    private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";
    private const string Menu = "Blob/Playtest/";

    private static bool RequirePlayMode()
    {
        if (Application.isPlaying)
            return true;

        EditorUtility.DisplayDialog("플레이 중에만 씁니다",
            "플레이 모드에서 실행하십시오. 가방은 실행 중에만 존재합니다.", "확인");

        return false;
    }

    private static T Load<T>(string path) where T : Object
        => AssetDatabase.LoadAssetAtPath<T>($"{ItemRoot}/{path}");

    private static List<ItemDefinition> LoadFolder(string folder)
        => AssetDatabase.FindAssets("t:ItemDefinition", new[] { $"{ItemRoot}/{folder}" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
            .Where(a => a != null)
            .ToList();

    private static int Give(IEnumerable<ItemDefinition> items)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        int added = 0;

        foreach (ItemDefinition item in items)
        {
            if (item == null)
                continue;

            if (bag.TryAdd(item) > 0)
                added++;
            else
                Debug.LogWarning($"[Playtest] 가방이 가득 차 「{item.DisplayName}」을 넣지 못했습니다.");
        }

        PlayerInventory.Instance.RefreshCapacity();

        Report($"{added}개를 가방에 넣었습니다. "
               + $"({bag.UsedSlots}/{bag.SlotCapacity}칸, {bag.TotalWeight:0.0}/{bag.WeightLimit:0.0}kg)");

        return added;
    }

    /// <summary>
    /// 결과를 콘솔과 화면 양쪽에 남긴다.
    ///
    /// 에디터 Console 창(Window ▸ General ▸ Console)을 띄워 두지 않으면
    /// Debug.Log는 없는 것과 같다. 가방 화면을 열어 같은 문장을 보여 준다.
    /// </summary>
    private static void Report(string message)
    {
        Debug.Log($"[Playtest] {message}");

        InventoryScreenUI.ShowBagWithMessage(message);
    }

    // ── 지급 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 티어 1 한 벌. 「입고 → 쏘고 → 죽으면 잃는다」 한 바퀴를 확인하는 최소 구성이다.
    /// </summary>
    [MenuItem(Menu + "출격 장비 — 티어 1 한 벌")]
    public static void GiveStarterKit()
    {
        if (!RequirePlayMode()) return;

        Give(new ItemDefinition[]
        {
            Load<WeaponDefinition>("Weapons/wpn_t1_pipe.asset"),
            Load<EquipmentDefinition>("Armour/arm_head_t1.asset"),
            Load<EquipmentDefinition>("Armour/arm_body_t1.asset"),
            Load<EquipmentDefinition>("Armour/arm_face_t1.asset"),
            Load<EquipmentDefinition>("Armour/arm_ears_t1.asset"),
            Load<EquipmentDefinition>("Backpacks/bag_t1_sack.asset")
        });
    }

    /// <summary>티어 6 한 벌 + 무거운 가방. 과중량과 최고 티어 수치를 함께 본다.</summary>
    [MenuItem(Menu + "출격 장비 — 티어 6 한 벌")]
    public static void GiveEndgameKit()
    {
        if (!RequirePlayMode()) return;

        Give(new ItemDefinition[]
        {
            Load<WeaponDefinition>("Weapons/wpn_t6_eraser.asset"),
            Load<EquipmentDefinition>("Armour/arm_head_t6.asset"),
            Load<EquipmentDefinition>("Armour/arm_body_t6_heavy.asset"),
            Load<EquipmentDefinition>("Armour/arm_face_fire_t6.asset"),
            Load<EquipmentDefinition>("Armour/arm_ears_t6.asset"),
            Load<EquipmentDefinition>("Backpacks/bag_t6_haul.asset")
        });
    }

    /// <summary>무기 6종 전부. 티어별 체감 차이를 한 번에 비교한다.</summary>
    [MenuItem(Menu + "무기 6종 전부")]
    public static void GiveAllWeapons()
    {
        if (!RequirePlayMode()) return;

        Give(LoadFolder("Weapons").OrderBy(w => w.Tier));
    }

    /// <summary>각인 24종 전부. 순증 금지와 대가가 실제로 체감되는지 본다.</summary>
    [MenuItem(Menu + "각인 24종 전부")]
    public static void GiveAllImprints()
    {
        if (!RequirePlayMode()) return;

        Give(LoadFolder("Imprints").OrderBy(i => i.Id));
    }

    /// <summary>대표 각인 4종만. 가방을 덜 먹는다.</summary>
    [MenuItem(Menu + "각인 — 진격Ⅱ · 중장Ⅲ · 경량Ⅲ · 정밀Ⅲ")]
    public static void GiveKeyImprints()
    {
        if (!RequirePlayMode()) return;

        Give(new ItemDefinition[]
        {
            Load<EquipmentDefinition>("Imprints/imp_charge_t2.asset"),
            Load<EquipmentDefinition>("Imprints/imp_bulwark_t3.asset"),
            Load<EquipmentDefinition>("Imprints/imp_feather_t3.asset"),
            Load<EquipmentDefinition>("Imprints/imp_precision_t3.asset")
        });
    }

    /// <summary>가방을 전리품으로 채운다. 과중량 3단계를 걸어 본다.</summary>
    [MenuItem(Menu + "가방 채우기 — 과중량 유발")]
    public static void FillBag()
    {
        if (!RequirePlayMode()) return;

        var scrap = AssetDatabase
            .LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Loot/scrap_metal.asset");

        if (scrap == null)
        {
            Debug.LogError("[Playtest] 고철 에셋이 없습니다. 「Blob/Loot/전리품 에셋 생성」을 실행하십시오.");
            return;
        }

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        // 무게 상한의 1.6배까지 채운다. 「움직일 수 없음」 단계까지 간다.
        float target = bag.WeightLimit * 1.6f;
        int guard = 0;

        while (bag.TotalWeight < target && guard++ < 500)
        {
            if (bag.TryAdd(scrap, 20) <= 0)
                break;
        }

        Report($"{bag.TotalWeight:0.0}/{bag.WeightLimit:0.0}kg — {bag.Encumbrance}");
    }

    // ── 젬 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 젬은 적 드랍이 유일한 경로다. 그래서 8번(젬이 효과를 내는가)을
    /// 확인하려면 원하는 젬이 떨어질 때까지 기다려야 했다. 직접 준다.
    /// </summary>
    private static void GiveGems(System.Func<SkillDefinition, bool> filter, string label)
    {
        List<ItemDefinition> gems = LoadFolder("Gems")
            .Where(g => g.Skill != null && filter(g.Skill))
            .OrderBy(g => g.Skill.RequiredLevel)
            .ThenBy(g => g.Id)
            .ToList();

        if (gems.Count == 0)
        {
            Report($"{label} 젬 에셋이 없습니다. 「Blob/Skill/젬 에셋 생성」을 먼저 실행하십시오.");
            return;
        }

        Give(gems);
    }

    [MenuItem(Menu + "젬 — Core 전부")]
    public static void GiveCoreGems()
    {
        if (!RequirePlayMode()) return;

        GiveGems(s => s.Category == SkillCategory.Core, "Core");
    }

    [MenuItem(Menu + "젬 — Support 전부")]
    public static void GiveSupportGems()
    {
        if (!RequirePlayMode()) return;

        GiveGems(s => s.Category == SkillCategory.Support, "Support");
    }

    /// <summary>체크리스트 8번의 최소 구성. 가방을 덜 먹는다.</summary>
    [MenuItem(Menu + "젬 — 서리 Core · 깊은 상처 · 원거리 사격")]
    public static void GiveCheckListGems()
    {
        if (!RequirePlayMode()) return;

        Give(new ItemDefinition[]
        {
            Load<ItemDefinition>("Gems/gem_core_frost.asset"),
            Load<ItemDefinition>("Gems/gem_core_laceration.asset"),
            Load<ItemDefinition>("Gems/gem_sup_deep_cuts.asset"),
            Load<ItemDefinition>("Gems/gem_sup_far_shot.asset")
        });
    }

    // ── 상태 확인 ─────────────────────────────────────────────────────

    /// <summary>현재 사격 성능·방어·이동 배율을 한 줄로 찍는다.</summary>
    [MenuItem(Menu + "현재 능력치 출력")]
    public static void DumpStats()
    {
        if (!RequirePlayMode()) return;

        var loadout = Object.FindAnyObjectByType<PlayerLoadout>();

        if (loadout == null)
        {
            Debug.LogError("[Playtest] PlayerLoadout을 찾지 못했습니다. 씬에 플레이어가 있습니까?");
            return;
        }

        loadout.Refresh();

        LoadoutSnapshot s = loadout.Current;

        string line =
            $"피해 {s.Weapon.Damage:0.#} / 간격 {s.Weapon.FireInterval:0.###}s "
            + $"/ 사거리 {s.Weapon.EffectiveRange:0.#}m / 관통 {s.Weapon.ArmourPenetration}\n"
            + $"치명타 {s.Weapon.CriticalChance:P0} × {s.Weapon.CriticalMultiplier:0.##} "
            + $"/ 기대 DPS {s.Weapon.ExpectedDps:0.#}\n"
            + $"방어도 머리 {s.Defence.headArmour:0.##} · 몸통 {s.Defence.bodyArmour:0.##} "
            + $"/ 최대 체력 {s.MaxHealth}\n"
            + $"이동 ×{s.MoveScale:0.##} / 대시 거리 ×{s.DashDistanceScale:0.##} "
            + $"· 쿨 ×{s.DashCooldownScale:0.##} / 무게 {s.Encumbrance}";

        Debug.Log("[Playtest] " + line);

        InventoryScreenUI.ShowBagWithMessage(
            $"피해 {s.Weapon.Damage:0.#} · 간격 {s.Weapon.FireInterval:0.###}s "
            + $"· 사거리 {s.Weapon.EffectiveRange:0.#}m · 관통 {s.Weapon.ArmourPenetration} "
            + $"· 치명타 {s.Weapon.CriticalChance:P0} · 방어도 {s.Defence.bodyArmour:0.##} "
            + $"· 이동 ×{s.MoveScale:0.##}");
    }

    /// <summary>가장 가까운 적에게 상태이상을 건다. 임계 전이를 눈으로 확인한다.</summary>
    [MenuItem(Menu + "가까운 적에게 냉각 6중첩 (→ 동결)")]
    public static void ChillNearestEnemy() => StackOnNearest(StatusEffectType.Chill, 6);

    [MenuItem(Menu + "가까운 적에게 감전 6중첩 (→ 마비)")]
    public static void ShockNearestEnemy() => StackOnNearest(StatusEffectType.Shock, 6);

    [MenuItem(Menu + "가까운 적에게 중독 10중첩 (→ 부식)")]
    public static void PoisonNearestEnemy() => StackOnNearest(StatusEffectType.Poison, 10);

    private static void StackOnNearest(StatusEffectType type, int times)
    {
        if (!RequirePlayMode()) return;

        var player = Object.FindAnyObjectByType<BlobController>();

        if (player == null)
        {
            Debug.LogError("[Playtest] 플레이어를 찾지 못했습니다.");
            return;
        }

        Health target = Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude)
            .Where(h => h.Team == Team.Enemy && !h.IsDead)
            .OrderBy(h => (h.transform.position - player.transform.position).sqrMagnitude)
            .FirstOrDefault();

        if (target == null)
        {
            Debug.LogError("[Playtest] 살아 있는 적이 없습니다.");
            return;
        }

        for (int i = 0; i < times; i++)
            target.ApplyStatus(type, 10f);

        StatusEffectType threshold = StatusEffectTable.ThresholdOf(type);

        Debug.Log($"[Playtest] {target.name} — {type} {times}회 부여. "
                  + $"원본 중첩 {target.Status.StacksOf(type)} / "
                  + $"{threshold} 걸림: {target.Status.Has(threshold)} / "
                  + $"행동 불능: {target.Status.IsIncapacitated}");
    }
}
