using UnityEngine;

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

    [Header("Fire")]
    [Tooltip("조준 스틱을 이 값 이상 기울이면 자동으로 사격한다.")]
    [Range(0f, 1f)]
    [SerializeField] private float autoFireThreshold = 0.3f;

    public Vector2 MoveInput => moveJoystick != null ? moveJoystick.Value : Vector2.zero;

    public Vector2 AimInput => aimJoystick != null ? aimJoystick.Value : Vector2.zero;

    /// <summary>조준 스틱을 일정 이상 기울이면 사격한다. (별도 사격 버튼 없음)</summary>
    public bool ShootHeld => AimInput.sqrMagnitude >= autoFireThreshold * autoFireThreshold;

    public bool DashPressed => dashButton != null && dashButton.ConsumePressed();

    public bool AbsorbPressed => absorbButton != null && absorbButton.ConsumePressed();

    private void Awake()
    {
        if (moveJoystick == null || aimJoystick == null)
            GameLogger.Error("[TouchInputSource] 조이스틱 참조가 비어 있습니다.", this);
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }
}
