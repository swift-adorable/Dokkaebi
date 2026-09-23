using UnityEngine;
using System;

/// <summary>
/// 한 대상에게 걸린 상태이상들의 상태. MonoBehaviour 의존이 없는 순수 클래스다.
///
/// 시간을 밖에서 주입받으므로 EditMode에서 전수 검증할 수 있다.
/// (docs/Blob_Combat_Baseline.md 7절)
/// </summary>
public sealed class StatusEffectState
{
    private const int TypeCount = 10; // None 포함. 냉각 · 마비 · 부식까지.

    private readonly double[] remaining = new double[TypeCount];
    private readonly int[] stacks = new int[TypeCount];
    private readonly float[] sourceDamage = new float[TypeCount];

    /// <summary>
    /// 아직 정수로 떨어지지 않은 피해의 잔여분.
    ///
    /// 틱마다 올림(Ceiling)하면 틱을 잘게 쪼갤수록 총 피해가 부풀어 오른다.
    /// (점화 10이 프레임 8분할에서 16이 되는 버그가 실제로 있었다)
    /// 소수를 누적했다가 1 이상이 될 때만 내보내고, 만료 시 남은 분을 반올림해 흘린다.
    /// 이렇게 해야 틱 간격과 무관하게 총량이 문서 수치와 일치한다.
    ///
    /// float이 아니라 double을 쓴다. 60fps로 6초를 누적하면 float32 오차가
    /// 1 단위 손실을 만든다(중독 9 → 8). 프레임마다 도는 경로가 아니므로 비용도 없다.
    /// </summary>
    private readonly double[] pending = new double[TypeCount];

    /// <summary>상태이상이 하나라도 걸려 있는지.</summary>
    public bool HasAny
    {
        get
        {
            for (int i = 1; i < TypeCount; i++)
            {
                if (remaining[i] > 0f)
                    return true;
            }

            return false;
        }
    }

    public bool Has(StatusEffectType type)
    {
        return IsValid(type) && remaining[(int)type] > 0f;
    }

    public int StacksOf(StatusEffectType type)
    {
        return IsValid(type) ? stacks[(int)type] : 0;
    }

    public float RemainingOf(StatusEffectType type)
    {
        return IsValid(type) ? (float)remaining[(int)type] : 0f;
    }

    /// <summary>
    /// 상태를 건다. 이미 걸려 있으면 지속시간을 갱신하고, 중첩형이면 중첩을 1 올린다.
    /// </summary>
    /// <param name="type">거는 상태</param>
    /// <param name="baseDamage">부여 시점의 기본 피해. 초당 피해의 기준이 된다.</param>
    /// <param name="durationScale">
    /// 지속시간 배율. 보조 젬 「치명적인 중독」·「깊은 상처」 같은 것이 여기를 건드린다.
    /// 【투사체 수명과 다른 축이다.】 (docs/Blob_Audit.md D2)
    /// </param>
    public void Apply(StatusEffectType type, float baseDamage, float durationScale = 1f)
    {
        if (!IsValid(type))
            return;

        StatusEffectSpec spec = StatusEffectTable.Get(type);

        if (spec.Duration <= 0f)
            return;

        int i = (int)type;

        // 이미 위험 상태에 걸려 있으면 원본을 다시 쌓지 않는다.
        // 그러지 않으면 행동 불능 중에 게이지가 다시 차 무한 제압이 된다.
        StatusEffectType threshold = StatusEffectTable.ThresholdOf(type);

        if (threshold != StatusEffectType.None && Has(threshold))
            return;

        // 지속시간은 항상 갱신된다. 중첩형이든 아니든 "다시 걸면 처음부터"다.
        remaining[i] = spec.Duration * Mathf.Max(0.1f, durationScale);

        if (stacks[i] < spec.MaxStacks)
            stacks[i]++;

        // 더 센 공격으로 다시 걸면 기준 피해도 올라간다.
        // 낮은 피해로 덮어써서 도트를 약화시킬 수 없게 한다.
        if (baseDamage > sourceDamage[i])
            sourceDamage[i] = baseDamage;

        // 【최대 중첩 = 한계치】 차는 순간 질적으로 다른 것이 된다.
        // 원본 중첩을 전부 소모하므로 한계치가 끝나면 처음부터 다시 쌓아야 한다.
        if (threshold != StatusEffectType.None && stacks[i] >= spec.MaxStacks)
        {
            Clear(type);
            ApplyThreshold(threshold, baseDamage);
        }
    }

    /// <summary>
    /// 위험 상태를 건다. 전이로만 호출되며 중첩하지 않는다.
    /// Apply를 거치지 않는 이유 — 위험 상태는 다시 한계치로 전이하지 않는다.
    /// </summary>
    private void ApplyThreshold(StatusEffectType type, float baseDamage)
    {
        StatusEffectSpec spec = StatusEffectTable.Get(type);

        int i = (int)type;

        remaining[i] = spec.Duration;
        stacks[i] = 1;

        if (baseDamage > sourceDamage[i])
            sourceDamage[i] = baseDamage;
    }

    /// <summary>상태를 즉시 제거한다. 기폭(소비)과 면역이 이 경로를 쓴다.</summary>
    public void Clear(StatusEffectType type)
    {
        if (!IsValid(type))
            return;

        int i = (int)type;
        remaining[i] = 0f;
        stacks[i] = 0;
        sourceDamage[i] = 0f;
        pending[i] = 0f;
    }

    public void ClearAll()
    {
        Array.Clear(remaining, 0, TypeCount);
        Array.Clear(stacks, 0, TypeCount);
        Array.Clear(sourceDamage, 0, TypeCount);
        Array.Clear(pending, 0, TypeCount);
    }

    /// <summary>
    /// deltaTime만큼 시간을 진행시키고, 이번 구간에 적용할 피해 요청 목록을 채운다.
    ///
    /// 피해를 직접 적용하지 않고 요청만 만들어 반환하므로 속성 상성과
    /// 난이도 보정이 DamageResolver 한 곳에서만 계산된다.
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    /// <param name="isMoving">대상이 이동 중인지. 출혈 배증 판정에 쓴다.</param>
    /// <param name="buffer">결과를 담을 리스트. 호출자가 재사용해 GC Alloc을 막는다.</param>
    public void Tick(float deltaTime, bool isMoving, System.Collections.Generic.List<DamageRequest> buffer)
    {
        buffer?.Clear();

        if (deltaTime <= 0f)
            return;

        for (int i = 1; i < TypeCount; i++)
        {
            if (remaining[i] <= 0f)
                continue;

            var type = (StatusEffectType)i;
            StatusEffectSpec spec = StatusEffectTable.Get(type);

            // 남은 시간보다 deltaTime이 크면 남은 만큼만 피해를 준다.
            double elapsed = deltaTime < remaining[i] ? deltaTime : remaining[i];

            double perSecond = (double)sourceDamage[i] * spec.DamagePerSecondCoeff * Math.Max(1, stacks[i]);

            if (type == StatusEffectType.Bleed && isMoving)
                perSecond *= StatusEffectTable.BleedMovingMultiplier;

            remaining[i] -= deltaTime;

            bool expired = remaining[i] <= 0f;

            if (expired)
            {
                remaining[i] = 0f;
                stacks[i] = 0;
            }

            if (!spec.IsDamaging || buffer == null || sourceDamage[i] <= 0f)
            {
                if (expired)
                {
                    sourceDamage[i] = 0f;
                    pending[i] = 0f;
                }

                continue;
            }

            pending[i] += perSecond * elapsed;


            // 만료되는 틱에서는 남은 소수를 반올림해 흘린다.
            // 그래야 지속시간 전체의 총합이 문서 수치와 정확히 일치한다.
            int whole = expired
                ? (int)Math.Round(pending[i], MidpointRounding.AwayFromZero)
                : (int)Math.Floor(pending[i]);

            if (whole <= 0)
            {
                if (expired)
                {
                    sourceDamage[i] = 0f;
                    pending[i] = 0f;
                }

                continue;
            }

            pending[i] -= whole;

            if (expired)
            {
                sourceDamage[i] = 0f;
                pending[i] = 0f;
            }

            buffer.Add(new DamageRequest
            {
                baseDamage = whole,
                increasedPercent = 0f,
                element = spec.Element,
                hitKind = HitKind.Ranged,
                armourPenetration = 0,
                distance = -1f,
                effectiveRange = 0f,
                isCritical = false,
                criticalMultiplier = 1f,
                // 상태이상은 방어도를 무시한다. 이것이 「갑각」의 대응 수단이 된다.
                bypassArmour = true
            });
        }
    }

    /// <summary>감전이 적용된 "받는 피해" 배율. 중첩에 비례하며 최대 중첩에서 +20%다.</summary>
    public float DamageTakenMultiplier
        => 1f + StatusEffectTable.ShockDamageTakenBonus
             * StatusEffectTable.ControlRatio(
                 StatusEffectType.Shock, StacksOf(StatusEffectType.Shock));

    /// <summary>
    /// 이동·공격 속도 배율.
    ///
    /// 행동 불능(동결·마비)이면 0이다. 그 외에는 냉각 중첩에 비례해 최대 −40%.
    /// </summary>
    public float SpeedMultiplier
    {
        get
        {
            if (IsIncapacitated)
                return 0f;

            return 1f - StatusEffectTable.ChillSlowRatio
                      * StatusEffectTable.ControlRatio(
                          StatusEffectType.Chill, StacksOf(StatusEffectType.Chill));
        }
    }

    /// <summary>행동 불능인지. 동결 또는 마비. 이동도 공격도 하지 못한다.</summary>
    public bool IsIncapacitated
        => Has(StatusEffectType.Freeze) || Has(StatusEffectType.Paralyze);

    /// <summary>부식이 적용된 방어도 배율. 부식 중이면 절반이다.</summary>
    public float ArmourMultiplier
        => Has(StatusEffectType.Corrode) ? StatusEffectTable.CorrodeHalfRatio : 1f;

    /// <summary>부식이 적용된 회복량 배율. 소모품이 만능이 아니게 하는 장치다.</summary>
    public float HealingMultiplier
        => Has(StatusEffectType.Corrode) ? StatusEffectTable.CorrodeHalfRatio : 1f;

    /// <summary>점화가 깎는 방어도. 「점화가 다음 피해를 키운다」의 구현이다.</summary>
    public float ArmourReduction
        => Has(StatusEffectType.Ignite) ? StatusEffectTable.IgniteArmourReduction : 0f;

    /// <summary>응집이 적용된 상태 전이 범위 배율.</summary>
    public float SpreadMultiplier
        => Has(StatusEffectType.Congeal) ? StatusEffectTable.CongealSpreadMultiplier : 1f;

    private static bool IsValid(StatusEffectType type)
    {
        return type != StatusEffectType.None && (int)type < TypeCount;
    }
}
