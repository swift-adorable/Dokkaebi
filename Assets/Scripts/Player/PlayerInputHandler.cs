using UnityEngine;

/// <summary>
/// 입력 수집 전담 컴포넌트. BlobController는 이 값만 읽는다.
///
/// 주의: 현재 레거시 Input(마우스/키보드) 기반이다.
/// iOS/Android에서는 단일 터치가 마우스 버튼0으로 자동 매핑되어
/// '사격'과 '조준'만 우연히 동작하고, 이동/대시/흡수는 동작하지 않는다.
/// 로드맵 1단계에서 Input System + 가상 조이스틱으로 전면 교체 예정.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }

    public Vector3 LookPoint { get; private set; }

    public bool ShootHeld { get; private set; }

    public bool DashPressed { get; private set; }

    public bool AbsorbPressed { get; private set; }

    private Camera mainCamera;

    private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

    private void Awake()
    {
        // Camera.main은 태그 기반 조회이므로 매 프레임 호출하지 않고 1회 캐싱한다.
        mainCamera = Camera.main;

        if (mainCamera == null)
            GameLogger.Error("[PlayerInputHandler] MainCamera 태그를 가진 카메라가 없습니다.", this);
    }

    private void Update()
    {
        ReadMoveInput();
        ReadLookInput();
        ReadShootInput();
        ReadDashInput();
        ReadAbsorbInput();
    }

    private void ReadMoveInput()
    {
        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;
    }

    private void ReadLookInput()
    {
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (groundPlane.Raycast(ray, out float distance))
        {
            LookPoint = ray.GetPoint(distance);
        }
    }

    private void ReadShootInput()
    {
        ShootHeld = Input.GetMouseButton(0);
    }

    private void ReadDashInput()
    {
        DashPressed = Input.GetKeyDown(KeyCode.Space);
    }

    private void ReadAbsorbInput()
    {
        AbsorbPressed = Input.GetKeyDown(KeyCode.E);
    }
}
