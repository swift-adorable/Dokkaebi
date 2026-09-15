using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        SetState(GameState.Playing);
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;

        switch (CurrentState)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                break;

            case GameState.Pause:
                Time.timeScale = 0f;
                break;

            case GameState.Mutation:
                Time.timeScale = 0f;
                break;

            case GameState.GameOver:
                Time.timeScale = 0f;
                break;
        }

        Debug.Log($"Game State : {CurrentState}");
    }

    public void Pause()
    {
        SetState(GameState.Pause);
    }

    public void Resume()
    {
        SetState(GameState.Playing);
    }

    public void OpenMutation()
    {
        SetState(GameState.Mutation);
    }

    public void CloseMutation()
    {
        SetState(GameState.Playing);
    }

    public void GameOver()
    {
        SetState(GameState.GameOver);
    }

    public bool IsPlaying()
    {
        return CurrentState == GameState.Playing;
    }
}