using UnityEngine;

/// <summary>사격 버튼을 눌렀을 때 조준 방향을 결정하는 방식.</summary>
public enum FireButtonAimMode
{
    /// <summary>
    /// 현재 캐릭터가 바라보는 방향으로 사격한다. (권장)
    /// 우측 스틱으로 방향을 잡고 손을 떼도 그 방향이 유지되므로,
    /// 후퇴하며 사격하는 카이팅이 가능하다.
    /// </summary>
    FacingDirection,

    /// <summary>
    /// 현재 이동 중인 방향으로 사격한다.
    /// 조준과 이동이 묶이므로 카이팅이 불가능하고, 정지 중에는 사격되지 않는다.
    /// </summary>
    MoveDirection
}

/// <summary>
/// 실기기(Android/iOS)용 입력 소스. 듀얼 가상 조이스틱 + 액션 버튼.
///
/// 이 컴포넌트는 모바일 입력 Canvas 루트에 붙는다.
/// 비활성 플랫폼에서는 GameObject 자체가 꺼지므로 UI가 화면에 그려지지 않는다.
/// </summary>
public class TouchInputSource : MonoBehaviour, IPlayerInputSource
{
    [Header("Joysticks")]
    [SerializeField] private VirtualJoystick moveJoystick;
    [SerializeField] private VirtualJoystick aimJoystick;

    [Header("Buttons")]
    [SerializeField] private VirtualButton dashButton;
    [SerializeField] private VirtualButton absorbButton;
    [SerializeField] private VirtualButton fireButton;

    [Header("Fire")]
    [Tooltip("조준 스틱을 이 값 이상 기울이면 자동으로 사격한다.")]
    [Range(0f, 1f)]
    [SerializeField] private float autoFireThreshold = 0.3f;

    [Tooltip("사격 버튼을 눌렀을 때의 조준 방식.")]
    [SerializeField] private FireButtonAimMode fireButtonAimMode = FireButtonAimMode.FacingDirection;

    public Vector2 MoveInput => moveJoystick != null ? moveJoystick.Value : Vector2.zero;

    /// <summary>
    /// 조준 방향. 우측 스틱이 우선이며, 스틱이 중립일 때만 사격 버튼 방식이 적용된다.
    ///
    /// FacingDirection 모드에서 Vector2.zero를 반환하는 것은 의도된 동작이다.
    /// 조준 입력이 없으면 BlobController가 회전하지 않고,
    /// 캐릭터는 마지막으로 바라보던 방향을 그대로 유지한 채 사격한다.
    /// </summary>
    public Vector2 AimInput
    {
        get
        {
            Vector2 stick = AimStick;

            if (stick.sqrMagnitude > 0f)
                return stick;

            if (FireButtonHeld && fireButtonAimMode == FireButtonAimMode.MoveDirection)
                return MoveInput;

            return Vector2.zero;
        }
    }

    /// <summary>조준 스틱을 충분히 기울였거나, 사격 버튼을 누르고 있으면 사격한다.</summary>
    public bool ShootHeld => IsAimStickFiring || FireButtonHeld;

    public bool DashPressed => dashButton != null && dashButton.ConsumePressed();

    public bool AbsorbPressed => absorbButton != null && absorbButton.ConsumePressed();

    private Vector2 AimStick => aimJoystick != null ? aimJoystick.Value : Vector2.zero;

    private bool IsAimStickFiring => AimStick.sqrMagnitude >= autoFireThreshold * autoFireThreshold;

    private bool FireButtonHeld => fireButton != null && fireButton.IsHeld;

    /// <summary>런타임 생성 시 참조를 주입한다.</summary>
    public void Initialize(
        VirtualJoystick move,
        VirtualJoystick aim,
        VirtualButton dash,
        VirtualButton absorb,
        VirtualButton fire,
        FireButtonAimMode aimMode)
    {
        moveJoystick = move;
        aimJoystick = aim;
        dashButton = dash;
        absorbButton = absorb;
        fireButton = fire;
        fireButtonAimMode = aimMode;
    }

    private void Start()
    {
        if (moveJoystick == null || aimJoystick == null)
            GameLogger.Error("[TouchInputSource] 조이스틱 참조가 비어 있습니다.", this);
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }
}
