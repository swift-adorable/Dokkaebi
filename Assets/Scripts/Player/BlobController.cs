using UnityEngine;

/// <summary>
/// 플레이어 오케스트레이터.
///
/// 직접 게임 로직을 수행하지 않는다. 입력을 읽어 각 전담 컴포넌트에 전달하는 역할만 한다.
/// 이전에는 이동/회전/사격/대시/흡수를 모두 이 클래스가 들고 있었으나(215줄),
/// 역할별로 분리하고 이 클래스는 연결만 담당하도록 축소했다.
///
/// 클래스명을 유지한 이유: 씬에 직렬화된 컴포넌트 참조를 깨뜨리지 않기 위함이다.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAiming))]
[RequireComponent(typeof(PlayerDash))]
[RequireComponent(typeof(PlayerWeapon))]
[RequireComponent(typeof(PlayerAbsorber))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(Health))]
public class BlobController : MonoBehaviour
{
    private PlayerInputHandler input;
    private PlayerMovement movement;
    private PlayerAiming aiming;
    private PlayerDash dash;
    private PlayerWeapon weapon;
    private PlayerAbsorber absorber;
    private PlayerLoadout loadout;
    private Health health;

    private GameManager gameManager;

    /// <summary>현재 흡수 가능한 시체. (기존 호출부 호환용 위임 프로퍼티)</summary>
    public CorpseController NearbyCorpse => absorber != null ? absorber.NearbyCorpse : null;

    /// <summary>지금 내는 소리. 적의 감지가 읽는다. (감사 A11)</summary>
    public PlayerNoise Noise => noise;

    private PlayerNoise noise;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        movement = GetComponent<PlayerMovement>();
        aiming = GetComponent<PlayerAiming>();
        dash = GetComponent<PlayerDash>();
        weapon = GetComponent<PlayerWeapon>();
        absorber = GetComponent<PlayerAbsorber>();
        health = GetComponent<Health>();

        // 씬 배치 의존을 만들지 않는다. (Master_Prompt 「씬 배치 의존 최소화」)
        // Player는 프리팹이 아니라 씬에 직접 놓여 있어 RequireComponent가
        // 기존 오브젝트에 소급 적용되지 않는다. 없으면 여기서 만든다.
        loadout = GetComponent<PlayerLoadout>();

        if (loadout == null)
            loadout = gameObject.AddComponent<PlayerLoadout>();

        // 소리도 같은 이유로 여기서 만든다. 없으면 적이 플레이어를
        // 【영원히 듣지 못한다】 — 뒤에서 아무리 뛰어도 조용한 셈이 된다.
        noise = GetComponent<PlayerNoise>();

        if (noise == null)
            noise = gameObject.AddComponent<PlayerNoise>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    /// <summary>
    /// 추출 실패(사망) 처리.
    ///
    /// 【죽으면 들고 있던 것 전부.】 각인만 남는다.
    /// 규칙 자체는 PlayerInventory·EquipmentLoadout에 있고 여기서는 부르기만 한다.
    /// (docs/Blob_Progression_System.md 6절)
    ///
    /// 이 호출이 없던 동안에는 죽어도 아무것도 잃지 않았다 —
    /// 추출 루팅 게임의 뼈대가 실행 경로에서 빠져 있었다. (docs/Blob_Audit.md A2)
    /// </summary>
    private void HandleDied()
    {
        GameLogger.Log("[BlobController] 플레이어 사망");

        movement.SetInput(Vector2.zero);

        int lost = PlayerInventory.EnsureInstance().DropOnDeath();

        // 각성 레벨과 소켓은 출격 안의 것이다. 남은 젬은 이미 위에서 사라졌다.
        if (SkillManager.HasInstance)
            SkillManager.Instance.ResetRun();

        // 장비를 잃었으므로 사격 성능·방어도·이동 배율을 즉시 다시 계산한다.
        if (loadout != null)
            loadout.Refresh();

        GameLogger.Log($"[BlobController] 소지품 {lost}점을 잃었습니다. 각인은 남습니다.");

        if (gameManager != null)
            gameManager.GameOver();
    }

    private void Start()
    {
        // 모든 Awake 이후에 실행되므로 싱글턴 초기화 순서가 보장된다.
        gameManager = GameManager.Instance;
    }

    private void Update()
    {
        if (gameManager == null || !gameManager.IsPlaying)
        {
            movement.SetInput(Vector2.zero);
            return;
        }

        movement.SetInput(input.MoveInput);
        aiming.SetAim(input.AimInput);

        if (input.ShootHeld)
            weapon.TryFire();

        if (input.DashPressed)
            dash.TryDash(movement.MoveDirection);

        if (input.AbsorbPressed)
            absorber.TryAbsorb();
    }
}
