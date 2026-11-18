using UnityEngine;

/// <summary>
/// 패시브 계열. 【단일 트리를 쓰지 않는다.】
/// (docs/Dokkaebi_Passive_System.md 1절 / 덕코프 조사 — 스킬은 독립 5계열 [확인됨])
///
/// 단일 트리는 「위로 한 줄」뿐이라 플레이어가 고를 것이 순서밖에 없다.
/// 계열이 갈리면 파밍 성향이 갈린다.
/// </summary>
public enum PassiveBranch
{
    /// <summary>적응 — 얼마나 들고 나가는가. (덕코프 「스킬 강화」)</summary>
    Adapt = 0,

    /// <summary>대사 — 얼마나 얻는가. (덕코프 「생존 스킬」)</summary>
    Metabolism = 1,

    /// <summary>회수 — 죽어도 무엇이 남는가. (Dokkaebi 고유)</summary>
    Recovery = 2,

    /// <summary>중개 — 벙커에서 무엇을 하는가. (덕코프 「블랙마켓 업그레이드」)</summary>
    Brokerage = 3,

    /// <summary>역행 — 조우해야 보인다. (덕코프 「이상한 개조」)</summary>
    Regression = 4,

    /// <summary>단련 — 몸을 단련한다: 체력 · 이동 · 대시 · 방어. (덕코프 「피지컬 강화」 · 결정 2-141)</summary>
    Training = 5,

    /// <summary>무예 — 무기를 다룬다: 피해 · 재장전. (덕코프 「스킬 강화」 · 결정 2-141)</summary>
    Martial = 6
}

/// <summary>
/// 계열이 열리는 방식. 셋뿐이다.
/// </summary>
public enum PassiveUnlockKind
{
    /// <summary>레벨 + 골드 + 필요 재료.</summary>
    Level = 0,

    /// <summary>레벨을 보지 않는다. 돈만 있으면 연다. (중개)</summary>
    GoldOnly = 1,

    /// <summary>조우 전까지 계열 자체가 보이지 않는다. (역행)</summary>
    Discovery = 2
}

public static class PassiveBranchInfo
{
    /// <summary>계열의 수 — 5 → 7 (결정 2-141, 전투 수치를 넣으며 단련 · 무예를 더했다). 화면 한 장에 탭 7개까지.</summary>
    public const int Count = 7;

    public static string Name(PassiveBranch branch)
    {
        switch (branch)
        {
            case PassiveBranch.Adapt:      return "적응";
            case PassiveBranch.Metabolism: return "대사";
            case PassiveBranch.Recovery:   return "회수";
            case PassiveBranch.Brokerage:  return "중개";
            case PassiveBranch.Regression: return "역행";
            case PassiveBranch.Training:   return "단련";
            case PassiveBranch.Martial:    return "무예";
            default:                       return "?";
        }
    }

    public static string Subtitle(PassiveBranch branch)
    {
        switch (branch)
        {
            case PassiveBranch.Adapt:      return "얼마나 들고 나가는가";
            case PassiveBranch.Metabolism: return "얼마나 얻는가";
            case PassiveBranch.Recovery:   return "죽어도 무엇이 남는가";
            case PassiveBranch.Brokerage:  return "소굴에서 무엇을 하는가";
            case PassiveBranch.Regression: return "기록되지 않은 것";
            case PassiveBranch.Training:   return "얼마나 버티고 달리는가";
            case PassiveBranch.Martial:    return "얼마나 잘 쏘는가";
            default:                       return string.Empty;
        }
    }

    /// <summary>계열이 열리는 방식.</summary>
    public static PassiveUnlockKind UnlockKind(PassiveBranch branch)
    {
        switch (branch)
        {
            case PassiveBranch.Brokerage:  return PassiveUnlockKind.GoldOnly;
            case PassiveBranch.Regression: return PassiveUnlockKind.Discovery;
            default:                       return PassiveUnlockKind.Level;
        }
    }

    public static Color Color(PassiveBranch branch)
    {
        switch (branch)
        {
            case PassiveBranch.Adapt:      return new Color(0.28f, 0.44f, 0.62f, 0.95f);
            case PassiveBranch.Metabolism: return new Color(0.30f, 0.52f, 0.36f, 0.95f);
            case PassiveBranch.Recovery:   return new Color(0.62f, 0.42f, 0.24f, 0.95f);
            case PassiveBranch.Brokerage:  return new Color(0.56f, 0.46f, 0.20f, 0.95f);
            case PassiveBranch.Training:   return new Color(0.62f, 0.28f, 0.26f, 0.95f);
            case PassiveBranch.Martial:    return new Color(0.26f, 0.50f, 0.56f, 0.95f);
            default:                       return new Color(0.48f, 0.30f, 0.56f, 0.95f);
        }
    }
}
