using System;
using System.Collections.Generic;

/// <summary>
/// 한 런(레이드) 동안 실제로 획득·장착된 변이의 상태. (v5 §10-9)
///
/// MutationInventory(중첩 모델)를 대체한다. v5에는 중첩 개념이 없다.
/// 획득한 변이는 해당 런의 풀에서 제거되므로 같은 변이를 두 번 얻을 수 없다.
///
/// MonoBehaviour에 의존하지 않는 순수 클래스다. EditMode 테스트 대상이다.
/// </summary>
public class RunMutationState
{
    /// <summary>Core 동시 보유 상한.</summary>
    public const int MaxCores = 2;

    /// <summary>Core 1개당 소켓 수.</summary>
    public const int SocketsPerCore = 3;

    /// <summary>기원형 Meta 동시 장착 상한.</summary>
    public const int MaxInvocations = 1;

    /// <summary>기본 Nucleus 상한. 추출 성공 누적으로 증가한다.</summary>
    public const int BaseNucleus = 100;

    private readonly List<MutationDefinition> acquired = new();
    private readonly List<MutationDefinition> cores = new();
    private readonly List<List<MutationDefinition>> sockets = new();
    private readonly List<MutationDefinition> metas = new();
    private readonly List<MutationDefinition> persistents = new();

    private readonly WeaponModifiers modifiers = new();

    private int nucleusCapacity = BaseNucleus;
    private bool isDirty = true;

    /// <summary>보유 구성이 바뀌었을 때 발행된다.</summary>
    public event Action OnChanged;

    public IReadOnlyList<MutationDefinition> Acquired => acquired;
    public IReadOnlyList<MutationDefinition> Cores => cores;
    public IReadOnlyList<MutationDefinition> Metas => metas;
    public IReadOnlyList<MutationDefinition> Persistents => persistents;

    public int AcquiredCount => acquired.Count;

    /// <summary>Nucleus 상한. 영구 성장 3축 중 하나다.</summary>
    public int NucleusCapacity
    {
        get => nucleusCapacity;
        set => nucleusCapacity = value < 0 ? 0 : value;
    }

    public int NucleusSpent
    {
        get
        {
            int total = 0;

            for (int i = 0; i < persistents.Count; i++)
                total += persistents[i].NucleusCost;

            return total;
        }
    }

    public int NucleusRemaining => nucleusCapacity - NucleusSpent;

    /// <summary>장착된 기원형. 없으면 null.</summary>
    public MutationDefinition Invocation
    {
        get
        {
            for (int i = 0; i < metas.Count; i++)
            {
                if (metas[i].IsInvocation)
                    return metas[i];
            }

            return null;
        }
    }

    /// <summary>비어 있는 소켓의 총 개수.</summary>
    public int FreeSocketCount
    {
        get
        {
            int free = 0;

            for (int i = 0; i < sockets.Count; i++)
                free += SocketsPerCore - sockets[i].Count;

            return free;
        }
    }

    public bool Has(MutationDefinition definition)
    {
        return definition != null && acquired.Contains(definition);
    }

    /// <summary>지정한 Core에 장착된 Support 목록.</summary>
    public IReadOnlyList<MutationDefinition> GetSockets(int coreIndex)
    {
        if (coreIndex < 0 || coreIndex >= sockets.Count)
            return Array.Empty<MutationDefinition>();

        return sockets[coreIndex];
    }

    /// <summary>
    /// 태그 게이팅 판정. 이 Support를 받아 줄 Core의 인덱스를 찾는다. 없으면 -1.
    ///
    /// Core가 Support의 requiredTags를 전부 포함하고 빈 소켓이 있어야 한다. (10-2 [1])
    /// </summary>
    public int FindSocketFor(MutationDefinition support)
    {
        if (support == null || support.Category != MutationCategory.Support)
            return -1;

        for (int i = 0; i < cores.Count; i++)
        {
            if (sockets[i].Count >= SocketsPerCore)
                continue;

            if (!cores[i].Tags.ContainsAll(support.RequiredTags))
                continue;

            return i;
        }

        return -1;
    }

    /// <summary>획득 가능한지. 선택 풀 필터가 이 판정을 그대로 쓴다.</summary>
    public bool CanAcquire(MutationDefinition definition)
    {
        if (definition == null)
            return false;

        // v5 §10-9: 획득한 것은 해당 런의 풀에서 제거된다. 중첩 개념이 없다.
        if (acquired.Contains(definition))
            return false;

        switch (definition.Category)
        {
            case MutationCategory.Core:
                return cores.Count < MaxCores;

            case MutationCategory.Support:
                return FindSocketFor(definition) >= 0;

            case MutationCategory.Meta:
                // 기원형은 동시에 1개만. 자동 발동형은 제한이 없다.
                return !definition.IsInvocation || Invocation == null;

            case MutationCategory.Persistent:
                return definition.NucleusCost <= NucleusRemaining;

            default:
                return false;
        }
    }

    /// <summary>획득한다. 불가능하면 false를 돌려주고 상태를 바꾸지 않는다.</summary>
    public bool TryAcquire(MutationDefinition definition)
    {
        if (!CanAcquire(definition))
            return false;

        switch (definition.Category)
        {
            case MutationCategory.Core:
                cores.Add(definition);
                sockets.Add(new List<MutationDefinition>(SocketsPerCore));
                break;

            case MutationCategory.Support:
                sockets[FindSocketFor(definition)].Add(definition);
                break;

            case MutationCategory.Meta:
                metas.Add(definition);
                break;

            case MutationCategory.Persistent:
                persistents.Add(definition);
                break;
        }

        acquired.Add(definition);

        isDirty = true;

        OnChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// 상호 배타로 무효화된 상태인지. (v5 §7-4 긴 퓨즈 ↔ 짧은 퓨즈)
    ///
    /// 동시 장착 시 양쪽 모두 무효화된다. 획득 자체는 막지 않는다.
    /// 중복 금지로 다양성을 강제하지 않는다는 10-1-2 원칙 때문이다.
    /// </summary>
    public bool IsNullified(MutationDefinition definition)
    {
        if (definition == null || !acquired.Contains(definition))
            return false;

        for (int i = 0; i < acquired.Count; i++)
        {
            MutationDefinition other = acquired[i];

            if (other == definition)
                continue;

            if (definition.IsMutuallyExclusiveWith(other) || other.IsMutuallyExclusiveWith(definition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 기능 배타로 이 상태를 유발할 수 없게 되었는지. (10-2 [3])
    /// 번제를 들면 점화를 유발할 수 없게 되는 식이다.
    /// </summary>
    public bool IsStatusBlocked(StatusEffectType status)
    {
        if (status == StatusEffectType.None)
            return false;

        for (int i = 0; i < acquired.Count; i++)
        {
            MutationDefinition definition = acquired[i];

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

        for (int i = 0; i < acquired.Count; i++)
        {
            MutationDefinition definition = acquired[i];

            // 무효화된 변이는 효과를 내지 않는다.
            if (IsNullified(definition))
                continue;

            modifiers.Apply(definition);
        }

        isDirty = false;

        return modifiers;
    }

    /// <summary>런 종료 시 전부 초기화한다. 도감과 적재 구성만 남는다. (v5 §1-5)</summary>
    public void Clear()
    {
        if (acquired.Count == 0)
        {
            isDirty = true;
            return;
        }

        acquired.Clear();
        cores.Clear();
        sockets.Clear();
        metas.Clear();
        persistents.Clear();

        isDirty = true;

        OnChanged?.Invoke();
    }
}
