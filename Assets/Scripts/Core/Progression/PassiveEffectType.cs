/// <summary>
/// 패시브가 줄 수 있는 것. (docs/Blob_Progression_System.md)
///
/// 【절대 규칙 — 패시브는 전투 수치를 주지 않는다.】
/// 방어도 · 방어 관통 · 피해 · 최대 체력 · 이동 속도 · 감지 거리 · 대시는
/// 전부 【장비】의 몫이다. 패시브가 그것을 주기 시작하면 장비를 고를 이유가 사라지고,
/// 성장 축 세 개(장비 / 인자 / 계정) 중 하나가 죽는다.
///
/// 덕코프의 「스킬 강화」에는 머리 방어력 같은 전투 항목이 있으나 그대로 따르지 않는다.
/// 덕코프는 장비 옵션이 우리보다 단순해 겹침이 문제가 되지 않는 구조다. [확인됨]
///
/// 패시브가 건드리는 것은 세 가지뿐이다 —
///   휴대 : 얼마나 들고 나갈 수 있는가
///   수집 : 얼마나 얻는가
///   벙커 : 무엇을 할 수 있게 되는가 (해금)
///
/// 휴대는 장비(가방)와 축이 겹치지만, 패시브 총합이 장비 최대치의 1/3을 넘지 않게 둔다.
/// 가방을 고르는 결정이 남아 있어야 한다.
/// </summary>
public enum PassiveEffectType
{
    None = 0,

    // ── 휴대 ──────────────────────────────────────────────────────────
    /// <summary>가방 적재 칸 +n.</summary>
    CarrySlots = 1,

    /// <summary>최대 소지 중량 +n kg.</summary>
    CarryWeight = 2,

    // ── 수집 ──────────────────────────────────────────────────────────
    /// <summary>경험치 획득량 +n%.</summary>
    AbsorbAmount = 10,

    /// <summary>시체 전리품 추첨 횟수 +n.</summary>
    LootRolls = 11,

    /// <summary>희귀 드랍 확률 +n%.</summary>
    RareDropRate = 12,

    // ── 벙커 (해금) ───────────────────────────────────────────────────
    /// <summary>창고 칸 +n.</summary>
    StashSlots = 20,

    /// <summary>판매가 +n%.</summary>
    SellPrice = 21,

    /// <summary>추출 실패 시 시체 회수 1회. 값이 아니라 해금이다.</summary>
    CorpseRecovery = 22,

    /// <summary>제작대 해금. 값이 아니라 해금이다.</summary>
    CraftBench = 23,

    /// <summary>지도에 전리품 위치 표시.</summary>
    MapLoot = 24
}

/// <summary>패시브 효과의 표시 이름과 성질.</summary>
public static class PassiveEffectInfo
{
    /// <summary>값이 아니라 켜고 끄는 해금인지.</summary>
    public static bool IsUnlockFlag(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.CorpseRecovery:
            case PassiveEffectType.CraftBench:
            case PassiveEffectType.MapLoot:
                return true;

            default:
                return false;
        }
    }

    /// <summary>백분율로 표시하는 효과인지.</summary>
    public static bool IsPercent(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.AbsorbAmount:
            case PassiveEffectType.RareDropRate:
            case PassiveEffectType.SellPrice:
                return true;

            default:
                return false;
        }
    }

    public static string Describe(PassiveEffectType type, float value)
    {
        if (IsUnlockFlag(type))
            return Name(type);

        string suffix = IsPercent(type) ? "%" : type == PassiveEffectType.CarryWeight ? " kg" : string.Empty;

        return $"{Name(type)} +{value:0.#}{suffix}";
    }

    public static string Name(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.CarrySlots:     return "가방 공간";
            case PassiveEffectType.CarryWeight:    return "소지 중량";
            case PassiveEffectType.AbsorbAmount:   return "경험치 획득";
            case PassiveEffectType.LootRolls:      return "전리품 추첨";
            case PassiveEffectType.RareDropRate:   return "희귀 드랍";
            case PassiveEffectType.StashSlots:     return "창고 칸";
            case PassiveEffectType.SellPrice:      return "판매가";
            case PassiveEffectType.CorpseRecovery: return "시체 회수";
            case PassiveEffectType.CraftBench:     return "제작대";
            case PassiveEffectType.MapLoot:        return "전리품 표시";
            default:                               return "없음";
        }
    }
}
