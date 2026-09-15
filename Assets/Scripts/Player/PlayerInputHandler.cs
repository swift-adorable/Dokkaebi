using UnityEngine;

/// <summary>입력 소스 선택 방식.</summary>
public enum InputSourceMode
{
    /// <summary>플랫폼에 따라 자동 선택. (에디터/PC: Desktop, 실기기: Touch)</summary>
    Auto,

    /// <summary>강제로 키보드/마우스 사용.</summary>
    ForceDesktop,

    /// <summary>강제로 터치 사용. (에디터에서 마우스로 조이스틱 테스트 시 유용)</summary>
    ForceTouch
}

/// <summary>
/// 입력 파사드(Facade). BlobController는 오직 이 클래스만 바라본다.
///
/// 실제 입력 수집은 IPlayerInputSource 구현체(Desktop / Touch)가 담당하며,
/// 이 클래스는 활성 소스를 고르고 값을 중계할 뿐이다.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(DesktopInputSource))]
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private InputSourceMode mode = InputSourceMode.Auto;

    [Header("References")]
    [SerializeField] private DesktopInputSource desktopSource;
    [Tooltip("모바일 입력 Canvas에 붙은 컴포넌트. 비워두면 런타임에 자동 탐색한다.")]
    [SerializeField] private TouchInputSource touchSource;

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }
    public bool ShootHeld { get; private set; }
    public bool DashPressed { get; private set; }
    public bool AbsorbPressed { get; private set; }

    /// <summary>현재 사용 중인 입력 소스가 터치인지.</summary>
    public bool IsUsingTouch { get; private set; }

    private IPlayerInputSource activeSource;

    private void Awake()
    {
        if (desktopSource == null)
            desktopSource = GetComponent<DesktopInputSource>();

        if (touchSource == null)
            touchSource = FindAnyObjectByType<TouchInputSource>(FindObjectsInactive.Include);

        ResolveActiveSource();
    }

    private void ResolveActiveSource()
    {
        IsUsingTouch = mode switch
        {
            InputSourceMode.ForceDesktop => false,
            InputSourceMode.ForceTouch => true,
            _ => Application.isMobilePlatform
        };

        // 사용하지 않는 소스는 꺼서 불필요한 Update와 UI 렌더링을 막는다.
        if (desktopSource != null)
            desktopSource.SetActive(!IsUsingTouch);

        if (touchSource != null)
            touchSource.SetActive(IsUsingTouch);

        activeSource = IsUsingTouch ? (IPlayerInputSource)touchSource : desktopSource;

        if (activeSource == null)
        {
            GameLogger.Error(
                $"[PlayerInputHandler] 활성 입력 소스를 찾을 수 없습니다. (터치 모드: {IsUsingTouch})", this);
            return;
        }

        GameLogger.Log($"[PlayerInputHandler] 입력 소스 -> {(IsUsingTouch ? "Touch" : "Desktop")}");
    }

    private void Update()
    {
        if (activeSource == null)
            return;

        MoveInput = activeSource.MoveInput;
        AimInput = activeSource.AimInput;
        ShootHeld = activeSource.ShootHeld;

        // 아래 두 값은 '1회 소비' 방식이므로 프레임당 정확히 한 번만 읽어야 한다.
        DashPressed = activeSource.DashPressed;
        AbsorbPressed = activeSource.AbsorbPressed;
    }
}
