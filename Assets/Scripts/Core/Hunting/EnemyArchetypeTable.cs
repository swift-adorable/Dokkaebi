using System;

/// <summary>
/// 이 유형이 플레이어에게 요구하는 답.
/// docs/Dokkaebi_Combat_Baseline.md 5절 표의 「요구하는 답」 열을 그대로 옮긴 것이다.
///
/// 【enum으로 둔 이유】
/// 「하나의 유형이 두 개의 답을 요구하게 만들지 않는다」는 설계 규칙을
/// 검사하려면 답이 데이터여야 한다. 주석에만 있으면 아무도 어기는 줄 모른다.
/// </summary>
public enum EnemyAnswer
{
    /// <summary>타이밍 — 예비동작을 보고 대시.</summary>
    Timing = 0,

    /// <summary>방어 관통 — 장비로 방어도를 뚫는다.</summary>
    Penetration = 1,

    /// <summary>각도 — 시야각 바깥으로 돈다.</summary>
    Angle = 2,

    /// <summary>선제 — 먼저 찾아야 한다.</summary>
    Initiative = 3,

    /// <summary>거리 — 장판 밖으로.</summary>
    Distance = 4,

    /// <summary>엄폐 — 사선을 끊는다.</summary>
    Cover = 5,

    /// <summary>속성 — 젬으로 속성을 바꾼다.</summary>
    Element = 6,

    /// <summary>회피 — 탄을 피한다.</summary>
    Dodge = 7
}

/// <summary>
/// 유형 한 종의 고정 수치.
/// 출처는 docs/Dokkaebi_Combat_Baseline.md 5절 표다. 여기서 값을 새로 만들지 않는다.
/// </summary>
[Serializable]
public struct EnemyArchetypeStats
{
    public int health;
    public int damage;
    public int armourPenetration;
    public float armour;

    /// <summary>소속. 진영 판정이 이 값을 본다.</summary>
    public Faction faction;

    /// <summary>이 유형이 요구하는 답. 유형당 하나다.</summary>
    public EnemyAnswer answer;

    /// <summary>
    /// 듣는 쪽의 청각 배율. 대상이 내는 소리 반경에 곱한다.
    /// 0.75(둔함) / 1.0(보통) / 2.0(밝음) 셋만 쓴다.
    /// [확인됨 — docs/research/duckov/05_적_AI_실측치.md 3절]
    /// </summary>
    public float hearingScale;

    /// <summary>
    /// 감지하지 못한 채 이만큼 흐르면 추적을 그만둔다(초).
    ///
    /// 【불리언 chasesForever를 대신한다.】
    /// 전에는 순라귀만 「끝까지 쫓는다」였고 나머지는 거리로 떨어졌다.
    /// 켜짐·꺼짐 두 단계뿐이라 중간이 없었다. 덕코프는 초 단위 숫자로
    /// 집요함을 표현한다 — 8×35 · 22×16 · 30×4.
    /// [확인됨 — docs/research/duckov/05_적_AI_실측치.md 2절]
    /// </summary>
    public float forgetTime;

    /// <summary>
    /// 이 거리(m) 안에서는 대상을 잊지 않는다. 0이면 없음.
    /// 덕코프 실측 0×46 · 15×6 · 40×3 · 60×4 중 0 / 15 / 40만 쓴다.
    /// 60은 구역 하나와 맞먹어, 플레이어에게 「구역을 뜬다」 외의 수가 없어진다.
    /// </summary>
    public float forcedChaseRange;

    /// <summary>
    /// 시야각 【전체】(도). 이 각도 밖에서 다가오면 보지 못한다.
    ///
    /// 【불확실 — 문서에 숫자가 없다.】
    /// 문서 8절은 「번개귀·침귀는 정면 넓고 측·후방 좁다」,
    /// 「잡귀는 시야가 좁다」처럼 말로만 적었다.
    /// 그 서술이 지키는 관계만 지키도록 값을 골랐고, 관계는 테스트가 강제한다.
    /// </summary>
    public float visionConeDegrees;

    /// <summary>시야 거리(m). 【불확실 — 문서에 숫자가 없다.】</summary>
    public float visionRange;

    // ── 행동 ──────────────────────────────────────────────────────────
    // 【전부 불확실 — 문서에 숫자가 없다.】
    // 기획서는 「느리고 단단」 「기습」 「연사」처럼 말로만 적었다.
    // 그 서술이 지키는 관계만 지키도록 값을 골랐고, 관계는 테스트가 강제한다.
    // 여기 두는 이유 — 프리팹에 손으로 적으면 표와 갈라진다.

    /// <summary>이동 속도(m/s).</summary>
    public float moveSpeed;

    /// <summary>근접인가 원거리인가.</summary>
    public bool ranged;

    /// <summary>공격이 거는 상태이상. 없으면 None.</summary>
    public StatusEffectType appliedStatus;

    /// <summary>원거리일 때 유지하려는 거리(m).</summary>
    public float preferredDistance;

    /// <summary>예비동작(초). 길수록 「보고 피할 수 있다」에 가깝다.</summary>
    public float windupDuration;

    /// <summary>공격 사이 간격(초).</summary>
    public float attackCooldown;

    /// <summary>유형 고유 저항. 등급·속성이 붙기 전의 값이다.</summary>
    public ElementalResistances resistances;

    /// <summary>
    /// 발소리를 내는가.
    ///
    /// false면 플레이어가 먼저 감지할 수 없다 — 수귀 · 무주귀.
    /// 「먼저 감지당하는가 / 먼저 감지하는가」가 이 게임의 핵심 긴장이다.
    /// (문서 8절. 실제 감지 로직은 7-D에서 붙인다)
    /// </summary>
    public bool makesFootsteps;

    /// <summary>
    /// 유형 고유 기믹. 표가 아니라 EnemyGimmickTable이 정한다 —
    /// 기믹 셋을 아홉 줄에 흩어 적으면 「누가 무슨 기믹인지」를 표에서 세어야 한다.
    /// </summary>
    public EnemyGimmick gimmick;
}

/// <summary>
/// 유형 9종의 고정 수치표. (docs/Dokkaebi_Combat_Baseline.md 5절 · Hunting 1·4·8절)
///
/// 【하나의 유형이 두 개의 답을 요구하게 만들지 않는다.】
/// "체력이 많고 빠르고 원거리에 방어도까지 높은" 적은 만들지 않는다.
/// 이 규칙은 EnemyArchetypeTests가 강제한다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class EnemyArchetypeTable
{
    public const int Count = 9;

    /// <summary>
    /// 시야 거리의 기본값(m).
    /// 덕코프 생물 59종의 중앙값이 17m다 (15×7 · 17×14 · 18×7 · 21×7).
    /// 기존 detectDistance 18m이 그 한복판이라 그대로 둔다.
    /// [확인됨 — docs/research/duckov/05_적_AI_실측치.md]
    /// </summary>
    public const float DefaultVisionRange = 18f;

    // ── 시야각 ────────────────────────────────────────────────────────
    // 【덕코프 실측 분포에 맞췄다. (2026-09-22)】
    // 처음에는 문서의 「누가 더 좁은가」만 보고 80/100/120/150을 골랐다.
    // 생물 59종을 실측해 보니 덕코프는 네 값으로 딱 갈린다 —
    //   100×14 · 120×5 · 140×27 · 150×2   (그 외 360은 전방위, 음수는 특수)
    // 80은 어디에도 없다. 문서가 요구하는 순서는 그대로 지키면서
    // 실제로 출시된 게임이 쓰는 값으로 옮긴다.
    // [확인됨 — docs/research/duckov/05_적_AI_실측치.md]

    /// <summary>「시야는 좁지만 우회 추적」 — 잡귀. 덕코프 최솟값.</summary>
    private const float NarrowCone = 100f;

    /// <summary>「정면 넓고 측·후방 좁다」 — 번개귀 · 침귀. 기본보다 좁다.</summary>
    private const float FrontalCone = 120f;

    /// <summary>기계 눈 — 절굿공이귀 · 순라귀. 좁을 이유가 없다.</summary>
    private const float MachineCone = 150f;

    // ── 청각 ──────────────────────────────────────────────────────────
    // 값은 Perception이 갖는다. 여기서는 어느 유형이 어느 귀를 쓰는지만 정한다.

    /// <summary>둔한 귀 — 기계형. 눈은 넓지만 귀로는 잘 못 찾는다.</summary>
    private const float DullEar = Perception.DullHearing;

    /// <summary>밝은 귀 — 수귀. 「먼저 찾는 쪽」이라는 정체성이 이 값이다.</summary>
    private const float KeenEar = Perception.KeenHearing;

    // ── 추적을 그만두는 시간 ──────────────────────────────────────────
    // 덕코프 실측 8×35 · 22×16 · 30×4 · (120·180은 보스급 소수라 쓰지 않는다)
    // [확인됨 — docs/research/duckov/05_적_AI_실측치.md 2절]

    /// <summary>보통 — 시야에서 사라지면 8초 뒤 포기한다.</summary>
    private const float ShortMemory = 8f;

    /// <summary>끈질김 — 번개귀 · 침귀.</summary>
    private const float LongMemory = 22f;

    /// <summary>집요함 — 순라귀. 옛 chasesForever의 자리다.</summary>
    private const float RelentlessMemory = 30f;

    // ── 강제 추적 거리 ────────────────────────────────────────────────

    /// <summary>한 번 물면 조금 끈질기다 — 번개귀 · 침귀.</summary>
    private const float NearForcedChase = 15f;

    /// <summary>이 거리 안에서는 사실상 떼어낼 수 없다 — 순라귀.</summary>
    private const float FarForcedChase = 40f;

    // ── 행동 기준값 ───────────────────────────────────────────────────
    // 잡귀가 기준이다. 나머지는 「잡귀보다 느리다/빠르다」로만 말한다.

    public const float BaseMoveSpeed = 2.5f;
    public const float BaseWindup = 0.35f;
    public const float BaseCooldown = 1.2f;

    /// <summary>원거리형이 유지하려는 거리(m). 「거리가 답」인 왕지네만 더 멀다.</summary>
    private const float RangedDistance = 7f;
    private const float LongDistance = 9f;

    /// <summary>전기 2배 · 카오스 면역. 기계형(절굿공이귀 · 순라귀)이 쓴다.</summary>
    private static ElementalResistances Mechanical => ElementalResistances.Mechanical;

    /// <summary>물리 0.66 · 화염 1.5. 허깨비 전용이다.</summary>
    private static ElementalResistances Settled => ElementalResistances.Settled;

    /// <summary>
    /// 물리 0.66만. 무주귀 전용.
    ///
    /// 허깨비와 나눠 둔 이유 — 문서 1절은 무주귀에 화염 1.5를 주지 않았다.
    /// 「정착 계열이니 같겠지」로 묶으면 원문에 없는 전제를 만드는 것이다.
    /// 결정 2-64로 문서도 이 값(화염 ×1.0)에 맞췄다 — 허깨비 = 「불」의 답,
    /// 무주귀 = 「물리 말고 아무거나」의 답 (Combat_Baseline 2-2).
    /// </summary>
    private static ElementalResistances Incorporeal
    {
        get
        {
            ElementalResistances r = ElementalResistances.Default;
            r.physical = 0.66f;
            return r;
        }
    }

    public static EnemyArchetypeStats Of(EnemyArchetype archetype)
    {
        EnemyArchetypeStats stats = Base(archetype);

        // 기믹은 EnemyGimmickTable이 정한다. 여기서 한 번만 얹는다 —
        // 아홉 줄에 흩어 적으면 「누가 무슨 기믹인지」를 표에서 세어야 한다.
        stats.gimmick = EnemyGimmickTable.Of(archetype);

        return stats;
    }

    private static EnemyArchetypeStats Base(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Scav:
                return Make(20, 8, 0, 0f, Faction.Wild, ElementalResistances.Default, true,
                            EnemyAnswer.Timing, cone: NarrowCone);   // 기준값 그대로

            // 기계 눈 — 넓게 보지만 귀는 둔하다.
            case EnemyArchetype.Crusher:
                return Make(90, 14, 1, 4f, Faction.Facility, Mechanical, true,
                            EnemyAnswer.Penetration, cone: MachineCone, hearing: DullEar,
                            // 「느리고 단단」 — 느린 만큼 예비동작도 길어 보고 피할 수 있다.
                            speed: 1.4f, windup: 0.60f, cooldown: 1.8f);

            // 한 번 물면 끈질기다. 각도로 따돌려도 금방 다시 붙는다.
            case EnemyArchetype.Dynamo:
                return Make(30, 10, 1, 0f, Faction.Subject, ElementalResistances.Default, true,
                            EnemyAnswer.Angle, cone: FrontalCone,
                            forget: LongMemory, forcedChase: NearForcedChase,
                            // 「기본 원거리 · 감전 부여」
                            speed: 2.6f, ranged: true, status: StatusEffectType.Shock,
                            distance: RangedDistance);

            // 발소리가 없고 귀가 밝다 — 매복형의 정체성이다.
            // 대신 놓치면 금방 잊고 다시 숨는다(기본 8초). 집요함까지 주면
            // 「선제」 하나였던 답이 둘이 된다.
            case EnemyArchetype.Lurker:
                return Make(25, 16, 2, 0f, Faction.Wild, ElementalResistances.Default, false,
                            EnemyAnswer.Initiative, hearing: KeenEar,
                            // 「기습」 — 빠르고 예비동작이 짧다. 먼저 찾지 못하면 맞고 시작한다.
                            speed: 3.0f, windup: 0.20f, cooldown: 1.4f);

            case EnemyArchetype.Chemic:
                // 「8방향 장판」 — 장판 자체는 아직 없다(7-F 잔여).
                // 지금은 「멀리 서서 느리게 쏜다」까지만 표현한다.
                return Make(45, 6, 0, 0f, Faction.Wild, ElementalResistances.Default, true,
                            EnemyAnswer.Distance,
                            speed: 1.9f, ranged: true, status: StatusEffectType.Corrode,
                            distance: LongDistance, windup: 0.50f, cooldown: 2.0f);

            // 번개귀와 같은 계열 — 끈질기다. 엄폐로 사선을 끊어도 계속 찾아온다.
            case EnemyArchetype.Specimen:
                return Make(60, 14, 3, 1f, Faction.Subject, ElementalResistances.Default, true,
                            EnemyAnswer.Cover, cone: FrontalCone,
                            forget: LongMemory, forcedChase: NearForcedChase,
                            // 「원거리 연사 · 중독 부여」 — 쿨다운이 가장 짧다.
                            // 연사가 곧 「엄폐로 사선을 끊어라」를 만든다.
                            speed: 2.2f, ranged: true, status: StatusEffectType.Poison,
                            distance: RangedDistance, windup: 0.30f, cooldown: 0.8f);

            case EnemyArchetype.Settled:
                // 「중력·은신」 — 둘 다 아직 없다(7-F 잔여). 지금은 느리고 무겁게만.
                return Make(120, 20, 0, 0f, Faction.Settled, Settled, true,
                            EnemyAnswer.Element,
                            speed: 1.8f, windup: 0.50f, cooldown: 1.5f);

            // 옛 chasesForever의 자리. 40m 안에서는 잊지 않고,
            // 벗어나도 30초를 더 찾아다닌다. 「끝까지」가 아니라 「멀리까지」다 —
            // 도망이라는 선택지를 없애지 않으려는 것이다.
            case EnemyArchetype.Sentry:
                return Make(70, 12, 2, 3f, Faction.Facility, Mechanical, true,
                            EnemyAnswer.Dodge, cone: MachineCone, hearing: DullEar,
                            forget: RelentlessMemory, forcedChase: FarForcedChase,
                            // 「3점사 × 2~4」 — 점사 자체는 아직 없다(7-F 잔여).
                            // 지금은 멀리서 또박또박 쏘는 것까지.
                            speed: 2.4f, ranged: true, distance: LongDistance,
                            windup: 0.40f, cooldown: 1.6f);

            // 비물질 — 발소리가 없다.
            case EnemyArchetype.Wraith:
                // 「비물질」 — 발소리가 없고 빠르다. 관통 5로 방어도를 그냥 뚫는다.
                return Make(80, 16, 5, 0f, Faction.Settled, Incorporeal, false,
                            EnemyAnswer.Element,
                            speed: 2.8f, windup: 0.30f, cooldown: 1.3f);

            default:
                return Make(20, 8, 0, 0f, Faction.Wild, ElementalResistances.Default, true,
                            EnemyAnswer.Timing);
        }
    }

    public static string Name(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Scav:     return "잡귀";
            case EnemyArchetype.Crusher:  return "절굿공이귀";
            case EnemyArchetype.Dynamo:   return "번개귀";
            case EnemyArchetype.Lurker:   return "수귀";
            case EnemyArchetype.Chemic:   return "왕지네";
            case EnemyArchetype.Specimen: return "침귀";
            case EnemyArchetype.Settled:  return "허깨비";
            case EnemyArchetype.Sentry:   return "순라귀";
            case EnemyArchetype.Wraith:   return "무주귀";
            default:                      return string.Empty;
        }
    }

    private static EnemyArchetypeStats Make(
        int health, int damage, int penetration, float armour,
        Faction faction, ElementalResistances resistances, bool footsteps,
        EnemyAnswer answer,
        float cone = Perception.DefaultConeDegrees, float sight = DefaultVisionRange,
        float hearing = Perception.NormalHearing,
        float forget = ShortMemory, float forcedChase = 0f,
        float speed = BaseMoveSpeed, bool ranged = false,
        StatusEffectType status = StatusEffectType.None,
        float distance = 0f, float windup = BaseWindup, float cooldown = BaseCooldown)
    {
        return new EnemyArchetypeStats
        {
            answer = answer,
            moveSpeed = speed,
            ranged = ranged,
            appliedStatus = status,
            preferredDistance = distance,
            windupDuration = windup,
            attackCooldown = cooldown,
            hearingScale = hearing,
            forgetTime = forget,
            forcedChaseRange = forcedChase,
            visionConeDegrees = cone,
            visionRange = sight,
            health = health,
            damage = damage,
            armourPenetration = penetration,
            armour = armour,
            faction = faction,
            resistances = resistances,
            makesFootsteps = footsteps
        };
    }
}
