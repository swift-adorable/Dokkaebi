using UnityEngine;

/// <summary>
/// 적 사망 후 남는 시체. 플레이어가 접근해 파밍한다.
///
/// 파밍 = 【경험치 흡수 + 전리품 창】 한 동작이다.
/// 경험치는 한 번만 들어오고, 전리품은 유저가 원하는 것만 집는다.
/// 다 집지 않아도 시체는 남으므로 나중에 돌아와 마저 집을 수 있다.
///
/// 근접 여부 판정은 PlayerAbsorber의 OnTriggerEnter/Exit가 단독으로 담당한다.
/// </summary>
public class CorpseController : MonoBehaviour, IPoolable
{
    [Header("Reward")]
    [Tooltip("적 등급 배수. 경험치와 전리품 추첨 횟수에 함께 곱한다.")]
    [SerializeField] private int valueMultiplier = 1;

    [Header("Loot")]
    [Tooltip("이 시체에서 나올 전리품 표. 비워 두면 인자 드랍만 굴린다.")]
    [SerializeField] private LootTable lootTable;

    [Tooltip("전리품 칸 수. 화면의 「전리품 (n/8)」의 8이다.")]
    [Min(1)]
    [SerializeField] private int lootSlots = LootContainer.DefaultCapacity;

    private LootContainer loot;
    private System.Random random;

    public int ValueMultiplier => Mathf.Max(1, valueMultiplier);

    /// <summary>이 시체에 남은 전리품.</summary>
    public LootContainer Loot => loot ??= new LootContainer(lootSlots);

    /// <summary>경험치를 이미 흡수했는지. 두 번 들어오지 않게 막는다.</summary>
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

        Loot.Clear();

        FillLoot();
    }

    public void OnDespawned()
    {
        // 남은 전리품은 여기서 사라진다. 시체가 풀로 돌아가는 것은
        // 유저가 전부 집었거나 멀어져 정리된 경우뿐이다.
        Loot.Clear();
    }

    /// <summary>경험치를 한 번만 지급하기 위한 표식. PlayerAbsorber가 호출한다.</summary>
    public bool TryMarkAbsorbed()
    {
        if (IsAbsorbed)
            return false;

        IsAbsorbed = true;

        return true;
    }

    /// <summary>
    /// 전리품을 채운다. 표에서 뽑은 것에 더해 인자 드랍을 굴린다.
    ///
    /// 인자를 가방에 바로 넣지 않고 여기 담는 이유 —
    /// 「무엇을 들고 갈지 고른다」가 추출 루팅의 결정이다.
    /// 자동으로 가방에 들어가면 그 결정이 사라진다.
    /// </summary>
    private void FillLoot()
    {
        random ??= new System.Random(Random.Range(int.MinValue, int.MaxValue));

        if (lootTable != null)
            lootTable.Fill(Loot, random, ValueMultiplier);

        // 패시브 「전리품 추첨 +n」. 표 자체는 그대로고 뽑는 횟수만 는다.
        int extraRolls = PassiveManager.HasInstance
            ? Mathf.RoundToInt(PassiveManager.Instance.Total(PassiveEffectType.LootRolls))
            : 0;

        if (lootTable != null && extraRolls > 0)
            LootRoller.Roll(lootTable.Entries, extraRolls, random, Loot);

        if (!SkillManager.HasInstance)
            return;

        ItemDefinition gem = SkillManager.Instance.RollGemDropItem(ValueMultiplier);

        if (gem != null)
            Loot.TryPut(gem);
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
