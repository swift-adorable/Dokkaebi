using UnityEngine;

/// <summary>
/// 유형 고유의 기믹. (docs/Blob_Hunting_System.md 1절)
///
/// 【수치만으로는 유형이 세 개도 안 나온다.】
/// 체력·피해·속도만 다른 적 아홉은 「체력이 다른 같은 적 아홉」이다.
/// 기믹은 「어떻게 싸우는가」를 가른다 — 대응이 달라져야 유형이다.
///
/// 셋만 둔다. 유형마다 기믹을 주면 아홉 가지를 외워야 하고,
/// 그러면 「이 적은 무슨 적이더라」가 매 조우마다 생긴다.
/// </summary>
public enum EnemyGimmick
{
    None = 0,

    /// <summary>화공체 — 8방향으로 흩뿌린다. 【피할 곳을 줄이는 방식】.</summary>
    RadialSpray = 1,

    /// <summary>보안기 — 3점사. 【한 번 걸리면 세 방 맞는다】.</summary>
    BurstFire = 2,

    /// <summary>정착체 — 끌어당기고 숨는다. 【거리를 못 벌린다】.</summary>
    GravityStealth = 3
}

/// <summary>기믹 하나의 수치.</summary>
public readonly struct GimmickSpec
{
    /// <summary>한 번 쏠 때 나가는 탄 수. 1이면 평범한 한 발이다.</summary>
    public readonly int ProjectilesPerShot;

    /// <summary>연달아 쏘는 횟수. 1이면 점사가 아니다.</summary>
    public readonly int BurstCount;

    /// <summary>점사 사이 간격(초).</summary>
    public readonly float BurstInterval;

    /// <summary>초당 끌어당기는 세기(m/s). 0이면 끌지 않는다.</summary>
    public readonly float PullStrength;

    /// <summary>이 거리(m) 안에 들어오면 모습이 드러난다. 0이면 늘 보인다.</summary>
    public readonly float RevealRange;

    public GimmickSpec(int projectiles, int burst, float interval,
                       float pull, float reveal)
    {
        ProjectilesPerShot = Mathf.Max(1, projectiles);
        BurstCount = Mathf.Max(1, burst);
        BurstInterval = Mathf.Max(0f, interval);
        PullStrength = Mathf.Max(0f, pull);
        RevealRange = Mathf.Max(0f, reveal);
    }
}

/// <summary>
/// 기믹 수치표. (docs/Blob_Hunting_System.md 1절)
///
/// 【전부 불확실 — 문서에 숫자가 없다.】
/// 기획서는 「8방향 장판」 「3점사 × 2~4」 「중력·은신」처럼 말로만 적었다.
/// 8과 3은 문서에 있는 숫자라 그대로 쓰고, 나머지는 그 서술이 지키는
/// 관계만 지키도록 골랐다. 관계는 테스트가 강제한다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class EnemyGimmickTable
{
    /// <summary>[확인됨 — 문서 1절] 화공체의 8방향.</summary>
    public const int SprayDirections = 8;

    /// <summary>[확인됨 — 문서 1절] 보안기의 3점사.</summary>
    public const int BurstShots = 3;

    /// <summary>
    /// [불확실] 점사 사이 간격.
    /// 세 발이 한 덩어리로 읽히되 대시로 두 번째부터는 뺄 수 있는 길이다.
    /// 대시 무적이 이보다 짧으면 점사가 회피 불가가 된다.
    /// </summary>
    public const float BurstInterval = 0.14f;

    /// <summary>
    /// [불확실] 정착체가 끌어당기는 세기(m/s).
    /// 기본 이동 속도(2.5)의 3할 남짓 — 「뒷걸음질이 느려진다」 정도다.
    /// 이보다 세면 도망 자체가 불가능해져 답이 하나로 줄어든다.
    /// </summary>
    public const float PullStrength = 0.8f;

    /// <summary>
    /// [불확실] 정착체가 드러나는 거리(m).
    /// 시야 거리(18)의 절반보다 짧아야 「눈앞에서 나타난다」가 된다.
    /// </summary>
    public const float RevealRange = 7f;

    public static GimmickSpec Get(EnemyGimmick gimmick)
    {
        switch (gimmick)
        {
            case EnemyGimmick.RadialSpray:
                return new GimmickSpec(SprayDirections, 1, 0f, 0f, 0f);

            case EnemyGimmick.BurstFire:
                return new GimmickSpec(1, BurstShots, BurstInterval, 0f, 0f);

            case EnemyGimmick.GravityStealth:
                return new GimmickSpec(1, 1, 0f, PullStrength, RevealRange);

            default:
                return new GimmickSpec(1, 1, 0f, 0f, 0f);
        }
    }

    /// <summary>유형이 가진 기믹. 셋만 가진다.</summary>
    public static EnemyGimmick Of(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Chemic:  return EnemyGimmick.RadialSpray;
            case EnemyArchetype.Sentry:  return EnemyGimmick.BurstFire;
            case EnemyArchetype.Settled: return EnemyGimmick.GravityStealth;
            default:                     return EnemyGimmick.None;
        }
    }

    /// <summary>
    /// 흩뿌리는 방향 하나. index가 0이면 정면, 나머지는 고르게 돈다.
    ///
    /// 【정면부터 시작한다.】 조준한 곳에 한 발도 안 가면
    /// 「나를 노린 공격」으로 읽히지 않고 그냥 환경 피해가 된다.
    /// </summary>
    public static Vector3 SprayDirection(Vector3 forward, int index, int count)
    {
        count = Mathf.Max(1, count);

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        float step = 360f / count;

        return Quaternion.Euler(0f, step * index, 0f) * forward.normalized;
    }
}
