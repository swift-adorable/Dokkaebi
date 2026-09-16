using UnityEngine;

/// <summary>
/// 보유한 Mutation을 모두 합산한 결과. 무기와 투사체가 이 값을 읽어 동작한다.
///
/// 순수 클래스이므로 합산 규칙을 단위 테스트로 검증할 수 있다.
/// </summary>
public class WeaponModifiers
{
    /// <summary>충돌 행동별 총 횟수.</summary>
    public ProjectileBehaviourState Behaviours;

    /// <summary>기본 1발에 더해지는 동시 발사 수.</summary>
    public int ExtraProjectiles { get; private set; }

    /// <summary>추가 투사체 간 각도(도). 여러 Mutation이 있으면 가장 큰 값을 쓴다.</summary>
    public float SpreadAngle { get; private set; }

    /// <summary>발사 간격 배수. 1보다 크면 느려진다.</summary>
    public float FireIntervalMultiplier { get; private set; } = 1f;

    /// <summary>투사체 수명 배수.</summary>
    public float LifetimeMultiplier { get; private set; } = 1f;

    /// <summary>투사체 속도 배수.</summary>
    public float SpeedMultiplier { get; private set; } = 1f;

    /// <summary>총 동시 발사 수. 최소 1발은 보장한다.</summary>
    public int TotalProjectiles => 1 + Mathf.Max(0, ExtraProjectiles);

    public void Reset()
    {
        Behaviours.Clear();

        ExtraProjectiles = 0;
        SpreadAngle = 0f;
        FireIntervalMultiplier = 1f;
        LifetimeMultiplier = 1f;
        SpeedMultiplier = 1f;
    }

    /// <summary>Mutation 하나를 지정한 중첩 수만큼 합산한다.</summary>
    public void Apply(MutationDefinition definition, int stacks)
    {
        if (definition == null || stacks <= 0)
            return;

        if (definition.GrantedBehaviour != ProjectileBehaviourType.None)
        {
            int current = Behaviours.GetRemaining(definition.GrantedBehaviour);

            Behaviours.SetRemaining(
                definition.GrantedBehaviour,
                current + definition.ChargesPerStack * stacks);
        }

        ExtraProjectiles += definition.ExtraProjectilesPerStack * stacks;

        if (definition.ExtraProjectilesPerStack > 0)
            SpreadAngle = Mathf.Max(SpreadAngle, definition.SpreadAngle);

        // 비용은 중첩마다 곱해진다. 누적될수록 대가가 커진다.
        FireIntervalMultiplier *= Mathf.Pow(definition.FireIntervalMultiplier, stacks);
        LifetimeMultiplier *= Mathf.Pow(definition.LifetimeMultiplier, stacks);
        SpeedMultiplier *= Mathf.Pow(definition.SpeedMultiplier, stacks);
    }
}
