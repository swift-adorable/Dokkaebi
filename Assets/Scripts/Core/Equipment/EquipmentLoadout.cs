using System.Collections.Generic;

/// <summary>
/// 착용 상태. 슬롯 9칸 (화살통 · 탄창 포함 — 결정 2-80). MonoBehaviour 의존이 없는 순수 클래스다.
/// (docs/Dokkaebi_Equipment_System.md 1절)
/// </summary>
public class EquipmentLoadout
{
    private const int SlotCount = 9;

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

        // 화살통 · 탄창 — 든 무기가 쓰는 탄만, 정해진 수까지 (결정 2-80).
        if (slot == EquipmentSlot.Ammo)
            return stack.Definition.Kind == ItemKind.Ammo
                   && stack.Definition.Id == AmmoId
                   && stack.Count <= AmmoTable.CapacityOf(AmmoId);

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

    /// <summary>든 무기가 쓰는 탄 id. 무기가 없으면 null — 맨손은 탄을 쓰지 않는다.</summary>
    public string AmmoId => (Get(EquipmentSlot.Weapon)?.Definition as WeaponDefinition)?.AmmoId;

    /// <summary>화살통 · 탄창에 든 수.</summary>
    public int LoadedAmmo => Get(EquipmentSlot.Ammo)?.Count ?? 0;

    /// <summary>화살통 · 탄창에 담기는 수. 무기가 없으면 0.</summary>
    public int AmmoCapacity => AmmoTable.CapacityOf(AmmoId);

    /// <summary>
    /// 한 발을 뺀다. 비면 칸을 비운다. 뺐으면 true.
    /// </summary>
    public bool ConsumeAmmo()
    {
        ItemStack held = Get(EquipmentSlot.Ammo);

        if (held == null || held.IsEmpty)
            return false;

        held.Take(1);

        if (held.IsEmpty)
            slots[(int)EquipmentSlot.Ammo] = null;

        return true;
    }

    /// <summary>
    /// 가방에서 통을 채운다 — 빈 자리만큼, 가방에 있는 만큼. 채운 수를 돌려준다.
    /// </summary>
    public int RefillAmmo(Inventory bag)
    {
        string id = AmmoId;

        if (string.IsNullOrEmpty(id) || bag == null)
            return 0;

        ItemDefinition ammo = FindInBag(bag, id);

        if (ammo == null)
            return 0;

        int amount = AmmoTable.RefillAmount(LoadedAmmo, AmmoCapacity, bag.CountOf(ammo));

        if (amount <= 0)
            return 0;

        bag.Remove(ammo, amount);

        int total = LoadedAmmo + amount;
        slots[(int)EquipmentSlot.Ammo] = new ItemStack(ammo, total);

        return amount;
    }

    /// <summary>
    /// 통에 든 것이 지금 무기의 탄이 아니면 가방으로 돌려보낸다 (무기를 벗거나 바꿨을 때).
    /// 가방이 받지 못하면 그대로 둔다. 돌려보냈거나 맞으면 true.
    /// </summary>
    public bool ReturnMismatchedAmmo(Inventory bag)
    {
        ItemStack held = Get(EquipmentSlot.Ammo);

        if (held == null)
            return true;

        if (held.Definition != null && held.Definition.Id == AmmoId)
            return true;

        if (bag == null || !bag.TryAddStack(held))
            return false;

        slots[(int)EquipmentSlot.Ammo] = null;
        return true;
    }

    private static ItemDefinition FindInBag(Inventory bag, string id)
    {
        foreach (ItemStack s in bag.Stacks)
            if (s?.Definition != null && s.Definition.Id == id)
                return s.Definition;

        return null;
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
    /// 세트 한계치 판정. 규칙 부여가 아니라 【누적 수치】다.
    ///
    /// 전부 아니면 전무가 아니라서 「장비 1점 + 소모품 1개」 조합이 성립한다.
    /// (docs/Dokkaebi_Equipment_System.md 5-3절)
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
    /// 철수 실패 시 각인을 제외한 전부를 벗긴다. 잃은 장비를 반환한다.
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
