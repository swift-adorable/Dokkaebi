using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chain이 다음 대상을 고르는 순수 로직.
///
/// v5 §10: "같은 시퀀스에서 동일 적 재타격 불가".
/// 이미 맞은 대상을 제외하고 반경 내 최근접 대상을 고른다.
///
/// 위치 배열과 제외 플래그 배열만 받으므로 Transform·물리에 의존하지 않고,
/// 델리게이트를 쓰지 않아 호출마다 클로저가 할당되지 않는다.
/// </summary>
public static class ChainTargetSelector
{
    /// <summary>
    /// 반경 내에서 제외되지 않은 최근접 대상의 인덱스를 돌려준다. 없으면 -1.
    ///
    /// 거리 비교는 제곱 거리로 하므로 Sqrt 연산이 발생하지 않는다.
    /// y 성분은 무시한다 (Top-Down).
    /// </summary>
    public static int SelectNearestIndex(
        Vector3 origin,
        float radius,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<bool> excluded)
    {
        if (positions == null || positions.Count == 0 || radius <= 0f)
            return -1;

        float sqrRadius = radius * radius;
        float bestSqrDistance = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < positions.Count; i++)
        {
            if (excluded != null && i < excluded.Count && excluded[i])
                continue;

            Vector3 offset = positions[i] - origin;
            offset.y = 0f;

            float sqrDistance = offset.sqrMagnitude;

            if (sqrDistance > sqrRadius)
                continue;

            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestIndex = i;
        }

        return bestIndex;
    }
}
