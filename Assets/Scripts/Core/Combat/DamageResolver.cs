using UnityEngine;

/// <summary>
/// 피해 계산의 유일한 구현. MonoBehaviour 의존이 없는 순수 클래스다.
///
///   최종 피해 = 기본 피해
///             × (1 + Σ증가%)
///             × 사거리 보정
///             × 치명타 배율
///             × 방어도 보정
///             × 속성 상성
///             × 난이도 보정
///
/// 이 공식을 다른 곳에 복제하지 않는다. 복제하면 반드시 드리프트가 생긴다.
/// (docs/Blob_Combat_Baseline.md 3절)
/// </summary>
public static class DamageResolver
{
    /// <summary>
    /// 방어도 보정 = K / (max(방어도 − 방어 관통, 0) + K)
    ///
    /// 정수 차감이 아니라 연속 감쇠다. 세 가지가 동시에 해결된다.
    ///  1. 식에 피해량이 들어가지 않으므로 저피해·다탄 빌드가 죽지 않는다.
    ///  2. 관통 ≥ 방어도이면 배율이 1로 고정되어 초과 관통에 보상이 없다.
    ///  3. 선형이 아니라 쌍곡선이라 낮은 구간에서 한 등급 차이가 크게 벌어진다.
    /// </summary>
    public static float ArmourMultiplier(int armour, int penetration)
    {
        int gap = armour - penetration;

        if (gap < 0)
            gap = 0;

        return CombatConstants.ArmourConstant / (gap + CombatConstants.ArmourConstant);
    }

    /// <summary>최종 피해를 계산한다. 난이도 보정은 호출자가 넘긴다.</summary>
    public static int Resolve(in DamageRequest request, in DefenceProfile defence, float difficultyMultiplier = 1f)
    {
        if (request.baseDamage <= 0)
            return 0;

        float value = request.baseDamage;

        // 모든 "증가"는 가산 합산. -100% 이하로는 내려가지 않는다.
        float increased = 1f + request.increasedPercent;

        if (increased <= 0f)
            return 0;

        value *= increased;

        float range = RangeFalloff.Multiplier(request.distance, request.effectiveRange);

        if (range <= 0f)
            return 0;

        value *= range;

        if (request.isCritical && request.criticalMultiplier > 0f)
            value *= request.criticalMultiplier;

        // 상태이상 피해는 방어도를 무시한다. 속성 상성은 그대로 받는다.
        if (!request.bypassArmour)
            value *= ArmourMultiplier(defence.ArmourFor(request.hitKind), request.armourPenetration);

        float resistance = defence.resistances.Get(request.element);

        // 내성 0은 완전 면역이다. 하한 1을 적용하지 않는다.
        if (resistance <= 0f)
            return 0;

        value *= resistance;

        if (difficultyMultiplier <= 0f)
            return 0;

        value *= difficultyMultiplier;

        if (value <= 0f)
            return 0;

        // 면역이 아닌 이상 최소 1은 들어간다.
        // 이 하한이 없으면 고방어 구간에서 "때려도 숫자가 안 뜨는" 상태가 된다.
        int result = Mathf.RoundToInt(value);

        return result < 1 ? 1 : result;
    }
}
