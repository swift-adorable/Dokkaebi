using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이 개체가 【무엇인가】를 정하고, 그 결과를 컴포넌트에 꽂는다.
///
/// 계산은 EnemyProfile(순수 구조체)이 하고, 여기서는 적용만 한다.
/// 나눠 두는 이유 — 계산이 MonoBehaviour 안에 있으면 EditMode에서 검증할 수 없다.
/// MCP로 PlayMode를 돌릴 수 없으므로 검증 가능한 쪽에 계산을 둔다.
///
/// 프리팹에는 유형만 지정한다. 등급과 속성은 스폰 때마다 다시 뽑는다 —
/// 「매 출격마다 무작위 재배치」가 문서 7절의 요구다.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyIdentity : MonoBehaviour, IPoolable
{
    [Header("Archetype")]
    [Tooltip("이 프리팹이 어떤 유형인가. 수치는 EnemyArchetypeTable이 갖는다.")]
    [SerializeField] private EnemyArchetype archetype = EnemyArchetype.Scav;

    [Header("Rarity")]
    [Tooltip("켜면 스폰마다 등급을 굴린다. 끄면 아래 고정 등급을 쓴다(보스 · 검증용).")]
    [SerializeField] private bool rollRarity = true;

    [SerializeField] private EnemyRarity fixedRarity = EnemyRarity.Normal;

    [Tooltip("고유 개체가 손으로 지정하는 속성. 등급이 Unique일 때만 쓴다.")]
    [SerializeField] private List<EnemyAffix> fixedAffixes = new();

    private Health health;
    private EnemyAttack attack;
    private EnemyMovement movement;

    private readonly List<EnemyAffix> rolled = new();

    private static readonly System.Random Rng = new System.Random();

    /// <summary>지금 이 개체의 최종 수치. 스폰 이후에만 유효하다.</summary>
    public EnemyProfile Profile { get; private set; }

    public EnemyArchetype Archetype => archetype;

    /// <summary>소속. 진영 AI(7-C)가 읽는다.</summary>
    public Faction Faction => Profile.faction;

    /// <summary>등급 · 속성이 바뀌었을 때. 표시(윤곽·이름)가 구독한다.</summary>
    public event Action<EnemyProfile> OnProfileChanged;

    private void Awake()
    {
        health = GetComponent<Health>();
        attack = GetComponent<EnemyAttack>();
        movement = GetComponent<EnemyMovement>();
    }

    public void OnSpawned()
    {
        EnemyRarity rarity = rollRarity
            ? EnemyRarityTable.Roll(Rng)
            : fixedRarity;

        // 고유는 속성을 굴리지 않는다 — 개체마다 고정이다. (문서 2절)
        if (rarity == EnemyRarity.Unique)
        {
            rolled.Clear();
            rolled.AddRange(fixedAffixes);
        }
        else
        {
            EnemyAffixRoller.Roll(rarity, Rng, rolled);
        }

        // 【이번 판의 조건을 마지막에 얹는다.】
        // 원형·등급·속성은 「이 개체가 무엇인가」이고, 조건은 「이번 판이 어떤
        // 판인가」다. 순서를 바꾸면 조건이 등급 배율에 다시 곱해져 두 번 먹는다.
        Apply(RaidManager.Current.Apply(
            EnemyProfile.Build(archetype, rarity, rolled)));
    }

    public void OnDespawned()
    {
    }

    /// <summary>
    /// 최종 수치를 컴포넌트에 꽂는다.
    ///
    /// 외부에서 부를 수 있게 열어 둔 이유 — 사전 배치(9단계)에서는
    /// 맵이 등급을 정해 내려 준다. 그때 스폰 시 굴리기를 끄고 이것만 부른다.
    /// </summary>
    public void Apply(EnemyProfile profile)
    {
        Profile = profile;

        if (health != null)
        {
            // 난이도 배율은 여기서 함께 건다. EnemyController는 EnemyIdentity가
            // 있으면 체력에 손대지 않는다 — 두 곳이 같은 값을 만지면
            // 컴포넌트 호출 순서에 따라 결과가 달라진다.
            int scaled = Mathf.Max(1,
                Mathf.RoundToInt(profile.health * GameManager.EnemyHealthMultiplier));

            health.SetMaxHealth(scaled, refill: true);

            health.SetDefence(profile.armour, profile.armour, profile.resistances);
        }

        if (attack != null)
            attack.SetOffence(profile.damage, profile.armourPenetration);

        if (movement != null)
            movement.BaseSpeedScale = profile.moveScale;

        OnProfileChanged?.Invoke(profile);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 【스폰마다 실제 결과를 남긴다.】
        // 프리팹에 적힌 기본값과 표에서 나온 값이 같으면, 계층을 들여다봐도
        // 「프로필이 적용됐다」와 「프로필이 무시됐다」를 구분할 수 없다.
        // 등급 배율이 붙은 개체가 나와야 구분되는데 그건 확률이라 기다릴 수 없다.
        // 출시 빌드에서는 통째로 빠진다. (DebugOnlyStripper와 같은 기준)
        GameLogger.Log(
            $"[EnemyIdentity] {profile.Describe()} · {profile.rarity} · "
            + $"체력 {profile.health} · 피해 {profile.damage} · 방어도 {profile.armour} · "
            + $"이동 ×{profile.moveScale:0.00} · {profile.faction} · "
            + $"물리 ×{profile.resistances.physical:0.00} 전기 ×{profile.resistances.lightning:0.00}");
#endif
    }
}
