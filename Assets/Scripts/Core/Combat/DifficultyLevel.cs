/// <summary>
/// 전역 난이도. 유저가 고르며, 장(Stage)은 "무엇을 만나는가"만 정한다.
///
/// 덕코프의 분리를 그대로 도용했다 — 구역이 바뀐다고 적 체력에 배율을 곱하지 않는다.
/// (docs/Blob_Combat_Baseline.md 5절)
/// </summary>
public enum DifficultyLevel
{
    /// <summary>채집 — 전투 최소화.</summary>
    Gathering = 0,
    Low = 1,
    /// <summary>균형 — 모바일 기본값.</summary>
    Balanced = 2,
    /// <summary>서바이벌 — 모든 기획 수치의 기준값.</summary>
    Survival = 3,
    /// <summary>극한 — 추출 실패 시 회수 불가.</summary>
    Extreme = 4,
    /// <summary>폭주 — 변경 불가. 골절·중상 상태이상이 추가된다.</summary>
    Frenzy = 5
}

/// <summary>난이도별 배율 표. 순수 클래스.</summary>
public static class DifficultyTable
{
    /// <summary>적이 주는 피해의 배율.</summary>
    public static float EnemyDamage(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Gathering: return 0.4f;
            case DifficultyLevel.Low: return 0.6f;
            case DifficultyLevel.Balanced: return 0.8f;
            case DifficultyLevel.Extreme: return 1.5f;
            case DifficultyLevel.Frenzy: return 1.6f;
            default: return 1f;
        }
    }

    /// <summary>적 최대 체력의 배율.</summary>
    public static float EnemyHealth(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Gathering: return 0.4f;
            case DifficultyLevel.Low: return 0.6f;
            case DifficultyLevel.Balanced: return 0.8f;
            // 폭주는 피해가 가장 높은 대신 체력이 가장 낮다.
            case DifficultyLevel.Frenzy: return 0.4f;
            default: return 1f;
        }
    }

    /// <summary>
    /// 골절·중상 같은 추가 상태이상이 활성화되는지.
    ///
    /// 난이도 상승이 배율만이 아니라 【상태이상 종류 자체를 추가】한다는 것이
    /// 덕코프에서 가장 중요한 도용 포인트다.
    /// </summary>
    public static bool HasExtendedAilments(DifficultyLevel level)
    {
        return level == DifficultyLevel.Frenzy;
    }
}
