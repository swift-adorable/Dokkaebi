using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 튜토리얼 안내 카드 (결정 2-85) — 화면 위 가운데, 퀘스트 HUD 바로 아래.
///   「튜토리얼 3/12 · 구역」 / 「환목궁을 무기 칸에 걸어라」 / 작은 글씨로 하는 법
/// 가방 화면(1000)보다 위에 그린다 — 장착 · 줍기처럼 가방 화면 안에서 하는 단계가 있다.
/// 단계를 마치면 잠깐 초록으로 깜빡인다. 튜토리얼 창 · 손가락 표시는 레이어 · 아트 때.
/// </summary>
public class TutorialHudUI : MonoBehaviour
{
    private static TutorialHudUI instance;

    private const float Width = 640f;
    private const float Height = 104f;
    private const float FlashSeconds = 0.8f;

    private Image card;
    private Text header;
    private Text title;
    private Text hint;
    private float flashUntil;

    public static TutorialHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("TutorialHudCanvas (Runtime)", 1100);
        instance = canvas.gameObject.AddComponent<TutorialHudUI>();
        instance.Build(canvas);
        return instance;
    }

    public static void Clear()
    {
        if (instance != null)
            Destroy(instance.gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        card = UIFactory.CreatePanel("TutorialCard", safe, new Color(0.08f, 0.10f, 0.14f, 0.82f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), UIFactory.Radius);
        card.raycastTarget = false;
        RectTransform rect = card.rectTransform;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(Width, Height);
        rect.anchoredPosition = new Vector2(0f, -96f);   // 퀵 HUD(66 + 여백) 아래
        UIFactory.CreateOutline(card, UIPalette.TextAccent, UIFactory.Radius, 2);

        header = UIFactory.CreateLabel(card.transform, "", 17, FontStyle.Bold,
            new Vector2(0.03f, 0.70f), new Vector2(0.97f, 0.97f), TextAnchor.MiddleLeft, UIPalette.TextAccent);
        title = UIFactory.CreateLabel(card.transform, "", 25, FontStyle.Bold,
            new Vector2(0.03f, 0.34f), new Vector2(0.97f, 0.72f), TextAnchor.MiddleLeft, UIPalette.TextOnGlass);
        hint = UIFactory.CreateLabel(card.transform, "", 17, FontStyle.Normal,
            new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.36f), TextAnchor.MiddleLeft, UIPalette.TextDim);

        foreach (Text t in new[] { header, title, hint })
            t.raycastTarget = false;
    }

    public void Show(int number, int total, string text, string howTo, string place)
    {
        header.text = $"튜토리얼 {number}/{total} · {place}";
        title.text = text;
        hint.text = howTo;
    }

    /// <summary>단계를 마쳤다 — 잠깐 초록.</summary>
    public void Flash() => flashUntil = Time.unscaledTime + FlashSeconds;

    private void Update()
    {
        bool flashing = Time.unscaledTime < flashUntil;
        card.color = flashing ? new Color(0.16f, 0.42f, 0.22f, 0.9f) : new Color(0.08f, 0.10f, 0.14f, 0.82f);
    }
}
