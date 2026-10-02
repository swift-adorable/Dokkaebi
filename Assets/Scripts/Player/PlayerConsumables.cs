using UnityEngine;

/// <summary>
/// 소모품을 실제로 쓰는 곳. 【계산은 ConsumableUse가 한다.】
/// 여기는 지금 상태를 모아 넘기고, 나온 결과를 적용하고, 한 개를 뺀다.
/// (docs/Dokkaebi_Consumable_System.md)
///
/// MonoBehaviour로 두지 않는 이유 — 쓰는 순간에만 필요한 일이라
/// 매 프레임 도는 것이 없다. 화면이 부르는 함수 하나면 충분하다.
/// </summary>
public static class PlayerConsumables
{
    /// <summary>
    /// 가방의 한 칸에서 하나를 쓴다. 결과 문장을 돌려준다.
    ///
    /// 【순서가 중요하다.】 값을 먼저 정하고(Evaluate) → 적용하고 →
    /// 마지막에 가방에서 뺀다. 빼는 것을 앞에 두면 중간에 실패했을 때
    /// 아이템만 사라진다.
    /// </summary>
    public static string Use(ItemStack stack)
    {
        if (stack == null || stack.Definition == null)
            return "쓸 물건이 없습니다.";

        ItemDefinition definition = stack.Definition;

        Health health = FindPlayerHealth();

        if (health == null)
            return "플레이어를 찾지 못했습니다.";

        ConsumableSubject subject = Snapshot(health, definition, stack.Durability);

        ConsumableOutcome outcome = ConsumableUse.Evaluate(definition, subject);

        if (!outcome.Ok)
            return ConsumableUse.Explain(outcome.Error);

        Apply(health, outcome);

        // 여기까지 와야 값을 치른다.
        string spent = Spend(stack, outcome);

        return Describe(definition, outcome) + spent;
    }

    /// <summary>
    /// 쓸 수 없는 이유. 쓸 수 있으면 null이다.
    ///
    /// 시전 시간이 있는 물건을 **시작하기 전에** 거르는 데 쓴다 —
    /// 3초를 서 있다가 「채울 것이 없습니다」를 보는 것은 벌이지 안내가 아니다.
    /// </summary>
    public static string Blocked(ItemStack stack)
    {
        if (stack?.Definition == null)
            return "쓸 물건이 없습니다.";

        Health health = FindPlayerHealth();

        if (health == null)
            return "플레이어를 찾지 못했습니다.";

        ConsumableOutcome outcome = ConsumableUse.Evaluate(
            stack.Definition, Snapshot(health, stack.Definition, stack.Durability));

        return outcome.Ok ? null : ConsumableUse.Explain(outcome.Error);
    }

    /// <summary>
    /// 쓰기 시작한다. 시전 시간이 있으면 ConsumableCaster가 시간을 잰다.
    /// 화면은 이 입구만 쓰면 된다 — 즉시인지 아닌지를 화면이 알 필요는 없다.
    /// </summary>
    public static void BeginUse(ItemStack stack, System.Action<string> onDone)
    {
        ConsumableCaster.EnsureInstance().Begin(stack, onDone);
    }

    /// <summary>
    /// 「사용」 줄을 띄울 물건인가.
    ///
    /// 【지금 효과가 있는지는 보지 않는다.】 체력이 가득하다고 줄을 감추면
    /// 있다가 없어지는 줄이 되어 고장인지 규칙인지 구분되지 않는다.
    /// 덕코프도 막지 않는다 — 대신 회복 아이템에 부수 효과와 내구도를 두어
    /// 「가득하면 쓸모없음」 자체가 거의 생기지 않게 만들었다. 우리도 같은 길로
    /// 간다. 눌렀는데 채울 것이 없으면 그 사실을 알리고 **아무것도 닳지 않는다.**
    /// </summary>
    public static bool CanUse(ItemDefinition definition)
    {
        return definition != null && definition.IsUsable;
    }

    private static ConsumableSubject Snapshot(Health health, ItemDefinition definition,
                                              int durability = 0)
    {
        ConsumableEffect effect = definition.Consumable;

        bool hasCureTarget = effect != null
                             && effect.Cures
                             && health.Status.Has(effect.Cure);

        // 생존 축은 아직 없을 수 있다 — 그때는 「가득」으로 보아
        // 음료·음식이 NothingToDo로 걸리게 둔다. 없는 축을 채울 수는 없다.
        if (!PlayerSurvival.HasInstance)
        {
            return new ConsumableSubject(health.Current, health.Max, health.IsDead,
                1f, 1f, 1f, 1f, hasCureTarget, durability);
        }

        SurvivalState survival = PlayerSurvival.Instance.State;

        return new ConsumableSubject(health.Current, health.Max, health.IsDead,
            survival.Water, survival.MaxWater,
            survival.Energy, survival.MaxEnergy,
            hasCureTarget, durability);
    }

    private static void Apply(Health health, in ConsumableOutcome outcome)
    {
        if (outcome.Heal > 0)
            health.Heal(outcome.Heal);

        if (outcome.Cure != StatusEffectType.None)
            health.Status.RemoveStacks(outcome.Cure, outcome.CureStacks);

        // sourceDamage는 도트 계산용이라 이로운 상태에는 쓰이지 않지만,
        // Health.ApplyStatus가 0 이하면 그대로 돌아가므로 1을 넘긴다.
        if (outcome.Grant != StatusEffectType.None)
            health.ApplyStatus(outcome.Grant, 1f);

        if (!PlayerSurvival.HasInstance)
            return;

        if (outcome.Water > 0f || outcome.Energy > 0f)
            PlayerSurvival.Instance.Restore(outcome.Water, outcome.Energy);

        // 【대가는 채운 뒤에 뺀다.】 먼저 빼면 상한에 걸려 채움이 줄어든다.
        if (outcome.WaterCost > 0f || outcome.EnergyCost > 0f)
            PlayerSurvival.Instance.Drain(outcome.WaterCost, outcome.EnergyCost);
    }

    /// <summary>
    /// 값을 치른다 — 내구도를 깎거나, 없으면 한 개를 뺀다.
    ///
    /// 【내구도가 있으면 아이템이 사라지지 않는다.】 환단은 여러 번
    /// 쓰는 물건이다. 다 쓰면 빈 통이 가방에 남는데, 그것도 맞다 —
    /// 버릴지 들고 나갈지가 무게 결정이 된다.
    /// </summary>
    private static string Spend(ItemStack stack, in ConsumableOutcome outcome)
    {
        if (outcome.UseCost <= 0)
        {
            PlayerInventory.EnsureInstance().Bag.Remove(stack.Definition, 1);

            return string.Empty;
        }

        stack.Damage(outcome.UseCost);

        return $"\n남은 충전 {stack.Durability}/{stack.MaxDurability}"
               + (stack.Durability < outcome.UseCost ? " — 다음엔 쓸 수 없습니다." : string.Empty);
    }

    /// <summary>무엇이 들어갔는지 한 줄로. 숫자가 안 보이면 쓴 것 같지 않다.</summary>
    private static string Describe(ItemDefinition definition, in ConsumableOutcome outcome)
    {
        var parts = new System.Collections.Generic.List<string>(4);

        if (outcome.Heal > 0)
            parts.Add($"체력 +{outcome.Heal}");

        if (outcome.Water > 0f)
            parts.Add($"수분 +{outcome.Water:0.#}");

        if (outcome.Energy > 0f)
            parts.Add($"에너지 +{outcome.Energy:0.#}");

        if (outcome.Cure != StatusEffectType.None)
        {
            parts.Add(outcome.CureStacks < 90
                ? $"{StatusEffectNames.Of(outcome.Cure)} {outcome.CureStacks}중첩 제거"
                : $"{StatusEffectNames.Of(outcome.Cure)} 해제");
        }

        if (outcome.WaterCost > 0f)
            parts.Add($"수분 −{outcome.WaterCost:0.#}");

        if (outcome.Grant != StatusEffectType.None)
        {
            float seconds = StatusEffectTable.Get(outcome.Grant).Duration;

            parts.Add($"{StatusEffectNames.Of(outcome.Grant)} {seconds:0}초");
        }

        if (outcome.EnergyCost > 0f)
            parts.Add($"에너지 −{outcome.EnergyCost:0.#}");

        return $"{definition.DisplayName} — {string.Join(" · ", parts)}";
    }

    private static Health FindPlayerHealth()
    {
        var player = Object.FindAnyObjectByType<DokkaebiController>(FindObjectsInactive.Exclude);

        return player != null ? player.GetComponent<Health>() : null;
    }
}
