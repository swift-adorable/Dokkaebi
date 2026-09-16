using UnityEngine;

/// <summary>
/// 시체(Core) 근접 감지 및 흡수 전담.
///
/// 이 컴포넌트가 붙은 오브젝트에는 흡수 범위를 나타내는 트리거 콜라이더가 필요하다.
/// </summary>
public class PlayerAbsorber : MonoBehaviour
{
    [Header("Absorb")]
    [Tooltip("Core 1개 흡수 시 획득하는 기본 경험치")]
    [SerializeField] private int xpPerCore = 1;

    private PoolManager poolManager;

    /// <summary>현재 흡수 가능한 시체. 없으면 null. (흡수 프롬프트 UI가 참조한다)</summary>
    public CorpseController NearbyCorpse { get; private set; }

    /// <summary>지금 흡수할 수 있는지.</summary>
    public bool CanAbsorb => NearbyCorpse != null;

    private void Start()
    {
        poolManager = PoolManager.EnsureInstance();
    }

    /// <summary>흡수를 시도한다. 대상이 없으면 false.</summary>
    public bool TryAbsorb()
    {
        if (NearbyCorpse == null)
            return false;

        int gainedXP = xpPerCore * NearbyCorpse.ValueMultiplier;

        GameObject corpseObject = NearbyCorpse.gameObject;
        NearbyCorpse = null;

        if (poolManager == null || !poolManager.Despawn(corpseObject))
            Destroy(corpseObject);

        if (PlayerStats.HasInstance)
            PlayerStats.Instance.AddXP(gainedXP);
        else
            GameLogger.Warning("[PlayerAbsorber] PlayerStats가 씬에 없어 경험치를 지급하지 못했습니다.");

        GameLogger.Log($"[PlayerAbsorber] Core Absorbed (+{gainedXP} XP)");

        return true;
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
