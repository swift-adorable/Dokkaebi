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
    // None 포함. 해로운 9종 + 이로운 9종(가속·강화·회복·소지 중량 증가·저항 4·폭주) + 대가 1종.
    private const int TypeCount = 20;

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

    /// <summary>
    /// 중첩을 지정한 수만큼만 덜어 낸다. 【소모품의 「출혈 2중첩 제거」가 이 경로다.】
    ///
    /// 전부 지우는 Clear와 나눠 두는 이유 — 싼 도구가 비싼 도구와 같은 일을
    /// 하면 비싼 쪽을 살 이유가 없다. 덕코프도 소형 구급상자는 출혈 1중첩,
    /// 구급상자는 2중첩을 지운다. [확인됨 — 아이템 #17 · #16]
    ///
    /// 남은 중첩이 0이 되면 지속시간도 함께 끝난다 — 중첩 0인데 상태가
    /// 살아 있으면 「걸려 있는데 아무 일도 안 일어나는」 칸이 남는다.
    /// </summary>
    /// <returns>실제로 덜어 낸 중첩 수.</returns>
    public int RemoveStacks(StatusEffectType type, int count)
    {
        if (!IsValid(type) || count <= 0)
            return 0;

        int i = (int)type;

        int removed = Mathf.Min(count, stacks[i]);

        if (removed <= 0)
            return 0;

        stacks[i] -= removed;

        if (stacks[i] <= 0)
            Clear(type);

        return removed;
    }

    public void ClearAll()
    {
        Array.Clear(remaining, 0, TypeCount);
        Array.Clear(stacks, 0, TypeCount);
        Array.Clear(sourceDamage, 0, TypeCount);
        Array.Clear(pending, 0, TypeCount);

        pendingHeal = 0d;
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

        // 【대가는 이 순회가 끝난 뒤에 건다.】 루프 안에서 바로 걸면,
        // 대가의 번호가 지금 번호보다 크면 같은 프레임에 다시 순회되어
        // 같은 deltaTime으로 그 자리에서 만료된다. 쇠약이 걸리자마자
        // 사라져 「대가가 없는 노란 주사약」가 됐다.
        aftermathPending = 0;

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

            // 【「회복」 상태는 피해가 아니다.】 buffer는 피해 요청만 담으므로
            // 여기서 따로 모아 두고 Health가 가져간다. 도트와 같은 소수 누적을
            // 쓰는 이유도 같다 — 틱을 잘게 쪼갤수록 총량이 부풀면 안 된다.
            if (type == StatusEffectType.Regen)
            {
                pendingHeal += StatusEffectTable.RegenPerSecond * elapsed;

                if (expired)
                    MarkAftermath(type);

                continue;
            }

            // 가속이 끝나면 쇠약이 온다. 대가를 별도 필드로 두지 않는다.
            if (expired)
                MarkAftermath(type);

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

        FlushAftermath();
    }

    /// <summary>이번 순회에서 끝난 것들의 대가를 모아 두는 비트 자리.</summary>
    private int aftermathPending;

    private void MarkAftermath(StatusEffectType type)
    {
        StatusEffectType aftermath = StatusEffectTable.AftermathOf(type);

        if (aftermath != StatusEffectType.None)
            aftermathPending |= 1 << (int)aftermath;
    }

    private void FlushAftermath()
    {
        if (aftermathPending == 0)
            return;

        for (int i = 1; i < TypeCount; i++)
        {
            if ((aftermathPending & (1 << i)) == 0)
                continue;

            remaining[i] = StatusEffectTable.Get((StatusEffectType)i).Duration;
            stacks[i] = 1;
        }

        aftermathPending = 0;
    }

    /// <summary>아직 정수로 떨어지지 않은 회복의 잔여분. 도트의 pending과 같은 이유다.</summary>
    private double pendingHeal;

    /// <summary>
    /// 이번에 적용할 회복량을 가져가고 비운다. Health가 Tick 직후에 부른다.
    ///
    /// 【1 미만은 넘기지 않는다.】 초당 2로 60프레임이면 프레임당 0.033이라
    /// 그대로 넘기면 매 프레임 0이 되어 영원히 회복되지 않는다.
    /// </summary>
    public int ConsumeHealing()
    {
        if (pendingHeal < 1d)
            return 0;

        int whole = (int)Math.Floor(pendingHeal);

        pendingHeal -= whole;

        return whole;
    }

    // 【대가는 면역으로 막히지 않는다.】 Apply를 거치지 않는 이유다.
    // 「좋은 것만 받고 대가는 피한다」가 되면 강화 소모품이 순수 증가가 된다.

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

            float scale = 1f - StatusEffectTable.ChillSlowRatio
                             * StatusEffectTable.ControlRatio(
                                 StatusEffectType.Chill, StacksOf(StatusEffectType.Chill));

            // 【이로운 것과 해로운 것을 곱으로 겹친다.】 더하기로 겹치면
            // 가속(+25%)과 냉각(−40%)이 −15%가 되어 「느려진 채로 빨라진」
            // 상태가 된다. 곱이면 0.75 × 1.25 = 0.94로, 둘 다 걸려 있다는
            // 사실이 수치에 남는다.
            if (Has(StatusEffectType.Haste))
                scale *= 1f + StatusEffectTable.HasteSpeedBonus;

            if (Has(StatusEffectType.Fatigue))
                scale *= 1f - StatusEffectTable.FatigueSpeedPenalty;

            if (Has(StatusEffectType.Frenzy))
                scale *= 1f + StatusEffectTable.FrenzySpeedBonus;

            return scale;
        }
    }

    /// <summary>행동 불능인지. 동결 또는 마비. 이동도 공격도 하지 못한다.</summary>
    public bool IsIncapacitated
        => Has(StatusEffectType.Freeze) || Has(StatusEffectType.Paralyze);

    /// <summary>부식이 적용된 방어도 배율. 부식 중이면 절반이다.</summary>
    public float ArmourMultiplier
        => Has(StatusEffectType.Corrode) ? StatusEffectTable.CorrodeHalfRatio : 1f;

    /// <summary>
    /// 강화가 더하는 방어도. 【배율이 아니라 덧셈이다.】
    ///
    /// 방어 공식이 2/(방어도−관통+2)이라 배율이면 이미 두꺼운 쪽이 더
    /// 이득을 본다. 덧셈이면 얇은 쪽의 체감이 커서 「맨몸에 한 대 버틸
    /// 것을 준다」가 된다. 덕코프도 「신체 방어구 +0.5」다. [확인됨]
    ///
    /// 부식(절반)이 곱해진 **뒤에** 더해진다 — 대가를 치르고 얻은 것을
    /// 남의 상태이상이 반으로 깎으면 강화 주사약을 쓸 이유가 사라진다.
    /// </summary>
    public float ArmourBonus
        => (Has(StatusEffectType.Bolster) ? StatusEffectTable.BolsterArmourBonus : 0f)
         + (Has(StatusEffectType.Frenzy) ? StatusEffectTable.FrenzyArmourBonus : 0f);

    /// <summary>
    /// 상태이상이 더하는 최대 체력. 지금은 폭주(+10)뿐이다.
    ///
    /// 끝나면 상한이 도로 줄고, 넘치는 체력은 잘린다 — 그것도 대가다.
    /// 「폭주로 늘린 10을 회복약으로 채워 두고 끝나면 그대로 남는다」가
    /// 되면 최대 체력을 사서 쌓는 방법이 생긴다.
    /// </summary>
    public int MaxHealthBonus
        => Has(StatusEffectType.Frenzy) ? StatusEffectTable.FrenzyMaxHealthBonus : 0;

    /// <summary>소지 중량 증가(중량 주사약)가 곱하는 최대 소지 중량 배율.</summary>
    public float CarryWeightMultiplier
        => Has(StatusEffectType.Overload)
            ? StatusEffectTable.OverloadWeightMultiplier
            : 1f;

    /// <summary>
    /// 걸려 있는 저항을 저항에 반영한다.
    ///
    /// 【장비 저항에 그냥 곱한다 — LowerTo를 쓰지 않는다.】
    /// LowerTo에는 「이미 잘 막고 있으면 더 못 깎는다」는 바닥이 있다.
    /// 그것은 **적의 속성을 합성할 때** 방어형 둘만으로 공략 불가가 되는
    /// 것을 막는 규칙이고, 플레이어가 쓴 소모품에는 맞지 않는다.
    /// 그 바닥을 그대로 쓰면 화염 저항 장비를 낀 사람에게 화염 저항 주사약이 아무 일도
    /// 하지 않는다 — 「썼는데 변화가 없다」가 된다.
    ///
    /// 곱해도 면역(0)에 닿지 않으므로 안전하다. 0.75는 0을 만들지 못한다.
    /// 약점도 뒤집지 않는다 — 전기 2.0에 전기 저항을 걸면 1.5가 된다.
    /// 「얘는 전기로 잡아라」는 그대로 남는다.
    ///
    /// 저항 넷은 서로 다른 속성을 맡으므로 한 속성에 두 번 곱해지지 않는다.
    /// </summary>
    public void ApplyWards(ref ElementalResistances resistances)
    {
        for (int i = 1; i < TypeCount; i++)
        {
            if (remaining[i] <= 0f)
                continue;

            var type = (StatusEffectType)i;

            if (!StatusEffectTable.IsWard(type))
                continue;

            DamageElement element = StatusEffectTable.Get(type).Element;

            resistances.Set(element,
                resistances.Get(element) * StatusEffectTable.WardMultiplier);
        }
    }

    /// <summary>
    /// 회복량 배율. 소모품이 만능이 아니게 하는 장치다.
    ///
    /// 부식과 강화가 같은 방향으로 깎는다 — 둘 다 걸리면 0.25다.
    /// 【강화를 쓰고 부식에 걸리면 회복이 거의 안 든다】가 맞는 결과다.
    /// 방어를 택한 대가를 두 번 치르는 것이 아니라, 방어를 택했으니
    /// 맞고 버티는 쪽이 아니라 안 맞는 쪽으로 풀라는 뜻이다.
    /// </summary>
    public float HealingMultiplier
    {
        get
        {
            float scale = Has(StatusEffectType.Corrode)
                ? StatusEffectTable.CorrodeHalfRatio
                : 1f;

            if (Has(StatusEffectType.Bolster))
                scale *= StatusEffectTable.BolsterHealingMultiplier;

            return scale;
        }
    }

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
