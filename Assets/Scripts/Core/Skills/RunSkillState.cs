using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 런(레이드) 동안 실제로 획득·장착된 스킬의 상태. (v5 §10-9)
///
/// SkillInventory(중첩 모델)를 대체한다. v5에는 중첩 개념이 없다.
/// 획득한 스킬는 해당 런의 풀에서 제거되므로 같은 스킬를 두 번 얻을 수 없다.
///
/// MonoBehaviour에 의존하지 않는 순수 클래스다. EditMode 테스트 대상이다.
/// </summary>
public class RunSkillState
{
    /// <summary>Core 동시 보유 상한. (v5 §10-9)</summary>
    public const int MaxCores = 2;

    /// <summary>
    /// 2번째 Core 슬롯이 열리는 레벨. (확정 기획)
    ///
    /// v5 §10-9의 "Core 보유 상한 2개"는 상한이지 즉시 지급 보장이 아니다.
    /// 초반에는 메커니즘 하나만 익히게 하고, 단일 Core 빌드를 충분히 완성한 뒤
    /// 확장하도록 2번째 슬롯을 중반에 연다. 상한 2개는 그대로이므로 규칙 위반이 아니다.
    /// </summary>
    public const int SecondCoreUnlockLevel = 7;

    /// <summary>Core 1개당 소켓 수.</summary>
    public const int SocketsPerCore = 3;

    /// <summary>기원형 Meta 동시 장착 상한.</summary>
    public const int MaxInvocations = 1;

    /// <summary>기본 Nucleus 상한. 추출 성공 누적으로 증가한다.</summary>
    public const int BaseNucleus = 100;

    private readonly List<SkillDefinition> acquired = new();
    private readonly List<SkillDefinition> cores = new();
    private readonly List<List<SkillDefinition>> sockets = new();
    private readonly List<SkillDefinition> metas = new();
    private readonly List<SkillDefinition> persistents = new();

    private readonly WeaponModifiers modifiers = new();

    private int nucleusCapacity = BaseNucleus;
    private int coreCapacity = MaxCores;
    private bool isDirty = true;

    /// <summary>보유 구성이 바뀌었을 때 발행된다.</summary>
    public event Action OnChanged;

    public IReadOnlyList<SkillDefinition> Acquired => acquired;
    public IReadOnlyList<SkillDefinition> Cores => cores;
    public IReadOnlyList<SkillDefinition> Metas => metas;
    public IReadOnlyList<SkillDefinition> Persistents => persistents;

    public int AcquiredCount => acquired.Count;

    /// <summary>
    /// 지금 보유할 수 있는 Core 수. 레벨에 따라 1 또는 2가 된다.
    /// SkillManager가 선택창을 열기 전에 GetCoreCapacity로 갱신한다.
    /// </summary>
    public int CoreCapacity
    {
        get => coreCapacity;
        set => coreCapacity = Mathf.Clamp(value, 1, MaxCores);
    }

    /// <summary>해당 레벨에서 보유 가능한 Core 수.</summary>
    public static int GetCoreCapacity(int playerLevel)
    {
        return playerLevel >= SecondCoreUnlockLevel ? MaxCores : 1;
    }

    /// <summary>2번째 Core 슬롯이 아직 잠겨 있는지. UI가 안내 문구를 띄우는 데 쓴다.</summary>
    public bool IsSecondCoreLocked => coreCapacity < MaxCores;

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
    public SkillDefinition Invocation
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

    public bool Has(SkillDefinition definition)
    {
        return definition != null && acquired.Contains(definition);
    }

    /// <summary>지정한 Core에 장착된 Support 목록.</summary>
    public IReadOnlyList<SkillDefinition> GetSockets(int coreIndex)
    {
        if (coreIndex < 0 || coreIndex >= sockets.Count)
            return Array.Empty<SkillDefinition>();

        return sockets[coreIndex];
    }

    /// <summary>
    /// 태그 게이팅 판정. 이 Support를 받아 줄 Core의 인덱스를 찾는다. 없으면 -1.
    ///
    /// Core가 Support의 requiredTags를 전부 포함하고 빈 소켓이 있어야 한다. (10-2 [1])
    /// </summary>
    public int FindSocketFor(SkillDefinition support)
    {
        if (support == null || support.Category != SkillCategory.Support)
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

    /// <summary>
    /// 이 Support를 받아 줄 수 있는 모든 Core 인덱스를 모은다.
    ///
    /// 두 Core가 모두 조건을 만족하면 어디에 넣을지는 유저가 정해야 한다.
    /// 소켓 탈착 비용을 없앤 대신, 장착 시점의 선택을 되돌릴 수 없게 만들어
    /// 결정의 무게를 유지한다. (확정 기획)
    /// </summary>
    public List<int> GetEligibleCoreIndices(SkillDefinition support, List<int> result = null)
    {
        result ??= new List<int>(MaxCores);
        result.Clear();

        if (support == null || support.Category != SkillCategory.Support)
            return result;

        for (int i = 0; i < cores.Count; i++)
        {
            if (sockets[i].Count >= SocketsPerCore)
                continue;

            if (!cores[i].Tags.ContainsAll(support.RequiredTags))
                continue;

            result.Add(i);
        }

        return result;
    }

    /// <summary>획득 가능한지. 선택 풀 필터가 이 판정을 그대로 쓴다.</summary>
    public bool CanAcquire(SkillDefinition definition)
    {
        if (definition == null)
            return false;

        // v5 §10-9: 획득한 것은 해당 런의 풀에서 제거된다. 중첩 개념이 없다.
        if (acquired.Contains(definition))
            return false;

        switch (definition.Category)
        {
            case SkillCategory.Core:
                return cores.Count < coreCapacity;

            case SkillCategory.Support:
                return FindSocketFor(definition) >= 0;

            case SkillCategory.Meta:
                // 기원형은 동시에 1개만. 자동 발동형은 제한이 없다.
                return !definition.IsInvocation || Invocation == null;

            case SkillCategory.Persistent:
                return definition.NucleusCost <= NucleusRemaining;

            default:
                return false;
        }
    }

    /// <summary>
    /// 획득한다. 불가능하면 false를 돌려주고 상태를 바꾸지 않는다.
    ///
    /// preferredCoreIndex는 Support를 어느 Core 소켓에 넣을지 지정한다.
    /// -1이거나 조건을 만족하지 않으면 자동으로 첫 번째 가능한 Core에 넣는다.
    /// </summary>
    public bool TryAcquire(SkillDefinition definition, int preferredCoreIndex = -1)
    {
        if (!CanAcquire(definition))
            return false;

        switch (definition.Category)
        {
            case SkillCategory.Core:
                cores.Add(definition);
                sockets.Add(new List<SkillDefinition>(SocketsPerCore));
                break;

            case SkillCategory.Support:
                sockets[ResolveSocketIndex(definition, preferredCoreIndex)].Add(definition);
                break;

            case SkillCategory.Meta:
                metas.Add(definition);
                break;

            case SkillCategory.Persistent:
                persistents.Add(definition);
                break;
        }

        acquired.Add(definition);

        isDirty = true;

        OnChanged?.Invoke();

        return true;
    }

    /// <summary>지정한 소켓이 유효하면 그것을, 아니면 자동 선택 결과를 돌려준다.</summary>
    private int ResolveSocketIndex(SkillDefinition support, int preferredCoreIndex)
    {
        if (preferredCoreIndex >= 0 &&
            preferredCoreIndex < cores.Count &&
            sockets[preferredCoreIndex].Count < SocketsPerCore &&
            cores[preferredCoreIndex].Tags.ContainsAll(support.RequiredTags))
        {
            return preferredCoreIndex;
        }

        return FindSocketFor(support);
    }

    /// <summary>
    /// 상호 배타로 무효화된 상태인지. (v5 §7-4 긴 퓨즈 ↔ 짧은 퓨즈)
    ///
    /// 동시 장착 시 양쪽 모두 무효화된다. 획득 자체는 막지 않는다.
    /// 중복 금지로 다양성을 강제하지 않는다는 10-1-2 원칙 때문이다.
    /// </summary>
    public bool IsNullified(SkillDefinition definition)
    {
        if (definition == null || !acquired.Contains(definition))
            return false;

        for (int i = 0; i < acquired.Count; i++)
        {
            SkillDefinition other = acquired[i];

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
            SkillDefinition definition = acquired[i];

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
            SkillDefinition definition = acquired[i];

            // 무효화된 스킬는 효과를 내지 않는다.
            if (IsNullified(definition))
                continue;

            modifiers.Apply(definition);

            // 합성 발사: 적재 계열 Core가 생성하는 상태를 탄에 싣는다. (확정 기획)
            // 기폭 계열은 투사체가 아니므로 합성 대상이 아니고,
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

        coreCapacity = MaxCores;
        isDirty = true;

        OnChanged?.Invoke();
    }
}
