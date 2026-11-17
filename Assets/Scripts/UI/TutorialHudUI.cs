using UnityEngine;

/// <summary>
/// 【튜토리얼 안내】 (결정 2-85 → 2-135) — 지금 단계의 글만 들고 있다. 화면에는 따로 카드를 띄우지 않고
/// 퀘스트 목록(QuestHudUI, 오른쪽 위)의 맨 위 줄로 보인다 — 사용자 「퀘스트 및 튜토리얼 관련은 전부 그 목록에」.
///   「튜토리얼 3/12 · 구역」 / 「환목궁을 무기 칸에 걸어라」 / 작은 글씨로 하는 법
/// 단계를 마치면 잠깐 초록으로 깜빡인다(Flashing).
/// </summary>
public class TutorialHudUI : MonoBehaviour
{
    private static TutorialHudUI instance;

    private const float FlashSeconds = 0.8f;

    public string Header { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Hint { get; private set; } = string.Empty;

    /// <summary>글이 바뀔 때마다 오른다 — 목록이 바로 다시 그리게.</summary>
    public int Version { get; private set; }

    private float flashUntil;
    private float newUntil;

    /// <summary>새 단계가 막 떴다 — 목록의 튜토리얼 줄이 몇 초 동안 밝게 숨 쉰다 (결정 2-138).</summary>
    public bool IsNew => Time.unscaledTime < newUntil;
    private const float NewSeconds = 4f;

    /// <summary>지금 보여 줄 단계가 있는가. 없으면 null.</summary>
    public static TutorialHudUI Current => instance != null && !string.IsNullOrEmpty(instance.Title) ? instance : null;

    public bool Flashing => Time.unscaledTime < flashUntil;

    public static TutorialHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = new GameObject("TutorialHud (Runtime)").AddComponent<TutorialHudUI>();
        return instance;
    }

    public static void Clear()
    {
        if (instance != null)
            Destroy(instance.gameObject);
        instance = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void Show(int number, int total, string text, string howTo, string place)
    {
        string header = $"◆ 튜토리얼 {number}/{total} · {place}에서";
        if (header == Header && text == Title && howTo == Hint)
            return;

        if (text != Title)
            newUntil = Time.unscaledTime + NewSeconds;
        Header = header;
        Title = text ?? string.Empty;
        Hint = howTo ?? string.Empty;
        Version++;
    }

    /// <summary>단계를 마쳤다 — 잠깐 초록.</summary>
    public void Flash()
    {
        flashUntil = Time.unscaledTime + FlashSeconds;
        Version++;
    }
}
