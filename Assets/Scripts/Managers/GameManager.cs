using System;
using UnityEngine;

/// <summary>
/// 게임 전역 상태 관리자. 상태 전환과 timeScale 제어를 단독으로 책임진다.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    public GameState CurrentState { get; private set; } = GameState.None;

    /// <summary>상태가 실제로 바뀌었을 때만 발행된다.</summary>
    public event Action<GameState> OnStateChanged;

    /// <summary>플레이 가능 상태 여부. (기존 IsPlaying() 메서드 -> 프로퍼티로 변경)</summary>
    public bool IsPlaying => CurrentState == GameState.Playing;

    [Header("Difficulty")]
    [Tooltip("전역 난이도. 서바이벌이 모든 기획 수치의 기준값이다.")]
    [SerializeField] private DifficultyLevel difficulty = DifficultyLevel.Survival;

    /// <summary>
    /// 현재 난이도.
    ///
    /// 장(Stage)은 「무엇을 만나는가」만 정한다 — 구역이 바뀐다고
    /// 적 체력에 배율을 곱하지 않는다. 배율은 이 축 하나뿐이다.
    /// (docs/Blob_Combat_Baseline.md 「적 원형」)
    /// </summary>
    public DifficultyLevel Difficulty
    {
        get => difficulty;
        set => difficulty = value;
    }

    /// <summary>
    /// 적이 주는 피해의 배율. 전투 경로가 이 값을 곱한다.
    ///
    /// 인스턴스가 없을 때 1을 돌려주는 이유 — 테스트와 씬 없는 실행에서
    /// 난이도 때문에 수치가 흔들리면 안 된다.
    /// </summary>
    public static float EnemyDamageMultiplier
        => HasInstance ? DifficultyTable.EnemyDamage(Instance.Difficulty) : 1f;

    /// <summary>적 최대 체력의 배율.</summary>
    public static float EnemyHealthMultiplier
        => HasInstance ? DifficultyTable.EnemyHealth(Instance.Difficulty) : 1f;

    private void Start()
    {
        SetState(GameState.Playing);
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;

        // Playing 외의 모든 상태는 게임 시간을 정지시킨다.
        Time.timeScale = newState == GameState.Playing ? 1f : 0f;

        GameLogger.Log($"[GameManager] State -> {CurrentState} (timeScale: {Time.timeScale})");

        OnStateChanged?.Invoke(CurrentState);
    }

    public void Pause() => SetState(GameState.Pause);

    public void Resume() => SetState(GameState.Playing);

    public void OpenSkill() => SetState(GameState.Skill);

    public void CloseSkill() => SetState(GameState.Playing);

    public void GameOver() => SetState(GameState.GameOver);
}
