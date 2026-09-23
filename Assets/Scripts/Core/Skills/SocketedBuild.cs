using System;
using System.Collections.Generic;

/// <summary>
/// 지금 소켓에 끼워져 있는 젬들. RunSkillState를 대체한다.
/// (docs/Blob_Skill_System.md 11·12절)
///
/// RunSkillState와의 차이 —
///   · 「획득」이 없다. 줍는 것은 가방이 하고, 여기는 【끼운 것】만 안다.
///   · 자리를 지정해 끼운다. 자동 배치를 하지 않는다.
///   · 뺄 수 있다. 뺀 젬은 사라지지 않고 호출부(가방)로 돌아간다.
///   · 슬롯 수는 각성 레벨이 정한다. (SocketUnlockTable)
///
/// 뺀 젬을 여기서 버리지 않고 호출부에 돌려주는 이유 —
/// 젬은 실물 아이템이므로 조작 도중에 소멸하면 안 된다.
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

    /// <summary>끼워진 젬 전부. 순서는 핵심 젬 → 소켓 → 발동 → 전령.</summary>
    public IReadOnlyList<SkillDefinition> Equipped
    {
        get
        {
            RebuildEquippedIfNeeded();
            return equipped;
        }
    }

    public int EquippedCount => Equipped.Count;

    /// <summary>공격 수단이 하나라도 있는지. 핵심 젬이 없으면 아무것도 쏘지 못한다.</summary>
    public bool HasCore => cores[0] != null || cores[1] != null;

    /// <summary>장착된 전령. 없으면 null.</summary>
    public SkillDefinition Herald => herald;

    /// <summary>지정한 핵심 젬 슬롯의 젬. 비었으면 null.</summary>
    public SkillDefinition GetCore(int coreIndex)
    {
        return IsValidCoreIndex(coreIndex) ? cores[coreIndex] : null;
    }

    /// <summary>지정한 소켓의 젬. 비었으면 null.</summary>
    public SkillDefinition GetSocket(int coreIndex, int socketIndex)
    {
        return IsValidSocket(coreIndex, socketIndex) ? sockets[coreIndex, socketIndex] : null;
    }

    /// <summary>지정한 발동 슬롯의 젬. 비었으면 null.</summary>
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
    /// 레벨은 출격 중에 내려가지 않으므로 축소에 따른 탈착은 다루지 않는다.
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

    /// <summary>이 젬을 이 핵심 젬 슬롯에 끼울 수 있는지.</summary>
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

        // 같은 핵심 젬을 두 자리에 넣는 것은 수치 중첩이므로 막는다. (14절 5번)
        for (int c = 0; c < MaxCores; c++)
        {
            if (c != coreIndex && cores[c] == definition)
                return SocketError.Duplicate;
        }

        return SocketError.None;
    }

    /// <summary>이 젬을 이 소켓에 끼울 수 있는지.</summary>
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

        // 태그 조건. 핵심 젬이 요구 태그를 전부 가져야 한다. (10-2 [1])
        if (!cores[coreIndex].Tags.ContainsAll(definition.RequiredTags))
            return SocketError.TagMismatch;

        // 같은 핵심 젬 안의 중복만 막는다.
        // 다른 핵심 젬에 같은 보조 젬을 하나씩 끼우는 것은 11-3절이 명시적으로 허용한다.
        for (int s = 0; s < SocketsPerCore; s++)
        {
            if (s != socketIndex && sockets[coreIndex, s] == definition)
                return SocketError.Duplicate;
        }

        return SocketError.None;
    }

    /// <summary>이 젬을 이 발동 슬롯에 끼울 수 있는지.</summary>
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

    /// <summary>이 젬을 전령 자리에 끼울 수 있는지.</summary>
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
    /// UI가 "이 젬을 지금 어디든 끼울 수 있는가"를 물을 때 쓴다.
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

    /// <summary>두 결과 중 플레이어에게 더 희망적인 쪽. None이면 무조건 None.</summary>
    private static SocketError Better(SocketError a, SocketError b)
    {
        if (a == SocketError.None || b == SocketError.None)
            return SocketError.None;

        // 자리가 잠긴 것은 "레벨을 올리면 된다"는 안내가 되므로 NoSuchSlot보다 낫다.
        return a == SocketError.NoSuchSlot ? b : a;
    }

    // ────────────────────────────────── 장착

    /// <summary>
    /// 핵심 젬 슬롯에 끼운다. 원래 있던 핵심 젬과, 새 핵심 젬의 태그를 만족하지 못하게 된
    /// 보조 젬이 <paramref name="returned"/>에 담겨 돌아간다. 호출부가 가방에 되돌린다.
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

        // 핵심 젬이 바뀌면 태그 조건을 다시 통과하지 못하는 보조 젬이 생긴다.
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

    /// <summary>소켓에 끼운다. 원래 있던 보조 젬이 returned에 담겨 돌아간다.</summary>
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
    /// 자리가 둘 이상 가능하면 플레이어가 직접 고르는 경로를 따로 둔다.
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
    /// 핵심 젬을 뺀다. 그 핵심 젬의 소켓에 있던 보조 젬도 전부 함께 빠진다.
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

    /// <summary>소켓에서 뺀다. 뺀 젬을 돌려준다.</summary>
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

    /// <summary>전부 뺀다. 뺀 젬은 returned에 담긴다. 추출 정산에 쓴다.</summary>
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
    /// 출격 종료 시 초기화한다. 【여기서 젬을 없애지 않는다.】
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

    /// <summary>
    /// 【그 핵심 젬이】 이 상태를 유발할 수 없게 되었는지.
    ///
    /// 차단 범위를 핵심 젬 하나로 좁힌 이유 —
    /// 빌드 전역으로 막으면 배타형 보조 젬이 구조적으로 영구 무효가 된다.
    /// 「번제」는 화염 태그를 요구하므로 화염 핵심 젬에만 끼울 수 있는데,
    /// 전역 차단이면 그 화염 핵심 젬의 점화까지 꺼져 조건(점화된 적)이 영원히 성립하지 않는다.
    /// 「점화를 유발할 수 없지만 점화된 적에게 큰 피해」라는 정체성 자체가 불가능해진다.
    ///
    /// 핵심 젬 단위로 좁히면 설계가 성립한다 —
    /// 【자기 핵심 젬은 못 걸고, 다른 발생원이 걸어 준 것을 이용한다.】
    /// 다른 발생원은 2번째 핵심 젬(Lv7)이거나 「화염 조율」 같은 속성 전환이다.
    /// 그래서 문서의 「단독으로는 전혀 작동하지 않는다」가 그대로 유지된다.
    /// (docs/Blob_Audit.md D3)
    /// </summary>
    public bool IsStatusBlockedForCore(int coreIndex, StatusEffectType status)
    {
        if (status == StatusEffectType.None || !IsValidCoreIndex(coreIndex))
            return false;

        // 핵심 젬 자신이 막는 경우.
        SkillDefinition core = cores[coreIndex];

        if (core != null && core.BlocksStatusCreation
            && core.BlockedStatus == status && !IsNullified(core))
        {
            return true;
        }

        // 그 핵심 젬의 소켓에 꽂힌 보조 젬만 본다. 다른 핵심 젬의 소켓은 상관없다.
        for (int s = 0; s < SocketsPerCore; s++)
        {
            SkillDefinition support = sockets[coreIndex, s];

            if (support == null || !support.BlocksStatusCreation)
                continue;

            if (IsNullified(support))
                continue;

            if (support.BlockedStatus == status)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 빌드 전체에서 이 상태를 만들 수 있는 핵심 젬이 하나도 없는지.
    /// UI가 「이 젬은 지금 아무 일도 하지 않습니다」를 알릴 때 쓴다.
    /// </summary>
    public bool IsStatusUnavailable(StatusEffectType status)
    {
        if (status == StatusEffectType.None)
            return false;

        for (int c = 0; c < MaxCores; c++)
        {
            if (EffectiveAilmentOf(c) == status)
                return false;

            if (AddedAilmentOf(c) == status)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 그 핵심 젬이 실제로 부여하는 상태. 속성 전환 보조 젬이 있으면 바뀐다.
    /// 차단되었거나 부여 계열이 아니면 None.
    /// </summary>
    public StatusEffectType EffectiveAilmentOf(int coreIndex)
    {
        if (!IsValidCoreIndex(coreIndex))
            return StatusEffectType.None;

        SkillDefinition core = cores[coreIndex];

        if (core == null || core.Category != SkillCategory.Core
            || core.Family != CoreFamily.Ailment || IsNullified(core))
        {
            return StatusEffectType.None;
        }

        StatusEffectType status = core.CreatesStatus;

        // 속성 전환 — 그 핵심 젬의 부여 속성 자체를 바꾼다.
        for (int s = 0; s < SocketsPerCore; s++)
        {
            SkillDefinition support = sockets[coreIndex, s];

            if (support == null || IsNullified(support))
                continue;

            if (support.AilmentOverride != StatusEffectType.None)
                status = support.AilmentOverride;
        }

        return IsStatusBlockedForCore(coreIndex, status) ? StatusEffectType.None : status;
    }

    /// <summary>
    /// 그 핵심 젬이 추가로 부여하는 2차 상태. 「원소 융합」이 만든다.
    /// 없으면 None.
    /// </summary>
    public StatusEffectType AddedAilmentOf(int coreIndex)
    {
        if (!IsValidCoreIndex(coreIndex))
            return StatusEffectType.None;

        SkillDefinition core = cores[coreIndex];

        if (core == null || core.Family != CoreFamily.Ailment || IsNullified(core))
            return StatusEffectType.None;

        for (int s = 0; s < SocketsPerCore; s++)
        {
            SkillDefinition support = sockets[coreIndex, s];

            if (support == null || IsNullified(support))
                continue;

            StatusEffectType added = support.AilmentAddition;

            if (added != StatusEffectType.None
                && !IsStatusBlockedForCore(coreIndex, added))
            {
                return added;
            }
        }

        return StatusEffectType.None;
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
        }

        // 합성 발사: 부여 계열 핵심 젬이 생성하는 상태를 탄에 싣는다.
        // 핵심 젬 단위로 계산한다 — 속성 전환과 기능 배타가 둘 다 핵심 젬별이기 때문이다.
        for (int c = 0; c < MaxCores; c++)
        {
            modifiers.AddAilment(EffectiveAilmentOf(c));
            modifiers.AddAilment(AddedAilmentOf(c));
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
