using System;
using System.Collections.Generic;

/// <summary>
/// 지금 소켓에 끼워져 있는 인자들. RunSkillState를 대체한다.
/// (docs/Blob_Skill_System.md 11·12절)
///
/// RunSkillState와의 차이 —
///   · 「획득」이 없다. 줍는 것은 가방이 하고, 여기는 【끼운 것】만 안다.
///   · 자리를 지정해 끼운다. 자동 배치를 하지 않는다.
///   · 뺄 수 있다. 뺀 인자는 사라지지 않고 호출부(가방)로 돌아간다.
///   · 슬롯 수는 각성 레벨이 정한다. (SocketUnlockTable)
///
/// 뺀 인자를 여기서 버리지 않고 호출부에 돌려주는 이유 —
/// 인자는 실물 아이템이므로 조작 도중에 소멸하면 안 된다.
/// 이 클래스는 아이템을 만들지도 없애지도 않는다. 자리만 관리한다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public class SocketedBuild
{
    public const int MaxCores = SocketUnlockTable.MaxCores;
    public const int SocketsPerCore = SocketUnlockTable.SocketsPerCore;
    public const int MaxMetas = SocketUnlockTable.MaxMetas;
    public const int MaxHeralds = SocketUnlockTable.MaxHeralds;

    private readonly SkillDefinition[] cores = new SkillDefinition[MaxCores];
    private readonly SkillDefinition[,] sockets = new SkillDefinition[MaxCores, SocketsPerCore];
    private readonly SkillDefinition[] metas = new SkillDefinition[MaxMetas];
    private SkillDefinition herald;

    private readonly List<SkillDefinition> equipped = new();
    private readonly WeaponModifiers modifiers = new();

    private int awakeningLevel = 1;
    private SocketCapacity capacity = SocketUnlockTable.Evaluate(1);

    private bool isDirty = true;
    private bool equippedDirty = true;

    /// <summary>구성이 바뀌었을 때 발행된다. UI가 구독한다.</summary>
    public event Action OnChanged;

    // ────────────────────────────────── 상태 조회

    /// <summary>각성 레벨. 슬롯 개방은 전부 이 값에서 나온다.</summary>
    public int AwakeningLevel => awakeningLevel;

    /// <summary>지금 열려 있는 슬롯 구성.</summary>
    public SocketCapacity Capacity => capacity;

    /// <summary>끼워진 인자 전부. 순서는 Core → 소켓 → 발동 → 전령.</summary>
    public IReadOnlyList<SkillDefinition> Equipped
    {
        get
        {
            RebuildEquippedIfNeeded();
            return equipped;
        }
    }

    public int EquippedCount => Equipped.Count;

    /// <summary>공격 수단이 하나라도 있는지. Core가 없으면 아무것도 쏘지 못한다.</summary>
    public bool HasCore => cores[0] != null || cores[1] != null;

    /// <summary>장착된 전령. 없으면 null.</summary>
    public SkillDefinition Herald => herald;

    /// <summary>지정한 Core 슬롯의 인자. 비었으면 null.</summary>
    public SkillDefinition GetCore(int coreIndex)
    {
        return IsValidCoreIndex(coreIndex) ? cores[coreIndex] : null;
    }

    /// <summary>지정한 소켓의 인자. 비었으면 null.</summary>
    public SkillDefinition GetSocket(int coreIndex, int socketIndex)
    {
        return IsValidSocket(coreIndex, socketIndex) ? sockets[coreIndex, socketIndex] : null;
    }

    /// <summary>지정한 발동 슬롯의 인자. 비었으면 null.</summary>
    public SkillDefinition GetMeta(int slot)
    {
        return slot >= 0 && slot < MaxMetas ? metas[slot] : null;
    }

    /// <summary>비어 있고 열려 있는 소켓의 개수. "주웠는데 끼울 데가 없다"를 UI가 알리는 데 쓴다.</summary>
    public int FreeSocketCount
    {
        get
        {
            int free = 0;

            for (int c = 0; c < MaxCores; c++)
            {
                if (cores[c] == null)
                    continue;

                for (int s = 0; s < capacity.SocketsIn(c); s++)
                {
                    if (sockets[c, s] == null)
                        free++;
                }
            }

            return free;
        }
    }

    /// <summary>같은 정의가 어디든 끼워져 있는지.</summary>
    public bool IsEquipped(SkillDefinition definition)
    {
        if (definition == null)
            return false;

        IReadOnlyList<SkillDefinition> list = Equipped;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == definition)
                return true;
        }

        return false;
    }

    // ────────────────────────────────── 각성 레벨

    /// <summary>
    /// 각성 레벨을 올린다. 자리가 열릴 뿐이고 끼워진 것은 건드리지 않는다.
    /// 레벨은 런 중에 내려가지 않으므로 축소에 따른 탈착은 다루지 않는다.
    /// </summary>
    public void SetAwakeningLevel(int level)
    {
        int clamped = SocketUnlockTable.ClampLevel(level);

        if (clamped == awakeningLevel)
            return;

        awakeningLevel = clamped;
        capacity = SocketUnlockTable.Evaluate(clamped);

        OnChanged?.Invoke();
    }

    // ────────────────────────────────── 장착 가능 판정

    /// <summary>이 인자를 이 Core 슬롯에 끼울 수 있는지.</summary>
    public SocketError CanEquipCore(SkillDefinition definition, int coreIndex)
    {
        if (definition == null)
            return SocketError.NullGem;

        if (definition.Category != SkillCategory.Core)
            return SocketError.WrongCategory;

        if (!IsValidCoreIndex(coreIndex))
            return SocketError.NoSuchSlot;

        if (coreIndex >= capacity.CoreSlots)
            return SocketError.SlotLocked;

        if (definition.RequiredLevel > awakeningLevel)
            return SocketError.LevelTooHigh;

        // 같은 Core를 두 자리에 넣는 것은 수치 중첩이므로 막는다. (14절 5번)
        for (int c = 0; c < MaxCores; c++)
        {
            if (c != coreIndex && cores[c] == definition)
                return SocketError.Duplicate;
        }

        return SocketError.None;
    }

    /// <summary>이 인자를 이 소켓에 끼울 수 있는지.</summary>
    public SocketError CanEquipSupport(SkillDefinition definition, int coreIndex, int socketIndex)
    {
        if (definition == null)
            return SocketError.NullGem;

        if (definition.Category != SkillCategory.Support)
            return SocketError.WrongCategory;

        if (!IsValidSocket(coreIndex, socketIndex))
            return SocketError.NoSuchSlot;

        if (socketIndex >= capacity.SocketsIn(coreIndex))
            return SocketError.SlotLocked;

        if (cores[coreIndex] == null)
            return SocketError.NoCore;

        if (definition.RequiredLevel > awakeningLevel)
            return SocketError.LevelTooHigh;

        // 태그 게이팅. Core가 요구 태그를 전부 가져야 한다. (10-2 [1])
        if (!cores[coreIndex].Tags.ContainsAll(definition.RequiredTags))
            return SocketError.TagMismatch;

        // 같은 Core 안의 중복만 막는다.
        // 다른 Core에 같은 Support를 하나씩 끼우는 것은 11-3절이 명시적으로 허용한다.
        for (int s = 0; s < SocketsPerCore; s++)
        {
            if (s != socketIndex && sockets[coreIndex, s] == definition)
                return SocketError.Duplicate;
        }

        return SocketError.None;
    }

    /// <summary>이 인자를 이 발동 슬롯에 끼울 수 있는지.</summary>
    public SocketError CanEquipMeta(SkillDefinition definition, int slot)
    {
        if (definition == null)
            return SocketError.NullGem;

        if (definition.Category != SkillCategory.Meta)
            return SocketError.WrongCategory;

        if (slot < 0 || slot >= MaxMetas)
            return SocketError.NoSuchSlot;

        if (slot >= capacity.MetaSlots)
            return SocketError.SlotLocked;

        if (definition.RequiredLevel > awakeningLevel)
            return SocketError.LevelTooHigh;

        for (int i = 0; i < MaxMetas; i++)
        {
            if (i != slot && metas[i] == definition)
                return SocketError.Duplicate;
        }

        return SocketError.None;
    }

    /// <summary>이 인자를 전령 자리에 끼울 수 있는지.</summary>
    public SocketError CanEquipHerald(SkillDefinition definition)
    {
        if (definition == null)
            return SocketError.NullGem;

        if (definition.Category != SkillCategory.Persistent)
            return SocketError.WrongCategory;

        if (capacity.HeraldSlots <= 0)
            return SocketError.SlotLocked;

        if (definition.RequiredLevel > awakeningLevel)
            return SocketError.LevelTooHigh;

        return SocketError.None;
    }

    /// <summary>
    /// 분류를 보고 알맞은 자리에 대해 판정한다.
    /// UI가 "이 인자를 지금 어디든 끼울 수 있는가"를 물을 때 쓴다.
    /// </summary>
    public SocketError CanEquipAnywhere(SkillDefinition definition)
    {
        if (definition == null)
            return SocketError.NullGem;

        SocketError best = SocketError.NoSuchSlot;

        switch (definition.Category)
        {
            case SkillCategory.Core:
                for (int c = 0; c < MaxCores; c++)
                    best = Better(best, CanEquipCore(definition, c));
                break;

            case SkillCategory.Support:
                for (int c = 0; c < MaxCores; c++)
                {
                    for (int s = 0; s < SocketsPerCore; s++)
                        best = Better(best, CanEquipSupport(definition, c, s));
                }
                break;

            case SkillCategory.Meta:
                for (int i = 0; i < MaxMetas; i++)
                    best = Better(best, CanEquipMeta(definition, i));
                break;

            case SkillCategory.Persistent:
                best = CanEquipHerald(definition);
                break;
        }

        return best;
    }

    /// <summary>두 결과 중 유저에게 더 희망적인 쪽. None이면 무조건 None.</summary>
    private static SocketError Better(SocketError a, SocketError b)
    {
        if (a == SocketError.None || b == SocketError.None)
            return SocketError.None;

        // 자리가 잠긴 것은 "레벨을 올리면 된다"는 안내가 되므로 NoSuchSlot보다 낫다.
        return a == SocketError.NoSuchSlot ? b : a;
    }

    // ────────────────────────────────── 장착

    /// <summary>
    /// Core 슬롯에 끼운다. 원래 있던 Core와, 새 Core의 태그를 만족하지 못하게 된
    /// Support가 <paramref name="returned"/>에 담겨 돌아간다. 호출부가 가방에 되돌린다.
    /// </summary>
    public bool TryEquipCore(SkillDefinition definition, int coreIndex,
                             List<SkillDefinition> returned = null)
    {
        if (CanEquipCore(definition, coreIndex) != SocketError.None)
            return false;

        SkillDefinition previous = cores[coreIndex];

        if (previous != null)
            returned?.Add(previous);

        cores[coreIndex] = definition;

        // Core가 바뀌면 태그 게이팅을 다시 통과하지 못하는 Support가 생긴다.
        // 조용히 무효화하지 않고 가방으로 돌려보낸다. 아이템을 없애지 않기 위해서다.
        for (int s = 0; s < SocketsPerCore; s++)
        {
            SkillDefinition support = sockets[coreIndex, s];

            if (support == null)
                continue;

            if (definition.Tags.ContainsAll(support.RequiredTags))
                continue;

            sockets[coreIndex, s] = null;
            returned?.Add(support);
        }

        MarkChanged();

        return true;
    }

    /// <summary>소켓에 끼운다. 원래 있던 Support가 returned에 담겨 돌아간다.</summary>
    public bool TryEquipSupport(SkillDefinition definition, int coreIndex, int socketIndex,
                                List<SkillDefinition> returned = null)
    {
        if (CanEquipSupport(definition, coreIndex, socketIndex) != SocketError.None)
            return false;

        SkillDefinition previous = sockets[coreIndex, socketIndex];

        if (previous != null)
            returned?.Add(previous);

        sockets[coreIndex, socketIndex] = definition;

        MarkChanged();

        return true;
    }

    /// <summary>발동 슬롯에 끼운다.</summary>
    public bool TryEquipMeta(SkillDefinition definition, int slot,
                             List<SkillDefinition> returned = null)
    {
        if (CanEquipMeta(definition, slot) != SocketError.None)
            return false;

        if (metas[slot] != null)
            returned?.Add(metas[slot]);

        metas[slot] = definition;

        MarkChanged();

        return true;
    }

    /// <summary>전령 자리에 끼운다.</summary>
    public bool TryEquipHerald(SkillDefinition definition,
                               List<SkillDefinition> returned = null)
    {
        if (CanEquipHerald(definition) != SocketError.None)
            return false;

        if (herald != null)
            returned?.Add(herald);

        herald = definition;

        MarkChanged();

        return true;
    }

    /// <summary>
    /// 분류를 보고 첫 번째로 가능한 자리에 끼운다.
    /// 자동 획득이 아니라 UI의 「빠른 장착」 편의 기능이다.
    /// 자리가 둘 이상 가능하면 유저가 직접 고르는 경로를 따로 둔다.
    /// </summary>
    public bool TryEquipAuto(SkillDefinition definition, List<SkillDefinition> returned = null)
    {
        if (definition == null)
            return false;

        switch (definition.Category)
        {
            case SkillCategory.Core:
                for (int c = 0; c < MaxCores; c++)
                {
                    if (cores[c] == null && TryEquipCore(definition, c, returned))
                        return true;
                }
                return false;

            case SkillCategory.Support:
                for (int c = 0; c < MaxCores; c++)
                {
                    for (int s = 0; s < SocketsPerCore; s++)
                    {
                        if (sockets[c, s] == null &&
                            TryEquipSupport(definition, c, s, returned))
                            return true;
                    }
                }
                return false;

            case SkillCategory.Meta:
                for (int i = 0; i < MaxMetas; i++)
                {
                    if (metas[i] == null && TryEquipMeta(definition, i, returned))
                        return true;
                }
                return false;

            case SkillCategory.Persistent:
                return herald == null && TryEquipHerald(definition, returned);

            default:
                return false;
        }
    }

    // ────────────────────────────────── 탈착

    /// <summary>
    /// Core를 뺀다. 그 Core의 소켓에 있던 Support도 전부 함께 빠진다.
    /// 뺀 것 전부가 returned에 담긴다.
    /// </summary>
    public SkillDefinition UnequipCore(int coreIndex, List<SkillDefinition> returned = null)
    {
        if (!IsValidCoreIndex(coreIndex) || cores[coreIndex] == null)
            return null;

        SkillDefinition removed = cores[coreIndex];
        cores[coreIndex] = null;

        returned?.Add(removed);

        for (int s = 0; s < SocketsPerCore; s++)
        {
            if (sockets[coreIndex, s] == null)
                continue;

            returned?.Add(sockets[coreIndex, s]);
            sockets[coreIndex, s] = null;
        }

        MarkChanged();

        return removed;
    }

    /// <summary>소켓에서 뺀다. 뺀 인자를 돌려준다.</summary>
    public SkillDefinition UnequipSupport(int coreIndex, int socketIndex)
    {
        if (!IsValidSocket(coreIndex, socketIndex) || sockets[coreIndex, socketIndex] == null)
            return null;

        SkillDefinition removed = sockets[coreIndex, socketIndex];
        sockets[coreIndex, socketIndex] = null;

        MarkChanged();

        return removed;
    }

    /// <summary>발동 슬롯에서 뺀다.</summary>
    public SkillDefinition UnequipMeta(int slot)
    {
        if (slot < 0 || slot >= MaxMetas || metas[slot] == null)
            return null;

        SkillDefinition removed = metas[slot];
        metas[slot] = null;

        MarkChanged();

        return removed;
    }

    /// <summary>전령을 뺀다.</summary>
    public SkillDefinition UnequipHerald()
    {
        if (herald == null)
            return null;

        SkillDefinition removed = herald;
        herald = null;

        MarkChanged();

        return removed;
    }

    /// <summary>전부 뺀다. 뺀 인자는 returned에 담긴다. 추출 정산에 쓴다.</summary>
    public void UnequipAll(List<SkillDefinition> returned = null)
    {
        for (int c = 0; c < MaxCores; c++)
            UnequipCore(c, returned);

        for (int i = 0; i < MaxMetas; i++)
        {
            SkillDefinition meta = UnequipMeta(i);

            if (meta != null)
                returned?.Add(meta);
        }

        SkillDefinition removedHerald = UnequipHerald();

        if (removedHerald != null)
            returned?.Add(removedHerald);
    }

    /// <summary>
    /// 런 종료 시 초기화한다. 【여기서 인자를 없애지 않는다.】
    /// 사망 시의 소멸은 Inventory.DropOnDeath가 담당한다. 규칙을 한 곳에만 둔다.
    /// </summary>
    public void Clear()
    {
        Array.Clear(cores, 0, cores.Length);
        Array.Clear(metas, 0, metas.Length);
        Array.Clear(sockets, 0, sockets.Length);

        herald = null;
        awakeningLevel = 1;
        capacity = SocketUnlockTable.Evaluate(1);

        MarkChanged();
    }

    // ────────────────────────────────── 효과 합산

    /// <summary>
    /// 상호 배타로 무효화된 상태인지. (긴 퓨즈 ↔ 짧은 퓨즈)
    /// 동시 장착 시 양쪽 모두 무효화된다. 장착 자체는 막지 않는다. (14절 2번)
    /// </summary>
    public bool IsNullified(SkillDefinition definition)
    {
        if (definition == null || !IsEquipped(definition))
            return false;

        IReadOnlyList<SkillDefinition> list = Equipped;

        for (int i = 0; i < list.Count; i++)
        {
            SkillDefinition other = list[i];

            if (other == definition)
                continue;

            if (definition.IsMutuallyExclusiveWith(other) || other.IsMutuallyExclusiveWith(definition))
                return true;
        }

        return false;
    }

    /// <summary>기능 배타로 이 상태를 유발할 수 없게 되었는지. (10-2 [3])</summary>
    public bool IsStatusBlocked(StatusEffectType status)
    {
        if (status == StatusEffectType.None)
            return false;

        IReadOnlyList<SkillDefinition> list = Equipped;

        for (int i = 0; i < list.Count; i++)
        {
            SkillDefinition definition = list[i];

            if (!definition.BlocksStatusCreation)
                continue;

            if (IsNullified(definition))
                continue;

            if (definition.BlockedStatus == status)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 합산된 무기 보정치. 변경이 없으면 이전 결과를 재사용하므로
    /// 매 발사마다 호출해도 GC Alloc이 발생하지 않는다.
    /// </summary>
    public WeaponModifiers GetModifiers()
    {
        if (!isDirty)
            return modifiers;

        modifiers.Reset();

        IReadOnlyList<SkillDefinition> list = Equipped;

        for (int i = 0; i < list.Count; i++)
        {
            SkillDefinition definition = list[i];

            if (IsNullified(definition))
                continue;

            modifiers.Apply(definition);

            // 합성 발사: 부여 계열 Core가 생성하는 상태를 탄에 싣는다.
            // 기폭 계열은 투사체가 아니므로 대상이 아니고,
            // 기능 배타로 차단된 상태(번제 → 점화)는 애초에 실리지 않는다.
            if (definition.Category == SkillCategory.Core &&
                definition.Family == CoreFamily.Ailment &&
                !IsStatusBlocked(definition.CreatesStatus))
            {
                modifiers.AddAilment(definition.CreatesStatus);
            }
        }

        isDirty = false;

        return modifiers;
    }

    // ────────────────────────────────── 내부

    private static bool IsValidCoreIndex(int coreIndex)
    {
        return coreIndex >= 0 && coreIndex < MaxCores;
    }

    private static bool IsValidSocket(int coreIndex, int socketIndex)
    {
        return IsValidCoreIndex(coreIndex) && socketIndex >= 0 && socketIndex < SocketsPerCore;
    }

    private void MarkChanged()
    {
        isDirty = true;
        equippedDirty = true;

        OnChanged?.Invoke();
    }

    private void RebuildEquippedIfNeeded()
    {
        if (!equippedDirty)
            return;

        equipped.Clear();

        for (int c = 0; c < MaxCores; c++)
        {
            if (cores[c] != null)
                equipped.Add(cores[c]);

            for (int s = 0; s < SocketsPerCore; s++)
            {
                if (sockets[c, s] != null)
                    equipped.Add(sockets[c, s]);
            }
        }

        for (int i = 0; i < MaxMetas; i++)
        {
            if (metas[i] != null)
                equipped.Add(metas[i]);
        }

        if (herald != null)
            equipped.Add(herald);

        equippedDirty = false;
    }
}
