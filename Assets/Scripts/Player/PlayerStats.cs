using System;
using UnityEngine;

/// <summary>
/// 【각성 레벨 — 이 게임의 유일한 레벨.】 (결정 2-33)
///
/// 예전에는 레벨이 둘이었다 — 각성(출격마다 초기화, 소켓을 연다)과
/// 계정(영구, 패시브를 연다). 계정 레벨은 오르는 길이 없었고, 각성 레벨은
/// 문서와 달리 실제로는 죽어도 초기화되지 않았다. 이제 하나로 합친다.
///
/// 【영구다. 죽어도 잃지 않는다.】 덕코프의 캐릭터 레벨도 하나이고 스킬
/// 트리를 연다 — 스킬 트리는 25에서 끝나지만 레벨은 그 뒤로도 오른다.
/// [커뮤니티 확인] 소켓과 패시브가 같은 레벨에서 열린다.
///
/// 레벨업이 일어나면 SkillManager에 횟수를 알린다(소켓 개방 알림).
/// </summary>
public class PlayerStats : Singleton<PlayerStats>
{
    /// <summary>레벨이나 경험치가 바뀌었을 때. 패시브 화면이 요구 레벨을 다시 그린다.</summary>
    public event Action OnChanged;

    public static PlayerStats EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<PlayerStats>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("PlayerStats (Runtime)").AddComponent<PlayerStats>();
    }

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

        OnChanged?.Invoke();

        if (levelUpCount <= 0)
            return;

        GameLogger.Log($"[PlayerStats] LEVEL UP x{levelUpCount} -> Lv.{Level}");

        SkillManager.EnsureInstance().EnqueueLevelUp(levelUpCount);
    }

    /// <summary>이 레벨에서 다음 레벨까지 필요한 경험치.</summary>
    public int RequiredXPAt(int level)
        => Mathf.Max(1, baseRequiredXP) + Mathf.Max(0, requiredXPGrowth) * (Mathf.Max(1, level) - 1);

    /// <summary>
    /// 세이브에서 되살린다. 【레벨업 알림을 내지 않는다.】
    /// 불러올 때마다 「소켓 개방」이 열 줄씩 뜨면 안 된다.
    /// </summary>
    public void Restore(int level, int experience)
    {
        Level = Mathf.Max(1, level);
        RequiredXP = RequiredXPAt(Level);

        // 필요량 이상이 저장돼 있으면 그대로 두면 다음 AddXP에서 레벨이 한꺼번에
        // 튄다. 곡선을 바꾼 뒤 옛 세이브를 열면 생긴다.
        CurrentXP = Mathf.Clamp(experience, 0, RequiredXP - 1);

        OnChanged?.Invoke();
    }
}
