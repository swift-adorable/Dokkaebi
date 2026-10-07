using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미리 채우기 버튼 (결정 2-81) — 든 무기의 통이 25% 이하로 남고 가방에 탄이 있을 때만 뜬다.
/// 누르면(데스크톱은 R도) 덜 찬 통을 채운다. 다 비면 저절로 채우므로 이 버튼은 「미리」를 위한 것이다.
///
/// 【자리는 임시】 오른손 엄지가 닿는 대시 버튼 왼쪽. 모바일 배틀그라운드 배치를 참고해 다시 정한다.
/// </summary>
public class ReloadButtonUI : MonoBehaviour
{
    private static ReloadButtonUI instance;

    private const float Size = 130f;
    // 대시 버튼(오른쪽 아래 190px · 가장자리 80px) 왼쪽 40px — MobileInputUIFactory 값과 맞춘다.
    private static readonly Vector2 Position = new(-(80f + 190f + 40f + Size * 0.5f), 80f + 95f);

    private GameObject root;
    private Text label;
    private bool hiddenByScreen;

    public static ReloadButtonUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        // 모바일 입력 캔버스보다 위 — 조준 스틱 영역 위에 떠도 눌린다.
        Canvas canvas = UIFactory.CreateCanvas("ReloadButtonCanvas (Runtime)", 900);
        instance = canvas.gameObject.AddComponent<ReloadButtonUI>();
        instance.Build(canvas);
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        Image disc = UIFactory.CreatePanel("ReloadButton", canvas.transform,
            new Color(0.10f, 0.12f, 0.16f, 0.72f), new Vector2(1f, 0f), new Vector2(1f, 0f), radius: 64);
        RectTransform rect = disc.rectTransform;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Size, Size);
        rect.anchoredPosition = Position;
        UIFactory.CreateOutline(disc, UIPalette.TextAccent, 64, 3);

        var button = disc.gameObject.AddComponent<Button>();
        button.targetGraphic = disc;
        button.onClick.AddListener(() => PlayerAmmo.Current?.RequestReload());

        label = UIFactory.CreateLabel(disc.transform, "", 24, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextAccent);
        label.raycastTarget = false;

        root = disc.gameObject;
        root.SetActive(false);
    }

    public void Show(bool visible, int loaded, int capacity)
    {
        visible &= !hiddenByScreen;

        if (root.activeSelf != visible)
            root.SetActive(visible);

        if (visible)
            label.text = $"채우기\n{loaded}/{capacity}";
    }

    /// <summary>가방 화면이 열리면 숨긴다.</summary>
    public static void SetHiddenByScreen(bool hidden)
    {
        if (instance == null)
            return;

        instance.hiddenByScreen = hidden;
        if (hidden)
            instance.root.SetActive(false);
    }
}
