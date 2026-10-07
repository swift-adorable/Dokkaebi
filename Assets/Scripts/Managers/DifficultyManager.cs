/// <summary>
/// 이번 게임의 난이도 (결정 2-69). 새로 시작할 때 프롤로그 뒤에 한 번 고르고, 바꿀 수 없다.
/// 세이브에 담긴다(SaveData.difficulty). 고르기 전에는 Normal로 친다.
/// </summary>
public static class DifficultyManager
{
    /// <summary>세이브에서 「아직 고르지 않았다」.</summary>
    public const int NotChosen = -1;

    private static int chosen = NotChosen;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => chosen = NotChosen;

    public static bool IsChosen => chosen != NotChosen;

    public static DifficultyLevel Level => IsChosen ? DifficultyTable.Normalize(chosen) : DifficultyLevel.Normal;

    /// <summary>고른다. 【한 번만】 — 이미 골랐으면 false.</summary>
    public static bool Choose(DifficultyLevel level)
    {
        if (IsChosen)
            return false;

        chosen = (int)level;
        return true;
    }

    public static int Capture() => chosen;

    /// <summary>되살린다. 옛 세이브(판 10 이전)는 값이 없어 NotChosen — 다음 소굴에서 고르게 한다.</summary>
    public static void Restore(int saved)
        => chosen = saved == NotChosen ? NotChosen : (int)DifficultyTable.Normalize(saved);

    public static void Reset() => chosen = NotChosen;
}
