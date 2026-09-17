using UnityEngine;

/// <summary>
/// 사거리 보정. 계단식이며 점진 감쇠가 아니다.
///
///   거리 ≤ 유효 사거리 / 2  →  ×1.0
///   거리 >  유효 사거리 / 2  →  ×0.5
///   거리 >  유효 사거리      →  ×0   (투사체 소멸 판정은 호출자의 몫)
///
/// 이 한 항이 만드는 것 — 카이팅이 공짜가 아니게 되고,
/// Support 대가 통화 「유효 사거리」가 실질적 의미를 갖는다.
/// (docs/Blob_Combat_Baseline.md 4절)
/// </summary>
public static class RangeFalloff
{
    /// <summary>거리에 따른 피해 배율.</summary>
    public static float Multiplier(float distance, float effectiveRange)
    {
        // 사거리를 쓰지 않는 공격(근접·장판·상태이상)은 보정하지 않는다.
        if (effectiveRange <= 0f || distance < 0f)
            return 1f;

        if (distance > effectiveRange)
            return 0f;

        return distance > effectiveRange * 0.5f ? CombatConstants.FarRangeMultiplier : 1f;
    }

    /// <summary>온전한 피해가 들어가는 최대 거리. UI 표시용.</summary>
    public static float FullDamageRange(float effectiveRange)
    {
        return Mathf.Max(0f, effectiveRange) * 0.5f;
    }
}
