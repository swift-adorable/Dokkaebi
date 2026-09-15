using UnityEngine;

/// <summary>
/// 플레이어 레벨/경험치 관리. 레벨업 발생 시 MutationManager에 '횟수'를 전달한다.
/// </summary>
public class PlayerStats : Singleton<PlayerStats>
{
    [Header("Level")]
    [Tooltip("레벨 1에서 2로 가는 데 필요한 경험치")]
    [SerializeField] private int baseRequiredXP = 10;

    [Tooltip("레벨업할 때마다 필요 경험치에 더해지는 값")]
    [SerializeField] private int requiredXPGrowth = 5;

    public int Level { get; private set; } = 1;

    public int CurrentXP { get; private set; }

    public int RequiredXP { get; private set; }

    protected override void OnSingletonAwake()
    {
        // 0 이하로 설정되면 AddXP의 while 루프가 무한 루프가 되므로 방어한다.
        RequiredXP = Mathf.Max(1, baseRequiredXP);
        requiredXPGrowth = Mathf.Max(0, requiredXPGrowth);
    }

    public void AddXP(int amount)
    {
        if (amount <= 0)
        {
            GameLogger.Warning($"[PlayerStats] 유효하지 않은 XP 값: {amount}");
            return;
        }

        CurrentXP += amount;

        // 여러 레벨이 한 번에 오를 수 있으므로 횟수를 센 뒤 '한 번만' 통지한다.
        int levelUpCount = 0;

        while (RequiredXP > 0 && CurrentXP >= RequiredXP)
        {
            CurrentXP -= RequiredXP;
            Level++;
            RequiredXP += requiredXPGrowth;
            levelUpCount++;
        }

        GameLogger.Log($"[PlayerStats] XP {CurrentXP}/{RequiredXP} (Lv.{Level})");

        if (levelUpCount <= 0)
            return;

        GameLogger.Log($"[PlayerStats] LEVEL UP x{levelUpCount} -> Lv.{Level}");

        MutationManager.Instance.EnqueueLevelUp(levelUpCount);
    }
}
