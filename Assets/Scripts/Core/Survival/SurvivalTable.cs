using UnityEngine;

/// <summary>
/// 수분 · 에너지의 수치. (docs/Dokkaebi_Survival_System.md · 결정 2-32)
///
/// 【여기 값을 고치는 것 말고 다른 곳을 고치지 않는다.】
/// 레이드 한 판의 길이가 정해지면 소모율만 바꾸면 되도록 한 곳에 모아 둔다.
///
/// 출처 표시 —
///   [확인됨]   덕코프 위키에서 그대로 가져온 값 (docs/research/duckov/07_...)
///   [불확실]   위키에 없어 우리가 정한 값
/// </summary>
public static class SurvivalTable
{
    // ── 기준값 ────────────────────────────────────────────────────────

    /// <summary>[불확실] 최대 수분. 덕코프 장비가 최대치를 +10씩 올리는 것으로 보아 100 자릿수.</summary>
    public const float MaxWater = 100f;

    /// <summary>[불확실] 최대 에너지.</summary>
    public const float MaxEnergy = 100f;

    /// <summary>
    /// [불확실] 초당 수분 소모. 가득 → 0 에 16분 40초.
    /// 한 판에 한 번은 마셔야 하는 속도로 잡았다.
    /// </summary>
    public const float WaterDrainPerSecond = 0.10f;

    /// <summary>
    /// [불확실] 초당 에너지 소모. 가득 → 0 에 27분 46초.
    ///
    /// 【수분보다 느리다.】 둘이 같은 순간에 바닥나면 게이지가 하나인 것과 같다.
    /// </summary>
    public const float EnergyDrainPerSecond = 0.06f;

    // ── 소지 중량 [확인됨 — 덕코프 값 그대로] ─────────────────────────

    /// <summary>
    /// 과중량 단계가 소모율을 태운다.
    ///
    /// 【이것이 이 시스템을 넣는 진짜 이유다.】
    /// 과중량은 지금까지 이동 속도만 깎았다. 느려지는 건 참을 수 있어서
    /// 「일단 다 들고 간다」가 늘 정답에 가까웠다. 무거우면 물이 빨리 마른다면
    /// 더 들수록 판이 짧아진다 — 「무엇을 두고 갈 것인가」가 시간 압박을 받는다.
    /// </summary>
    public static float DrainMultiplier(EncumbranceLevel level)
    {
        switch (level)
        {
            case EncumbranceLevel.Heavy:      return 1.10f;
            case EncumbranceLevel.Overloaded: return 1.25f;
            case EncumbranceLevel.Immobile:   return 1.40f;
            default:                          return 1f;
        }
    }

    // ── 탈수 (수분 0) ─────────────────────────────────────────────────

    /// <summary>[확인됨] 탈수의 이동 배율. 덕코프 −20%.</summary>
    public const float DehydratedMoveScale = 0.80f;

    /// <summary>
    /// [확인됨] 탈수일 때 에너지가 차는 비율. 덕코프 「체력 회복 −70%」 그대로.
    ///
    /// 덕코프의 「체력」은 스태미나이고, Dokkaebi는 스태미나를 에너지에 합쳤다
    /// (용어 기준표 1-2). 그래서 이 −70%는 **에너지가 차는 양**에 붙는다.
    /// 「물이 없으면 먹어도 배가 덜 찬다」 — 물부터 마셔야 음식이 제값을 한다.
    ///
    /// 【체력(HP)에는 손대지 않는다.】 이 축의 존재 이유는 철수 압박 하나이고,
    /// 회복약을 덜 듣게 만드는 것은 전투 페널티라 방침에서 벗어난다.
    /// (Survival_System 6절)
    /// </summary>
    public const float DehydratedEnergyRestoreScale = 0.30f;

    // ── 허기 (에너지 0) ───────────────────────────────────────────────

    /// <summary>[확인됨] 허기의 이동 배율. 덕코프 −10%.</summary>
    public const float StarvingMoveScale = 0.90f;

    // 【허기의 회복 페널티는 넣지 않는다.】
    // 덕코프의 배고픔은 「체력(스태미나) 회복 −50%」인데, Dokkaebi는 스태미나를
    // 에너지에 합쳤으므로 그대로 옮기면 「굶주리면 에너지가 덜 찬다」가 되어
    // 자기 자신을 가리킨다. 탈수까지 겹치면 0.3 × 0.5 = 15%만 차서
    // 빠져나올 수 없는 나선이 된다 — 원본에는 없던 문제다.
    // 허기의 압박은 이동 −10%와 지속 피해로 충분하다(둘 다 원본 값 그대로).

    /// <summary>[확인됨] 허기 최대 중첩. 덕코프 3.</summary>
    public const int StarvingMaxStacks = 3;

    /// <summary>[확인됨] 허기 피해 간격(초). 덕코프 2초마다 중첩당 1.</summary>
    public const float StarvingDamageInterval = 2f;

    /// <summary>[확인됨] 허기 중첩당 피해.</summary>
    public const int StarvingDamagePerStack = 1;

    /// <summary>
    /// [불확실] 중첩이 하나 오르는 데 걸리는 시간(초).
    /// 위키에 조건이 없어 직접 정했다.
    ///
    /// 3중첩이어도 체력 100을 비우는 데 66초다 — 빠져나갈 시간은 남는다.
    /// (Survival_System 6절 「바닥나도 즉사시키지 않는다」)
    /// </summary>
    public const float StarvingStackInterval = 30f;

    // ── 경고선 ────────────────────────────────────────────────────────

    /// <summary>이 비율 아래로 내려가면 화면에서 경고색이 된다.</summary>
    public const float WarnRatio = 0.25f;

    /// <summary>체력 경고선. 수분·에너지보다 늦게 켠다 — 체력은 순식간에 준다.</summary>
    public const float HealthWarnRatio = 0.30f;
}
