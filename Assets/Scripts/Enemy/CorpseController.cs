using UnityEngine;

/// <summary>
/// 적 사망 후 남는 시체. 플레이어가 접근해 파밍한다.
///
/// 파밍 = 【경험치 흡수 + 전리품 창】 한 동작이다.
/// 경험치는 한 번만 들어오고, 전리품은 플레이어가 원하는 것만 집는다.
/// 다 집지 않아도 시체는 남으므로 나중에 돌아와 마저 집을 수 있다.
///
/// 근접 여부 판정은 PlayerAbsorber의 OnTriggerEnter/Exit가 단독으로 담당한다.
/// </summary>
public class CorpseController : MonoBehaviour, IPoolable
{
    [Header("Reward")]
    [Tooltip("적 등급 배율. 경험치와 전리품 추첨 횟수에 함께 곱한다.")]
    [SerializeField] private int valueMultiplier = 1;

    [Header("Loot")]
    [Tooltip("이 시체에서 나올 전리품 표. 비워 두면 젬 드롭만 굴린다.")]
    [SerializeField] private LootTable lootTable;

    [Tooltip("전리품 칸 수. 화면의 「전리품 (n/8)」의 8이다.")]
    [Min(1)]
    [SerializeField] private int lootSlots = LootContainer.DefaultCapacity;

    private LootContainer loot;
    private System.Random random;

    /// <summary>
    /// 죽은 적의 등급. 보상과 드롭 품질을 전부 이 값이 정한다.
    /// EnemyController가 시체를 만들면서 넣어 준다.
    /// </summary>
    public EnemyRarity Rarity { get; private set; } = EnemyRarity.Normal;

    /// <summary>이 개체가 만족시킨 조건부 드롭. (Hunting 6-3절)</summary>

    /// <summary>
    /// 경험치·추첨에 곱하는 배율.
    ///
    /// 인스펙터 값은 EnemyRarity를 넘겨받지 못한 시체(씬에 직접 놓인 것 등)의
    /// 기본값으로만 남는다. 등급을 받으면 표가 이긴다.
    /// </summary>
    public int ValueMultiplier => rewardSet ? EnemyRewardTable.LootRolls(Rarity) : Mathf.Max(1, valueMultiplier);

    /// <summary>죽은 적이 등급을 넘겨주었는가 — 씬에 직접 놓인 시체는 아니다.</summary>
    private bool rewardSet;

    /// <summary>
    /// 정기 · 엽전을 주는 시체인가 (결정 2-101) — 내가 쓰러뜨린 것만. 다른 적이 죽인 시체는 전리품만 남는다.
    /// 씬에 직접 놓인 시체(등급을 넘겨받지 않음)는 예전처럼 준다.
    /// </summary>
    public bool GivesEssence => !rewardSet || KilledByPlayer;

    /// <summary>등급이 정한 경험치 배율. 정수로 깎지 않는다.</summary>
    public float ExperienceMultiplier => EnemyRewardTable.ExperienceMultiplier(Rarity);

    /// <summary>등급이 정한 골드 배율.</summary>
    public float GoldMultiplier => EnemyRewardTable.GoldMultiplier(Rarity);

    /// <summary>
    /// 죽은 적의 정보를 받는다. EnemyController가 사망 직후 부른다.
    ///
    /// 【시체가 스스로 정하지 않는다.】 등급·속성·처치 조건은 전부
    /// 죽은 쪽이 아는 정보다. 시체가 짐작하면 반드시 어긋난다.
    /// </summary>
    public void SetReward(EnemyRarity rarity, in KillContext context)
    {
        Rarity = rarity;
        KilledByPlayer = context.killedByPlayer;
        rewardSet = true;

        // 【등급을 받은 뒤 다시 채운다】 (결정 2-99) — 풀에서 꺼낼 때(OnSpawned) 이미 일반 등급으로 채웠다.
        // 그대로 두면 고유 보스도 일반과 같은 추첨 횟수 · 같은 구슬 확률이었다 (Hunting 6절 「등급이 곧 드롭 품질」).
        Loot.Clear();
        FillLoot();
    }

    /// <summary>
    /// 반드시 넣을 것 (무기 도면 · 이야기 선물). 칸이 가득하면 가장 값싼 것을 빼고 넣는다 — 조용히 사라지지 않게 (결정 2-99).
    /// </summary>
    public bool PutGuaranteed(ItemDefinition item)
    {
        if (item == null)
            return false;

        if (Loot.TryPut(item))
            return true;

        int cheapest = -1;
        int value = int.MaxValue;

        for (int i = 0; i < Loot.Capacity; i++)
        {
            ItemStack s = Loot.Get(i);
            if (s?.Definition == null)
                continue;

            int v = s.Definition.BaseValue * s.Count;
            if (v < value)
            {
                value = v;
                cheapest = i;
            }
        }

        if (cheapest < 0)
            return false;

        Loot.Take(cheapest);
        return Loot.TryPut(item);
    }

    /// <summary>이 시체에 남은 전리품.</summary>
    public LootContainer Loot => loot ??= new LootContainer(lootSlots);

    /// <summary>경험치를 이미 흡수했는지. 두 번 들어오지 않게 막는다.</summary>
    /// <summary>플레이어가 쓰러뜨린 적의 시체인가 — 「전리품 표시」가 이것만 지도에 그린다 (결정 2-93).</summary>
    public bool KilledByPlayer { get; private set; }

    /// <summary>판 안의 시체 — 지도 표시용.</summary>
    public static readonly System.Collections.Generic.List<CorpseController> Active = new();

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    public bool IsAbsorbed { get; private set; }

    /// <summary>집을 것이 남았는지. UI가 프롬프트 문구를 고르는 데 쓴다.</summary>
    public bool HasLoot => !Loot.IsEmpty;

    private void Awake()
    {
        EnsurePassable();
    }

    public void OnSpawned()
    {
        // 풀에서 꺼낼 때마다 확인한다. 프리팹이 바뀌어도 규칙이 깨지지 않는다.
        EnsurePassable();

        IsAbsorbed = false;
        rewardSet = false;

        // 풀에서 재사용될 때 이전 개체의 등급·조건이 남으면
        // 일반 적에게서 고유 보상이 나온다.
        Rarity = EnemyRarity.Normal;
        KilledByPlayer = false;

        Loot.Clear();

        FillLoot();
    }

    public void OnDespawned()
    {
        // 남은 전리품은 여기서 사라진다. 시체가 풀로 돌아가는 것은
        // 플레이어가 전부 집었거나 멀어져 정리된 경우뿐이다.
        Loot.Clear();
    }

    /// <summary>경험치를 한 번만 지급하기 위한 마커. PlayerAbsorber가 호출한다.</summary>
    public bool TryMarkAbsorbed()
    {
        if (IsAbsorbed)
            return false;

        IsAbsorbed = true;

        return true;
    }

    /// <summary>
    /// 전리품을 채운다. 표에서 뽑은 것에 더해 젬 드롭을 굴린다.
    ///
    /// 젬을 가방에 바로 넣지 않고 여기 담는 이유 —
    /// 「무엇을 들고 갈지 고른다」가 철수 루팅의 결정이다.
    /// 자동으로 가방에 들어가면 그 결정이 사라진다.
    /// </summary>
    /// <summary>패시브 「희귀 드롭」의 배율. 젬 드롭(SkillManager)도 같은 값을 쓴다.</summary>
    public static float DropBonus => PassiveManager.HasInstance
        ? 1f + PassiveManager.Instance.Total(PassiveEffectType.RareDropRate) * 0.01f
        : 1f;

    private void FillLoot()
    {
        random ??= new System.Random(Random.Range(int.MinValue, int.MaxValue));

        // 요리 재료는 이번 구역이 속한 장의 것만 나온다 (결정 2-73). 공용 재료 · 다른 전리품은 그대로다.
        ChapterData chapter = ZoneDataTable.ChapterOfZone(StoryManager.TargetZone);
        int chapterNumber = chapter != null ? chapter.Chapter : 0;
        // 탄도 그 장에서 쓰는 무기의 것만 (결정 2-80).
        System.Func<ItemDefinition, bool> allow = item =>
            IngredientTable.DropsIn(item.Id, chapterNumber) && AmmoTable.DropsIn(item.Id, chapterNumber);

        // 패시브 「희귀 드롭 +n%」 — 무언가 나올 확률이 (1 + n%)배 (Audit A9 · 결정 2-74 「전반적인 드롭」).
        // 기우는 달(하현 · 그믐)이면 재료가 더 나온다 (결정 2-62).
        float findBonus = DropBonus * RaidManager.Current.LootScale;

        // 구슬을 먼저 — 등급이 높아 추첨이 많으면 칸이 차서 가장 귀한 것이 빠졌다 (결정 2-99).
        if (SkillManager.HasInstance)
        {
            ItemDefinition gem = SkillManager.Instance.RollGemDropItem(ValueMultiplier, chapterNumber);

            if (gem != null)
                Loot.TryPut(gem);
        }

        if (lootTable != null)
            lootTable.Fill(Loot, random, ValueMultiplier, allow, findBonus);

        // 패시브 「전리품 추첨 +n」. 표 자체는 그대로고 뽑는 횟수만 는다.
        int extraRolls = PassiveManager.HasInstance
            ? Mathf.RoundToInt(PassiveManager.Instance.Total(PassiveEffectType.LootRolls))
            : 0;

        if (lootTable != null && extraRolls > 0)
            LootRoller.Roll(lootTable.Entries, extraRolls, random, Loot, allow, findBonus);
    }

    /// <summary>
    /// 시체를 밟고 지나갈 수 있게 만든다.
    ///
    /// 시체는 파밍 대상이지 장애물이 아니다. 물리 충돌을 남겨 두면
    /// 적을 많이 잡을수록 바닥이 막혀 이동이 나빠진다.
    /// 흡수 감지는 PlayerAbsorber의 트리거가 담당하므로 트리거로 두면
    /// "지나갈 수 있으면서 감지도 되는" 두 요구가 동시에 충족된다.
    ///
    /// 프리팹 설정에 의존하지 않고 코드로 보장한다.
    /// (씬·프리팹 설정을 잊어 런타임에 어긋나는 실패 지점을 만들지 않는다)
    /// </summary>
    private void EnsurePassable()
    {
        var colliders = GetComponentsInChildren<Collider>(includeInactive: true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
                colliders[i].isTrigger = true;
        }

        // Rigidbody가 있으면 물리 밀림도 받지 않게 한다.
        if (TryGetComponent(out Rigidbody body))
        {
            body.isKinematic = true;
            body.detectCollisions = true;
        }
    }
}
