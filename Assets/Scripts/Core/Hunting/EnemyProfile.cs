using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유형 + 등급 + 속성을 합쳐 낸 【최종 수치】.
///
/// 이 구조체가 7단계의 계산 중심이다. MonoBehaviour가 하나도 없으므로
/// EditMode에서 전부 검증된다 — MCP로 PlayMode를 돌릴 수 없는 제약 아래에서
/// 「했다」고 말할 수 있는 근거를 남기려면 계산이 여기 있어야 한다.
///
/// 적용(Health·이동에 꽂는 일)은 EnemyIdentity가 맡는다. 계산과 적용을 나눈다.
/// </summary>
public struct EnemyProfile
{
    public EnemyArchetype archetype;
    public EnemyRarity rarity;
    public Faction faction;

    public int health;
    public int damage;
    public int armourPenetration;

    /// <summary>유형 방어도 + 등급 보너스 + 갑각.</summary>
    public float armour;

    /// <summary>유형 저항과 속성 저항을 【가장 낮은 것 하나만】으로 합친 결과.</summary>
    public ElementalResistances resistances;

    /// <summary>이동 속도 배율. 신속 ×1.4 · 육중 ×0.8.</summary>
    public float moveScale;

    /// <summary>첫 피격을 한 번 무효로 만드는가. (껍질)</summary>
    public bool hasShell;

    /// <summary>밀어내기·기절에 면역인가. (육중)</summary>
    public bool immuneToControl;

    /// <summary>사망 시 2마리로 분열하는가. (분열성)</summary>
    public bool splitsOnDeath;

    /// <summary>
    /// 초당 회복하는 최대 체력 비율. 「재생」 속성과 「되살아나는 잡귀」 레이드 특성이 쓴다.
    /// 둘 다 걸려도 더하지 않는다 — 가장 큰 것 하나만 남는다.
    /// </summary>
    public float regenPerSecond;

    /// <summary>발소리를 내는가. 유형이 정한다.</summary>
    public bool makesFootsteps;

    /// <summary>듣는 쪽의 청각 배율. 유형이 정한다.</summary>
    public float hearingScale;

    /// <summary>감지하지 못한 채 이만큼 흐르면 추적을 그만둔다(초).</summary>
    public float forgetTime;

    /// <summary>이 거리(m) 안에서는 대상을 잊지 않는다. 0이면 없음.</summary>
    public float forcedChaseRange;

    /// <summary>붙은 속성. 표시와 잔향(사망 시 전달)이 읽는다.</summary>
    public EnemyAffix[] affixes;

    /// <summary>
    /// 유형 · 등급 · 속성으로 최종 수치를 만든다.
    ///
    /// 【저항을 곱하지 않는 이유】(문서 9절)
    /// 곱연산이면 원천 두 개만으로 「이 속성으로는 못 잡는다」가 된다.
    /// 상태이상 피해에도 상성이 곱해지므로 방어도 무시라는 우회로까지 같이 막힌다.
    /// 철수 루팅에서 그건 파밍을 버리라는 뜻이다.
    /// </summary>
    public static EnemyProfile Build(
        EnemyArchetype archetype, EnemyRarity rarity, IReadOnlyList<EnemyAffix> affixes)
    {
        EnemyArchetypeStats stats = EnemyArchetypeTable.Of(archetype);

        var profile = new EnemyProfile
        {
            archetype = archetype,
            rarity = rarity,
            faction = stats.faction,
            armourPenetration = stats.armourPenetration,
            makesFootsteps = stats.makesFootsteps,
            hearingScale = stats.hearingScale,
            forgetTime = stats.forgetTime,
            forcedChaseRange = stats.forcedChaseRange,
            moveScale = 1f,
            resistances = stats.resistances,
            affixes = System.Array.Empty<EnemyAffix>()
        };

        profile.health = Mathf.Max(1,
            Mathf.RoundToInt(stats.health * EnemyRarityTable.HealthMultiplier(rarity)));

        profile.damage = Mathf.Max(1,
            Mathf.RoundToInt(stats.damage * EnemyRarityTable.DamageMultiplier(rarity)));

        profile.armour = stats.armour + EnemyRarityTable.ArmourBonus(rarity);

        if (affixes == null || affixes.Count == 0)
            return profile;

        var kept = new List<EnemyAffix>(affixes.Count);

        for (int i = 0; i < affixes.Count; i++)
        {
            EnemyAffix affix = affixes[i];

            // 꺼져 있는 속성(과민)은 데이터로 들어와도 적용하지 않는다.
            // 뽑는 쪽을 믿지 않는다 — 고유 개체는 속성을 손으로 지정한다.
            if (!EnemyAffixTable.IsEnabled(affix))
                continue;

            kept.Add(affix);

            switch (affix)
            {
                case EnemyAffix.Carapace:
                    profile.armour += EnemyAffixTable.CarapaceArmour;
                    break;

                case EnemyAffix.Swift:
                    profile.moveScale *= EnemyAffixTable.SwiftMoveScale;
                    break;

                case EnemyAffix.Hulking:
                    profile.moveScale *= EnemyAffixTable.HulkingMoveScale;
                    profile.immuneToControl = true;
                    break;

                case EnemyAffix.Shell:
                    profile.hasShell = true;
                    break;

                case EnemyAffix.Splitting:
                    profile.splitsOnDeath = true;
                    break;

                case EnemyAffix.Regenerating:
                    profile.regenPerSecond = EnemyAffixTable.RegenPerSecond;
                    break;
            }

            // 【손댄 속성 하나만 내린다.】
            // 전부 1.0인 묶음을 만들어 TakeLowest에 넣으면, 절굿공이귀의 전기 2배와
            // 허깨비의 화염 1.5배가 속성 하나 붙었다는 이유로 1.0으로 눌린다.
            // 유형이 요구하던 답이 지워진다. (테스트가 이 실수를 잡아냈다)
            if (EnemyAffixTable.TryGetResistance(affix, out DamageElement element))
                profile.resistances.LowerTo(element, EnemyAffixTable.ResistanceMultiplier);
        }

        profile.affixes = kept.ToArray();

        return profile;
    }

    /// <summary>표시용 이름. 희귀 이상은 속성을 함께 적는다.</summary>
    public string Describe()
    {
        string name = EnemyArchetypeTable.Name(archetype);

        if (affixes == null || affixes.Length == 0)
            return name;

        var builder = new System.Text.StringBuilder(name);

        builder.Append(" (");

        for (int i = 0; i < affixes.Length; i++)
        {
            if (i > 0)
                builder.Append(" · ");

            builder.Append(EnemyAffixTable.Name(affixes[i]));
        }

        builder.Append(')');

        return builder.ToString();
    }
}
