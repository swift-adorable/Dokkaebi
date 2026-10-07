/// <summary>
/// 난이도 — 새로 시작할 때 프롤로그(인트로) 뒤에 한 번 고르고, **바꿀 수 없다** (결정 2-69).
///
/// 【값을 바꾸지 않는다】 — 세이브와 씬에 숫자로 저장된다. 덕코프식 6단계(채집 · 낮음 · 균형 ·
/// 서바이벌 · 극한 · 폭주)를 셋으로 줄이면서 옛 서바이벌 · 극한 · 폭주의 숫자를 그대로 물려받았다.
/// 0 · 1 · 2(옛 채집 · 낮음 · 균형)는 다시 쓰지 않는다 — 읽히면 Normal로 본다(DifficultyTable.Normalize).
/// </summary>
public enum DifficultyLevel
{
    /// <summary>Normal(보통) — 모든 기획 수치의 기준값(100%). 보통 플레이어가 「약간 어렵지만 할만하다」.</summary>
    Normal = 3,

    /// <summary>Nightmare(악몽) — 적 피해 ×1.5 (덕코프 「극한」).</summary>
    Nightmare = 4,

    /// <summary>Hell(지옥) — 적 피해 ×2 · 적 체력 ×1.25 · 골절 · 중상 상태이상 추가 [미구현].</summary>
    Hell = 5
}

/// <summary>난이도별 배율 표. 순수 클래스. (docs/Dokkaebi_Combat_Baseline.md 6절)</summary>
public static class DifficultyTable
{
    public static readonly DifficultyLevel[] All = { DifficultyLevel.Normal, DifficultyLevel.Nightmare, DifficultyLevel.Hell };

    /// <summary>옛 값(0~2)이나 모르는 값은 Normal로.</summary>
    public static DifficultyLevel Normalize(int value)
        => value == (int)DifficultyLevel.Nightmare ? DifficultyLevel.Nightmare
         : value == (int)DifficultyLevel.Hell ? DifficultyLevel.Hell
         : DifficultyLevel.Normal;

    /// <summary>적이 주는 피해의 배율.</summary>
    public static float EnemyDamage(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Nightmare: return 1.5f;
            case DifficultyLevel.Hell:      return 2f;
            default:                        return 1f;
        }
    }

    /// <summary>적 최대 체력의 배율.</summary>
    public static float EnemyHealth(DifficultyLevel level)
        => level == DifficultyLevel.Hell ? 1.25f : 1f;

    /// <summary>
    /// 골절 · 중상 같은 추가 상태이상이 켜지는지 — 지옥만. 상태 자체는 [미구현].
    /// 난이도가 배율만이 아니라 【상태이상 종류 자체를 추가】한다는 것이 덕코프에서 가져온 핵심이다.
    /// </summary>
    public static bool HasExtendedAilments(DifficultyLevel level) => level == DifficultyLevel.Hell;

    /// <summary>화면 이름.</summary>
    public static string NameOf(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Nightmare: return "악몽 (Nightmare)";
            case DifficultyLevel.Hell:      return "지옥 (Hell)";
            default:                        return "보통 (Normal)";
        }
    }

    /// <summary>고르는 화면의 설명 한 줄.</summary>
    public static string DescriptionOf(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Nightmare: return "적이 1.5배 아프게 때린다. 피하는 실력이 필요하다.";
            case DifficultyLevel.Hell:      return "적이 2배 아프게 때리고 더 단단하다.";
            default:                        return "약간 어렵지만 할 만하다. 처음이라면 이것.";
        }
    }
}
