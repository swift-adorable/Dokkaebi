using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 검증 동작. **런타임 코드다** — 에디터와 개발 빌드 양쪽에서 같은 것이 돈다.
///
/// 【왜 런타임으로 옮겼나】
/// 원래는 Editor의 MenuItem이었다. MenuItem은 빌드에 컴파일되지 않으므로
/// 실기(iOS) 빌드에서는 지급·출력 수단이 아예 없었다.
/// 모바일 게임을 실기에서 확인하는 것은 당연한 일이고,
/// 검증 도구가 거기서 동작하지 않으면 도구가 아니다.
///
/// 결과를 문자열로 돌려주는 이유 — 부르는 쪽이 콘솔에 찍든 화면에 띄우든
/// 고를 수 있어야 한다. 실기에는 Console 창이 없다.
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

    public static string GiveCoreGems()
        => Catalog == null ? NoCatalog : Give(Catalog.CoreGems, "Core 젬");

    public static string GiveSupportGems()
        => Catalog == null ? NoCatalog : Give(Catalog.SupportGems, "Support 젬");

    public static string GiveChecklistGems()
        => Catalog == null ? NoCatalog : Give(Catalog.ChecklistGems, "검증용 젬");

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

        foreach (Health health in Object.FindObjectsByType<Health>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
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
