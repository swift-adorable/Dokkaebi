using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배치 모드의 버튼 — 회전 · 배치 · 취소. 화면 아래 가운데. (로드맵 8-K)
///
/// 【가운데 아래에 둔다】 — 왼쪽 아래는 이동 조이스틱, 오른쪽 아래는 조준 · 대시다.
/// 배치 중에도 걸어 다녀야 하므로 조이스틱을 덮지 않는다.
/// </summary>
public class PlacementHudUI : MonoBehaviour
{
    private static PlacementHudUI instance;

    private GameObject root;
    private Text title;
    private Text flash;
    private float flashUntil;

    public static void Show(string buildingName)
    {
        EnsureInstance();

        instance.title.text = $"{buildingName} — 걸어가서 자리를 잡으십시오";
        instance.flash.text = string.Empty;
        instance.root.SetActive(true);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.root.SetActive(false);
    }

    public static void Flash(string message)
    {
        if (instance == null)
            return;

        instance.flash.text = message;
        instance.flashUntil = Time.unscaledTime + 1.6f;
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        Canvas canvas = UIFactory.CreateCanvas("PlacementHudCanvas (Runtime)", 950);
        instance = canvas.gameObject.AddComponent<PlacementHudUI>();
        instance.Build(canvas);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        root = UIFactory.CreateRegion("Placement", safe,
            new Vector2(0.32f, 0.02f), new Vector2(0.68f, 0.26f)).gameObject;

        Transform parent = root.transform;

        title = UIFactory.CreateLabel(parent, string.Empty, 22, FontStyle.Bold,
            new Vector2(0f, 0.74f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, UIPalette.TextOnGlass);

        flash = UIFactory.CreateLabel(parent, string.Empty, 20, FontStyle.Normal,
            new Vector2(0f, 0.52f), new Vector2(1f, 0.74f), TextAnchor.MiddleCenter, UIPalette.Warning);

        UIFactory.CreateButton(parent, "회전", new Vector2(0f, 0.04f), new Vector2(0.31f, 0.48f),
            UIPalette.Subtle, () => BunkerBuildings.Instance?.Rotate(), 26);

        UIFactory.CreateButton(parent, "배치", new Vector2(0.345f, 0.04f), new Vector2(0.655f, 0.48f),
            UIPalette.Action, () => BunkerBuildings.Instance?.Confirm(), 26);

        UIFactory.CreateButton(parent, "취소", new Vector2(0.69f, 0.04f), new Vector2(1f, 0.48f),
            UIPalette.Subtle, () => BunkerBuildings.Instance?.EndPlacing(), 26);

        root.SetActive(false);
    }

    private void Update()
    {
        if (flash != null && flash.text.Length > 0 && Time.unscaledTime > flashUntil)
            flash.text = string.Empty;
    }
}
