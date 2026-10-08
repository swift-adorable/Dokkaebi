using UnityEngine;

/// <summary>
/// 【장비 닳기 · 수리 · 분해】 (결정 2-96 · Equipment 4절) — 수치는 [임시값]. MonoBehaviour 의존 없음.
///
///   · 무기는 쏠 때마다, 머리 · 몸통은 그 방어도가 받은 타격만큼, 탈은 속성 피해를 받을 때 닳는다 (덕코프처럼)
///     — 한 판(쏘기 300 · 맞기 150 남짓)에 티어 1은 절반쯤, 티어 4 이상은 15~20%쯤
///   · 33% 아래면 성능 저하 · 0이면 방어 옵션 정지 (EquipmentModifiers — 이미 있다)
///   · 「눅눅한 밤」은 닳기 ×1.5
///   · 수리 = 엽전 (닳은 비율 × 물건 값 × 30%) · 티어 4 이상은 고칠 때마다 최대 내구도 −5% · 고치기 전에 알려 준다
///   · 분해 = 장비만 · 작업대 제작법의 재료 절반(없으면 티어로) · 닳을수록 덜 나온다
/// </summary>
public static class WearTable
{
    /// <summary>쏠 때마다 무기가 닳는 양.</summary>
    public const float WeaponPerShot = 0.06f;

    /// <summary>받은 피해 1마다 그 방어구(머리 · 몸통)가 닳는 양.</summary>
    public const float ArmourPerDamage = 0.15f;

    /// <summary>속성 피해 1마다 탈이 닳는 양.</summary>
    public const float MaskPerDamage = 0.1f;

    /// <summary>원거리 · 투사체는 머리, 근접 · 폭발은 몸통 — 방어도와 같은 나눔 (Equipment 3절).</summary>
    public static EquipmentSlot ArmourSlotFor(HitKind kind) => kind == HitKind.Ranged ? EquipmentSlot.Head : EquipmentSlot.Body;

    // ── 수리 ──────────────────────────────────────────────────────────

    public const float RepairCostRatio = 0.3f;

    /// <summary>고칠 것이 있는가 — 내구도가 있고 지금 상한보다 낮다.</summary>
    public static bool NeedsRepair(ItemStack stack)
        => stack?.Definition != null && stack.Definition.HasDurability && stack.Durability < stack.MaxDurability;

    /// <summary>수리 값 (엽전). 고칠 것이 없으면 0.</summary>
    public static int RepairCost(ItemStack stack)
    {
        if (!NeedsRepair(stack))
            return 0;

        float worn = (stack.MaxDurability - stack.Durability) / (float)Mathf.Max(1, stack.Definition.MaxDurability);
        return Mathf.Max(1, Mathf.CeilToInt(worn * stack.Definition.BaseValue * RepairCostRatio));
    }

    /// <summary>고친 뒤의 최대 내구도 — 미리 알려 준다 (Equipment 4절 「수리 전 사전 고지는 필수」).</summary>
    public static int MaxAfterRepair(ItemStack stack) => stack?.MaxDurabilityAfterRepair ?? 0;

    // ── 분해 ──────────────────────────────────────────────────────────

    /// <summary>분해할 수 있는가 — 무기 · 방어구 · 가방. 각인 · 소모품 · 탄 · 재료는 안 된다.</summary>
    public static bool CanDismantle(ItemDefinition definition)
        => definition != null
           && (definition.Kind == ItemKind.Weapon || definition.Kind == ItemKind.Armour || definition.Kind == ItemKind.Backpack);

    /// <summary>닳은 정도 (0 ~ 1). 내구도가 없으면 1.</summary>
    public static float Condition(ItemStack stack)
        => stack?.Definition == null || !stack.Definition.HasDurability || stack.Definition.MaxDurability <= 0
            ? 1f
            : Mathf.Clamp01(stack.Durability / (float)stack.Definition.MaxDurability);

    /// <summary>
    /// 분해하면 나오는 것 — 작업대 제작법이 있으면 그 재료의 절반, 없으면 티어로(쇠붙이 2T · 새끼 뭉치 T · 숯 T−1)의 절반.
    /// 닳은 만큼 줄어든다(최소 25%). 쇠붙이는 적어도 하나.
    /// </summary>
    public static System.Collections.Generic.List<MaterialCost> DismantleYield(ItemStack stack)
    {
        var result = new System.Collections.Generic.List<MaterialCost>();
        if (stack?.Definition == null || !CanDismantle(stack.Definition))
            return result;

        float scale = 0.5f * Mathf.Max(0.25f, Condition(stack));
        CraftRecipe recipe = WorkbenchTable.Find(stack.Definition.Id);
        int tier = Mathf.Max(1, stack.Definition.Tier);

        MaterialCost[] source = recipe != null
            ? recipe.Inputs
            : new[]
            {
                new MaterialCost(WorkbenchTable.Scrap, tier * 2),
                new MaterialCost(WorkbenchTable.Rope, tier),
                new MaterialCost(WorkbenchTable.Charcoal, Mathf.Max(1, tier - 1)),
            };

        bool scrap = false;
        foreach (MaterialCost c in source)
        {
            int n = Mathf.FloorToInt(c.Count * scale);
            if (n <= 0)
                continue;
            result.Add(new MaterialCost(c.ItemId, n));
            scrap |= c.ItemId == WorkbenchTable.Scrap;
        }

        if (!scrap)
            result.Add(new MaterialCost(WorkbenchTable.Scrap, 1));

        return result;
    }
}

/// <summary>닳기를 조금씩 모았다가 1이 되면 내구도를 1 깎는다 (한 발에 0.06처럼 작은 값을 위해).</summary>
public sealed class WearAccumulator
{
    private float pending;

    /// <summary>모은다. 깎을 정수를 돌려준다.</summary>
    public int Add(float amount)
    {
        if (amount <= 0f)
            return 0;

        pending += amount;
        int whole = Mathf.FloorToInt(pending);
        pending -= whole;
        return whole;
    }

    public void Clear() => pending = 0f;
}
