using System.Collections.Generic;

/// <summary>
/// 착용 상태. 슬롯 8칸. MonoBehaviour 의존이 없는 순수 클래스다.
/// (docs/Blob_Equipment_System.md 1절)
/// </summary>
public class EquipmentLoadout
{
    private const int SlotCount = 8;

    private readonly ItemStack[] slots = new ItemStack[SlotCount];

    private readonly EquipmentModifiers modifiers = new();

    private bool isDirty = true;

    public ItemStack Get(EquipmentSlot slot) => slots[(int)slot];

    public EquipmentDefinition GetDefinition(EquipmentSlot slot)
    {
        return slots[(int)slot]?.Definition as EquipmentDefinition;
    }

    public bool IsEmpty(EquipmentSlot slot) => slots[(int)slot] == null;

    /// <summary>
    /// 착용할 수 있는지.
    ///
    /// 각인은 【종류와 등급이 둘 다 같으면】 중복 장착할 수 없다.
    /// 하나라도 다르면 가능하다 (감지 II + 감지 III 는 장착된다). [확인됨 — 덕코프]
    /// 이 규칙이 있어야 "최상위 하나를 두 개 끼는" 단조로운 답이 막힌다.
    /// </summary>
    public bool CanEquip(ItemStack stack, EquipmentSlot slot)
    {
        if (stack == null || stack.IsEmpty)
            return false;

        if (stack.Definition is not EquipmentDefinition definition)
            return false;

        if (!FitsSlot(definition, slot))
            return false;

        if (!IsImprintSlot(slot))
            return true;

        EquipmentSlot other = slot == EquipmentSlot.ImprintA
            ? EquipmentSlot.ImprintB
            : EquipmentSlot.ImprintA;

        EquipmentDefinition opposite = GetDefinition(other);

        if (opposite == null)
            return true;

        bool sameFamily = !string.IsNullOrEmpty(definition.ImprintFamily)
                          && definition.ImprintFamily == opposite.ImprintFamily;

        return !(sameFamily && definition.Tier == opposite.Tier);
    }

    /// <summary>착용한다. 기존 장비는 반환된다. 실패하면 false.</summary>
    public bool TryEquip(ItemStack stack, EquipmentSlot slot, out ItemStack previous)
    {
        previous = null;

        if (!CanEquip(stack, slot))
            return false;

        previous = slots[(int)slot];
        slots[(int)slot] = stack;

        isDirty = true;

        return true;
    }

    /// <summary>벗는다. 벗은 장비를 반환한다.</summary>
    public ItemStack Unequip(EquipmentSlot slot)
    {
        ItemStack removed = slots[(int)slot];

        slots[(int)slot] = null;
        isDirty = true;

        return removed;
    }

    /// <summary>합산된 옵션. 변경이 있을 때만 다시 계산한다.</summary>
    public EquipmentModifiers Modifiers
    {
        get
        {
            if (!isDirty)
                return modifiers;

            modifiers.Reset();

            for (int i = 0; i < SlotCount; i++)
            {
                ItemStack stack = slots[i];

                if (stack?.Definition is not EquipmentDefinition definition)
                    continue;

                modifiers.Add(definition, stack.IsBroken, stack.IsWorn);
            }

            isDirty = false;

            return modifiers;
        }
    }

    /// <summary>
    /// 세트 게이트 판정. 규칙 부여가 아니라 【누적 수치】다.
    ///
    /// 올 오어 낫싱이 아니라서 「장비 1점 + 소모품 1개」 조합이 성립한다.
    /// (docs/Blob_Equipment_System.md 5-3절)
    /// </summary>
    public int ContainmentWard(int consumableBonus = 0)
    {
        return UnityEngine.Mathf.Max(0,
            UnityEngine.Mathf.RoundToInt(Modifiers.Get(EquipmentStatType.ContainmentWard)) + consumableBonus);
    }

    /// <summary>착용 장비의 총 무게. 가방 내용물과 합쳐 과중량을 판정한다.</summary>
    public float TotalWeight
    {
        get
        {
            float total = 0f;

            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null)
                    total += slots[i].TotalWeight;
            }

            return total;
        }
    }

    /// <summary>착용 중인 장비 목록. 사망 처리와 UI가 쓴다.</summary>
    public IEnumerable<ItemStack> All()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] != null)
                yield return slots[i];
        }
    }

    /// <summary>
    /// 추출 실패 시 각인을 제외한 전부를 벗긴다. 잃은 장비를 반환한다.
    /// </summary>
    public List<ItemStack> DropOnDeath()
    {
        var lost = new List<ItemStack>();

        for (int i = 0; i < SlotCount; i++)
        {
            ItemStack stack = slots[i];

            if (stack?.Definition == null || stack.Definition.SurvivesDeath)
                continue;

            lost.Add(stack);
            slots[i] = null;
        }

        if (lost.Count > 0)
            isDirty = true;

        return lost;
    }

    public void MarkDirty() => isDirty = true;

    private static bool IsImprintSlot(EquipmentSlot slot)
        => slot == EquipmentSlot.ImprintA || slot == EquipmentSlot.ImprintB;

    /// <summary>각인은 두 슬롯 어디에나 들어간다. 나머지는 지정된 슬롯에만.</summary>
    private static bool FitsSlot(EquipmentDefinition definition, EquipmentSlot slot)
    {
        if (IsImprintSlot(slot))
            return IsImprintSlot(definition.Slot);

        return definition.Slot == slot;
    }
}
