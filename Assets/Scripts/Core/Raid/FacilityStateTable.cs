using UnityEngine;

/// <summary>밤 상태 하나가 하는 일.</summary>
public struct FacilityStateEffect
{
    /// <summary>이 상태에서 추가로 나오는 유형.</summary>
    public EnemyArchetype extraSpawn;

    /// <summary>시야 거리 배율. 적과 플레이어 양쪽에 걸린다.</summary>
    public float visionScale;

    /// <summary>순라귀가 멈추는가. (그믐)</summary>
    public bool disablesSentry;

    /// <summary>이동 속도 배율. 적과 플레이어 양쪽에 걸린다.</summary>
    public float moveScale;

    /// <summary>이 속성의 피해가 커진다. 배율이 1이면 뜻이 없다.</summary>
    public DamageElement amplifiedElement;
    public float amplifiedMultiplier;

    /// <summary>플레이어가 초당 받는 화학 피해. 0이면 없음.</summary>
    public float chemicalDamagePerSecond;

    /// <summary>이 유형이 강해진다. 배율은 EmpowerScale.</summary>
    public EnemyArchetype empoweredArchetype;
    public float empowerScale;
}

/// <summary>
/// 밤 상태 3종의 고정 표. (docs/Dokkaebi_Hunting_System.md 5절)
///
/// 【한 번 나온 개체는 상태가 풀려도 사라지지 않는다.】(문서 5절)
/// 그래서 이 표는 「스폰할 때 무엇을 더 섞을까」만 정하고,
/// 이미 있는 개체를 지우는 일은 하지 않는다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class FacilityStateTable
{
    public const int Count = 3;

    // ── 수치 ──────────────────────────────────────────────────────────
    // 문서는 「대폭 감소」 「이동 감소」 「지속 화학 피해」처럼 말로만 적었다.
    // 아래 숫자는 전부 【불확실】이며, 그 서술이 지키는 관계만 지킨다.
    // 관계는 테스트가 강제한다.

    /// <summary>
    /// 그믐의 시야 배율. 「대폭 감소」라 절반 아래로 둔다.
    /// 0.45면 기본 시야 18m가 8m가 된다 — 총성(22m)보다 짧아져
    /// 「보기 전에 듣는다」가 뒤집힌다. 그것이 그믐의 정체다.
    /// </summary>
    public const float BlackoutVisionScale = 0.45f;

    /// <summary>큰물의 이동 배율. 「감소」이지 「못 움직인다」가 아니다.</summary>
    public const float FloodedMoveScale = 0.75f;

    /// <summary>큰물에서 감전 피해 배율. 문서에 「2배」로 적혀 있다. [확인됨]</summary>
    public const float FloodedLightningMultiplier = 2f;

    /// <summary>
    /// 독안개의 초당 화학 피해.
    /// 방어구 없이 버틸 수는 있되 오래 머물면 죽는 선으로 둔다 —
    /// 기본 체력 100 기준 100초면 「환경 장비를 갖춰 오라」는 뜻이 된다.
    /// </summary>
    public const float DecontaminationDamagePerSecond = 1f;

    /// <summary>독안개에서 왕지네가 강해지는 배율. 【불확실】</summary>
    public const float EmpowerScale = 1.3f;

    public static FacilityStateEffect Of(FacilityState state)
    {
        switch (state)
        {
            // 시야를 줄이는 대신 순라귀를 멈춘다 — 양날의 본보기다.
            case FacilityState.Blackout:
                return new FacilityStateEffect
                {
                    extraSpawn = EnemyArchetype.Wraith,
                    visionScale = BlackoutVisionScale,
                    disablesSentry = true,
                    moveScale = 1f,
                    amplifiedElement = DamageElement.Physical,
                    amplifiedMultiplier = 1f,
                    empoweredArchetype = EnemyArchetype.Wraith,
                    empowerScale = 1f
                };

            // 느려지는 대신 번개 빌드가 두 배로 일한다.
            case FacilityState.Flooded:
                return new FacilityStateEffect
                {
                    extraSpawn = EnemyArchetype.Lurker,
                    visionScale = 1f,
                    moveScale = FloodedMoveScale,
                    amplifiedElement = DamageElement.Lightning,
                    amplifiedMultiplier = FloodedLightningMultiplier,
                    empoweredArchetype = EnemyArchetype.Lurker,
                    empowerScale = 1f
                };

            // 계속 깎이는 대신 왕지네가 몰려 나와 처치 밀도가 높다.
            case FacilityState.Decontamination:
                return new FacilityStateEffect
                {
                    extraSpawn = EnemyArchetype.Chemic,
                    visionScale = 1f,
                    moveScale = 1f,
                    amplifiedElement = DamageElement.Physical,
                    amplifiedMultiplier = 1f,
                    chemicalDamagePerSecond = DecontaminationDamagePerSecond,
                    empoweredArchetype = EnemyArchetype.Chemic,
                    empowerScale = EmpowerScale
                };

            default:
                return new FacilityStateEffect
                {
                    extraSpawn = EnemyArchetype.Scav,
                    visionScale = 1f,
                    moveScale = 1f,
                    amplifiedElement = DamageElement.Physical,
                    amplifiedMultiplier = 1f,
                    empoweredArchetype = EnemyArchetype.Scav,
                    empowerScale = 1f
                };
        }
    }

    public static string Name(FacilityState state)
    {
        switch (state)
        {
            case FacilityState.Blackout:        return "그믐";
            case FacilityState.Flooded:         return "큰물";
            case FacilityState.Decontamination: return "독안개";
            default:                            return "여느 밤";
        }
    }

    /// <summary>
    /// 파밍 전 화면에 적을 두 줄 — 손해와 기회.
    /// 【들어가서 알게 하지 않는다.】(문서 5절)
    /// </summary>
    public static string Describe(FacilityState state)
    {
        switch (state)
        {
            case FacilityState.Blackout:
                return "시야가 크게 줄어든다 · 순라귀가 멈춘다";

            case FacilityState.Flooded:
                return "느려진다 · 감전 피해 2배";

            case FacilityState.Decontamination:
                return "계속 화학 피해를 받는다 · 왕지네가 몰려 있다";

            default:
                return string.Empty;
        }
    }

    /// <summary>이 상태가 이 유형의 시야를 어떻게 바꾸는가.</summary>
    public static float VisionRange(FacilityState state, EnemyArchetype archetype)
    {
        float range = EnemyArchetypeTable.Of(archetype).visionRange;

        return range * Mathf.Max(0.05f, Of(state).visionScale);
    }

    /// <summary>
    /// 이 유형이 이 상태에서 움직이는가.
    ///
    /// 그믐에 순라귀가 멈추는 것은 【기회】다 — 위험을 하나 감수하고
    /// 다른 하나를 지운다. 이 한 줄이 「위험하지만 갈 만하다」를 만든다.
    /// </summary>
    public static bool IsActive(FacilityState state, EnemyArchetype archetype)
    {
        return !(Of(state).disablesSentry && archetype == EnemyArchetype.Sentry);
    }
}
