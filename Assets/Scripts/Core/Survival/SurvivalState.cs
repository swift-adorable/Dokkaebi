using UnityEngine;

/// <summary>
/// 수분 · 에너지 두 축의 상태와 그로부터 나오는 페널티.
/// (docs/Dokkaebi_Survival_System.md · 결정 2-32)
///
/// 【왜 순수 클래스인가】
/// "15분 뒤에 탈수가 되는가", "3중첩까지 몇 초 걸리는가"는 게임을 띄우지 않고
/// 확인할 수 있어야 한다. 시간은 Tick에 넘겨받는다 — Time.deltaTime을 읽지 않는다.
///
/// 【체력은 여기서 다루지 않는다.】 이미 Health가 있다. 이 클래스는 새로 생긴
/// 두 축만 맡고, 허기의 지속 피해는 「얼마를 줄지」만 돌려준다.
/// 실제로 깎는 것은 부르는 쪽(PlayerSurvival)의 몫이다.
/// </summary>
public class SurvivalState
{
    private float water;
    private float energy;

    private float maxWater;
    private float maxEnergy;

    /// <summary>에너지가 바닥난 채 흐른 시간. 중첩을 올린다.</summary>
    private float starvingTime;

    /// <summary>마지막 허기 피해 이후 흐른 시간.</summary>
    private float damageTimer;

    public SurvivalState(float maxWaterValue = SurvivalTable.MaxWater,
                         float maxEnergyValue = SurvivalTable.MaxEnergy)
    {
        maxWater = Mathf.Max(1f, maxWaterValue);
        maxEnergy = Mathf.Max(1f, maxEnergyValue);

        Refill();
    }

    public float Water => water;
    public float Energy => energy;

    public float MaxWater => maxWater;
    public float MaxEnergy => maxEnergy;

    public float WaterRatio => water / maxWater;
    public float EnergyRatio => energy / maxEnergy;

    /// <summary>수분이 바닥났는가.</summary>
    public bool IsDehydrated => water <= 0f;

    /// <summary>에너지가 바닥났는가.</summary>
    public bool IsStarving => energy <= 0f;

    /// <summary>
    /// 허기 중첩(0~3). 바닥난 채 버틸수록 오른다.
    /// 0이면 아직 허기가 걸리지 않았거나 막 걸린 것이다.
    /// </summary>
    public int StarvingStacks
    {
        get
        {
            if (!IsStarving)
                return 0;

            int grown = 1 + Mathf.FloorToInt(starvingTime / SurvivalTable.StarvingStackInterval);

            return Mathf.Min(grown, SurvivalTable.StarvingMaxStacks);
        }
    }

    /// <summary>
    /// 지금 곱해야 할 이동 배율. 둘 다 걸리면 곱한다.
    ///
    /// 【더하지 않고 곱하는 이유】 −20%와 −10%를 더해 −30%로 두면
    /// 세 번째 페널티가 생겼을 때 0 이하로 떨어진다. 곱은 절대 0이 되지 않는다.
    /// </summary>
    public float MoveMultiplier
    {
        get
        {
            float scale = 1f;

            if (IsDehydrated)
                scale *= SurvivalTable.DehydratedMoveScale;

            if (IsStarving)
                scale *= SurvivalTable.StarvingMoveScale;

            return scale;
        }
    }

    /// <summary>
    /// 지금 에너지가 차는 비율. 탈수면 덕코프대로 30%만 찬다.
    ///
    /// 【물부터 마셔야 음식이 제값을 한다.】 두 게이지가 따로 놀지 않고 엮인다.
    /// 체력(HP) 회복에는 손대지 않는다 — 이 축은 철수 압박용이지 전투 페널티가 아니다.
    /// </summary>
    public float EnergyRestoreMultiplier
        => IsDehydrated ? SurvivalTable.DehydratedEnergyRestoreScale : 1f;

    // ── 시간 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 시간을 흘린다. 돌려주는 값은 【이번 틱에 허기로 입어야 할 피해】다.
    /// 0이면 피해가 없다.
    /// </summary>
    /// <param name="deltaTime">흐른 시간(초).</param>
    /// <param name="drainMultiplier">소모율 배율. 과중량 단계가 정한다.</param>
    /// <param name="waterScale">수분에만 곱한다 — 폭염 · 독안개 (결정 2-61).</param>
    /// <param name="energyScale">에너지에만 곱한다 — 추위 중첩 (결정 2-61).</param>
    public int Tick(float deltaTime, float drainMultiplier = 1f, float waterScale = 1f, float energyScale = 1f)
    {
        if (deltaTime <= 0f)
            return 0;

        float scale = Mathf.Max(0f, drainMultiplier);

        water = Mathf.Max(0f, water - SurvivalTable.WaterDrainPerSecond * scale * Mathf.Max(0f, waterScale) * deltaTime);
        energy = Mathf.Max(0f, energy - SurvivalTable.EnergyDrainPerSecond * scale * Mathf.Max(0f, energyScale) * deltaTime);

        return TickStarvation(deltaTime);
    }

    private int TickStarvation(float deltaTime)
    {
        if (!IsStarving)
        {
            // 먹으면 중첩이 풀린다. 다시 굶으면 1중첩부터 시작한다.
            starvingTime = 0f;
            damageTimer = 0f;

            return 0;
        }

        starvingTime += deltaTime;
        damageTimer += deltaTime;

        if (damageTimer < SurvivalTable.StarvingDamageInterval)
            return 0;

        // 한 틱이 길어도 밀린 만큼 전부 준다 — 프레임이 튀었다고 피해가 사라지면
        // 「버벅이면 안 아프다」가 된다.
        int hits = Mathf.FloorToInt(damageTimer / SurvivalTable.StarvingDamageInterval);

        damageTimer -= hits * SurvivalTable.StarvingDamageInterval;

        return hits * StarvingStacks * SurvivalTable.StarvingDamagePerStack;
    }

    // ── 채우고 비우기 ─────────────────────────────────────────────────

    /// <summary>
    /// 음료·음식이 채운다. 8단계의 소모품이 쓸 입구다.
    ///
    /// 【배율은 부르기 전의 상태로 정한다.】 물을 먼저 더하고 나서 재면,
    /// 물과 음식이 한 아이템에 들어 있을 때와 따로 먹을 때의 결과가 달라진다.
    /// 「마시기 전에 목이 말랐다면 그 끼니는 덜 찬다」가 설명하기 쉽다.
    /// </summary>
    public void Restore(float waterAmount, float energyAmount)
    {
        float energyScale = EnergyRestoreMultiplier;

        water = Mathf.Clamp(water + Mathf.Max(0f, waterAmount), 0f, maxWater);

        energy = Mathf.Clamp(
            energy + Mathf.Max(0f, energyAmount) * energyScale, 0f, maxEnergy);

        ClearStarvationIfFed();
    }

    /// <summary>
    /// 굶주림이 풀렸으면 누적을 지운다.
    ///
    /// 【Tick을 기다리지 않는다.】 예전에는 다음 틱에서야 지웠다. 그래서
    /// 「먹었는데 중첩이 아직 3」인 순간이 한 프레임 존재했고, 그 프레임에
    /// 다시 굶으면 1중첩부터가 아니라 3중첩부터 시작했다.
    /// 값을 바꾼 자리에서 바로 정리하는 편이 안전하다.
    /// </summary>
    private void ClearStarvationIfFed()
    {
        if (IsStarving)
            return;

        starvingTime = 0f;
        damageTimer = 0f;
    }

    /// <summary>
    /// 강화 소모품의 대가. 덕코프의 주사약이 거의 전부 수분을 태운다 —
    /// 「버프를 쓸수록 물이 급해진다」가 소모품을 무한히 쓰지 못하게 하는 장치다.
    /// </summary>
    public void Drain(float waterAmount, float energyAmount)
    {
        water = Mathf.Clamp(water - Mathf.Max(0f, waterAmount), 0f, maxWater);
        energy = Mathf.Clamp(energy - Mathf.Max(0f, energyAmount), 0f, maxEnergy);
    }

    /// <summary>파밍할 때 가득 채운다. 판이 시작부터 불리하면 안 된다.</summary>
    public void Refill()
    {
        water = maxWater;
        energy = maxEnergy;

        starvingTime = 0f;
        damageTimer = 0f;
    }

    /// <summary>
    /// 최대치를 바꾼다. 장비·패시브가 올릴 자리다(Survival_System 8절 TBD).
    /// 지금 값이 새 최대치를 넘으면 잘라 낸다.
    /// </summary>
    public void SetMax(float newMaxWater, float newMaxEnergy)
    {
        maxWater = Mathf.Max(1f, newMaxWater);
        maxEnergy = Mathf.Max(1f, newMaxEnergy);

        water = Mathf.Min(water, maxWater);
        energy = Mathf.Min(energy, maxEnergy);

        ClearStarvationIfFed();
    }
}
