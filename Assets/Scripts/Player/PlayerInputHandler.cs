using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }

    public Vector3 LookPoint { get; private set; }

    public bool ShootHeld { get; private set; }

    public bool DashPressed { get; private set; }

    public bool AbsorbPressed { get; private set; }

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
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

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