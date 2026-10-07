using UnityEngine;

/// <summary>
/// 시체 근접 감지 및 경험치 흡수 전담.
///
/// 이 컴포넌트가 붙은 오브젝트에는 흡수 범위를 나타내는 트리거 콜라이더가 필요하다.
/// </summary>
public class PlayerAbsorber : MonoBehaviour
{
    [Header("Absorb")]
    // 1이었을 때는 흡수 보너스 패시브(+10%·+20%)가 반올림에 먹혀 아무 일도 하지 않았다
    // (1 × 1.1 → 1). 10이면 11 · 12로 살아난다. (결정 2-34)
    // 필드 이름을 바꾼 이유: 씬에 저장된 옛 값 1이 새 기본값을 덮지 않게.
    [Tooltip("시체 1구 흡수 시 획득하는 기본 경험치")]
    [Min(1)]
    [SerializeField] private int experiencePerCorpse = 10;

    [Tooltip("시체 하나가 주는 기본 골드. 등급 배율이 여기에 곱해진다.")]
    [Min(0)]
    [UnityEngine.Serialization.FormerlySerializedAs("creditsPerCorpse")]
    [SerializeField] private int goldPerCorpse = 4;

    private PoolManager poolManager;

    /// <summary>현재 흡수 가능한 시체. 없으면 null. (흡수 프롬프트 UI가 참조한다)</summary>
    public CorpseController NearbyCorpse { get; private set; }

    /// <summary>지금 흡수할 수 있는지.</summary>
    public bool CanAbsorb => NearbyCorpse != null;

    private SphereCollider sense;
    private float baseSenseRadius;
    private float senseTimer;

    private void Start()
    {
        poolManager = PoolManager.EnsureInstance();

        // 패시브 「흡수 감지 범위 +n m」 (Audit A9) — 시체를 감지하는 트리거 구의 반경에 더한다.
        foreach (SphereCollider s in GetComponents<SphereCollider>())
        {
            if (!s.isTrigger)
                continue;

            sense = s;
            baseSenseRadius = s.radius;
            break;
        }
    }

    private void Update()
    {
        if (sense == null)
            return;

        senseTimer -= Time.deltaTime;
        if (senseTimer > 0f)
            return;
        senseTimer = 0.5f;

        float bonus = PassiveManager.HasInstance ? PassiveManager.Instance.Total(PassiveEffectType.AbsorbRange) : 0f;
        float radius = baseSenseRadius + Mathf.Max(0f, bonus);

        if (!Mathf.Approximately(sense.radius, radius))
            sense.radius = radius;
    }

    /// <summary>
    /// 파밍을 시도한다. 【경험치 흡수 + 전리품 창】이 한 동작이다.
    ///
    /// 경험치는 한 번만 들어온다. 전리품은 플레이어가 원하는 것만 집는다.
    /// 다 집지 않아도 시체는 남으므로 나중에 돌아와 마저 집을 수 있다.
    ///
    /// 【이전 동작】 흡수 즉시 시체가 사라지고 젬이 가방에 자동으로 들어갔다.
    /// 「무엇을 들고 갈지 고른다」는 철수 루팅의 핵심 결정이 빠져 있었다.
    /// </summary>
    public bool TryAbsorb()
    {
        CorpseController corpse = NearbyCorpse;

        if (corpse == null)
            return false;

        // 정기 · 엽전은 다가가는 순간 이미 들어왔다 (결정 2-77 · GrantRewards). 여기서는 전리품 창만 연다.
        GrantRewards(corpse);

        // 집을 것이 없으면 창을 열지 않고 시체를 정리한다.
        if (!corpse.HasLoot)
        {
            Despawn(corpse);
            return true;
        }

        ExchangeWindowUI.EnsureInstance().Open(corpse);

        return true;
    }

    /// <summary>
    /// 【정기(경험치) · 엽전은 자동으로 거둔다】 (결정 2-77) — 시체에 다가가 감지 범위에 들어오면 바로 들어온다.
    /// 파밍 버튼은 전리품 창을 열 때만 쓴다. 한 시체에서 한 번만(TryMarkAbsorbed).
    /// </summary>
    public void GrantRewards(CorpseController corpse)
    {
        if (corpse == null)
            return;

        if (corpse.TryMarkAbsorbed())
        {
            int baseAmount = Mathf.RoundToInt(experiencePerCorpse * corpse.ValueMultiplier);

            int gainedXP = EnemyRewardTable.Experience(
                baseAmount, corpse.Rarity, AbsorbMultiplier());

            if (PlayerStats.HasInstance)
                PlayerStats.Instance.AddXP(gainedXP);
            else
                GameLogger.Warning("[PlayerAbsorber] PlayerStats가 씬에 없어 경험치를 지급하지 못했습니다.");

            // ── 골드 (Hunting 6-1절) ────────────────────────────────
            // 【경험치와 따로 준다.】 문서는 「소켓을 열려면 아래층,
            // 벙커를 지으려면 위층」이라고 정했다. 두 보상이 한 덩어리로
            // 들어오면 그 선택 자체가 생기지 않는다.
            //
            // 흡수 보너스(패시브)는 경험치 축의 것이므로 골드에 곱하지 않는다.
            int gainedGold = EnemyRewardTable.Gold(
                Mathf.RoundToInt(goldPerCorpse * corpse.ValueMultiplier), corpse.Rarity);

            if (gainedGold > 0)
                PassiveManager.EnsureInstance().AddGold(gainedGold);

            GameLogger.Log(
                $"[PlayerAbsorber] {corpse.Rarity} 흡수 — 경험치 +{gainedXP} · 골드 +{gainedGold}"
                + (corpse.ConditionalDrops != ConditionalDrop.None
                    ? $" · 조건 {corpse.ConditionalDrops}"
                    : string.Empty));
        }

    }

    /// <summary>시체를 풀로 돌려보낸다. 전리품 창이 비었을 때도 호출된다.</summary>
    public void Despawn(CorpseController corpse)
    {
        if (corpse == null)
            return;

        if (NearbyCorpse == corpse)
            NearbyCorpse = null;

        GameObject corpseObject = corpse.gameObject;

        if (poolManager == null || !poolManager.Despawn(corpseObject))
            Destroy(corpseObject);
    }

    /// <summary>패시브 「경험치 획득 +n%」를 배율로 바꾼다.</summary>
    private static float AbsorbMultiplier()
    {
        if (!PassiveManager.HasInstance)
            return 1f;

        return 1f + PassiveManager.Instance.Total(PassiveEffectType.AbsorbAmount) * 0.01f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out CorpseController corpse))
            return;

        NearbyCorpse = corpse;

        // 다가가기만 해도 정기 · 엽전이 들어온다 (결정 2-77).
        GrantRewards(corpse);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out CorpseController corpse) && NearbyCorpse == corpse)
            NearbyCorpse = null;
    }
}
