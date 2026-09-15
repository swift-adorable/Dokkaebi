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
///
/// 터치 UI가 씬에 없으면 런타임에 직접 생성하므로, 씬 구성 상태와 무관하게 항상 동작한다.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(DesktopInputSource))]
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private InputSourceMode mode = InputSourceMode.Auto;

    [Header("References")]
    [SerializeField] private DesktopInputSource desktopSource;

    [Tooltip("비워두면 씬에서 탐색하고, 그래도 없으면 런타임에 생성한다.")]
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

        if (IsUsingTouch)
            EnsureTouchSource();

        if (desktopSource != null)
            desktopSource.SetActive(!IsUsingTouch);

        if (touchSource != null)
            touchSource.SetActive(IsUsingTouch);

        activeSource = IsUsingTouch ? (IPlayerInputSource)touchSource : desktopSource;

        // 최후 방어: 터치 UI 구성에 실패해도 입력이 완전히 죽지 않도록 되돌린다.
        if (activeSource == null && desktopSource != null)
        {
            GameLogger.Error("[PlayerInputHandler] 터치 입력 소스를 만들지 못해 Desktop으로 대체합니다.", this);

            IsUsingTouch = false;
            desktopSource.SetActive(true);
            activeSource = desktopSource;
        }

        if (activeSource == null)
        {
            GameLogger.Error("[PlayerInputHandler] 사용 가능한 입력 소스가 없습니다.", this);
            return;
        }

        GameLogger.Log($"[PlayerInputHandler] 입력 소스 -> {(IsUsingTouch ? "Touch" : "Desktop")}");
    }

    private void EnsureTouchSource()
    {
        if (touchSource != null)
            return;

        touchSource = FindAnyObjectByType<TouchInputSource>(FindObjectsInactive.Include);

        if (touchSource != null)
        {
            GameLogger.Log("[PlayerInputHandler] 씬에서 TouchInputSource를 찾았습니다.");
            return;
        }

        // 씬에 없으면 코드로 만든다. 씬 저장 여부와 무관하게 항상 동작한다.
        touchSource = MobileInputUIFactory.Create();
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
