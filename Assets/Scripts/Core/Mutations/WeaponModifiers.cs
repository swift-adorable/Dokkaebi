using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 획득한 Mutation을 모두 합산한 결과. 무기와 투사체가 이 값을 읽어 동작한다.
///
/// 순수 클래스이므로 합산 규칙을 단위 테스트로 검증할 수 있다.
///
/// ※ v5에는 중첩(stack)이 없으므로 중첩 배수 연산을 제거했다.
///    같은 변이를 두 번 얻을 수 없다. (10-9)
/// </summary>
public class WeaponModifiers
{
    /// <summary>충돌 행동별 총 횟수. Split → Pierce → Fork → Chain → Return</summary>
    public ProjectileBehaviourState Behaviours;

    /// <summary>
    /// 지형 충돌 튕김 횟수. 「튕겨 쏘기」 계열.
    /// 충돌 우선순위 큐와 별개이므로 Behaviours에 넣지 않는다. (v5 §10)
    /// </summary>
    public int RicochetBounces { get; private set; }

    /// <summary>기본 1발에 더해지는 동시 발사 수.</summary>
    public int ExtraProjectiles { get; private set; }

    /// <summary>추가 투사체 간 각도(도). 여러 Mutation이 있으면 가장 큰 값을 쓴다.</summary>
    public float SpreadAngle { get; private set; }

    /// <summary>발사 간격 배수. 1보다 크면 느려진다. (대가: 탄막 밀도)</summary>
    public float FireIntervalMultiplier { get; private set; } = 1f;

    /// <summary>투사체 수명 배수. 1보다 작으면 사거리가 줄어든다. (대가: 유효 사거리)</summary>
    public float LifetimeMultiplier { get; private set; } = 1f;

    /// <summary>투사체 속도 배수.</summary>
    public float SpeedMultiplier { get; private set; } = 1f;

    /// <summary>총 동시 발사 수. 최소 1발은 보장한다.</summary>
    public int TotalProjectiles => 1 + Mathf.Max(0, ExtraProjectiles);

    /// <summary>
    /// 합성 발사로 탄에 실리는 적재 속성 목록. (확정 기획 — 합성 발사)
    ///
    /// 적재 계열 Core가 생성하는 상태만 들어간다.
    /// 기능 배타(번제 등)로 유발이 차단된 상태는 여기에 들어오지 않는다.
    /// 2개가 되면 탄마다 번갈아 부여된다. CompositeFireState가 순번을 관리한다.
    /// </summary>
    public IReadOnlyList<StatusEffectType> Ailments => ailments;

    private readonly List<StatusEffectType> ailments = new(2);

    /// <summary>적재 속성을 추가한다. None과 중복은 무시한다.</summary>
    public void AddAilment(StatusEffectType status)
    {
        if (status == StatusEffectType.None)
            return;

        if (ailments.Contains(status))
            return;

        ailments.Add(status);
    }

    public void Reset()
    {
        Behaviours.Clear();
        ailments.Clear();

        RicochetBounces = 0;
        ExtraProjectiles = 0;
        SpreadAngle = 0f;
        FireIntervalMultiplier = 1f;
        LifetimeMultiplier = 1f;
        SpeedMultiplier = 1f;
    }

    /// <summary>Mutation 하나를 합산한다.</summary>
    public void Apply(MutationDefinition definition)
    {
        if (definition == null)
            return;

        if (definition.GrantedBehaviour != ProjectileBehaviourType.None &&
            definition.BehaviourCharges > 0)
        {
            int current = Behaviours.GetRemaining(definition.GrantedBehaviour);

            Behaviours.SetRemaining(
                definition.GrantedBehaviour,
                current + definition.BehaviourCharges);
        }

        RicochetBounces += definition.RicochetBounces;

        ExtraProjectiles += definition.ExtraProjectiles;

        if (definition.ExtraProjectiles > 0)
            SpreadAngle = Mathf.Max(SpreadAngle, definition.SpreadAngle);

        FireIntervalMultiplier *= definition.FireIntervalMultiplier;
        LifetimeMultiplier *= definition.LifetimeMultiplier;
        SpeedMultiplier *= definition.SpeedMultiplier;
    }
}
