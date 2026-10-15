using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 튜토리얼 안내 카드 (결정 2-85 → 2-132) — **오른쪽 위, 장비 · 스킬 · 패시브 · 도감 버튼 바로 아래**.
/// 가운데에 두니 싸울 때 화면을 가렸다(사용자 「너무 거슬려」) — 퀘스트 목록을 오른쪽에 두는 게임들처럼 옮겼다.
///   「튜토리얼 3/12 · 구역」 / 「환목궁을 무기 칸에 걸어라」 / 작은 글씨로 하는 법
/// 가방 화면(1000)보다 위에 그린다 — 장착 · 줍기처럼 가방 화면 안에서 하는 단계가 있다.
/// 단계를 마치면 잠깐 초록으로 깜빡인다. 튜토리얼 창 · 손가락 표시는 레이어 · 아트 때.
/// </summary>
public class TutorialHudUI : MonoBehaviour
{
    private static TutorialHudUI instance;

    private const float Width = 440f;
    private const float Height = 112f;
    private const float Margin = 10f;
    private const float ButtonRow = 62f;   // 오른쪽 위 버튼 높이 (InventoryScreenUI.HudButtonHeight)
    private const float FlashSeconds = 0.8f;

    private static readonly Color Normal = new(0.08f, 0.10f, 0.14f, 0.72f);

    private Image card;
    private RectTransform cardRect;
    private bool docked;

    /// <summary>
    /// 창이 떠 있으면(가방 · 창고 · 가게 …) 오른쪽 위에 두면 창의 머리 · 탭을 덮는다(결정 2-133 — 사용자 「다른 창 위로 겹쳐 보임」).
    /// 그동안은 아래 가운데로 내려 둔다. 가방 안에서 하는 단계(무기 걸기 · 화살 채우기 · 창고)가 있어 숨기지는 않는다.
    /// </summary>
    private void Dock(bool bottom)
    {
        docked = bottom;
        if (bottom)
        {
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0f);
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.sizeDelta = new Vector2(DockedWidth, DockedHeight);
            cardRect.anchoredPosition = new Vector2(0f, Margin);
        }
        else
        {
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(1f, 1f);
            cardRect.pivot = new Vector2(1f, 1f);
            cardRect.sizeDelta = new Vector2(Width, Height);
            cardRect.anchoredPosition = new Vector2(-Margin, HudLayout.TopRightButtonsY - ButtonRow - Margin);
        }
    }

    private const float DockedWidth = 620f;
    private const float DockedHeight = 96f;
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

        card = UIFactory.CreatePanel("TutorialCard", safe, Normal,
            new Vector2(1f, 1f), new Vector2(1f, 1f), UIFactory.Radius);
        card.raycastTarget = false;
        RectTransform rect = card.rectTransform;
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(Width, Height);
        cardRect = rect;
        Dock(false);
        UIFactory.CreateOutline(card, UIPalette.TextAccent, UIFactory.Radius, 2);

        header = UIFactory.CreateLabel(card.transform, "", 17, FontStyle.Bold,
            new Vector2(0.03f, 0.70f), new Vector2(0.97f, 0.97f), TextAnchor.MiddleLeft, UIPalette.TextAccent);
        title = UIFactory.CreateLabel(card.transform, "", 22, FontStyle.Bold,
            new Vector2(0.03f, 0.34f), new Vector2(0.97f, 0.72f), TextAnchor.MiddleLeft, UIPalette.TextOnGlass);
        hint = UIFactory.CreateLabel(card.transform, "", 16, FontStyle.Normal,
            new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.36f), TextAnchor.MiddleLeft, UIPalette.TextDim);

        foreach (Text t in new[] { header, title, hint })
            t.raycastTarget = false;

        // 폭이 좁아졌으므로 긴 문장은 글씨를 줄여 칸 안에 넣는다.
        StationFrame.Shrink(title, 15);
        StationFrame.Shrink(hint, 12);
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
        if (cardRect != null && docked != GamePause.IsHeld)
            Dock(GamePause.IsHeld);

        bool flashing = Time.unscaledTime < flashUntil;
        card.color = flashing ? new Color(0.16f, 0.42f, 0.22f, 0.9f) : Normal;
    }
}
