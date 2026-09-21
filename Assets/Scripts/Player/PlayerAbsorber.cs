using UnityEngine;

/// <summary>
/// 시체 근접 감지 및 경험치 흡수 전담.
///
/// 이 컴포넌트가 붙은 오브젝트에는 흡수 범위를 나타내는 트리거 콜라이더가 필요하다.
/// </summary>
public class PlayerAbsorber : MonoBehaviour
{
    [Header("Absorb")]
    [Tooltip("시체 1구 흡수 시 획득하는 기본 경험치")]
    [SerializeField] private int xpPerCorpse = 1;

    [Tooltip("시체 하나가 주는 기본 크레딧. 등급 배수가 여기에 곱해진다.")]
    [Min(0)]
    [SerializeField] private int creditsPerCorpse = 4;

    private PoolManager poolManager;

    /// <summary>현재 흡수 가능한 시체. 없으면 null. (흡수 프롬프트 UI가 참조한다)</summary>
    public CorpseController NearbyCorpse { get; private set; }

    /// <summary>지금 흡수할 수 있는지.</summary>
    public bool CanAbsorb => NearbyCorpse != null;

    private void Start()
    {
        poolManager = PoolManager.EnsureInstance();
    }

    /// <summary>
    /// 파밍을 시도한다. 【경험치 흡수 + 전리품 창】이 한 동작이다.
    ///
    /// 경험치는 한 번만 들어온다. 전리품은 유저가 원하는 것만 집는다.
    /// 다 집지 않아도 시체는 남으므로 나중에 돌아와 마저 집을 수 있다.
    ///
    /// 【이전 동작】 흡수 즉시 시체가 사라지고 젬이 가방에 자동으로 들어갔다.
    /// 「무엇을 들고 갈지 고른다」는 추출 루팅의 핵심 결정이 빠져 있었다.
    /// </summary>
    public bool TryAbsorb()
    {
        CorpseController corpse = NearbyCorpse;

        if (corpse == null)
            return false;

        // 경험치는 최초 1회만. 창을 여러 번 열어도 다시 들어오지 않는다.
        if (corpse.TryMarkAbsorbed())
        {
            int baseAmount = Mathf.RoundToInt(xpPerCorpse * corpse.ValueMultiplier);

            int gainedXP = EnemyRewardTable.Experience(
                baseAmount, corpse.Rarity, AbsorbMultiplier());

            if (PlayerStats.HasInstance)
                PlayerStats.Instance.AddXP(gainedXP);
            else
                GameLogger.Warning("[PlayerAbsorber] PlayerStats가 씬에 없어 경험치를 지급하지 못했습니다.");

            // ── 크레딧 (Hunting 6-1절) ────────────────────────────────
            // 【경험치와 따로 준다.】 문서는 「소켓을 열려면 아래층,
            // 벙커를 지으려면 위층」이라고 정했다. 두 보상이 한 덩어리로
            // 들어오면 그 선택 자체가 생기지 않는다.
            //
            // 흡수 보너스(패시브)는 경험치 축의 것이므로 크레딧에 곱하지 않는다.
            int gainedCredits = EnemyRewardTable.Credits(
                Mathf.RoundToInt(creditsPerCorpse * corpse.ValueMultiplier), corpse.Rarity);

            if (gainedCredits > 0)
                PassiveManager.EnsureInstance().AddCredits(gainedCredits);

            GameLogger.Log(
                $"[PlayerAbsorber] {corpse.Rarity} 흡수 — 경험치 +{gainedXP} · 크레딧 +{gainedCredits}"
                + (corpse.ConditionalDrops != ConditionalDrop.None
                    ? $" · 조건 {corpse.ConditionalDrops}"
                    : string.Empty));
        }

        // 집을 것이 없으면 창을 열지 않고 시체를 정리한다.
        if (!corpse.HasLoot)
        {
            Despawn(corpse);
            return true;
        }

        LootWindowUI.EnsureInstance().Open(corpse);

        return true;
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

    /// <summary>패시브 「경험치 획득 +n%」를 배수로 바꾼다.</summary>
    private static float AbsorbMultiplier()
    {
        if (!PassiveManager.HasInstance)
            return 1f;

        return 1f + PassiveManager.Instance.Total(PassiveEffectType.AbsorbAmount) * 0.01f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out CorpseController corpse))
            NearbyCorpse = corpse;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out CorpseController corpse) && NearbyCorpse == corpse)
            NearbyCorpse = null;
    }
}
