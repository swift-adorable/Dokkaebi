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

    public void OpenMutation() => SetState(GameState.Mutation);

    public void CloseMutation() => SetState(GameState.Playing);

    public void GameOver() => SetState(GameState.GameOver);
}
