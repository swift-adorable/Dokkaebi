using System;

/// <summary>
/// 이 원형이 플레이어에게 요구하는 답.
/// docs/Blob_Combat_Baseline.md 5절 표의 「요구하는 답」 열을 그대로 옮긴 것이다.
///
/// 【enum으로 둔 이유】
/// 「하나의 원형이 두 개의 답을 요구하게 만들지 않는다」는 설계 규칙을
/// 검사하려면 답이 데이터여야 한다. 주석에만 있으면 아무도 어기는 줄 모른다.
/// </summary>
public enum EnemyAnswer
{
    /// <summary>타이밍 — 예비동작을 보고 대시.</summary>
    Timing = 0,

    /// <summary>방어 관통 — 장비로 방어도를 뚫는다.</summary>
    Penetration = 1,

    /// <summary>각도 — 시야 콘 바깥으로 돈다.</summary>
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
/// 원형 한 종의 고정 수치.
/// 출처는 docs/Blob_Combat_Baseline.md 5절 표다. 여기서 값을 새로 만들지 않는다.
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

    /// <summary>이 원형이 요구하는 답. 원형당 하나다.</summary>
    public EnemyAnswer answer;

    /// <summary>
    /// 한 번 물면 거리와 무관하게 놓지 않는가.
    ///
    /// 보안기만 true다 — "한 번 추적하면 끝까지 쫓는다." (문서 8절)
    /// 전부 true로 두면 도망이라는 선택지가 사라지고,
    /// 전부 false면 보안기의 정체성이 없어진다.
    /// </summary>
    public bool chasesForever;

    /// <summary>원형 고유 내성. 등급·속성이 붙기 전의 값이다.</summary>
    public ElementalResistances resistances;

    /// <summary>
    /// 발소리를 내는가.
    ///
    /// false면 플레이어가 먼저 감지할 수 없다 — 잠복체 · 데이터체.
    /// 「먼저 감지당하는가 / 먼저 감지하는가」가 이 게임의 핵심 긴장이다.
    /// (문서 8절. 실제 감지 로직은 7-D에서 붙인다)
    /// </summary>
    public bool makesFootsteps;
}

/// <summary>
/// 원형 9종의 고정 수치표. (docs/Blob_Combat_Baseline.md 5절 · Hunting 1·4·8절)
///
/// 【하나의 원형이 두 개의 답을 요구하게 만들지 않는다.】
/// "체력이 많고 빠르고 원거리에 방어도까지 높은" 적은 만들지 않는다.
/// 이 규칙은 EnemyArchetypeTests가 강제한다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class EnemyArchetypeTable
{
    public const int Count = 9;

    /// <summary>전기 2배 · 카오스 면역. 기계형(압착기 · 보안기)이 쓴다.</summary>
    private static ElementalResistances Mechanical => ElementalResistances.Mechanical;

    /// <summary>물리 0.66 · 화염 1.5. 정착체 전용이다.</summary>
    private static ElementalResistances Settled => ElementalResistances.Settled;

    /// <summary>
    /// 물리 0.66만. 데이터체 전용.
    ///
    /// 정착체와 나눠 둔 이유 — 문서 1절은 데이터체에 화염 1.5를 주지 않았다.
    /// 「정착 계열이니 같겠지」로 묶으면 원문에 없는 전제를 만드는 것이다.
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
        switch (archetype)
        {
            case EnemyArchetype.Scav:
                return Make(20, 8, 0, 0f, Faction.Wild, ElementalResistances.Default, true,
                            EnemyAnswer.Timing);

            case EnemyArchetype.Crusher:
                return Make(90, 14, 1, 4f, Faction.Facility, Mechanical, true,
                            EnemyAnswer.Penetration);

            case EnemyArchetype.Dynamo:
                return Make(30, 10, 1, 0f, Faction.Subject, ElementalResistances.Default, true,
                            EnemyAnswer.Angle);

            // 발소리가 없다 — 매복형의 정체성이다.
            case EnemyArchetype.Lurker:
                return Make(25, 16, 2, 0f, Faction.Wild, ElementalResistances.Default, false,
                            EnemyAnswer.Initiative);

            case EnemyArchetype.Chemic:
                return Make(45, 6, 0, 0f, Faction.Wild, ElementalResistances.Default, true,
                            EnemyAnswer.Distance);

            case EnemyArchetype.Specimen:
                return Make(60, 14, 3, 1f, Faction.Subject, ElementalResistances.Default, true,
                            EnemyAnswer.Cover);

            case EnemyArchetype.Settled:
                return Make(120, 20, 0, 0f, Faction.Settled, Settled, true,
                            EnemyAnswer.Element);

            case EnemyArchetype.Sentry:
                return Make(70, 12, 2, 3f, Faction.Facility, Mechanical, true,
                            EnemyAnswer.Dodge, chasesForever: true);

            // 비물질 — 발소리가 없다.
            case EnemyArchetype.Wraith:
                return Make(80, 16, 5, 0f, Faction.Settled, Incorporeal, false,
                            EnemyAnswer.Element);

            default:
                return Make(20, 8, 0, 0f, Faction.Wild, ElementalResistances.Default, true,
                            EnemyAnswer.Timing);
        }
    }

    public static string Name(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Scav:     return "스캐브";
            case EnemyArchetype.Crusher:  return "압착기";
            case EnemyArchetype.Dynamo:   return "자전체";
            case EnemyArchetype.Lurker:   return "잠복체";
            case EnemyArchetype.Chemic:   return "화공체";
            case EnemyArchetype.Specimen: return "검체";
            case EnemyArchetype.Settled:  return "정착체";
            case EnemyArchetype.Sentry:   return "보안기";
            case EnemyArchetype.Wraith:   return "데이터체";
            default:                      return string.Empty;
        }
    }

    private static EnemyArchetypeStats Make(
        int health, int damage, int penetration, float armour,
        Faction faction, ElementalResistances resistances, bool footsteps,
        EnemyAnswer answer, bool chasesForever = false)
    {
        return new EnemyArchetypeStats
        {
            answer = answer,
            chasesForever = chasesForever,
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
