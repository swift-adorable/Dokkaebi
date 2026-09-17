/// <summary>
/// 과중량 단계. 무게 상한 대비 비율로 결정된다.
/// (docs/Blob_Equipment_System.md 5-1절)
/// </summary>
public enum EncumbranceLevel
{
    /// <summary>상한 이하. 페널티 없음.</summary>
    Normal = 0,

    /// <summary>상한 초과. 이동이 느려진다.</summary>
    Heavy = 1,

    /// <summary>상한의 1.25배 초과. 대시 거리도 줄어든다.</summary>
    Overloaded = 2,

    /// <summary>상한의 1.5배 초과. 달릴 수 없다.</summary>
    Immobile = 3
}

/// <summary>
/// 무게 판정. MonoBehaviour 의존이 없는 순수 클래스다.
///
/// 초반 병목은 적재 공간, 중반 이후 진짜 병목은 무게다. [확인됨 — 덕코프]
/// 그래서 두 축을 분리해 두고 각각 다른 시점에 압박이 오게 한다.
/// </summary>
public static class WeightCalculator
{
    /// <summary>각 단계가 시작되는 상한 대비 비율.</summary>
    public const float HeavyRatio = 1.0f;
    public const float OverloadedRatio = 1.25f;
    public const float ImmobileRatio = 1.5f;

    /// <summary>단계별 이동 능력 배율.</summary>
    public static float MoveMultiplier(EncumbranceLevel level)
    {
        switch (level)
        {
            case EncumbranceLevel.Heavy: return 0.8f;
            case EncumbranceLevel.Overloaded: return 0.6f;
            case EncumbranceLevel.Immobile: return 0.35f;
            default: return 1f;
        }
    }

    /// <summary>단계별 대시 거리 배율. Heavy까지는 대시가 온전하다.</summary>
    public static float DashMultiplier(EncumbranceLevel level)
    {
        switch (level)
        {
            case EncumbranceLevel.Overloaded: return 0.7f;
            case EncumbranceLevel.Immobile: return 0.4f;
            default: return 1f;
        }
    }

    /// <summary>현재 무게와 상한으로 단계를 판정한다.</summary>
    public static EncumbranceLevel Evaluate(float currentWeight, float limit)
    {
        // 상한이 0 이하면 판정할 수 없다. 페널티를 주지 않는다.
        if (limit <= 0f)
            return EncumbranceLevel.Normal;

        float ratio = currentWeight / limit;

        if (ratio > ImmobileRatio)
            return EncumbranceLevel.Immobile;

        if (ratio > OverloadedRatio)
            return EncumbranceLevel.Overloaded;

        if (ratio > HeavyRatio)
            return EncumbranceLevel.Heavy;

        return EncumbranceLevel.Normal;
    }
}
