using UnityEngine;

/// <summary>충돌 1회를 해결한 결과. 호출부는 이 값만 보고 행동한다.</summary>
public struct ProjectileCollisionResult
{
    /// <summary>소비된 행동. None이면 남은 행동이 없다.</summary>
    public ProjectileBehaviourType Behaviour;

    /// <summary>생성할 자식 투사체 수. 0이면 분열하지 않는다.</summary>
    public int ChildCount;

    /// <summary>자식이 퍼지는 좌우 최대 각도(도).</summary>
    public float SpreadAngle;

    /// <summary>이 충돌 후에도 투사체가 살아남아야 하는지.</summary>
    public bool KeepAlive;

    /// <summary>발사 지점으로 귀환을 시작해야 하는지.</summary>
    public bool BeginReturn;

    /// <summary>다음 대상을 찾아 재유도해야 하는지.</summary>
    public bool SeekNextTarget;
}

/// <summary>
/// 충돌 우선순위 큐를 해결하는 순수 로직. (v5 §10)
///
///     Split → Pierce → Fork → Chain → Return
///
/// 한 번의 충돌에는 단 하나만 해결된다. 이 배타성이 곱연산을 구조적으로 막는다.
///
/// MonoBehaviour와 물리에 의존하지 않으므로 EditMode에서 전수 검증한다.
/// BulletController는 이 결과를 화면에 옮기기만 한다.
///
/// ※ 지형 충돌(튕겨 쏘기)은 이 큐와 별개다. ProjectileRicochetState를 쓴다.
/// </summary>
public static class ProjectileCollisionResolver
{
    /// <summary>분열(Core)의 갈래 수. v5 §6-1 "3갈래로 갈라집니다".</summary>
    public const int SplitChildCount = 3;

    /// <summary>Fork의 갈래 수. PoE2 기준 2개. (마스터 프롬프트 3절)</summary>
    public const int ForkChildCount = 2;

    /// <summary>Fork의 좌우 각도. PoE2 기준 ±60도. (마스터 프롬프트 3절)</summary>
    public const float DefaultForkAngle = 60f;

    /// <summary>
    /// 적 충돌 1회를 해결한다.
    ///
    /// isReturning이 true면 큐를 더 소비하지 않는다.
    /// 귀환 중에 Chain이 남아 있다고 해서 방향을 틀면 Return이 성립하지 않기 때문이다.
    /// </summary>
    public static ProjectileCollisionResult Resolve(
        ref ProjectileBehaviourState state,
        bool isReturning,
        float splitAngle = DefaultForkAngle,
        float forkAngle = DefaultForkAngle)
    {
        // 귀환 중: 피해만 주고 궤도를 유지한다. 재타격이 허용되는 유일한 구간이다.
        if (isReturning)
        {
            return new ProjectileCollisionResult
            {
                Behaviour = ProjectileBehaviourType.Return,
                KeepAlive = true
            };
        }

        ProjectileBehaviourType behaviour = state.ConsumeNext();

        switch (behaviour)
        {
            // 분열: 3갈래. 갈라진 탄은 재분열하지 않는다. (v5 §6-1)
            case ProjectileBehaviourType.Split:
                return new ProjectileCollisionResult
                {
                    Behaviour = behaviour,
                    ChildCount = SplitChildCount,
                    SpreadAngle = splitAngle,
                    KeepAlive = false
                };

            // 관통: 궤도를 유지한 채 계속 나아간다. 아무것도 하지 않는 것이 곧 관통이다.
            case ProjectileBehaviourType.Pierce:
                return new ProjectileCollisionResult
                {
                    Behaviour = behaviour,
                    KeepAlive = true
                };

            // Fork: 2갈래, 원 궤도 기준 ±60도.
            case ProjectileBehaviourType.Fork:
                return new ProjectileCollisionResult
                {
                    Behaviour = behaviour,
                    ChildCount = ForkChildCount,
                    SpreadAngle = forkAngle,
                    KeepAlive = false
                };

            // Chain: 근처 다른 적으로 재유도. 대상이 없으면 호출부가 소멸시킨다.
            case ProjectileBehaviourType.Chain:
                return new ProjectileCollisionResult
                {
                    Behaviour = behaviour,
                    KeepAlive = true,
                    SeekNextTarget = true
                };

            // Return: 발사 지점으로 귀환하며 재타격한다.
            case ProjectileBehaviourType.Return:
                return new ProjectileCollisionResult
                {
                    Behaviour = behaviour,
                    KeepAlive = true,
                    BeginReturn = true
                };

            default:
                return new ProjectileCollisionResult
                {
                    Behaviour = ProjectileBehaviourType.None,
                    KeepAlive = false
                };
        }
    }

    /// <summary>
    /// 자식 투사체 index가 원 궤도에서 틀어야 하는 각도(도).
    ///
    /// count가 1이면 0도, 2면 ±spread, 3이면 -spread / 0 / +spread로 균등 분배된다.
    /// </summary>
    public static float GetChildAngle(int index, int count, float spreadAngle)
    {
        if (count <= 1)
            return 0f;

        int clamped = Mathf.Clamp(index, 0, count - 1);

        float step = spreadAngle * 2f / (count - 1);

        return -spreadAngle + clamped * step;
    }
}
