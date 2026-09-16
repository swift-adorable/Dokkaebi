using UnityEngine;

/// <summary>
/// 플레이어 오케스트레이터.
///
/// 직접 게임 로직을 수행하지 않는다. 입력을 읽어 각 전담 컴포넌트에 전달하는 역할만 한다.
/// 이전에는 이동/회전/사격/대시/흡수를 모두 이 클래스가 들고 있었으나(215줄),
/// 역할별로 분리하고 이 클래스는 배선만 담당하도록 축소했다.
///
/// 클래스명을 유지한 이유: 씬에 직렬화된 컴포넌트 참조를 깨뜨리지 않기 위함이다.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAiming))]
[RequireComponent(typeof(PlayerDash))]
[RequireComponent(typeof(PlayerWeapon))]
[RequireComponent(typeof(PlayerAbsorber))]
public class BlobController : MonoBehaviour
{
    private PlayerInputHandler input;
    private PlayerMovement movement;
    private PlayerAiming aiming;
    private PlayerDash dash;
    private PlayerWeapon weapon;
    private PlayerAbsorber absorber;

    private GameManager gameManager;

    /// <summary>현재 흡수 가능한 시체. (기존 호출부 호환용 위임 프로퍼티)</summary>
    public CorpseController NearbyCorpse => absorber != null ? absorber.NearbyCorpse : null;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        movement = GetComponent<PlayerMovement>();
        aiming = GetComponent<PlayerAiming>();
        dash = GetComponent<PlayerDash>();
        weapon = GetComponent<PlayerWeapon>();
        absorber = GetComponent<PlayerAbsorber>();
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
