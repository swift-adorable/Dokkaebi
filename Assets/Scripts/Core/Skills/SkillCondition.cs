/// <summary>
/// 조건부 Support가 보는 조건. (docs/Blob_Skill_System.md 「조건부 계열」)
///
/// 조건을 명중 시점에 판정하는 이유 —
/// 「멀리 있는 적일수록」과 「출혈 중인 적에게」는 발사 시점에 알 수 없다.
/// 대상과 거리가 정해지는 순간에만 답이 나온다.
/// </summary>
public enum SkillConditionKind
{
    None = 0,

    /// <summary>유효 사거리 절반을 넘는 거리. 사거리 보정이 걸리는 구간과 같다.</summary>
    FarTarget = 1,

    /// <summary>유효 사거리 절반 이내.</summary>
    NearTarget = 2,

    /// <summary>대상이 지정한 상태이상에 걸려 있다.</summary>
    TargetHasStatus = 3
}

/// <summary>
/// 조건 하나와 그때 더해지는 피해 증가율.
///
/// 구조체로 둔 이유 — 탄 하나가 여러 개를 들고 명중 때마다 훑는다.
/// 클래스면 발사마다 할당이 생긴다. (매 프레임 경로에서 GC Alloc 금지)
/// </summary>
public readonly struct SkillCondition
{
    public readonly SkillConditionKind Kind;
    public readonly StatusEffectType Status;
    public readonly float DamageIncrease;

    public SkillCondition(SkillConditionKind kind, StatusEffectType status, float damageIncrease)
    {
        Kind = kind;
        Status = status;
        DamageIncrease = damageIncrease;
    }

    public bool IsValid => Kind != SkillConditionKind.None && DamageIncrease != 0f;
}
