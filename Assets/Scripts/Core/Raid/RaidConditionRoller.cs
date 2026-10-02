using System.Collections.Generic;

/// <summary>
/// 파밍 조건을 뽑는다.
///
/// 【무작위를 주입받는다.】 같은 씨앗이면 같은 판이 나와야
/// "이 조건에서 이 빌드가 되는가"를 두 번 확인할 수 있다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class RaidConditionRoller
{
    /// <summary>
    /// 밤 상태가 걸릴 확률.
    ///
    /// 【불확실 — 문서에 수치가 없다.】
    /// 절반이 넘으면 평시가 오히려 특별해진다. 셋 중 하나쯤이
    /// "이번엔 뭐가 걸렸나"를 확인하게 만드는 선이다.
    /// </summary>
    public const float FacilityChance = 0.35f;

    /// <summary>특성이 하나라도 걸릴 확률. 【불확실】</summary>
    public const float TraitChance = 0.5f;

    /// <summary>특성이 걸렸을 때 두 개가 될 확률. 【불확실】</summary>
    public const float SecondTraitChance = 0.3f;

    private static readonly RaidTrait[] AllTraits =
    {
        RaidTrait.Dense, RaidTrait.Scattered,
        RaidTrait.Hardened, RaidTrait.FireProof, RaidTrait.ColdAdapted,
        RaidTrait.Insulated, RaidTrait.Antibody,
        RaidTrait.Regenerating, RaidTrait.Congealed, RaidTrait.Corrosive
    };

    private static readonly FacilityState[] AllStates =
    {
        FacilityState.Blackout, FacilityState.Flooded, FacilityState.Decontamination
    };

    public static RaidConditions Roll(System.Random random)
    {
        if (random == null)
            return RaidConditions.None;

        var conditions = new RaidConditions
        {
            facility = random.NextDouble() < FacilityChance
                ? AllStates[random.Next(AllStates.Length)]
                : FacilityState.Normal,

            traits = System.Array.Empty<RaidTrait>()
        };

        if (random.NextDouble() >= TraitChance)
            return conditions;

        var picked = new List<RaidTrait>(RaidConditions.MaxTraits);

        picked.Add(AllTraits[random.Next(AllTraits.Length)]);

        if (random.NextDouble() < SecondTraitChance)
        {
            // 같이 걸 수 있는 것만 후보로 남긴다. 다 걸러지면 하나로 끝난다 —
            // 「두 번째를 반드시 채운다」로 두면 금지 조합을 우회하게 된다.
            var candidates = new List<RaidTrait>(AllTraits.Length);

            for (int i = 0; i < AllTraits.Length; i++)
            {
                if (RaidTraitTable.CanCombine(picked[0], AllTraits[i]))
                    candidates.Add(AllTraits[i]);
            }

            if (candidates.Count > 0)
                picked.Add(candidates[random.Next(candidates.Count)]);
        }

        conditions.traits = picked.ToArray();

        return conditions;
    }
}
