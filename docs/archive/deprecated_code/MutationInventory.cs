using System;
using System.Collections.Generic;

/// <summary>
/// 플레이어가 보유한 Mutation과 중첩 수.
///
/// MonoBehaviour에 의존하지 않으므로 합산 규칙과 중첩 상한을 단위 테스트로 검증할 수 있다.
/// </summary>
public class MutationInventory
{
    private readonly Dictionary<MutationDefinition, int> stacks = new();
    private readonly WeaponModifiers modifiers = new();

    private bool isDirty = true;

    /// <summary>보유 목록이 바뀌었을 때 발행된다.</summary>
    public event Action OnChanged;

    /// <summary>보유한 Mutation 종류 수.</summary>
    public int UniqueCount => stacks.Count;

    /// <summary>모든 중첩의 총합.</summary>
    public int TotalStacks
    {
        get
        {
            int total = 0;

            foreach (int value in stacks.Values)
                total += value;

            return total;
        }
    }

    public IReadOnlyDictionary<MutationDefinition, int> Stacks => stacks;

    public int GetStacks(MutationDefinition definition)
    {
        if (definition == null)
            return 0;

        return stacks.TryGetValue(definition, out int value) ? value : 0;
    }

    /// <summary>중첩 상한에 도달하지 않아 더 얻을 수 있는지.</summary>
    public bool CanAdd(MutationDefinition definition)
    {
        if (definition == null)
            return false;

        return GetStacks(definition) < definition.MaxStacks;
    }

    /// <summary>1중첩 추가한다. 상한에 도달했거나 null이면 false.</summary>
    public bool Add(MutationDefinition definition)
    {
        if (!CanAdd(definition))
            return false;

        stacks[definition] = GetStacks(definition) + 1;

        isDirty = true;

        OnChanged?.Invoke();

        return true;
    }

    /// <summary>보유 내역을 모두 비운다. 런 종료 시 호출한다.</summary>
    public void Clear()
    {
        if (stacks.Count == 0)
            return;

        stacks.Clear();

        isDirty = true;

        OnChanged?.Invoke();
    }

    /// <summary>
    /// 합산된 무기 보정치를 반환한다.
    /// 변경이 없으면 이전 결과를 재사용하므로 매 발사마다 호출해도 안전하다.
    /// </summary>
    public WeaponModifiers GetModifiers()
    {
        if (!isDirty)
            return modifiers;

        modifiers.Reset();

        foreach (KeyValuePair<MutationDefinition, int> pair in stacks)
            modifiers.Apply(pair.Key, pair.Value);

        isDirty = false;

        return modifiers;
    }
}
