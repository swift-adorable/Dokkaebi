using UnityEngine;

/// <summary>
/// 지형 충돌 튕김 상태 — 「튕겨 쏘기」(Core). (v5 §6-1)
///
/// ※ 충돌 우선순위 큐와 의도적으로 분리되어 있다.
///    v5 §10: "튕겨 쏘기는 이 큐와 별개입니다. 지형 충돌에서 발동하며
///    적 충돌 큐를 소모하지 않습니다."
///    따라서 ProjectileBehaviourType에 Ricochet을 추가하면 안 된다.
///    같은 이유로 튕겨 쏘기와 관통은 서로를 막지 않는다.
///
/// 순수 구조체이므로 EditMode에서 검증한다.
/// </summary>
[System.Serializable]
public struct ProjectileRicochetState
{
    /// <summary>「튕겨 쏘기」 기본 튕김 횟수. (v5 §6-1 "기본 3회")</summary>
    public const int DefaultBounces = 3;

    public int Remaining;

    public bool HasAny => Remaining > 0;

    public void Set(int count)
    {
        Remaining = Mathf.Max(0, count);
    }

    public void Add(int count)
    {
        Remaining = Mathf.Max(0, Remaining + count);
    }

    public void Clear()
    {
        Remaining = 0;
    }

    /// <summary>튕김 1회를 소비한다. 남은 횟수가 없으면 false이며 투사체는 소멸해야 한다.</summary>
    public bool TryConsume()
    {
        if (Remaining <= 0)
            return false;

        Remaining--;

        return true;
    }

    /// <summary>
    /// 지형 법선에 대한 평면 반사 방향. Top-Down이므로 y 성분은 버린다.
    ///
    /// 법선이 유효하지 않거나 정면 충돌이 아니면 입사 방향을 그대로 돌려준다.
    /// (모서리에서 방향이 NaN이 되어 투사체가 사라지는 것을 막는다)
    /// </summary>
    public static Vector3 Reflect(Vector3 incoming, Vector3 surfaceNormal)
    {
        incoming.y = 0f;
        surfaceNormal.y = 0f;

        if (incoming.sqrMagnitude < 0.000001f)
            return Vector3.forward;

        if (surfaceNormal.sqrMagnitude < 0.000001f)
            return incoming.normalized;

        Vector3 reflected = Vector3.Reflect(incoming.normalized, surfaceNormal.normalized);

        reflected.y = 0f;

        return reflected.sqrMagnitude < 0.000001f
            ? incoming.normalized
            : reflected.normalized;
    }
}
