using UnityEngine;

/// <summary>
/// 개발용 치트 입력. 에디터/개발 빌드에서만 동작한다.
/// </summary>
public class DebugManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    [Header("Cheat Values")]
    [SerializeField] private int xpPerCheat = 10;

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableDebug)
            return;

        HandleDebugInput();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void HandleDebugInput()
    {
        // F1 : 경험치 획득
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (PlayerStats.HasInstance)
                PlayerStats.Instance.AddXP(xpPerCheat);
            else
                GameLogger.Warning("[DebugManager] PlayerStats가 씬에 없습니다.");
        }

        // F2 : 모든 Mutation 획득
        if (Input.GetKeyDown(KeyCode.F2))
        {
            GameLogger.Log("[DebugManager] TODO : Give All Mutations");
        }

        // F3 : 적 즉시 생성
        if (Input.GetKeyDown(KeyCode.F3))
        {
            GameLogger.Log("[DebugManager] TODO : Spawn Enemy");
        }

        // F4 : 체력 회복
        if (Input.GetKeyDown(KeyCode.F4))
        {
            GameLogger.Log("[DebugManager] TODO : Heal");
        }
    }
#endif
}
