using UnityEngine;

/// <summary>
/// 에디터/PC용 입력 소스. 키보드 + 마우스.
///
/// 실기기 빌드에서는 비활성화되며, 개발 중 빠른 반복 테스트만을 위해 존재한다.
/// 레거시 Input을 쓰는 이유: Project Settings의 Active Input Handling이
/// 'Both'로 설정되어 있어 그대로 동작하며, 에디터 전용 경로에 Input System
/// 액션 에셋을 별도 구성하는 것은 비용 대비 이득이 없기 때문이다.
/// </summary>
[DefaultExecutionOrder(-200)]
public class DesktopInputSource : MonoBehaviour, IPlayerInputSource
{
    [Header("Keys")]
    [SerializeField] private KeyCode dashKey = KeyCode.Space;
    [SerializeField] private KeyCode absorbKey = KeyCode.E;

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }
    public bool ShootHeld { get; private set; }
    public bool DashPressed { get; private set; }
    public bool AbsorbPressed { get; private set; }

    private Camera mainCamera;
    private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
    private bool isActive = true;

    private void Awake()
    {
        // Camera.main은 태그 조회이므로 1회만 캐싱한다.
        mainCamera = Camera.main;

        if (mainCamera == null)
            GameLogger.Error("[DesktopInputSource] MainCamera 태그를 가진 카메라가 없습니다.", this);
    }

    public void SetActive(bool active)
    {
        isActive = active;

        if (!active)
            ClearInput();
    }

    private void Update()
    {
        if (!isActive)
            return;

        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        AimInput = ReadAimDirection();
        ShootHeld = Input.GetMouseButton(0);
        DashPressed = Input.GetKeyDown(dashKey);
        AbsorbPressed = Input.GetKeyDown(absorbKey);
    }

    /// <summary>마우스 위치를 지면에 투영해 플레이어 기준 조준 '방향'을 구한다.</summary>
    private Vector2 ReadAimDirection()
    {
        if (mainCamera == null)
            return Vector2.zero;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!groundPlane.Raycast(ray, out float distance))
            return Vector2.zero;

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector3 direction = worldPoint - transform.position;

        Vector2 planar = new Vector2(direction.x, direction.z);

        return planar.sqrMagnitude < 0.0001f ? Vector2.zero : planar.normalized;
    }

    private void ClearInput()
    {
        MoveInput = Vector2.zero;
        AimInput = Vector2.zero;
        ShootHeld = false;
        DashPressed = false;
        AbsorbPressed = false;
    }
}
