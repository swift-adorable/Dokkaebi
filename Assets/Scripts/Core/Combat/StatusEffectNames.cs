/// <summary>
/// 상태이상의 한국어 이름과 색. 【화면에 띄우려면 이름이 필요하다.】
///
/// enum 이름(Ignite·Chill)은 코드의 것이고, 유저가 보는 것은 「점화 · 냉각」이다.
/// (용어 기준표 — 코드 이름은 영어, 사람이 읽는 글은 한국어)
///
/// 색은 속성을 따른다 — 불은 주황, 냉기는 하늘, 번개는 노랑, 독은 초록.
/// 【임계 상태 셋(동결·마비·부식)은 붉은 계열로 묶는다.】
/// 「지금 움직일 수 없다」와 「조금 아프다」는 한눈에 구분되어야 한다.
/// </summary>
public static class StatusEffectNames
{
    public static string Of(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Ignite:   return "점화";
            case StatusEffectType.Poison:   return "중독";
            case StatusEffectType.Freeze:   return "동결";
            case StatusEffectType.Shock:    return "감전";
            case StatusEffectType.Bleed:    return "출혈";
            case StatusEffectType.Congeal:  return "응집";
            case StatusEffectType.Chill:    return "냉각";
            case StatusEffectType.Paralyze: return "마비";
            case StatusEffectType.Corrode:  return "부식";
            default:                        return "—";
        }
    }

    /// <summary>【임계 상태인가.】 동결·마비는 행동 불능, 부식은 방어·회복 절반.</summary>
    public static bool IsCritical(StatusEffectType type)
    {
        return type == StatusEffectType.Freeze
               || type == StatusEffectType.Paralyze
               || type == StatusEffectType.Corrode;
    }
}
