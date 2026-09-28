using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 벙커 자리의 버튼. 흡수 버튼과 같은 자리(대시 버튼 위)에 뜬다. (8-J)
///
/// 【가까운 자리가 없으면 완전히 사라진다】 — 버튼이 보인다는 것이 곧
/// 「지금 누를 수 있다」는 신호다 (AbsorbPrompt와 같은 규칙).
/// 가방 화면이나 창고·상점 패널이 열려 있으면 감춘다.
/// </summary>
public class StationPromptUI : MonoBehaviour
{
    // 흡수 버튼(MobileInputUIFactory)과 같은 자리 · 같은 크기. 벙커에는 시체가 없어 겹치지 않는다.
    private const float Size = MobileInputUIFactory.AbsorbPromptSize;
    private static readonly Vector2 Position = MobileInputUIFactory.AbsorbButtonPosition;

    private static StationPromptUI instance;

    private CanvasGroup group;
    private Text label;
    private Transform player;
    private BunkerStation current;

    public static StationPromptUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("StationPromptCanvas (Runtime)", 900);
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        instance = canvas.gameObject.AddComponent<StationPromptUI>();
        instance.Build(safe);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(RectTransform safe)
    {
        var root = new GameObject("StationPrompt", typeof(RectTransform));
        root.transform.SetParent(safe, false);

        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Size, Size);
        rect.anchoredPosition = Position;

        group = root.AddComponent<CanvasGroup>();

        Button button = UIFactory.CreateButton(rect, "", Vector2.zero, Vector2.one,
            UIPalette.Action, OnPressed, 28, radius: Mathf.RoundToInt(Size * 0.5f));

        label = button.GetComponentInChildren<Text>();

        SetVisible(false);
    }

    private void Update()
    {
        if (player == null)
        {
            var blob = FindAnyObjectByType<BlobController>(FindObjectsInactive.Exclude);
            player = blob != null ? blob.transform : null;
        }

        bool covered = (InventoryScreenUI.HasInstance && InventoryScreenUI.Instance.IsOpen)
                       || ExchangeWindowUI.IsOpenIn(ExchangeWindowUI.Mode.Stash)
                       || ExchangeWindowUI.IsOpenIn(ExchangeWindowUI.Mode.Shop);

        current = player != null && !covered ? BunkerStation.NearestTo(player.position) : null;

        if (current != null && label != null)
            label.text = current.Label;

        SetVisible(current != null);
    }

    private void SetVisible(bool visible)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    private void OnPressed()
    {
        if (current != null)
            current.Use();
    }
}
