using UnityEngine;

/// <summary>이번 프레임 판단에 필요한 입력 전부.</summary>
public struct EngagementInput
{
    /// <summary>대상까지의 거리(m).</summary>
    public float distance;

    /// <summary>유지하려는 거리(m). 근접은 짧고 원거리는 길다.</summary>
    public float preferredDistance;

    /// <summary>유지 거리의 허용 폭(m). 이 안이면 붙지도 물러나지도 않는다.</summary>
    public float band;

    /// <summary>공격 판정이 닿는 거리(m).</summary>
    public float attackRange;

    /// <summary>이 적이 교전을 인지하는 최대 거리(m).</summary>
    public float detectDistance;

    /// <summary>지금 공격 차례를 쥐고 있는지. (동시 공격 제한)</summary>
    public bool hasAttackToken;

    /// <summary>재장전 중인지.</summary>
    public bool isReloading;

    /// <summary>대상이 보이는지. 벽에 가리면 false.</summary>
    public bool hasLineOfSight;
}

/// <summary>이번 프레임에 무엇을 할지.</summary>
public struct EngagementPlan
{
    public EngagementPhase phase;

    /// <summary>−1 물러남 ~ +1 접근. 대상 방향 성분의 가중치다.</summary>
    public float approach;

    /// <summary>0 ~ 1. 대상을 축으로 옆으로 도는 성분의 가중치다.</summary>
    public float strafe;

    /// <summary>이번 프레임에 공격을 시작해도 되는지.</summary>
    public bool mayAttack;

    /// <summary>이동 성분이 하나라도 있는지.</summary>
    public bool IsMoving => !Mathf.Approximately(approach, 0f) || strafe > 0f;
}

/// <summary>
/// 교전 판단. 거리·차례·재장전만 보고 「붙을까 / 돌까 / 물러날까 / 쏠까」를 정한다.
/// (docs/Blob_Hunting_System.md — 적은 무작정 달라붙지 않는다)
///
/// 왜 순수 정적 클래스인가 —
/// 이 판단이 곧 전투의 체감이다. 씬을 띄우지 않고 거리별로 전수 검증할 수 있어야
/// "어떤 거리에서 적이 멈춰 서 있다" 같은 문제를 눈이 아니라 테스트로 잡는다.
///
/// 【핵심】 「지금 내 차례인가」(hasAttackToken)가 사람다움의 8할이다.
/// 차례가 아닌 적은 쏘지 않고 옆으로 돈다. 그래서 다섯 마리가 나와도
/// 다섯 방향에서 동시에 달려들지 않고, 둘이 쏘고 셋이 자리를 잡는 그림이 된다.
/// </summary>
public static class EngagementPlanner
{
    /// <summary>차례가 아닐 때 유지하려는 추가 거리 배수. 뒤로 한 발 빼고 돈다.</summary>
    public const float RepositionDistanceScale = 1.25f;

    public static EngagementPlan Plan(in EngagementInput input)
    {
        var plan = new EngagementPlan();

        // 인지 범위 밖이면 아무것도 하지 않는다.
        if (input.distance > input.detectDistance)
        {
            plan.phase = EngagementPhase.Idle;
            return plan;
        }

        // 재장전이 최우선이다. 쏘지 못하는 동안 앞에 서 있으면 그냥 과녁이다.
        if (input.isReloading)
        {
            plan.phase = EngagementPhase.Reload;
            plan.approach = -0.7f;
            plan.strafe = 0.8f;
            plan.mayAttack = false;

            return plan;
        }

        // 보이지 않으면 쏠 수 없다. 각을 잡으러 붙는다.
        if (!input.hasLineOfSight)
        {
            plan.phase = EngagementPhase.Advance;
            plan.approach = 1f;
            plan.strafe = 0.45f;
            plan.mayAttack = false;

            return plan;
        }

        float band = Mathf.Max(0.1f, input.band);

        // 차례가 아니면 한 발 물러난 거리에서 돈다. 쏘지 않는다.
        if (!input.hasAttackToken)
        {
            float standoff = input.preferredDistance * RepositionDistanceScale;

            plan.phase = EngagementPhase.Reposition;
            plan.approach = input.distance > standoff + band ? 0.6f
                          : input.distance < standoff - band ? -0.6f
                          : 0f;
            plan.strafe = 1f;
            plan.mayAttack = false;

            return plan;
        }

        // 여기부터는 내 차례다.
        if (input.distance > input.preferredDistance + band)
        {
            plan.phase = EngagementPhase.Advance;
            plan.approach = 1f;
            plan.strafe = 0.25f;

            // 붙는 도중이라도 사거리에 닿으면 쏜다. 멈춰 설 이유가 없다.
            plan.mayAttack = input.distance <= input.attackRange;

            return plan;
        }

        if (input.distance < input.preferredDistance - band)
        {
            plan.phase = EngagementPhase.Back;
            plan.approach = -1f;
            plan.strafe = 0.5f;
            plan.mayAttack = true;

            return plan;
        }

        plan.phase = EngagementPhase.Hold;
        plan.approach = 0f;
        plan.strafe = 0.75f;
        plan.mayAttack = true;

        return plan;
    }

    /// <summary>
    /// 접근·측면 성분을 실제 이동 방향으로 합친다.
    /// toTarget은 정규화되어 있어야 하며 수평 평면 벡터다.
    /// </summary>
    public static Vector3 ToDirection(in EngagementPlan plan, Vector3 toTarget, float strafeSign)
    {
        Vector3 side = Vector3.Cross(Vector3.up, toTarget);

        Vector3 direction = toTarget * plan.approach + side * (plan.strafe * Mathf.Sign(strafeSign));

        return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
    }
}
