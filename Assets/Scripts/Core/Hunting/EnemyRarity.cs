using UnityEngine;

/// <summary>
/// 등급 4종 (PoE2 방식). (docs/Dokkaebi_Hunting_System.md 2절)
///
/// poe2db — "몬스터 속성 하나당 드롭 희귀도가 100% 이상 증가한다" [확인됨]
/// → 【등급이 곧 드롭 품질이다.】 별도 드롭 테이블을 두지 않는다.
/// </summary>
public enum EnemyRarity
{
    Normal = 0,
    Magic = 1,
    Rare = 2,

    /// <summary>고유(보스). 속성이 무작위가 아니라 개체마다 고정이다.</summary>
    Unique = 3
}

/// <summary>
/// 등급이 주는 배율과 속성 개수.
/// 수치 출처는 docs/Dokkaebi_Combat_Baseline.md 5절 「등급 배율」이다.
///
/// 【체력은 ×15까지 오르는데 피해는 ×1.8까지만 오른다. 의도된 비대칭이다.】
/// 체력 배율은 파밍 안에서 젬으로 대응하고,
/// 피해 배율은 파밍 사이에 장비로 대응한다.
/// 보스는 아프게가 아니라 단단하게 위협한다.
/// </summary>
public static class EnemyRarityTable
{
    public const int Count = 4;

    /// <summary>등급이 붙일 속성 개수의 범위. 최댓값을 포함한다.</summary>
    public static void AffixRange(EnemyRarity rarity, out int min, out int max)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  min = 1; max = 2; break;
            case EnemyRarity.Rare:   min = 3; max = 4; break;

            // 고유는 개체마다 고정 속성을 갖는다. 무작위로 굴리지 않는다.
            // 그래서 여기서 0을 주고, 실제 속성은 개체 정의가 지정한다.
            case EnemyRarity.Unique: min = 0; max = 0; break;

            default:                 min = 0; max = 0; break;
        }
    }

    public static float HealthMultiplier(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 2f;
            case EnemyRarity.Rare:   return 4.5f;
            case EnemyRarity.Unique: return 15f;
            default:                 return 1f;
        }
    }

    public static float DamageMultiplier(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 1.2f;
            case EnemyRarity.Rare:   return 1.4f;
            case EnemyRarity.Unique: return 1.8f;
            default:                 return 1f;
        }
    }

    /// <summary>등급이 더해 주는 방어도. 곱이 아니라 덧셈이다.</summary>
    public static float ArmourBonus(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 1f;
            case EnemyRarity.Rare:   return 2f;
            case EnemyRarity.Unique: return 3f;
            default:                 return 0f;
        }
    }

    /// <summary>
    /// 등급 분포 (맵 1회분). 문서 7절 — 일반 75 / 마법 20 / 희귀 4~5%.
    /// 고유는 확률이 아니라 지정 구역에 1~2체 고정이므로 여기에 없다.
    /// </summary>
    public static EnemyRarity Roll(System.Random random)
    {
        if (random == null)
            return EnemyRarity.Normal;

        int value = random.Next(0, 100);

        if (value < 75) return EnemyRarity.Normal;
        if (value < 95) return EnemyRarity.Magic;

        return EnemyRarity.Rare;
    }

    /// <summary>
    /// 표시 색. 【색만으로 구분하지 않는다】 — 윤곽 두께를 함께 쓴다.
    /// (문서 2절, 색각 이상 대응)
    /// </summary>
    public static Color OutlineColour(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return new Color(0.45f, 0.62f, 1f);
            case EnemyRarity.Rare:   return new Color(1f, 0.84f, 0.32f);
            case EnemyRarity.Unique: return new Color(1f, 0.48f, 0.18f);
            default:                 return new Color(1f, 1f, 1f, 0f);
        }
    }

    /// <summary>윤곽 두께(픽셀). 색을 못 보는 사람도 등급을 읽을 수 있어야 한다.</summary>
    public static int OutlineWidth(EnemyRarity rarity)
    {
        switch (rarity)
        {
            case EnemyRarity.Magic:  return 2;
            case EnemyRarity.Rare:   return 4;
            case EnemyRarity.Unique: return 6;
            default:                 return 0;
        }
    }
}
