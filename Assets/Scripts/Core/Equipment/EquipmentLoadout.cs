using System.Collections.Generic;

/// <summary>
/// 착용 상태. 슬롯 11칸 — 무기 두 자루 · 각자의 화살통 · 탄창 (결정 2-80 · 2-81). MonoBehaviour 의존이 없는 순수 클래스다.
/// (docs/Dokkaebi_Equipment_System.md 1절)
/// </summary>
public class EquipmentLoadout
{
    private const int SlotCount = 11;

    /// <summary>든 무기 — 0 = 무기 1, 1 = 무기 2 (결정 2-81). 든 쪽만 사격 성능 · 옵션이 들어간다.</summary>
    public int ActiveWeapon { get; private set; }

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

        // 화살통 · 탄창 — 그 무기가 쓰는 탄만, 정해진 수까지 (결정 2-80).
        if (IsAmmoSlot(slot))
        {
            string id = AmmoIdOf(WeaponSlotOf(slot));
            return stack.Definition.Kind == ItemKind.Ammo
                   && stack.Definition.Id == id
                   && stack.Count <= AmmoTable.CapacityOf(id);
        }

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

    // ── 무기 두 자루 (결정 2-81) ─────────────────────────────────────

    public static bool IsWeaponSlot(EquipmentSlot slot) => slot == EquipmentSlot.Weapon || slot == EquipmentSlot.Weapon2;
    public static bool IsAmmoSlot(EquipmentSlot slot) => slot == EquipmentSlot.Ammo || slot == EquipmentSlot.Ammo2;

    public static EquipmentSlot WeaponSlot(int index) => index == 1 ? EquipmentSlot.Weapon2 : EquipmentSlot.Weapon;
    public static EquipmentSlot AmmoSlot(int index) => index == 1 ? EquipmentSlot.Ammo2 : EquipmentSlot.Ammo;
    public static EquipmentSlot AmmoSlotOf(EquipmentSlot weaponSlot) => weaponSlot == EquipmentSlot.Weapon2 ? EquipmentSlot.Ammo2 : EquipmentSlot.Ammo;
    public static EquipmentSlot WeaponSlotOf(EquipmentSlot ammoSlot) => ammoSlot == EquipmentSlot.Ammo2 ? EquipmentSlot.Weapon2 : EquipmentSlot.Weapon;

    public WeaponDefinition WeaponAt(int index) => Get(WeaponSlot(index))?.Definition as WeaponDefinition;

    /// <summary>든 무기. 없으면 null — 맨손.</summary>
    public WeaponDefinition ActiveWeaponDefinition => WeaponAt(ActiveWeapon);

    /// <summary>무기를 바꿔 든다. 같은 쪽이면 false.</summary>
    public bool SetActiveWeapon(int index)
    {
        index = index == 1 ? 1 : 0;
        if (index == ActiveWeapon)
            return false;

        ActiveWeapon = index;
        isDirty = true;
        return true;
    }

    /// <summary>
    /// 새 무기가 들어갈 자리 — 보고 있는(든) 쪽이 비었으면 거기, 아니면 다른 빈 쪽, 둘 다 차 있으면 든 쪽과 바꾼다.
    /// 가방 화면의 I · II 탭으로 고른 쪽에 걸린다 (결정 2-82).
    /// </summary>
    public EquipmentSlot FreeWeaponSlot()
    {
        EquipmentSlot active = WeaponSlot(ActiveWeapon);
        EquipmentSlot other = WeaponSlot(1 - ActiveWeapon);

        if (Get(active) == null) return active;
        if (Get(other) == null) return other;
        return active;
    }

    /// <summary>
    /// 주운 장비가 바로 들어갈 빈 자리 (결정 2-84). 없으면 null — 그 부위가 차 있으면 가방으로 간다.
    /// 무기는 든 쪽 → 다른 쪽, 새김패는 1 → 2(같은 계열 · 단계 겹침 규칙은 CanEquip이 본다).
    /// </summary>
    public EquipmentSlot? EmptySlotFor(ItemStack stack)
    {
        if (stack?.Definition is not EquipmentDefinition definition)
            return null;

        EquipmentSlot[] candidates = definition.Slot switch
        {
            EquipmentSlot.Weapon => new[] { WeaponSlot(ActiveWeapon), WeaponSlot(1 - ActiveWeapon) },
            EquipmentSlot.ImprintA or EquipmentSlot.ImprintB => new[] { EquipmentSlot.ImprintA, EquipmentSlot.ImprintB },
            _ => new[] { definition.Slot },
        };

        foreach (EquipmentSlot slot in candidates)
            if (Get(slot) == null && CanEquip(stack, slot))
                return slot;

        return null;
    }

    private string AmmoIdOf(EquipmentSlot weaponSlot) => (Get(weaponSlot)?.Definition as WeaponDefinition)?.AmmoId;

    /// <summary>든 무기가 쓰는 탄 id. 무기가 없으면 null — 맨손은 탄을 쓰지 않는다.</summary>
    public string AmmoId => AmmoIdOf(WeaponSlot(ActiveWeapon));

    /// <summary>그 무기(0 · 1)가 쓰는 탄 · 통에 든 수 · 담는 수.</summary>
    public string AmmoIdAt(int index) => AmmoIdOf(WeaponSlot(index));
    public int LoadedAt(int index) => Get(AmmoSlot(index))?.Count ?? 0;
    public int CapacityAt(int index) => AmmoTable.CapacityOf(AmmoIdAt(index));

    /// <summary>든 무기의 화살통 · 탄창에 든 수.</summary>
    public int LoadedAmmo => LoadedAt(ActiveWeapon);

    /// <summary>화살통 · 탄창에 담기는 수. 무기가 없으면 0.</summary>
    public int AmmoCapacity => AmmoTable.CapacityOf(AmmoId);

    /// <summary>
    /// 한 발을 뺀다. 비면 칸을 비운다. 뺐으면 true.
    /// </summary>
    public bool ConsumeAmmo()
    {
        EquipmentSlot slot = AmmoSlot(ActiveWeapon);
        ItemStack held = Get(slot);

        if (held == null || held.IsEmpty)
            return false;

        held.Take(1);

        if (held.IsEmpty)
            slots[(int)slot] = null;

        return true;
    }

    /// <summary>
    /// 가방에서 통을 채운다 — 빈 자리만큼, 가방에 있는 만큼. 채운 수를 돌려준다.
    /// </summary>
    public int RefillAmmo(Inventory bag) => RefillAmmo(bag, ActiveWeapon);

    /// <summary>그 무기(0 · 1)의 통을 가방에서 채운다.</summary>
    public int RefillAmmo(Inventory bag, int index)
    {
        string id = AmmoIdAt(index);

        if (string.IsNullOrEmpty(id) || bag == null)
            return 0;

        ItemDefinition ammo = FindInBag(bag, id);

        if (ammo == null)
            return 0;

        int amount = AmmoTable.RefillAmount(LoadedAt(index), CapacityAt(index), bag.CountOf(ammo));

        if (amount <= 0)
            return 0;

        bag.Remove(ammo, amount);

        int total = LoadedAt(index) + amount;
        slots[(int)AmmoSlot(index)] = new ItemStack(ammo, total);

        return amount;
    }

    /// <summary>
    /// 통에 든 것이 지금 무기의 탄이 아니면 가방으로 돌려보낸다 (무기를 벗거나 바꿨을 때).
    /// 가방이 받지 못하면 그대로 둔다. 돌려보냈거나 맞으면 true.
    /// </summary>
    public bool ReturnMismatchedAmmo(Inventory bag)
    {
        bool ok = true;

        for (int index = 0; index < 2; index++)
        {
            EquipmentSlot slot = AmmoSlot(index);
            ItemStack held = Get(slot);

            if (held == null)
                continue;

            if (held.Definition != null && held.Definition.Id == AmmoIdAt(index))
                continue;

            if (bag == null || !bag.TryAddStack(held))
            {
                ok = false;
                continue;
            }

            slots[(int)slot] = null;
        }

        return ok;
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

                // 메고만 있는 무기의 옵션은 들어가지 않는다 — 든 무기만 (결정 2-81).
                if (IsWeaponSlot((EquipmentSlot)i) && (EquipmentSlot)i != WeaponSlot(ActiveWeapon))
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

        ActiveWeapon = 0;

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

        // 무기는 두 자리 어디에나 (결정 2-81).
        if (IsWeaponSlot(slot))
            return definition.Slot == EquipmentSlot.Weapon;

        return definition.Slot == slot;
    }
}
