using UnityEngine;

/// <summary>
/// 개발용 치트 입력. 에디터/개발 빌드에서만 동작한다.
///
/// macOS에서 F1~F4는 시스템 기능키(밝기/볼륨)로 선점되므로
/// 기본 키를 숫자키로 둔다. 필요하면 Inspector에서 자유롭게 변경할 수 있다.
/// </summary>
public class DebugManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    [Header("Cheat Keys")]
    [Tooltip("경험치 획득")]
    [SerializeField] private KeyCode addXPKey = KeyCode.Alpha1;

    [Tooltip("모든 Skill 획득")]
    [SerializeField] private KeyCode giveAllSkillsKey = KeyCode.Alpha2;

    [Tooltip("적 즉시 생성")]
    [SerializeField] private KeyCode spawnEnemyKey = KeyCode.Alpha3;

    [Tooltip("체력 회복")]
    [SerializeField] private KeyCode healKey = KeyCode.Alpha4;

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
        if (Input.GetKeyDown(addXPKey))
            CheatAddXP();

        if (Input.GetKeyDown(giveAllSkillsKey))
            CheatGiveAllSkills();

        if (Input.GetKeyDown(spawnEnemyKey))
            CheatSpawnEnemy();

        if (Input.GetKeyDown(healKey))
            CheatHeal();
    }

    /// <summary>경험치 획득. UI 버튼 OnClick에도 연결할 수 있도록 public으로 노출한다.</summary>
    public void CheatAddXP()
    {
        if (!PlayerStats.HasInstance)
        {
            GameLogger.Warning("[DebugManager] PlayerStats가 씬에 없습니다.");
            return;
        }

        PlayerStats.Instance.AddXP(xpPerCheat);
    }

    public void CheatGiveAllSkills()
    {
        GameLogger.Log("[DebugManager] TODO : Give All Skills");
    }

    public void CheatSpawnEnemy()
    {
        GameLogger.Log("[DebugManager] TODO : Spawn Enemy");
    }

    public void CheatHeal()
    {
        GameLogger.Log("[DebugManager] TODO : Heal");
    }
#endif
}
