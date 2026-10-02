using UnityEngine;

/// <summary>
/// 인벤토리 한 칸에 들어 있는 실제 아이템. MonoBehaviour 의존이 없는 순수 클래스다.
///
/// 정의(ItemDefinition)는 공유되고, 개체 상태(개수·내구도)는 여기에 있다.
/// </summary>
public class ItemStack
{
    public ItemDefinition Definition { get; }

    /// <summary>겹쳐진 개수. 겹치지 않는 아이템은 항상 1이다.</summary>
    public int Count { get; private set; }

    /// <summary>현재 내구도. 내구도가 없는 아이템은 0이다.</summary>
    public int Durability { get; private set; }

    /// <summary>
    /// 이 개체의 현재 최대 내구도. 수리할수록 깎인다.
    ///
    /// ItemDefinition이 아니라 여기에 두는 이유 — 정의는 모든 개체가 공유하는
    /// 에셋이라 「이 개체는 상한이 깎였다」를 담을 자리가 없다.
    ///
    /// ※ 현재 감소량은 0이다. 수리 비용과 경제가 8단계에 오므로
    ///   그때 RepairLossRatio를 정한다. 지금 필드를 만들어 두는 이유는
    ///   나중에 세이브 포맷을 깨지 않기 위함이다.
    /// (docs/Dokkaebi_Equipment_System.md 「내구도」)
    /// </summary>
    public int MaxDurability { get; private set; }

    public ItemStack(ItemDefinition definition, int count = 1, int durability = -1)
    {
        Definition = definition;

        Count = definition == null ? 0 : Mathf.Clamp(count, 1, definition.StackMax);

        if (definition != null && definition.HasDurability)
        {
            MaxDurability = definition.MaxDurability;
            Durability = durability < 0
                ? MaxDurability
                : Mathf.Clamp(durability, 0, MaxDurability);
        }
        else
        {
            MaxDurability = 0;
            Durability = 0;
        }
    }

    public bool IsEmpty => Definition == null || Count <= 0;

    /// <summary>이 칸의 총 무게.</summary>
    /// <summary>
    /// 어깨에 걸리는 무게. 【젬은 0이다.】 (ItemDefinition.IsCargo — 결정 2-31)
    /// 낱개 무게(Definition.Weight)는 상세에 그대로 적는다 — 물건의 성질이고,
    /// 소켓에 끼우기 전까지 "이게 무거운 물건인가"는 여전히 정보다.
    /// </summary>
    public float TotalWeight => Definition == null ? 0f : Definition.WeightCost * Count;

    /// <summary>이 칸이 차지하는 칸 수.</summary>
    public int TotalSlots => Definition == null ? 0 : Definition.SlotCost;

    /// <summary>더 겹칠 수 있는 여유 개수.</summary>
    public int FreeSpace => Definition == null ? 0 : Definition.StackMax - Count;

    /// <summary>
    /// 최대 내구도의 33% 이하인지. 이 지점에서 성능이 떨어지고 UI가 경고색이 된다.
    /// 0이 되어야 망가지는 것이 아니다. (docs/Dokkaebi_Equipment_System.md 4절)
    /// </summary>
    public bool IsWorn
        => Definition != null && Definition.HasDurability
           && Durability <= Mathf.CeilToInt(MaxDurability * WornThreshold);

    /// <summary>방어 옵션이 정지했는지. 탐지·수집·보관 옵션은 계속 작동한다.</summary>
    public bool IsBroken => Definition != null && Definition.HasDurability && Durability <= 0;

    public const float WornThreshold = 0.33f;

    /// <summary>같은 정의끼리 겹칠 수 있는지. 내구도가 있는 아이템은 겹치지 않는다.</summary>
    public bool CanMergeWith(ItemStack other)
    {
        return other != null
               && Definition != null
               && other.Definition == Definition
               && Definition.IsStackable
               && FreeSpace > 0;
    }

    /// <summary>다른 칸을 흡수한다. 실제로 옮긴 개수를 반환한다.</summary>
    public int Merge(ItemStack other)
    {
        if (!CanMergeWith(other))
            return 0;

        int moved = Mathf.Min(FreeSpace, other.Count);

        Count += moved;
        other.Count -= moved;

        return moved;
    }

    /// <summary>개수를 줄인다. 실제로 줄인 양을 반환한다.</summary>
    public int Take(int amount)
    {
        if (amount <= 0)
            return 0;

        int taken = Mathf.Min(amount, Count);
        Count -= taken;

        return taken;
    }

    /// <summary>내구도를 깎는다. 실제로 깎인 양을 반환한다.</summary>
    public int Damage(int amount)
    {
        if (amount <= 0 || Definition == null || !Definition.HasDurability)
            return 0;

        int applied = Mathf.Min(amount, Durability);
        Durability -= applied;

        return applied;
    }

    /// <summary>
    /// 수리할 때 최대 내구도가 깎이는 비율. 티어 4 이상에만 적용한다.
    ///
    /// 【현재 0이다.】 수리 비용과 경제가 8단계에 오므로 그때 값을 정한다.
    /// 0이어도 경로는 살아 있으므로, 값 하나만 바꾸면 경제 싱크가 켜진다.
    /// </summary>
    public const float RepairLossRatio = 0f;

    /// <summary>상한 감소가 적용되기 시작하는 티어. 하위 장비는 부담 없이 수리한다.</summary>
    public const int RepairLossMinTier = 4;

    /// <summary>
    /// 수리한다. 【최대 내구도까지 회복되지만, 그 상한 자체가 깎인다.】
    ///
    /// 수리할수록 상한이 줄어 결국 폐기된다 — 장비가 자연 소멸하는 경제 싱크다.
    /// 덕코프에서 불만이 큰 축이라 완화한다: 티어 4 이상에만, 감소량을 작게.
    /// (docs/Dokkaebi_Equipment_System.md 「내구도」)
    /// </summary>
    public int Repair(int amount)
    {
        if (amount <= 0 || Definition == null || !Definition.HasDurability)
            return 0;

        ReduceMaxDurability();

        int before = Durability;

        Durability = Mathf.Min(MaxDurability, Durability + amount);

        return Durability - before;
    }

    /// <summary>수리에 따른 상한 감소. 최소 1은 남겨 아이템이 즉시 소멸하지 않게 한다.</summary>
    private void ReduceMaxDurability()
    {
        if (RepairLossRatio <= 0f || Definition.Tier < RepairLossMinTier)
            return;

        int loss = Mathf.Max(1, Mathf.RoundToInt(Definition.MaxDurability * RepairLossRatio));

        MaxDurability = Mathf.Max(1, MaxDurability - loss);

        if (Durability > MaxDurability)
            Durability = MaxDurability;
    }
}
