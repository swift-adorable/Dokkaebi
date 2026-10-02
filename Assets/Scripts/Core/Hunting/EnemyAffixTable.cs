using System.Collections.Generic;

/// <summary>
/// 속성의 분류 · 효과 · 조합 금지 규칙. (docs/Dokkaebi_Hunting_System.md 3절)
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class EnemyAffixTable
{
    /// <summary>
    /// None을 제외한 실제 속성 수.
    /// 문서 3절은 13종이지만 냉기 저항(내한성)을 더해 14종이다.
    /// 이유는 EnemyAffix.Cryostable 주석에 있다.
    /// </summary>
    public const int Count = 14;

    /// <summary>「갑각」이 더하는 방어도. (문서 3절)</summary>
    public const float CarapaceArmour = 3f;

    /// <summary>저항 계열 속성이 거는 배율. 곱하지 않고 가장 낮은 것 하나만 남는다.</summary>
    public const float ResistanceMultiplier = 0.5f;

    /// <summary>
    /// 「재생」의 초당 회복량. 최대 체력에 대한 비율이다.
    ///
    /// 문서 3절은 「초당 회복」이라고만 적고 수치를 비워 두었다.
    /// PoE2의 Regenerates Life가 초당 최대 생명의 2%다.
    /// [확인됨 — docs/research/poe2/01_몬스터_속성.md]
    /// 같은 수치를 쓰되, 출처가 PoE2이고 Dokkaebi에서 검증되지 않았음을 밝힌다.
    /// </summary>
    public const float RegenPerSecond = 0.02f;

    /// <summary>「신속」의 이동 배율.</summary>
    public const float SwiftMoveScale = 1.4f;

    /// <summary>「육중」의 이동 배율.</summary>
    public const float HulkingMoveScale = 0.8f;

    /// <summary>한 개체에 붙을 수 있는 방어형 속성의 최대 개수.</summary>
    /// <remarks>
    /// 문서는 「방어형 3개 이상 동시 부여 금지」와 「희귀는 방어형 최대 2개」를
    /// 따로 적었지만 두 문장은 같은 상한을 말한다. 등급별로 다른 값을 만들지 않는다.
    /// </remarks>
    public const int MaxDefenceAffixes = 2;

    private static readonly EnemyAffix[] All =
    {
        EnemyAffix.Carapace, EnemyAffix.Hardened, EnemyAffix.FireProof,
        EnemyAffix.Insulated, EnemyAffix.Antibody, EnemyAffix.Cryostable,
        EnemyAffix.Regenerating, EnemyAffix.Shell, EnemyAffix.Splitting,
        EnemyAffix.Swift, EnemyAffix.Hair, EnemyAffix.Hulking,
        EnemyAffix.Broadcast, EnemyAffix.Echo
    };

    /// <summary>13종 전부. 순서가 고정이라 테스트가 기댈 수 있다.</summary>
    public static IReadOnlyList<EnemyAffix> Every => All;

    public static EnemyAffixGroup GroupOf(EnemyAffix affix)
    {
        switch (affix)
        {
            case EnemyAffix.Carapace:
            case EnemyAffix.Hardened:
            case EnemyAffix.FireProof:
            case EnemyAffix.Insulated:
            case EnemyAffix.Antibody:
            case EnemyAffix.Cryostable:
                return EnemyAffixGroup.Defence;

            case EnemyAffix.Regenerating:
            case EnemyAffix.Shell:
            case EnemyAffix.Splitting:
                return EnemyAffixGroup.Survival;

            case EnemyAffix.Swift:
            case EnemyAffix.Hair:
            case EnemyAffix.Hulking:
                return EnemyAffixGroup.Offence;

            default:
                return EnemyAffixGroup.Support;
        }
    }

    /// <summary>
    /// 지금 붙여도 되는 속성인가.
    ///
    /// 「과민」만 false다 — 예비동작 단축을 알리는 시각 신호가 아직 없다.
    /// 신호가 생기면 이 한 줄만 지운다. 롤 코드를 고치지 않아도 되도록 여기 둔다.
    /// </summary>
    public static bool IsEnabled(EnemyAffix affix)
    {
        return affix != EnemyAffix.Hair;
    }

    /// <summary>이 속성이 저항을 깎는가. 깎는다면 어느 속성을.</summary>
    public static bool TryGetResistance(EnemyAffix affix, out DamageElement element)
    {
        switch (affix)
        {
            case EnemyAffix.Hardened:  element = DamageElement.Physical;  return true;
            case EnemyAffix.FireProof: element = DamageElement.Fire;      return true;
            case EnemyAffix.Insulated: element = DamageElement.Lightning; return true;
            case EnemyAffix.Antibody:  element = DamageElement.Chaos;     return true;
            case EnemyAffix.Cryostable: element = DamageElement.Cold;     return true;

            default: element = DamageElement.Physical; return false;
        }
    }

    /// <summary>
    /// 이미 고른 것들 위에 이 속성을 더 붙여도 되는가.
    ///
    /// 조합 금지 (문서 3절)
    ///   · 방어형 3개 이상 동시 부여 금지 → 최대 2개
    ///   · 갑각과 경화를 동시에 붙이지 않는다 — 두 답(방어 관통 / 속성 전환)을
    ///     모두 막아 버리기 때문이다
    /// </summary>
    public static bool CanAdd(IReadOnlyList<EnemyAffix> chosen, EnemyAffix candidate)
    {
        if (candidate == EnemyAffix.None || !IsEnabled(candidate))
            return false;

        if (chosen == null)
            return true;

        int defence = 0;

        for (int i = 0; i < chosen.Count; i++)
        {
            EnemyAffix existing = chosen[i];

            if (existing == candidate)
                return false;

            if (GroupOf(existing) == EnemyAffixGroup.Defence)
                defence++;

            bool clash =
                (existing == EnemyAffix.Carapace && candidate == EnemyAffix.Hardened) ||
                (existing == EnemyAffix.Hardened && candidate == EnemyAffix.Carapace);

            if (clash)
                return false;
        }

        if (GroupOf(candidate) == EnemyAffixGroup.Defence && defence >= MaxDefenceAffixes)
            return false;

        return true;
    }

    public static string Name(EnemyAffix affix)
    {
        switch (affix)
        {
            case EnemyAffix.Carapace:     return "갑각";
            case EnemyAffix.Hardened:     return "경화";
            case EnemyAffix.FireProof:    return "내화성";
            case EnemyAffix.Insulated:    return "절연성";
            case EnemyAffix.Antibody:     return "독 견딤";
            case EnemyAffix.Cryostable:   return "내한성";
            case EnemyAffix.Regenerating: return "재생";
            case EnemyAffix.Shell:        return "껍질";
            case EnemyAffix.Splitting:    return "분열성";
            case EnemyAffix.Swift:        return "신속";
            case EnemyAffix.Hair:         return "과민";
            case EnemyAffix.Hulking:      return "육중";
            case EnemyAffix.Broadcast:    return "전파";
            case EnemyAffix.Echo:         return "잔향";
            default:                      return string.Empty;
        }
    }
}
