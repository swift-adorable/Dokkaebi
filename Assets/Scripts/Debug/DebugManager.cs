using UnityEngine;

public class DebugManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    private void Update()
    {
        if (!enableDebug)
            return;

        HandleDebugInput();
    }

    private void HandleDebugInput()
    {
        // F1 : 경험치 +10
        if (Input.GetKeyDown(KeyCode.F1))
        {
            PlayerStats.Instance.AddXP(10);
        }

        // F2 : 모든 Mutation 획득 (추후 구현)
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Debug.Log("TODO : Give All Mutations");
        }

        // F3 : 적 생성 (추후 구현)
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Debug.Log("TODO : Spawn Enemy");
        }

        // F4 : 체력 회복 (추후 구현)
        if (Input.GetKeyDown(KeyCode.F4))
        {
            Debug.Log("TODO : Heal");
        }
    }
}