using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 파밍의 조건 — 시설 상태 하나 + 레이드 특성 0~2개.
/// (docs/Blob_Hunting_System.md 5·9절)
///
/// 【파밍 전 화면에 전부 표시한다.】(문서 5절)
/// 들어가서 알게 하면 조건이 아니라 사고가 된다. 빌드를 바꿔서 대응하라는
/// 축인데, 들어간 뒤에는 바꿀 수가 없다.
///
/// MonoBehaviour 의존이 없는 순수 구조체다. EditMode 테스트 대상.
/// </summary>
public struct RaidConditions
{
    /// <summary>
    /// 한 판에 걸리는 특성의 최대 수.
    ///
    /// 【불확실 — 문서에 수치가 없다.】
    /// 셋을 걸면 「배치 하나 + 저항 하나 + 그 외 하나」로 꽉 차서
    /// 특성 목록을 다 외워야 판을 읽을 수 있다. 둘이면 한눈에 읽힌다.
    /// </summary>
    public const int MaxTraits = 2;

    public FacilityState facility;

    /// <summary>걸린 특성. null이거나 비어 있으면 없다.</summary>
    public RaidTrait[] traits;

    public static RaidConditions None => new RaidConditions
    {
        facility = FacilityState.Normal,
        traits = System.Array.Empty<RaidTrait>()
    };

    public bool Has(RaidTrait trait)
    {
        if (traits == null)
            return false;

        for (int i = 0; i < traits.Length; i++)
        {
            if (traits[i] == trait)
                return true;
        }

        return false;
    }

    // ── 배치 ──────────────────────────────────────────────────────────

    /// <summary>한 무리의 크기 배율. 밀집·산개가 이것만 건드린다.</summary>
    public float GroupSizeScale => Fold(e => e.groupSizeScale);

    /// <summary>무리가 흩어지는 정도의 배율.</summary>
    public float SpreadScale => Fold(e => e.spreadScale);

    // ── 그 외 ─────────────────────────────────────────────────────────

    /// <summary>출혈이 걸리지 않는가. (응고 혈액)</summary>
    public bool BlocksBleed => Has(RaidTrait.Congealed);

    /// <summary>장비 내구도 소모 배율. (부식 대기)</summary>
    public float DurabilityLossScale => Fold(e => e.durabilityLossScale);

    /// <summary>플레이어가 초당 받는 환경 피해. (소독 사이클)</summary>
    public float EnvironmentDamagePerSecond
        => FacilityStateTable.Of(facility).chemicalDamagePerSecond;

    /// <summary>
    /// 이 속성의 피해에 걸리는 배율. 침수에서 번개가 2배다.
    ///
    /// 【이것만은 곱한다.】 저항은 「가장 낮은 하나」지만, 이쪽은 반대 방향이다.
    /// 환경이 특정 빌드에게 기회를 주는 자리라 지워지면 안 된다.
    /// </summary>
    public float DamageMultiplier(DamageElement element)
    {
        FacilityStateEffect effect = FacilityStateTable.Of(facility);

        return effect.amplifiedElement == element ? Mathf.Max(1f, effect.amplifiedMultiplier) : 1f;
    }

    /// <summary>
    /// 이 몬스터 속성이 뽑힐 가중치 배율. 【중복 완화】(문서 9절)
    /// 여러 특성이 같은 속성을 가리키면 가장 낮은 것을 쓴다 — 곱하지 않는다.
    /// </summary>
    public float AffixWeight(EnemyAffix affix)
    {
        if (traits == null)
            return 1f;

        float lowest = 1f;

        for (int i = 0; i < traits.Length; i++)
            lowest = Mathf.Min(lowest, RaidTraitTable.AffixWeightScale(traits[i], affix));

        return lowest;
    }

    // ── 적에게 적용 ───────────────────────────────────────────────────

    /// <summary>
    /// 이미 만들어진 개체 수치에 파밍 조건을 얹는다.
    ///
    /// 【EnemyProfile.Build를 건드리지 않은 이유】
    /// 원형 · 등급 · 속성은 개체가 무엇인지를 정하고, 파밍 조건은
    /// 「이번 판이 어떤 판인가」다. 둘을 한 함수에 넣으면 같은 원형이
    /// 판마다 다른 것이 되어, 도감과 퀘스트가 무엇을 가리키는지 흐려진다.
    ///
    /// ⚠️ 저항은 곱하지 않는다. LowerTo가 가장 낮은 것 하나만 남긴다.
    /// </summary>
    public EnemyProfile Apply(EnemyProfile profile)
    {
        if (traits != null)
        {
            for (int i = 0; i < traits.Length; i++)
            {
                RaidTraitEffect effect = RaidTraitTable.Of(traits[i]);

                if (effect.givesResistance)
                {
                    profile.resistances.LowerTo(
                        effect.element, RaidTraitTable.ResistanceMultiplier);
                }

                // 재생은 겹쳐도 더하지 않는다 — 속성과 특성 둘 다 재생이면
                // 초당 4%가 되어 화력 요구가 두 배로 뛴다.
                if (effect.regenPerSecond > profile.regenPerSecond)
                    profile.regenPerSecond = effect.regenPerSecond;
            }
        }

        FacilityStateEffect state = FacilityStateTable.Of(facility);

        // 소독 사이클은 화공체를 강하게 만든다. 「그 상태에서 만나면 다르다」가
        // 없으면 추가 스폰은 그냥 숫자가 느는 것에 지나지 않는다.
        if (state.empowerScale > 1f && profile.archetype == state.empoweredArchetype)
        {
            profile.health = Mathf.Max(1, Mathf.RoundToInt(profile.health * state.empowerScale));
            profile.damage = Mathf.Max(1, Mathf.RoundToInt(profile.damage * state.empowerScale));
        }

        return profile;
    }

    /// <summary>파밍 전 화면에 적을 줄들. 시설 상태가 먼저다.</summary>
    public List<string> Describe()
    {
        var lines = new List<string>(MaxTraits + 1);

        if (facility != FacilityState.Normal)
        {
            lines.Add($"{FacilityStateTable.Name(facility)} — "
                      + FacilityStateTable.Describe(facility));
        }

        if (traits == null)
            return lines;

        for (int i = 0; i < traits.Length; i++)
        {
            if (traits[i] == RaidTrait.None)
                continue;

            lines.Add($"{RaidTraitTable.Name(traits[i])} — "
                      + RaidTraitTable.Describe(traits[i]));
        }

        return lines;
    }

    private float Fold(System.Func<RaidTraitEffect, float> pick)
    {
        if (traits == null)
            return 1f;

        float value = 1f;

        for (int i = 0; i < traits.Length; i++)
            value *= pick(RaidTraitTable.Of(traits[i]));

        return value;
    }
}
