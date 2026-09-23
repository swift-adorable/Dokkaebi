using UnityEngine;

/// <summary>
/// 등급이 정하는 보상. (docs/Blob_Hunting_System.md 6절)
///
/// 【등급이 곧 드롭 품질이다.】
/// poe2db — "몬스터 속성 하나당 드롭 희귀도가 100% 이상 증가한다" [확인됨]
/// 그래서 등급과 별개인 드롭 표를 따로 두지 않는다.
/// 속성을 많이 단 적이 곧 좋은 것을 떨어뜨린다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class EnemyRewardTable
{
    /// <summary>경험치 배율. 문서 6절 표.</summary>
    public static float ExperienceMultiplier(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 1.5f;
            case EnemyRarity.Rare:   return 3f;
            case EnemyRarity.Unique: return 6f;
            default:                 return 1f;
        }
    }

    /// <summary>
    /// 크레딧 배율.
    ///
    /// 【경험치와 같은 값을 쓰지 않는다 — 두 축을 나누는 것이 6-1절의 요지다.】
    /// 문서는 「경험치는 저층이, 크레딧은 고층이 효율이 좋다」고 정했다.
    /// 장(Stage) 축은 9단계에 오므로 지금은 구역 차이를 낼 수 없다.
    /// 대신 등급에 따른 기울기를 경험치보다 완만하게 두어,
    /// 「강한 적을 잡는 것」이 크레딧보다 경험치에 더 크게 답하도록 한다.
    /// 구역 차이가 생기면 여기에 곱해진다.
    ///
    /// 【불확실】 문서에 크레딧 배율 표가 없다.
    /// </summary>
    public static float CreditMultiplier(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 1.3f;
            case EnemyRarity.Rare:   return 2f;
            case EnemyRarity.Unique: return 3.5f;
            default:                 return 1f;
        }
    }

    /// <summary>전리품 추첨 횟수. 등급이 높을수록 많이 굴린다.</summary>
    public static int LootRolls(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 2;
            case EnemyRarity.Rare:   return 4;
            case EnemyRarity.Unique: return 8;
            default:                 return 1;
        }
    }

    /// <summary>장비 티어 범위. 문서 6절 표를 그대로 옮긴 것이다.</summary>
    public static void EquipmentTierRange(EnemyRarity rarity, out int min, out int max)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  min = 2; max = 3; break;
            case EnemyRarity.Rare:   min = 3; max = 5; break;
            case EnemyRarity.Unique: min = 4; max = 6; break;
            default:                 min = 1; max = 2; break;
        }
    }

    /// <summary>
    /// 젬이 나올 확률(0~1). 고유는 확정 1개다.
    /// 【불확실】 문서는 「낮음 / 보통 / 높음 / 확정」이라고만 적었다.
    /// </summary>
    public static float GemChance(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 0.12f;
            case EnemyRarity.Rare:   return 0.35f;
            case EnemyRarity.Unique: return 1f;
            default:                 return 0.03f;
        }
    }

    /// <summary>
    /// 변이 샘플이 나올 확률(0~1). 희귀 이상만이다. (문서 6-4절)
    /// 【불확실】 문서는 희귀를 「확률」이라고만 적었다.
    /// </summary>
    public static float SampleChance(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Rare:   return 0.25f;
            case EnemyRarity.Unique: return 1f;
            default:                 return 0f;
        }
    }

    /// <summary>
    /// 고유가 떨어뜨린 장비의 내구도 비율. 문서 6절이 「내구도 손상」이라고 적었다.
    ///
    /// 티어 4~6짜리를 온전하게 주면 수리 경제가 시작도 전에 무너진다.
    /// 좋은 것을 주되 손을 봐야 쓰게 만든다.
    /// 【불확실】 문서에 수치가 없다.
    /// </summary>
    public const float UniqueDurabilityRatio = 0.45f;

    /// <summary>최종 경험치. 반올림은 한 곳에서만 한다.</summary>
    public static int Experience(int baseAmount, EnemyRarity rarity, float bonusMultiplier = 1f)
    {
        return Mathf.Max(0, Mathf.RoundToInt(
            baseAmount * ExperienceMultiplier(rarity) * Mathf.Max(0f, bonusMultiplier)));
    }

    /// <summary>최종 크레딧.</summary>
    public static int Credits(int baseAmount, EnemyRarity rarity, float bonusMultiplier = 1f)
    {
        return Mathf.Max(0, Mathf.RoundToInt(
            baseAmount * CreditMultiplier(rarity) * Mathf.Max(0f, bonusMultiplier)));
    }
}
