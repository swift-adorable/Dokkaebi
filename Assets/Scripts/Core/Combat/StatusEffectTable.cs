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
    /// <summary>감전이 최대 중첩에서 올려 주는 "받는 피해" 증가율.</summary>
    public const float ShockDamageTakenBonus = 0.2f;

    /// <summary>냉각이 최대 중첩에서 깎는 이동·공격 속도 비율.</summary>
    public const float ChillSlowRatio = 0.4f;

    /// <summary>점화 중에 대상이 잃는 방어도. 「점화가 다음 피해를 키운다」의 구현이다.</summary>
    public const float IgniteArmourReduction = 1f;

    /// <summary>부식이 곱하는 방어도·회복량 배율.</summary>
    public const float CorrodeHalfRatio = 0.5f;

    /// <summary>출혈이 이동 중인 대상에게 곱하는 배율.</summary>
    public const float BleedMovingMultiplier = 2f;

    /// <summary>응집이 넓히는 상태 전이 범위 배율.</summary>
    public const float CongealSpreadMultiplier = 2f;

    // ── 이로운 상태의 수치 ────────────────────────────────────────────
    //
    // 문서 4절의 강화 소모품 표를 그대로 옮긴 것이다.
    //   노란 주사약 — 이동 +25%, 120초 → 끝나면 60초 동안 −15%
    //   강화 주사약 — 방어도 +0.5, 120초, 대가로 회복량 절반
    // 「회복」 상태는 덕코프 「회복」#1018 「1초마다 체력 2 회복, 30초」 그대로다 [확인됨].
    // 30초 × 2 = 60이면 구급상자 두 번보다 조금 많다. 다만 30초 동안
    // 나눠 들어오므로 급할 때의 한 방을 대신하지 못한다.

    public const float HasteSeconds = 120f;
    public const float HasteSpeedBonus = 0.25f;

    public const float FatigueSeconds = 60f;
    public const float FatigueSpeedPenalty = 0.15f;

    public const float BolsterSeconds = 120f;

    /// <summary>
    /// 강화가 더하는 방어도. 【덕코프 「강화」#1013의 「신체 방어구 +0.5」 그대로다.】
    ///
    /// 배율(×1.25)이 아니라 덧셈인 것이 중요하다. 방어 공식이
    /// 2/(방어도−관통+2)이라 배율이면 이미 두꺼운 쪽이 더 이득을 본다.
    /// 덧셈이면 얇은 쪽의 체감이 더 커서 「맨몸에 한 대 버틸 것을 준다」가 된다.
    /// </summary>
    public const float BolsterArmourBonus = 0.5f;

    /// <summary>강화의 대가. 부식과 같은 절반이다 — 대가가 가벼우면 안 쓸 이유가 없다.</summary>
    public const float BolsterHealingMultiplier = 0.5f;

    public const float RegenSeconds = 30f;

    /// <summary>
    /// 「회복」 상태의 초당 회복량.
    /// 【덕코프 「회복」#1018의 「1초마다 체력 2 회복」 그대로다.】 [확인됨]
    /// </summary>
    public const float RegenPerSecond = 2f;

    /// <summary>
    /// 소지 중량 증가(중량 주사약)가 곱하는 최대 소지 중량.
    /// 【덕코프 「소지 중량 증가」#1012의 「최대 소지 중량 +50%」 그대로다.】 [확인됨]
    /// </summary>
    public const float OverloadSeconds = 240f;
    public const float OverloadWeightMultiplier = 1.5f;

    /// <summary>
    /// 저항이 곱하는 「받는 피해」 배율.
    /// 【덕코프 저항 buff의 「−25%」 그대로다.】 [확인됨 — #1072 · #1074 · #1075]
    /// </summary>
    public const float WardSeconds = 120f;
    public const float WardMultiplier = 0.75f;

    // ── 폭주 — 덕코프 「타길라의 힘」#1206 ───────────────────────────
    //
    //   신체 방어구 +1 · 이동 능력 +0.1 · 최대 생명력 +10 ·
    //   1초마다 2 피해를 받음 · 90초 · 배타 태그 Tagilla   [확인됨]
    //
    // 시야 거리 +4 · 감지 거리 +1 · 체력(스태미나) +15 · 근접 +0.1은 옮기지 않는다.
    // Blob에는 플레이어 시야·스태미나·근접 축이 없다.
    //
    // 【90초 × 2 = 180 피해다.】 최대 체력 110보다 많다. 회복 없이 끝까지
    // 버티면 죽는다 — 덕코프가 그렇게 만들었다. 「이기려고 쓰는 것이 아니라
    // 살아 나오려고 쓰는 것」(소모품 문서 4-2)이 이 숫자로 성립한다.

    public const float FrenzySeconds = 90f;
    public const float FrenzyArmourBonus = 1f;
    public const float FrenzySpeedBonus = 0.1f;
    public const int FrenzyMaxHealthBonus = 10;

    /// <summary>폭주의 초당 자해 피해. 계수로 쓰므로 부여 시 기준 피해는 1로 넘긴다.</summary>
    public const float FrenzySelfDamagePerSecond = 2f;

    public static StatusEffectSpec Get(StatusEffectType type)
    {
        switch (type)
        {
            // ── 피해형 ────────────────────────────────────────────────
            // 【중첩이 피해를 늘리는 것은 중독뿐이다.】
            // 점화·출혈은 갱신형이라 다시 걸면 지속시간만 처음으로 돌아간다.
            // 셋 다 중첩하면 "쌓아서 녹인다"는 카오스의 정체성이 사라진다.

            // 점화 — "한 발을 더 쏜 것"과 같은 총량(100%)을 4초에 걸쳐 준다.
            // 차이는 방어도 무시와 지속 중 행동 가능, 그리고 방어도를 깎는다는 점이다.
            case StatusEffectType.Ignite:
                return new StatusEffectSpec(0.25f, 4f, 1, DamageElement.Fire);

            // 출혈 — 정지한 적에게 기본 피해의 60%, 이동 중이면 120%.
            case StatusEffectType.Bleed:
                return new StatusEffectSpec(0.20f, 3f, 1, DamageElement.Physical);

            // 중독 — 유일하게 중첩한다. 10중첩 총량이 기본 피해의 3배다.
            //
            // 이전 계수 0.15는 10중첩에서 총 9배였다. 10번 맞히는 데 4초면 되고
            // 지속이 6초라 실제로 도달하므로, 도트가 직접 피해의 두 배 가까이 됐다.
            // 3배로 묶어 「쌓으면 세다」는 유지하되 직접 피해를 압도하지 않게 했다.
            case StatusEffectType.Poison:
                return new StatusEffectSpec(0.05f, 6f, 10, DamageElement.Chaos);

            // ── 통제형 ────────────────────────────────────────────────
            // 피해가 없다. 중첩은 효과의 세기이자 한계치까지의 게이지다.
            // 최대 중첩에서 문서 수치에 도달하고, 그 순간 위험 상태로 전이한다.

            case StatusEffectType.Shock:
                return new StatusEffectSpec(0f, 6f, 6, DamageElement.Lightning);

            case StatusEffectType.Chill:
                return new StatusEffectSpec(0f, 3f, 6, DamageElement.Cold);

            // 응집 — 상태 전이 범위만 넓힌다.
            case StatusEffectType.Congeal:
                return new StatusEffectSpec(0f, 1.5f, 1, DamageElement.Physical);

            // ── 위험 상태 ─────────────────────────────────────────────
            // 직접 부여되지 않는다. 전이로만 생기고 중첩하지 않는다.

            case StatusEffectType.Freeze:
                return new StatusEffectSpec(0f, 3f, 1, DamageElement.Cold);

            case StatusEffectType.Paralyze:
                return new StatusEffectSpec(0f, 2f, 1, DamageElement.Lightning);

            case StatusEffectType.Corrode:
                return new StatusEffectSpec(0f, 8f, 1, DamageElement.Chaos);

            // ── 이로운 상태 ───────────────────────────────────────────
            // 【중첩하지 않는다.】 소모품 문서 6절의 「같은 분류는 덮어쓴다」다.
            // 노란 주사약 두 개를 겹쳐 쓸 수 있으면 대가가 있는 의미가 사라진다.
            // 다시 걸면 지속시간만 처음으로 돌아간다.

            case StatusEffectType.Haste:
                return new StatusEffectSpec(0f, HasteSeconds, 1, DamageElement.Physical);

            case StatusEffectType.Bolster:
                return new StatusEffectSpec(0f, BolsterSeconds, 1, DamageElement.Physical);

            // 「회복」 상태는 피해를 주지 않는다. 계수를 두면 IsDamaging에 걸리므로
            // 0으로 두고, 초당 회복량은 RegenPerSecond가 따로 정한다.
            case StatusEffectType.Regen:
                return new StatusEffectSpec(0f, RegenSeconds, 1, DamageElement.Physical);

            // 대가. 노란 주사약이 끝나면 저절로 걸린다.
            case StatusEffectType.Fatigue:
                return new StatusEffectSpec(0f, FatigueSeconds, 1, DamageElement.Physical);

            case StatusEffectType.Overload:
                return new StatusEffectSpec(0f, OverloadSeconds, 1, DamageElement.Physical);

            // 저항 넷. Element는 「무엇을 막는가」를 담는 자리로 쓴다.
            case StatusEffectType.WardFire:
                return new StatusEffectSpec(0f, WardSeconds, 1, DamageElement.Fire);

            case StatusEffectType.WardCold:
                return new StatusEffectSpec(0f, WardSeconds, 1, DamageElement.Cold);

            case StatusEffectType.WardLightning:
                return new StatusEffectSpec(0f, WardSeconds, 1, DamageElement.Lightning);

            case StatusEffectType.WardChaos:
                return new StatusEffectSpec(0f, WardSeconds, 1, DamageElement.Chaos);

            // 폭주 — 이로운 상태 중 유일하게 피해를 준다. 기준 피해 1 × 계수 2 = 초당 2.
            // 물리로 두되 상태이상 틱이므로 방어도를 무시한다 — 방어도를 올려 주는
            // 상태가 제 자해를 방어도로 막으면 대가가 줄어든다.
            case StatusEffectType.Frenzy:
                return new StatusEffectSpec(FrenzySelfDamagePerSecond, FrenzySeconds, 1,
                    DamageElement.Physical);

            default:
                return new StatusEffectSpec(0f, 0f, 1, DamageElement.Physical);
        }
    }

    /// <summary>
    /// 최대 중첩에 도달했을 때 전이하는 상태. 없으면 None.
    ///
    /// 【최대 중첩 = 한계치】로 통일한 이유 — 규칙이 한 줄이 된다.
    /// 전이 시 원본 중첩을 전부 소모하므로, 위험 상태가 끝나면
    /// 처음부터 다시 쌓아야 한다. 무한 제압이 막힌다.
    /// </summary>
    public static StatusEffectType ThresholdOf(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Chill: return StatusEffectType.Freeze;
            case StatusEffectType.Shock: return StatusEffectType.Paralyze;
            case StatusEffectType.Poison: return StatusEffectType.Corrode;
            default: return StatusEffectType.None;
        }
    }

    /// <summary>
    /// 끝났을 때 저절로 걸리는 대가 상태. 없으면 None.
    ///
    /// 【대가를 별도 필드로 만들지 않는 이유】 「끝나면 −15%」는 결국
    /// 「다른 상태가 걸린다」와 같은 말이다. 상태이상 하나로 표현하면
    /// 화면도 그것을 그대로 보여 준다 — 플레이어는 쇠약이 걸린 것을
    /// 상태 줄에서 보고, 왜 느려졌는지 스스로 안다.
    /// (docs/Blob_Consumable_System.md 8절)
    /// </summary>
    public static StatusEffectType AftermathOf(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Haste: return StatusEffectType.Fatigue;
            default: return StatusEffectType.None;
        }
    }

    /// <summary>이로운 상태인가. 화면이 줄의 자리와 색을 정할 때 본다.</summary>
    public static bool IsBeneficial(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Haste:
            case StatusEffectType.Bolster:
            case StatusEffectType.Regen:
            case StatusEffectType.Overload:
            case StatusEffectType.WardFire:
            case StatusEffectType.WardCold:
            case StatusEffectType.WardLightning:
            case StatusEffectType.WardChaos:
            case StatusEffectType.Frenzy:
                return true;

            default:
                return false;
        }
    }

    /// <summary>저항이 막는 속성. 저항이 아니면 None을 뜻하는 Physical을 돌려준다.</summary>
    public static bool IsWard(StatusEffectType type)
    {
        return type == StatusEffectType.WardFire
               || type == StatusEffectType.WardCold
               || type == StatusEffectType.WardLightning
               || type == StatusEffectType.WardChaos;
    }

    /// <summary>행동 불능 상태인지. 이 상태에서는 이동도 공격도 하지 못한다.</summary>
    public static bool IsIncapacitating(StatusEffectType type)
        => type == StatusEffectType.Freeze || type == StatusEffectType.Paralyze;

    /// <summary>
    /// 통제형 상태의 세기 비율(0~1). 중첩에 비례하며 최대 중첩에서 1이 된다.
    ///
    /// 1중첩에 문서 수치를 전부 주면 한 발만 맞혀도 −40% 감속이 된다.
    /// 비례시키면 쌓는 과정 자체가 의미를 갖고, 최대치는 문서대로 유지된다.
    /// </summary>
    public static float ControlRatio(StatusEffectType type, int stacks)
    {
        StatusEffectSpec spec = Get(type);

        if (spec.MaxStacks <= 1)
            return stacks > 0 ? 1f : 0f;

        return UnityEngine.Mathf.Clamp01((float)stacks / spec.MaxStacks);
    }
}
