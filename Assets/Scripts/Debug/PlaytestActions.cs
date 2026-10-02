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
        "검증 카탈로그가 없습니다. 에디터에서 「Dokkaebi/Playtest/검증 카탈로그 생성」을 실행한 뒤 다시 빌드하십시오.";

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

    /// <summary>파밍 상태로 되돌린다.</summary>
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
    /// 레벨을 한 칸 올린다. 【소켓이 열리는 것을 보려고 쓴다.】
    ///
    /// Level을 직접 밀어 넣지 않고 모자란 경험치를 그대로 채운다.
    /// 실제 레벨업 경로(AddXP → EnqueueLevelUp → SocketUnlockTable)를 똑같이 타야
    /// 「검증에서는 열렸는데 게임에서는 안 열린다」가 생기지 않는다.
    /// </summary>
    public static string RaiseLevel()
    {
        PlayerStats stats = PlayerStats.EnsureInstance();

        int before = stats.Level;

        stats.AddXP(stats.RequiredXP);

        string opened = SocketUnlockTable.DescribeUnlock(stats.Level);

        // 소켓과 패시브가 같은 레벨에서 열린다 (결정 2-33).
        string message = $"Lv.{before} → Lv.{stats.Level} · 패시브 요구 레벨도 이 값이다";

        return string.IsNullOrEmpty(opened) ? message : $"{message} — {opened} 개방";
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

    /// <summary>
    /// 소모품을 한 벌 지급한다. 회복 · 해제 · 음료와 음식이 전부 들어온다.
    ///
    /// 【쓸 수 있는 것만 카탈로그에 들어 있다.】 강화·저항은 담을 축이
    /// 아직 없어 효과가 비어 있고, 그런 것은 사이드 메뉴에 「사용」 줄이
    /// 뜨지 않으므로 지급해도 확인할 것이 없다.
    /// </summary>
    public static string GiveConsumables()
    {
        if (Catalog == null)
            return NoCatalog;

        IReadOnlyList<ItemDefinition> items = Catalog.Consumables;

        if (items == null || items.Count == 0)
            return "소모품이 카탈로그에 없습니다.\n"
                   + "「Dokkaebi/Items/소모품 에셋 생성」 다음에 "
                   + "「Dokkaebi/Playtest/검증 카탈로그 생성」을 실행하십시오.";

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        int given = 0;

        foreach (ItemDefinition item in items)
        {
            if (item == null)
                continue;

            // 겹치는 것은 두 개씩. 쓰고 나서 개수가 줄어드는지 봐야 한다.
            given += bag.TryAdd(item, item.StackMax > 1 ? 2 : 1);
        }

        PlayerInventory.Instance.RefreshCapacity();

        return $"소모품 {given}개 지급 ({items.Count}종)\n"
               + $"({bag.UsedSlots}/{bag.SlotCapacity}칸, {bag.TotalWeight:0.0}/{bag.WeightLimit:0.0}kg)\n"
               + "가방에서 칸을 눌러 「사용」 줄을 확인하십시오 — "
               + "체력·수분이 가득하면 그 줄은 뜨지 않습니다.";
    }

    // ── 세이브 (8-F) ──────────────────────────────────────────────────
    //
    // 【게임에서는 파밍 경계(사망)에서만 저장한다.】 덕코프와 같다.
    // 아래 「지금 저장」은 그 규칙을 일부러 어기는 검증용 버튼이다 —
    // 죽지 않고 저장·불러오기를 확인하려면 필요하다.

    /// <summary>지금 상태를 저장한다. 게임 규칙상으로는 파밍 중에 할 수 없는 일이다.</summary>
    public static string SaveNow()
    {
        if (!SaveManager.Commit("디버그"))
            return "저장하지 않았습니다 — 더 새 판의 세이브를 지키는 중입니다.";

        SaveData data = SaveManager.Capture();

        return $"저장했습니다.\nLv.{data.level} · 골드 {data.gold} · "
               + $"패시브 {data.learnedPassives.Count} · 도감 {data.codex.Count}\n"
               + "게임에서는 사망할 때만 저장됩니다 — 파밍 중 종료는 롤백입니다.";
    }

    /// <summary>디스크에서 다시 읽어 적용한다.</summary>
    public static string LoadNow()
    {
        SaveLoadResult result = SaveManager.Load();

        switch (result)
        {
            case SaveLoadResult.None:    return "세이브가 없습니다.";
            case SaveLoadResult.Main:    return "불러왔습니다.";
            case SaveLoadResult.Backup:  return "본 파일이 깨져 백업에서 불러왔습니다.";
            case SaveLoadResult.Corrupt: return "세이브와 백업이 모두 깨졌습니다. 옆으로 옮겨 두었습니다.";
            case SaveLoadResult.TooNew:  return "더 새 판의 세이브입니다. 읽지 않았습니다.";
            default:                     return result.ToString();
        }
    }

    /// <summary>세이브를 지운다. 【이 실행의 상태는 그대로다】 — 다음 실행부터 처음이다.</summary>
    public static string WipeSave()
    {
        SaveManager.Wipe();

        return "세이브와 백업을 지웠습니다.\n"
               + "지금 화면의 레벨·골드는 그대로이고, 다음 실행부터 처음 상태로 시작합니다.";
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
            $"Lv{build.Level} · 장착 {build.EquippedCount}개"
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
        var player = Object.FindAnyObjectByType<DokkaebiController>();

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

    // ── 내 상태이상 ───────────────────────────────────────────────────
    //
    // 【위의 StackOnNearest는 적에게 건다.】 화면 좌상단의 상태이상 줄은
    // 【내】 상태(DokkaebiController의 Health.Status)를 읽으므로, 적에게 걸어서는
    // 그 줄이 뜨지 않는다. 줄 자체를 확인하려면 나에게 걸어야 한다.

    /// <summary>플레이어의 Health. 없으면 null.</summary>
    private static Health SelfHealth()
    {
        var player = Object.FindAnyObjectByType<DokkaebiController>();

        return player != null ? player.GetComponent<Health>() : null;
    }

    /// <summary>
    /// 표시를 확인할 때 쓰는 기준 피해.
    ///
    /// 【1로 둔다.】 처음에는 10으로 뒀는데, 점화 0.25 · 출혈 0.20 ·
    /// 중독 0.05의 계수가 겹쳐 여섯 종이면 20~30 피해가 들어갔다.
    /// 두세 번 누르면 죽었고, 죽으면 Health.ApplyStatus가 IsDead에서
    /// 조용히 돌아가 줄이 하나도 뜨지 않았다. 게다가 사망은 timeScale을
    /// 0으로 만들어 화면이 통째로 멈춘다 — 표시를 보려고 누른 버튼이
    /// 표시를 못 보게 만든 셈이다.
    ///
    /// 여기서 확인하려는 것은 【줄이 뜨는가】이지 피해량이 아니다.
    /// 피해량 검증은 EditMode의 StatusEffectStateTests가 이미 한다.
    /// </summary>
    private const float TestDamage = 1f;

    /// <summary>걸기 전 상태 점검. 죽어 있으면 이유를 돌려준다.</summary>
    private static string PrepareSelf(Health self)
    {
        if (self.IsDead)
            return "플레이어가 죽어 있습니다 — 「체력 회복」을 먼저 누르십시오.\n"
                   + "사망하면 timeScale이 0이 되어 화면 전체가 멈춥니다.";

        if (self.Current < self.Max)
            self.Heal(self.Max - self.Current);

        return null;
    }

    /// <summary>
    /// 체력을 가득 채운다. 【죽어 있으면 되살린다.】
    ///
    /// HealthPool.Heal은 IsDead면 0을 돌려준다 — 죽은 대상이 회복으로
    /// 살아나면 「죽음」이 규칙이 아니게 되므로 맞는 설계다. 그래서
    /// 죽은 상태에서는 회복이 아니라 스폰과 같은 경로로 되돌린다.
    ///
    /// 이 버튼이 필요한 이유 — 사망은 GameManager를 GameOver로 보내고
    /// Time.timeScale을 0으로 만드는데, GameState.GameOver를 듣는 화면이
    /// 아직 하나도 없다. 멈춘 채로 아무것도 뜨지 않아 Play Mode를 껐다
    /// 켜는 것 말고는 나올 길이 없었다. (docs/Dokkaebi_Audit.md A12)
    ///
    /// 【되돌려 주지 않는 것】 가방은 죽는 순간 DropOnDeath로 이미 떨어졌고
    /// 레벨과 소켓도 SkillManager.ResetRun으로 초기화되었다.
    /// 규칙대로 사라진 것이므로 여기서 되살리지 않는다.
    /// </summary>
    public static string HealSelf()
    {
        Health self = SelfHealth();

        if (self == null)
            return "플레이어를 찾지 못했습니다.";

        if (!self.IsDead)
        {
            int healed = self.Heal(self.Max - self.Current);

            return $"체력 {self.Current}/{self.Max} (+{healed})";
        }

        // 체력 가득 · 상태이상 해제 · 무적 초기화를 한 번에 한다.
        self.OnSpawned();

        // 수분·에너지가 0이면 살려도 허기 피해로 곧 다시 죽는다.
        if (PlayerSurvival.HasInstance)
            PlayerSurvival.Instance.Refill();

        // timeScale 0 → 1. 이것을 안 하면 살아나도 화면이 멈춘 채다.
        if (GameManager.HasInstance)
            GameManager.Instance.Resume();

        return $"죽어 있어 되살렸습니다 — 체력 {self.Current}/{self.Max} · 시간이 다시 흐릅니다.\n"
               + "가방과 레벨은 죽을 때 규칙대로 사라졌으므로 돌아오지 않습니다.";
    }

    // ── 적 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 적 생성을 다시 켠다.
    ///
    /// EnemySpawner는 Update에서 스스로 돌지만 두 가지에 막힌다 —
    /// 컴포넌트가 꺼져 있거나(「적 생성 정지」를 눌렀을 때),
    /// GameManager가 Playing이 아닐 때(사망·일시정지)다. 둘 다 푼다.
    /// </summary>
    public static string StartSpawning()
    {
        var spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include);

        if (spawners.Length == 0)
            return "씬에 EnemySpawner가 없습니다.";

        foreach (EnemySpawner spawner in spawners)
            spawner.enabled = true;

        if (GameManager.HasInstance && !GameManager.Instance.IsPlaying)
            GameManager.Instance.Resume();

        int active = EnemyManager.HasInstance ? EnemyManager.Instance.ActiveCount : 0;

        return $"적 생성을 켰습니다 (스포너 {spawners.Length}개) · 현재 적 {active}마리";
    }

    /// <summary>
    /// 적 생성을 멈춘다. 이미 나와 있는 적은 그대로 둔다.
    ///
    /// 【「모든 적 제거」와 짝이다.】 생성이 계속 도는 동안에는 제거를
    /// 눌러도 몇 초 만에 다시 차오른다. 화면을 비워 두고 UI를 보려면
    /// 멈추는 쪽이 먼저 있어야 한다.
    /// </summary>
    public static string StopSpawning()
    {
        var spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include);

        if (spawners.Length == 0)
            return "씬에 EnemySpawner가 없습니다.";

        foreach (EnemySpawner spawner in spawners)
            spawner.enabled = false;

        int active = EnemyManager.HasInstance ? EnemyManager.Instance.ActiveCount : 0;

        return $"적 생성을 멈췄습니다 (스포너 {spawners.Length}개)\n"
               + $"이미 나와 있는 {active}마리는 그대로입니다 — 「모든 적 제거」로 치웁니다.";
    }

    /// <summary>
    /// 지금 나와 있는 적을 전부 풀로 되돌린다.
    ///
    /// 【죽이지 않고 되돌린다.】 처치로 처리하면 경험치·전리품·시체가
    /// 쏟아져 화면을 비우려던 목적과 반대가 된다. EnemyManager가 멀어진
    /// 적을 정리할 때 쓰는 것과 같은 경로다.
    /// </summary>
    public static string KillAllEnemies()
    {
        if (!EnemyManager.HasInstance)
            return "EnemyManager가 없습니다 — 아직 적이 한 번도 나오지 않았습니다.";

        var enemies = EnemyManager.Instance.ActiveEnemies;

        int removed = 0;

        // 뒤에서부터 도는 이유 — ReturnToPool이 목록에서 자기를 빼므로
        // 앞에서부터 돌면 인덱스가 밀려 절반만 지워진다.
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = enemies[i];

            if (enemy == null)
                continue;

            enemy.ReturnToPool();
            removed++;
        }

        return $"적 {removed}마리를 치웠습니다 · 남은 적 {EnemyManager.Instance.ActiveCount}마리\n"
               + "생성이 켜져 있으면 곧 다시 나옵니다 — 「적 생성 정지」와 같이 쓰십시오.";
    }

    /// <summary>나에게 한 종류를 여러 번 건다. 중첩 → 전이를 볼 때 쓴다.</summary>
    public static string StackOnSelf(StatusEffectType type, int times)
    {
        Health self = SelfHealth();

        if (self == null)
            return "플레이어를 찾지 못했습니다.";

        string blocked = PrepareSelf(self);

        if (blocked != null)
            return blocked;

        // 면역이면 아무 일도 일어나지 않는다. 그 사실을 먼저 알려야
        // 「UI가 고장 났나」를 의심하며 시간을 버리지 않는다.
        if (self.IsImmuneTo(type))
            return $"{StatusEffectNames.Of(type)}에 면역입니다 — 장비를 벗고 다시 하십시오.";

        for (int i = 0; i < times; i++)
            self.ApplyStatus(type, TestDamage);

        StatusEffectType threshold = StatusEffectTable.ThresholdOf(type);

        return $"내게 {StatusEffectNames.Of(type)} {times}회 · 체력 {self.Current}/{self.Max}\n"
               + $"중첩 {self.Status.StacksOf(type)} · "
               + $"{StatusEffectNames.Of(threshold)} {self.Status.Has(threshold)} · "
               + $"행동 불능 {self.Status.IsIncapacitated}\n"
               + "좌상단 게이지 아래에 줄이 떠야 합니다.";
    }

    /// <summary>
    /// 나에게 기본 상태이상 6종(점화 · 중독 · 감전 · 출혈 · 응집 · 냉각)을 한 번씩 건다.
    ///
    /// 【위험 상태 셋을 여기서 같이 걸지 않는 이유】
    /// StatusEffectState.Apply는 한계치 상태가 이미 걸려 있으면 원본을 쌓지
    /// 않는다 — 행동 불능 중에 게이지가 다시 차 무한 제압이 되는 것을 막는
    /// 장치다. 그래서 동결을 먼저 걸면 냉각이, 마비를 먼저 걸면 감전이,
    /// 부식을 먼저 걸면 중독이 통째로 걸리지 않는다. 아홉을 한 번에 거는
    /// 버튼은 【누를 때마다 다른 결과가 나오는】 도구였다. 둘로 나눈다.
    /// </summary>
    public static string StackAllOnSelf()
    {
        Health self = SelfHealth();

        if (self == null)
            return "플레이어를 찾지 못했습니다.";

        string blocked = PrepareSelf(self);

        if (blocked != null)
            return blocked;

        int applied = 0;
        int immune = 0;

        foreach (StatusEffectType type in BaseAilments)
        {
            if (self.IsImmuneTo(type))
            {
                immune++;
                continue;
            }

            self.ApplyStatus(type, TestDamage);
            applied++;
        }

        return $"내게 기본 상태이상 {applied}종 · 체력 {self.Current}/{self.Max}"
               + (immune > 0 ? $" (면역 {immune}종 제외)" : string.Empty)
               + "\n좌상단 게이지 아래에 줄이 쌓이고, 남은 시간이 짧은 순으로\n"
               + "정렬되어야 합니다. 위험 상태는 아래 버튼으로 따로 거십시오.";
    }

    /// <summary>한계치 상태로만 걸리는 셋. 전이를 거치지 않고 직접 건다.</summary>
    public static string CriticalOnSelf()
    {
        Health self = SelfHealth();

        if (self == null)
            return "플레이어를 찾지 못했습니다.";

        string blocked = PrepareSelf(self);

        if (blocked != null)
            return blocked;

        foreach (StatusEffectType type in CriticalAilments)
            self.ApplyStatus(type, TestDamage);

        return $"내게 동결 · 마비 · 부식 · 체력 {self.Current}/{self.Max}\n"
               + "붉은 바탕의 띠로 맨 위에 와야 합니다.\n"
               + "동결·마비로 움직일 수 없습니다 — 「내 상태이상 해제」로 푸십시오.";
    }

    /// <summary>내게 걸린 상태이상을 전부 지운다.</summary>
    public static string ClearStatusOnSelf()
    {
        Health self = SelfHealth();

        if (self == null)
            return "플레이어를 찾지 못했습니다.";

        self.Status.ClearAll();

        return "내 상태이상을 전부 해제했습니다. 좌상단의 줄이 사라져야 합니다.";
    }

    /// <summary>전이 없이 스스로 걸리는 상태이상. 위험 상태 셋은 뺀다.</summary>
    private static readonly StatusEffectType[] BaseAilments =
    {
        StatusEffectType.Ignite,
        StatusEffectType.Poison,
        StatusEffectType.Shock,
        StatusEffectType.Bleed,
        StatusEffectType.Congeal,
        StatusEffectType.Chill
    };

    /// <summary>위험 상태 셋. 원래는 한계치 전이로만 걸린다.</summary>
    private static readonly StatusEffectType[] CriticalAilments =
    {
        StatusEffectType.Freeze,
        StatusEffectType.Paralyze,
        StatusEffectType.Corrode
    };

    // ── 창고 · 잡화 상점 (8-I) ─────────────────────────────────────────
    //
    // 【벙커가 아직 없다.】 그래서 여기서 연다. 파밍 도중에 창고를 쓸 수 있는 것은
    // 사실상 철수이므로 검증용이다 — 벙커가 생기면 보관고 · 잡화 상점 건물이 연다.

    public static string OpenStash()
    {
        ExchangeWindowUI.EnsureInstance().OpenStash();
        Inventory stash = PlayerInventory.EnsureInstance().Stash;
        return $"창고를 열었습니다. {stash.UsedSlots}/{stash.SlotCapacity}칸.\n"
               + "가방 칸을 누르면 「창고에 넣기」가 맨 위에 뜹니다.";
    }

    public static string OpenShop()
    {
        ExchangeWindowUI.EnsureInstance().OpenShop(ShopKind.General);
        return $"{ShopTable.GeneralStoreName}을 열었습니다. 골드 {PassiveManager.EnsureInstance().Gold:N0}.\n"
               + "가방 칸을 누르면 「판매」가, 상점 칸을 누르면 「구매」가 뜹니다.";
    }

    public static string GiveGold()
    {
        PassiveManager.EnsureInstance().AddGold(5000);
        InventoryScreenUI.RefreshIfOpen();
        ExchangeWindowUI.RefreshIfOpen();
        return $"골드 +5,000 → {PassiveManager.Instance.Gold:N0}";
    }

    public static string RestockShop()
    {
        ShopManager.RestockAfterRun();
        ExchangeWindowUI.RefreshIfOpen();
        return "잡화 상점 재고를 채웠습니다. (원래는 파밍이 끝날 때 찬다)";
    }

    // ── 벙커 (8-J) ───────────────────────────────────────────────────

    /// <summary>철수 지점이 9단계라 여기서 대신 철수한다. 가방·장비를 들고 벙커로 간다.</summary>
    public static string ExtractNow()
    {
        if (SceneFlow.InBunker)
            return "벙커에서는 철수할 수 없습니다.";

        if (!SceneFlow.HasBunker)
            return "빌드 설정에 벙커 씬이 없습니다. 「Dokkaebi/Bunker/벙커 씬 생성」을 실행하십시오.";

        SceneFlow.Extract();
        return "철수합니다. 가방과 장비를 들고 벙커로 돌아갑니다.";
    }

    // ── 건설 (8-K) ───────────────────────────────────────────────────

    /// <summary>건물 넷을 다 지을 만큼의 골드 · 재료를 창고에 넣는다.</summary>
    public static string GiveBuildingMaterials()
    {
        ItemCatalog catalog = ItemCatalog.Load();

        if (catalog == null)
            return "Resources/ItemCatalog가 없습니다.";

        Inventory stash = PlayerInventory.EnsureInstance().Stash;
        int added = 0;

        foreach ((string id, int count) in new[] { ("scrap_metal", 20), ("cell_battery", 6), ("wire_bundle", 3) })
        {
            ItemDefinition definition = catalog.Find(id);

            if (definition != null)
                added += stash.TryAdd(definition, count);
        }

        PassiveManager.EnsureInstance().AddGold(400);

        return $"창고에 건설 재료 {added}개 · 400골드를 넣었습니다. 설계도 테이블(「건설」)에서 지으십시오.";
    }

    public static string OpenBuildingScreen()
    {
        if (!SceneFlow.InBunker)
            return "벙커에서만 지을 수 있습니다.";

        BuildingScreenUI.Open();
        return "건물 목록을 열었습니다.";
    }

    /// <summary>
    /// 【검증용】 재료를 주고 건물 넷을 전부 지어 정해진 자리에 놓는다.
    /// 화면을 누르지 않고 「짓기 → 배치 → 상점 열기」 길을 한 번에 지나가 본다.
    /// </summary>
    public static string BuildAllForTest()
    {
        if (!SceneFlow.InBunker)
            return "벙커에서만 지을 수 있습니다.";

        GiveBuildingMaterials();

        var spots = new System.Collections.Generic.Dictionary<string, BuildingPose>
        {
            [BuildingTable.Workbench]    = new BuildingPose(-7f, -1f, 0),
            [BuildingTable.GeneralStore] = new BuildingPose(7f, 3f, 0),
            [BuildingTable.WeaponShop]   = new BuildingPose(7f, 0f, 0),
            [BuildingTable.ArmourShop]   = new BuildingPose(7f, -3f, 0),
        };

        var log = new System.Text.StringBuilder();

        foreach (BuildingDefinition definition in BuildingTable.All)
        {
            BuildError error = BuildingManager.State.Owns(definition.Id)
                ? BuildError.None
                : BuildingManager.Build(definition);

            bool placed = error == BuildError.None
                          && spots.TryGetValue(definition.Id, out BuildingPose pose)
                          && BuildingManager.Place(definition.Id, pose);

            log.AppendLine($"{definition.Name} — {(error == BuildError.None ? "지음" : BuildingState.Explain(error))}"
                           + (placed ? " · 놓음" : string.Empty));
        }

        SaveManager.Commit("건설 (검증)");

        return log.ToString().TrimEnd();
    }
}
