using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 검증 동작. **런타임 코드다** — 에디터와 개발 빌드 양쪽에서 같은 것이 돈다.
///
/// 【왜 런타임으로 옮겼나】
/// 원래는 Editor의 MenuItem이었다. MenuItem은 빌드에 컴파일되지 않으므로
/// 실제 기기(iOS) 빌드에서는 지급·출력 수단이 아예 없었다.
/// 모바일 게임을 실제 기기에서 확인하는 것은 당연한 일이고,
/// 검증 도구가 거기서 동작하지 않으면 도구가 아니다.
///
/// 결과를 문자열로 돌려주는 이유 — 부르는 쪽이 콘솔에 찍든 화면에 띄우든
/// 고를 수 있어야 한다. 실제 기기에는 Console 창이 없다.
/// </summary>
public static class PlaytestActions
{
    private static PlaytestCatalog cached;

    private static PlaytestCatalog Catalog => cached != null ? cached : cached = PlaytestCatalog.Load();

    private const string NoCatalog =
        "검증 카탈로그가 없습니다. 에디터에서 「Blob/Playtest/검증 카탈로그 생성」을 실행한 뒤 다시 빌드하십시오.";

    // ── 지급 ──────────────────────────────────────────────────────────

    private static string Give(IReadOnlyList<ItemDefinition> items, string label)
    {
        if (items == null || items.Count == 0)
            return $"{label} 목록이 비어 있습니다. 검증 카탈로그를 다시 생성하십시오.";

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        int added = 0;
        int rejected = 0;

        foreach (ItemDefinition item in items)
        {
            if (item == null)
                continue;

            if (bag.TryAdd(item) > 0)
                added++;
            else
                rejected++;
        }

        PlayerInventory.Instance.RefreshCapacity();

        string message = $"{label} {added}개 지급 "
                         + $"({bag.UsedSlots}/{bag.SlotCapacity}칸, {bag.TotalWeight:0.0}/{bag.WeightLimit:0.0}kg)";

        // 조용히 삼키지 않는다. 가방이 차서 못 들어간 것이 「지급이 안 된다」로 보인다.
        if (rejected > 0)
            message += $" — 자리가 없어 {rejected}개는 넣지 못했습니다.";

        return message;
    }

    public static string GiveStarterKit()
        => Catalog == null ? NoCatalog : Give(Catalog.StarterKit, "티어1 한 벌");

    public static string GiveEndgameKit()
        => Catalog == null ? NoCatalog : Give(Catalog.EndgameKit, "티어6 한 벌");

    public static string GiveAllWeapons()
        => Catalog == null ? NoCatalog : Give(Catalog.Weapons, "무기");

    public static string GiveAllImprints()
        => Catalog == null ? NoCatalog : Give(Catalog.Imprints, "각인");

    public static string GiveKeyImprints()
        => Catalog == null ? NoCatalog : Give(Catalog.KeyImprints, "대표 각인");

    /// <summary>
    /// 젬 네 범주를 한 번에. 【젬은 이제 칸을 먹지 않는다(2-31).】
    /// 나눠 줄 이유가 사라졌다 — 다 받아도 가방이 그대로다.
    /// </summary>
    public static string GiveAllGems()
    {
        if (Catalog == null)
            return NoCatalog;

        var all = new List<ItemDefinition>(
            Catalog.CoreGems.Count + Catalog.SupportGems.Count
            + Catalog.MetaGems.Count + Catalog.HeraldGems.Count);

        all.AddRange(Catalog.CoreGems);
        all.AddRange(Catalog.SupportGems);
        all.AddRange(Catalog.MetaGems);
        all.AddRange(Catalog.HeraldGems);

        return Give(all, "젬");
    }

    // ── 생존 ──────────────────────────────────────────────────────────

    /// <summary>수분·에너지를 절반만 남긴다. 게이지가 실제로 줄어드는지 본다.</summary>
    public static string HalveSurvival()
    {
        PlayerSurvival survival = PlayerSurvival.EnsureInstance();

        if (survival == null)
            return "플레이어를 찾지 못했습니다. 레이드 중에만 됩니다.";

        SurvivalState state = survival.State;

        survival.Drain(state.Water * 0.5f, state.Energy * 0.5f);

        return $"수분 {state.Water:0} / 에너지 {state.Energy:0}";
    }

    /// <summary>
    /// 수분·에너지를 0으로 만든다. 【탈수·허기 페널티를 바로 본다.】
    /// 16분을 기다려야 확인되는 것을 기다리지 않게 한다.
    /// </summary>
    public static string EmptySurvival()
    {
        PlayerSurvival survival = PlayerSurvival.EnsureInstance();

        if (survival == null)
            return "플레이어를 찾지 못했습니다. 레이드 중에만 됩니다.";

        survival.Drain(SurvivalTable.MaxWater, SurvivalTable.MaxEnergy);

        SurvivalState state = survival.State;

        return $"탈수·허기 적용 — 이동 ×{state.MoveMultiplier:0.00} · "
               + $"음식 ×{state.EnergyRestoreMultiplier:0.00} · 허기 {state.StarvingStacks}중첩";
    }

    /// <summary>출격 상태로 되돌린다.</summary>
    public static string RefillSurvival()
    {
        PlayerSurvival survival = PlayerSurvival.EnsureInstance();

        if (survival == null)
            return "플레이어를 찾지 못했습니다. 레이드 중에만 됩니다.";

        survival.Refill();

        return $"수분·에너지 가득 ({SurvivalTable.MaxWater:0} / {SurvivalTable.MaxEnergy:0})";
    }

    // ── 레벨 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 각성 레벨을 한 칸 올린다. 【소켓이 열리는 것을 보려고 쓴다.】
    ///
    /// Level을 직접 밀어 넣지 않고 모자란 경험치를 그대로 채운다.
    /// 실제 레벨업 경로(AddXP → EnqueueLevelUp → SocketUnlockTable)를 똑같이 타야
    /// 「검증에서는 열렸는데 게임에서는 안 열린다」가 생기지 않는다.
    /// </summary>
    public static string RaiseAwakeningLevel()
    {
        if (!PlayerStats.HasInstance)
            return "PlayerStats가 없습니다. 레이드 중에만 올릴 수 있습니다.";

        PlayerStats stats = PlayerStats.Instance;

        int before = stats.Level;

        stats.AddXP(stats.RequiredXP);

        string opened = SocketUnlockTable.DescribeUnlock(stats.Level);

        string message = $"각성 Lv.{before} → Lv.{stats.Level}";

        return string.IsNullOrEmpty(opened) ? message : $"{message} — {opened} 개방";
    }

    /// <summary>
    /// 계정 레벨을 한 칸 올린다. 패시브 해금 조건이 이것이다.
    /// 각성과 달리 런과 무관한 영구 축이라 그냥 올린다.
    /// </summary>
    public static string RaiseAccountLevel()
    {
        PassiveManager manager = PassiveManager.EnsureInstance();

        int before = manager.AccountLevel;

        manager.AccountLevel = before + 1;

        return $"계정 Lv.{before} → Lv.{manager.AccountLevel}";
    }

    /// <summary>무게 상한의 1.6배까지 채운다. 「움직일 수 없음」 단계까지 간다.</summary>
    public static string FillBag()
    {
        if (Catalog == null)
            return NoCatalog;

        ItemDefinition bulk = Catalog.BulkMaterial;

        if (bulk == null)
            return "과중량용 재료가 카탈로그에 없습니다.";

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        float target = bag.WeightLimit * 1.6f;
        int guard = 0;

        while (bag.TotalWeight < target && guard++ < 500)
        {
            if (bag.TryAdd(bulk, bulk.StackMax) <= 0)
                break;
        }

        PlayerInventory.Instance.RefreshCapacity();

        return $"{bag.TotalWeight:0.0}/{bag.WeightLimit:0.0}kg — {bag.Encumbrance} "
               + $"({bag.UsedSlots}/{bag.SlotCapacity}칸)";
    }

    /// <summary>
    /// 겹치는 재료를 여러 개씩 준다. 【개수 배지가 실제로 뜨는지】 확인하는 용도다.
    ///
    /// 왜 따로 만들었나 — 하나씩 주우면 전부 1개라 숫자가 뜰 일이 없다.
    /// 그래서 「개수가 표시되지 않는다」와 「겹치지 않는다」를 구분할 수 없었다.
    /// </summary>
    public static string GiveStackables()
    {
        if (Catalog == null)
            return NoCatalog;

        IReadOnlyList<ItemDefinition> items = Catalog.Stackables;

        if (items == null || items.Count == 0)
            return "겹치는 재료가 카탈로그에 없습니다. 검증 카탈로그를 다시 생성하십시오.";

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        var parts = new List<string>(items.Count);

        foreach (ItemDefinition item in items)
        {
            if (item == null)
                continue;

            // 스택 상한의 절반 + 1. 상한에 딱 맞추면 「두 칸으로 갈리는가」를 못 본다.
            int want = Mathf.Max(2, item.StackMax / 2 + 1);

            int added = bag.TryAdd(item, want);

            if (added > 0)
                parts.Add($"{item.DisplayName} {added}");
        }

        PlayerInventory.Instance.RefreshCapacity();

        return $"겹치는 재료 지급 — {string.Join(" · ", parts)}\n"
               + $"({bag.UsedSlots}/{bag.SlotCapacity}칸, {bag.TotalWeight:0.0}/{bag.WeightLimit:0.0}kg)";
    }

    /// <summary>가방을 통째로 비운다. 과중량 실험을 되돌린다.</summary>
    public static string ClearBag()
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        int slots = bag.UsedSlots;

        bag.Clear();

        PlayerInventory.Instance.RefreshCapacity();

        return $"가방 {slots}칸을 비웠습니다.";
    }

    // ── 상태 출력 ─────────────────────────────────────────────────────

    public static string DumpStats()
    {
        var loadout = Object.FindAnyObjectByType<PlayerLoadout>();

        if (loadout == null)
            return "PlayerLoadout을 찾지 못했습니다. 씬에 플레이어가 있습니까?";

        loadout.Refresh();

        LoadoutSnapshot s = loadout.Current;

        return $"피해 {s.Weapon.Damage:0.#} · 간격 {s.Weapon.FireInterval:0.###}s "
               + $"· 사거리 {s.Weapon.EffectiveRange:0.#}m · 관통 {s.Weapon.ArmourPenetration}\n"
               + $"치명타 {s.Weapon.CriticalChance:P0} ×{s.Weapon.CriticalMultiplier:0.##} "
               + $"· 기대 DPS {s.Weapon.ExpectedDps:0.#}\n"
               + $"방어도 머리 {s.Defence.headArmour:0.##} · 몸통 {s.Defence.bodyArmour:0.##} "
               + $"· 최대 체력 {s.MaxHealth}\n"
               + $"이동 ×{s.MoveScale:0.##} · 대시 ×{s.DashDistanceScale:0.##} · {s.Encumbrance}";
    }

    public static string DumpBuild()
    {
        SocketedBuild build = SkillManager.EnsureInstance().Build;
        WeaponModifiers m = build.GetModifiers();

        var lines = new List<string>(6)
        {
            $"각성 Lv{build.AwakeningLevel} · 장착 {build.EquippedCount}개"
        };

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            SkillDefinition core = build.GetCore(c);

            if (core == null)
                continue;

            var supports = new List<string>(SocketedBuild.SocketsPerCore);

            for (int i = 0; i < SocketedBuild.SocketsPerCore; i++)
            {
                SkillDefinition sup = build.GetSocket(c, i);

                if (sup != null)
                    supports.Add(sup.DisplayName);
            }

            StatusEffectType effective = build.EffectiveAilmentOf(c);
            StatusEffectType added = build.AddedAilmentOf(c);

            string ailment = effective == StatusEffectType.None ? "없음" : effective.ToString();

            if (added != StatusEffectType.None)
                ailment += $" + {added}";

            lines.Add($"Core{c + 1} {core.DisplayName} → {ailment}"
                      + (supports.Count > 0 ? $" / {string.Join(", ", supports)}" : string.Empty));
        }

        if (build.EquippedCount == 0)
            lines.Add("끼운 젬이 없습니다.");

        lines.Add($"탄 {m.TotalProjectiles}발 · 피해 {m.DamageIncrease:+0%;-0%;+0%} "
                  + $"· 위력 {m.AilmentPower:+0%;-0%;+0%} · 지속 ×{m.AilmentDurationMultiplier:0.##}");

        lines.Add($"탄이 싣는 상태이상: "
                  + (m.Ailments.Count == 0 ? "없음" : string.Join(", ", m.Ailments)));

        return string.Join("\n", lines);
    }

    // ── 상태이상 ──────────────────────────────────────────────────────

    public static string StackOnNearest(StatusEffectType type, int times)
    {
        var player = Object.FindAnyObjectByType<BlobController>();

        if (player == null)
            return "플레이어를 찾지 못했습니다.";

        Health target = null;
        float best = float.PositiveInfinity;

        foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude))
        {
            if (health.Team != Team.Enemy || health.IsDead)
                continue;

            float distance = Vector3.Distance(player.transform.position, health.transform.position);

            if (distance >= best)
                continue;

            best = distance;
            target = health;
        }

        if (target == null)
            return "살아 있는 적이 없습니다.";

        for (int i = 0; i < times; i++)
            target.ApplyStatus(type, 10f);

        StatusEffectType threshold = StatusEffectTable.ThresholdOf(type);

        return $"{target.name} — {type} {times}회\n"
               + $"{type} 중첩 {target.Status.StacksOf(type)} · "
               + $"{threshold} {target.Status.Has(threshold)} · "
               + $"행동 불능 {target.Status.IsIncapacitated}";
    }
}
