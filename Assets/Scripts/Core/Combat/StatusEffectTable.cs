/// <summary>
/// 상태이상 6종의 수치 표. (docs/Blob_Combat_Baseline.md 7절)
///
/// 초당 피해는 【부여 시점의 기본 피해】에 계수를 곱해 계산한다.
/// 상태이상 피해는 방어도를 무시하고 속성 상성만 받는다.
/// </summary>
public readonly struct StatusEffectSpec
{
    /// <summary>초당 피해 계수. 0이면 피해가 없는 상태다.</summary>
    public readonly float DamagePerSecondCoeff;

    /// <summary>지속 시간(초).</summary>
    public readonly float Duration;

    /// <summary>최대 중첩. 1이면 갱신형이다.</summary>
    public readonly int MaxStacks;

    /// <summary>해당 피해의 속성.</summary>
    public readonly DamageElement Element;

    public StatusEffectSpec(float coeff, float duration, int maxStacks, DamageElement element)
    {
        DamagePerSecondCoeff = coeff;
        Duration = duration;
        MaxStacks = maxStacks;
        Element = element;
    }

    public bool IsDamaging => DamagePerSecondCoeff > 0f;
}

public static class StatusEffectTable
{
    /// <summary>감전이 올려 주는 "받는 피해" 증가율.</summary>
    public const float ShockDamageTakenBonus = 0.2f;

    /// <summary>동결이 깎는 이동·공격 속도 비율.</summary>
    public const float FreezeSlowRatio = 0.4f;

    /// <summary>출혈이 이동 중인 대상에게 곱하는 배율.</summary>
    public const float BleedMovingMultiplier = 2f;

    /// <summary>응집이 넓히는 상태 전이 범위 배율.</summary>
    public const float CongealSpreadMultiplier = 2f;

    public static StatusEffectSpec Get(StatusEffectType type)
    {
        switch (type)
        {
            // 점화 — "한 발을 더 쏜 것"과 같은 총량(100%)을 4초에 걸쳐 준다.
            // 즉발과 도트의 총량을 맞추고, 차이는 방어도 무시와 지속 중 행동 가능으로 낸다.
            case StatusEffectType.Ignite:
                return new StatusEffectSpec(0.25f, 4f, 1, DamageElement.Fire);

            // 중독 — 유일하게 크게 중첩한다. 카오스가 "쌓아서 녹이는" 정체성을 갖는다.
            case StatusEffectType.Poison:
                return new StatusEffectSpec(0.15f, 6f, 10, DamageElement.Chaos);

            // 출혈 — 이동 중인 대상에게 2배. 추격형에 강하고 고정형에 약하다.
            // ※ 최대 중첩 5는 TBD. 문서에는 "중첩"이라고만 되어 있다.
            case StatusEffectType.Bleed:
                return new StatusEffectSpec(0.20f, 3f, 5, DamageElement.Physical);

            // 감전 — 피해가 없다. 증폭만 담당한다. (PoE2 문법)
            case StatusEffectType.Shock:
                return new StatusEffectSpec(0f, 6f, 1, DamageElement.Lightning);

            // 동결 — 피해가 없다. 통제만 담당한다.
            case StatusEffectType.Freeze:
                return new StatusEffectSpec(0f, 3f, 1, DamageElement.Cold);

            // 응집 — 피해가 없다. 상태 전이 범위만 넓힌다.
            case StatusEffectType.Congeal:
                return new StatusEffectSpec(0f, 1.5f, 1, DamageElement.Physical);

            default:
                return new StatusEffectSpec(0f, 0f, 1, DamageElement.Physical);
        }
    }
}
