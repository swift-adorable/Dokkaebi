using UnityEngine;

/// <summary>
/// 한 발의 투사체가 가진 행동 잔여 횟수.
///
/// 충돌 때마다 ConsumeNext()를 호출하면 우선순위에 따라 '하나만' 소비되고
/// 그 행동이 반환된다. 이 규칙이 Skill 조합의 핵심이다.
///
/// 예: Fork와 Chain을 둘 다 가진 투사체는 첫 충돌에서 Fork만 발동한다.
///     분열된 투사체가 다음 충돌에서 Chain을 쓴다. 곱해지지 않고 단계가 된다.
/// </summary>
[System.Serializable]
public struct ProjectileBehaviourState
{
    public int SplitRemaining;
    public int PierceRemaining;
    public int ForkRemaining;
    public int ChainRemaining;
    public int ReturnRemaining;

    /// <summary>남은 행동이 하나라도 있는지.</summary>
    public bool HasAny =>
        SplitRemaining > 0 || PierceRemaining > 0 ||
        ForkRemaining > 0 || ChainRemaining > 0 || ReturnRemaining > 0;

    /// <summary>남은 행동 총합.</summary>
    public int TotalRemaining =>
        Mathf.Max(0, SplitRemaining) + Mathf.Max(0, PierceRemaining) +
        Mathf.Max(0, ForkRemaining) + Mathf.Max(0, ChainRemaining) +
        Mathf.Max(0, ReturnRemaining);

    /// <summary>특정 행동의 남은 횟수를 조회한다.</summary>
    public int GetRemaining(ProjectileBehaviourType type)
    {
        return type switch
        {
            ProjectileBehaviourType.Split => SplitRemaining,
            ProjectileBehaviourType.Pierce => PierceRemaining,
            ProjectileBehaviourType.Fork => ForkRemaining,
            ProjectileBehaviourType.Chain => ChainRemaining,
            ProjectileBehaviourType.Return => ReturnRemaining,
            _ => 0
        };
    }

    /// <summary>특정 행동의 남은 횟수를 설정한다. 음수는 0으로 보정한다.</summary>
    public void SetRemaining(ProjectileBehaviourType type, int value)
    {
        value = Mathf.Max(0, value);

        switch (type)
        {
            case ProjectileBehaviourType.Split: SplitRemaining = value; break;
            case ProjectileBehaviourType.Pierce: PierceRemaining = value; break;
            case ProjectileBehaviourType.Fork: ForkRemaining = value; break;
            case ProjectileBehaviourType.Chain: ChainRemaining = value; break;
            case ProjectileBehaviourType.Return: ReturnRemaining = value; break;
        }
    }

    /// <summary>
    /// 우선순위가 가장 높은 행동 하나를 소비하고 반환한다.
    /// 남은 행동이 없으면 None을 반환하며, 이 경우 투사체는 소멸해야 한다.
    /// </summary>
    public ProjectileBehaviourType ConsumeNext()
    {
        if (SplitRemaining > 0)
        {
            SplitRemaining--;
            return ProjectileBehaviourType.Split;
        }

        if (PierceRemaining > 0)
        {
            PierceRemaining--;
            return ProjectileBehaviourType.Pierce;
        }

        if (ForkRemaining > 0)
        {
            ForkRemaining--;
            return ProjectileBehaviourType.Fork;
        }

        if (ChainRemaining > 0)
        {
            ChainRemaining--;
            return ProjectileBehaviourType.Chain;
        }

        if (ReturnRemaining > 0)
        {
            ReturnRemaining--;
            return ProjectileBehaviourType.Return;
        }

        return ProjectileBehaviourType.None;
    }

    /// <summary>
    /// 분열로 생성되는 자식 투사체가 물려받을 상태.
    /// Fork는 이미 소비되었으므로 부모의 남은 횟수가 그대로 복제된다.
    /// </summary>
    public ProjectileBehaviourState CreateChildState() => this;

    /// <summary>모든 잔여 횟수를 0으로 만든다.</summary>
    public void Clear()
    {
        SplitRemaining = 0;
        PierceRemaining = 0;
        ForkRemaining = 0;
        ChainRemaining = 0;
        ReturnRemaining = 0;
    }
}
