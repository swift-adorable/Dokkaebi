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

    public ItemStack(ItemDefinition definition, int count = 1, int durability = -1)
    {
        Definition = definition;

        Count = definition == null ? 0 : Mathf.Clamp(count, 1, definition.StackMax);

        if (definition != null && definition.HasDurability)
            Durability = durability < 0 ? definition.MaxDurability : Mathf.Clamp(durability, 0, definition.MaxDurability);
        else
            Durability = 0;
    }

    public bool IsEmpty => Definition == null || Count <= 0;

    /// <summary>이 칸의 총 무게.</summary>
    public float TotalWeight => Definition == null ? 0f : Definition.Weight * Count;

    /// <summary>이 칸이 차지하는 적재 칸 수.</summary>
    public int TotalSlots => Definition == null ? 0 : Definition.SlotSize;

    /// <summary>더 겹칠 수 있는 여유 개수.</summary>
    public int FreeSpace => Definition == null ? 0 : Definition.StackMax - Count;

    /// <summary>
    /// 최대 내구도의 33% 이하인지. 이 지점에서 성능이 떨어지고 UI가 경고색이 된다.
    /// 0이 되어야 망가지는 것이 아니다. (docs/Blob_Equipment_System.md 4절)
    /// </summary>
    public bool IsWorn
        => Definition != null && Definition.HasDurability
           && Durability <= Mathf.CeilToInt(Definition.MaxDurability * WornThreshold);

    /// <summary>방어 옵션이 정지했는지. 탐지·수집·적재 옵션은 계속 작동한다.</summary>
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
    /// 수리한다. 최대 내구도까지 회복되지 않는 것이 핵심이다.
    ///
    /// 수리할수록 상한이 깎여 결국 폐기된다. 장비가 자연 소멸하는 경제 싱크다. [확인됨]
    /// ※ 상한 감소는 티어 4 이상에만 적용한다. (완화안 — 장비 문서 4절)
    /// </summary>
    public int Repair(int amount)
    {
        if (amount <= 0 || Definition == null || !Definition.HasDurability)
            return 0;

        int before = Durability;

        Durability = Mathf.Min(Definition.MaxDurability, Durability + amount);

        return Durability - before;
    }
}
