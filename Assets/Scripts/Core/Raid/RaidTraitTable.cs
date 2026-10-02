using UnityEngine;

/// <summary>레이드 특성 하나가 하는 일.</summary>
public struct RaidTraitEffect
{
    /// <summary>저항을 주는 속성인가.</summary>
    public bool givesResistance;

    /// <summary>어떤 속성에. givesResistance가 false면 뜻이 없다.</summary>
    public DamageElement element;

    /// <summary>적이 초당 회복하는 최대 체력 비율. 0이면 없음.</summary>
    public float regenPerSecond;

    /// <summary>한 무리의 크기 배율. 1이면 그대로.</summary>
    public float groupSizeScale;

    /// <summary>무리가 흩어지는 정도의 배율. 1이면 그대로.</summary>
    public float spreadScale;

    /// <summary>출혈이 걸리지 않는가. (피 없는 잡귀)</summary>
    public bool blocksBleed;

    /// <summary>장비 내구도 소모 배율. 1이면 그대로. (눅눅한 밤)</summary>
    public float durabilityLossScale;

    /// <summary>
    /// 이 특성과 같은 계열의 몬스터 속성. 출현 확률을 낮춘다.
    ///
    /// 【중복 완화】(문서 9절)
    /// 「돌을 삼킨 잡귀」 레이드에서 「경화」 적까지 자주 나오면,
    /// 물리 빌드가 아예 못 싸우는 구간이 생긴다. 저항이 곱해지지 않더라도
    /// 「전부 ×0.5」인 판은 그 빌드에게 재미가 아니라 벽이다.
    /// </summary>
    public EnemyAffix overlappingAffix;
}

/// <summary>
/// 레이드 특성 10종의 고정 표. (docs/Blob_Hunting_System.md 9절)
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class RaidTraitTable
{
    public const int Count = 10;

    /// <summary>저항 특성이 주는 배율. 몬스터 속성과 같은 값이다.</summary>
    public const float ResistanceMultiplier = EnemyAffixTable.ResistanceMultiplier;

    /// <summary>
    /// 되살아나는 잡귀의 초당 회복량(최대 체력 대비).
    /// 몬스터 속성 「재생」과 같은 값을 쓴다 — 같은 이름의 것이 두 값을 갖지 않는다.
    /// </summary>
    public const float RegenPerSecond = EnemyAffixTable.RegenPerSecond;

    /// <summary>
    /// 밀집·산개의 무리 크기 배율.
    /// 【불확실 — 문서에 수치가 없다.】 「조우 단위를 키운다」만 적혀 있다.
    /// 1.6배면 3마리 무리가 5마리가 된다 — 전령(처치 시 연쇄)이 값을 하는 크기다.
    /// </summary>
    public const float DenseGroupScale = 1.6f;

    /// <summary>산개는 그 반대. 0.6배면 3마리가 2마리가 된다.</summary>
    public const float ScatteredGroupScale = 0.6f;

    /// <summary>밀집은 좁게, 산개는 넓게 흩어진다. 【불확실】</summary>
    public const float DenseSpreadScale = 0.6f;
    public const float ScatteredSpreadScale = 1.8f;

    /// <summary>
    /// 눅눅한 밤의 내구도 소모 배율.
    /// 【불확실 — 문서에 수치가 없다.】 2배면 한 판에 장비가 반쯤 망가진다.
    /// 1.5배는 「오래 있으면 손해」를 느끼되 한 판을 버리게 하지는 않는 선이다.
    /// </summary>
    public const float CorrosiveDurabilityScale = 1.5f;

    /// <summary>
    /// 중복 완화 — 같은 계열 몬스터 속성의 출현 가중치 배율.
    /// 【불확실 — 문서는 「낮춘다」고만 적었다.】 0으로 두면 그 속성이
    /// 아예 사라져 「돌을 삼킨 잡귀 판에서는 경화가 없다」는 이상한 규칙이 된다.
    /// </summary>
    public const float OverlapWeightScale = 0.35f;

    public static RaidTraitEffect Of(RaidTrait trait)
    {
        switch (trait)
        {
            case RaidTrait.Dense:
                return Make(groupScale: DenseGroupScale, spreadScale: DenseSpreadScale);

            case RaidTrait.Scattered:
                return Make(groupScale: ScatteredGroupScale, spreadScale: ScatteredSpreadScale);

            case RaidTrait.Hardened:
                return Resist(DamageElement.Physical, EnemyAffix.Hardened);

            case RaidTrait.FireProof:
                return Resist(DamageElement.Fire, EnemyAffix.FireProof);

            case RaidTrait.ColdAdapted:
                return Resist(DamageElement.Cold, EnemyAffix.Cryostable);

            case RaidTrait.Insulated:
                return Resist(DamageElement.Lightning, EnemyAffix.Insulated);

            case RaidTrait.Antibody:
                return Resist(DamageElement.Chaos, EnemyAffix.Antibody);

            case RaidTrait.Regenerating:
                return Make(regen: RegenPerSecond, overlap: EnemyAffix.Regenerating);

            case RaidTrait.Congealed:
                return Make(blocksBleed: true);

            case RaidTrait.Corrosive:
                return Make(durability: CorrosiveDurabilityScale);

            default:
                return Make();
        }
    }

    public static string Name(RaidTrait trait)
    {
        switch (trait)
        {
            case RaidTrait.Dense:        return "떼 지은 밤";
            case RaidTrait.Scattered:    return "흩어진 밤";
            case RaidTrait.Hardened:     return "돌을 삼킨 잡귀";
            case RaidTrait.FireProof:    return "불을 삼킨 잡귀";
            case RaidTrait.ColdAdapted:  return "얼음을 삼킨 잡귀";
            case RaidTrait.Insulated:    return "벼락을 삼킨 잡귀";
            case RaidTrait.Antibody:     return "독을 삼킨 잡귀";
            case RaidTrait.Regenerating: return "되살아나는 잡귀";
            case RaidTrait.Congealed:    return "피 없는 잡귀";
            case RaidTrait.Corrosive:    return "눅눅한 밤";
            default:                     return string.Empty;
        }
    }

    /// <summary>파밍 전 화면에 적을 한 줄. 「무엇이 달라지는가」만 적는다.</summary>
    public static string Describe(RaidTrait trait)
    {
        switch (trait)
        {
            case RaidTrait.Dense:        return "적이 크게 뭉쳐 나온다";
            case RaidTrait.Scattered:    return "적이 흩어져 순찰한다";
            case RaidTrait.Hardened:     return "물리 피해 절반";
            case RaidTrait.FireProof:    return "화염 피해 절반";
            case RaidTrait.ColdAdapted:  return "냉기 피해 절반";
            case RaidTrait.Insulated:    return "번개 피해 절반";
            case RaidTrait.Antibody:     return "카오스 피해 절반";
            case RaidTrait.Regenerating: return "적이 체력을 회복한다";
            case RaidTrait.Congealed:    return "출혈이 걸리지 않는다";
            case RaidTrait.Corrosive:    return "장비가 빨리 닳는다";
            default:                     return string.Empty;
        }
    }

    /// <summary>
    /// 두 특성을 같이 걸어도 되는가.
    ///
    /// 【배치 계열 둘은 정반대다.】(문서 9절 ※)
    /// 밀집과 산개를 같이 걸면 서로를 지워 아무 일도 일어나지 않는다.
    ///
    /// 【저항 특성도 하나만 건다.】
    /// 문서 9절 — "깊이는 여러 속성에 동시에 저항하는 적으로 낸다."
    /// 그건 개체 단위의 이야기다. 맵 전체에 둘을 걸면 남은 속성이 셋뿐이라
    /// 「빌드를 바꾸면 된다」가 「이 빌드로는 못 온다」가 된다.
    /// </summary>
    public static bool CanCombine(RaidTrait a, RaidTrait b)
    {
        if (a == RaidTrait.None || b == RaidTrait.None || a == b)
            return a != b;

        if (IsPlacement(a) && IsPlacement(b))
            return false;

        return !(Of(a).givesResistance && Of(b).givesResistance);
    }

    public static bool IsPlacement(RaidTrait trait)
        => trait == RaidTrait.Dense || trait == RaidTrait.Scattered;

    /// <summary>
    /// 이 특성이 걸린 판에서 그 몬스터 속성이 뽑힐 가중치 배율.
    /// 겹치지 않으면 1이다.
    /// </summary>
    public static float AffixWeightScale(RaidTrait trait, EnemyAffix affix)
    {
        RaidTraitEffect effect = Of(trait);

        return effect.overlappingAffix != EnemyAffix.None && effect.overlappingAffix == affix
            ? OverlapWeightScale
            : 1f;
    }

    private static RaidTraitEffect Resist(DamageElement element, EnemyAffix overlap)
    {
        return new RaidTraitEffect
        {
            givesResistance = true,
            element = element,
            groupSizeScale = 1f,
            spreadScale = 1f,
            durabilityLossScale = 1f,
            overlappingAffix = overlap
        };
    }

    private static RaidTraitEffect Make(
        float regen = 0f, float groupScale = 1f, float spreadScale = 1f,
        bool blocksBleed = false, float durability = 1f,
        EnemyAffix overlap = EnemyAffix.None)
    {
        return new RaidTraitEffect
        {
            givesResistance = false,
            element = DamageElement.Physical,
            regenPerSecond = regen,
            groupSizeScale = groupScale,
            spreadScale = spreadScale,
            blocksBleed = blocksBleed,
            durabilityLossScale = durability,
            overlappingAffix = overlap
        };
    }
}
