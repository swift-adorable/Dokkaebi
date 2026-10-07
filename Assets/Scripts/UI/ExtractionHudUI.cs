using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 철수 지점 HUD (결정 2-78) — 가장 가까운 철수 지점을 가리키는 화살표 + 거리,
/// 원 안에서는 화면 아래 가운데에 「철수 중」 막대.
///
/// 지점이 화면 밖이면 화살표가 화면 가장자리에 붙어 그쪽을 가리키고,
/// 화면 안이면 기둥 위에서 아래를 가리킨다. 아트 작업 때 다시 그린다.
/// </summary>
public class ExtractionHudUI : MonoBehaviour
{
    private static ExtractionHudUI instance;

    private static readonly Color Green = new(0.45f, 0.95f, 0.55f, 1f);
    private const float EdgeMargin = 70f;   // 캔버스 픽셀
    private const float BarWidth = 420f;
    private const float BarHeight = 46f;

    private Canvas canvas;
    private RectTransform root;
    private RectTransform arrow;
    private Text arrowGlyph;
    private Text distanceLabel;
    private GameObject bar;
    private RectTransform barFill;
    private Text barLabel;
    private bool hiddenByScreen;

    public static ExtractionHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        // 생존 HUD(800) · 퀘스트 HUD(810) 바로 위. 가방 화면(1000)이 덮는다.
        Canvas c = UIFactory.CreateCanvas("ExtractionHudCanvas (Runtime)", 805);
        instance = c.gameObject.AddComponent<ExtractionHudUI>();
        instance.Build(c);
        return instance;
    }

    public static void SetHiddenByScreen(bool hidden)
    {
        if (instance == null)
            return;

        instance.hiddenByScreen = hidden;
        if (hidden)
            instance.HideAll();
    }

    /// <summary>철수했거나 구역이 닫힐 때 — 화면에서 치운다.</summary>
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

    private void Build(Canvas c)
    {
        canvas = c;
        root = (RectTransform)c.transform;

        GameObject arrowObject = UIFactory.CreateChild("Arrow", root);
        arrow = arrowObject.GetComponent<RectTransform>();
        arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.pivot = new Vector2(0.5f, 0.5f);
        arrow.sizeDelta = new Vector2(64f, 64f);

        arrowGlyph = UIFactory.CreateLabel(arrow, "▲", 44, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, Green);
        arrowGlyph.raycastTarget = false;
        arrowGlyph.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

        distanceLabel = UIFactory.CreateLabel(root, "", 22, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, Green);
        distanceLabel.rectTransform.sizeDelta = new Vector2(220f, 30f);
        distanceLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        distanceLabel.raycastTarget = false;
        distanceLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

        Image back = UIFactory.CreatePanel("ExtractionBar", root, UIPalette.Panel,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        back.raycastTarget = false;
        bar = back.gameObject;
        var barRect = back.rectTransform;
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(BarWidth, BarHeight);
        barRect.anchoredPosition = new Vector2(0f, 180f);

        Image fill = UIFactory.CreatePanel("Fill", barRect, new Color(0.30f, 0.80f, 0.40f, 0.85f),
            Vector2.zero, new Vector2(0f, 1f));
        fill.raycastTarget = false;
        barFill = fill.rectTransform;
        UIFactory.Inset(barFill, 4f);

        barLabel = UIFactory.CreateLabel(barRect, "", 22, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextOnGlass);
        barLabel.raycastTarget = false;

        HideAll();
    }

    private void HideAll()
    {
        arrow.gameObject.SetActive(false);
        distanceLabel.gameObject.SetActive(false);
        bar.SetActive(false);
    }

    /// <summary>매 프레임 — 철수 지점 연출이 부른다.</summary>
    public void Show(Vector3 player, Vector3 target, ExtractionChannel channel, bool playerDead)
    {
        if (hiddenByScreen || playerDead)
        {
            HideAll();
            return;
        }

        bool holding = channel.Holding;
        bar.SetActive(holding);

        if (holding)
        {
            barFill.anchorMax = new Vector2(channel.Progress, 1f);
            barLabel.text = $"철수 중 — {channel.Remaining:0.0}초 (원을 벗어나면 끊긴다)";
            arrow.gameObject.SetActive(false);
            distanceLabel.gameObject.SetActive(false);
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            HideAll();
            return;
        }

        PlaceArrow(cam, target + Vector3.up * 8.5f);

        float dx = player.x - target.x;
        float dz = player.z - target.z;
        distanceLabel.text = $"{ExtractionDirector.Name} {Mathf.Sqrt(dx * dx + dz * dz):0}m";
    }

    private void PlaceArrow(Camera cam, Vector3 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        float w = Screen.width;
        float h = Screen.height;
        float margin = EdgeMargin * canvas.scaleFactor;

        // 카메라 뒤쪽이면 화면 좌표가 뒤집힌다 — 가운데를 기준으로 되돌린다.
        if (sp.z < 0f)
        {
            sp.x = w - sp.x;
            sp.y = h - sp.y;
        }

        bool onScreen = sp.z >= 0f && sp.x >= margin && sp.x <= w - margin
                        && sp.y >= margin && sp.y <= h - margin;

        Vector2 center = new(w * 0.5f, h * 0.5f);
        Vector2 screen;
        float angle;

        if (onScreen)
        {
            screen = new Vector2(sp.x, sp.y);
            angle = 180f; // 기둥 위에서 아래를 가리킨다
        }
        else
        {
            Vector2 dir = new Vector2(sp.x, sp.y) - center;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector2.up;

            float kx = Mathf.Abs(dir.x) > 0.001f ? (center.x - margin) / Mathf.Abs(dir.x) : float.MaxValue;
            float ky = Mathf.Abs(dir.y) > 0.001f ? (center.y - margin) / Mathf.Abs(dir.y) : float.MaxValue;
            screen = center + dir * Mathf.Min(kx, ky);
            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);

        arrow.gameObject.SetActive(true);
        distanceLabel.gameObject.SetActive(true);
        arrow.anchoredPosition = local;
        arrow.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 거리는 화살표의 화면 안쪽에 둔다 — 가장자리에서 잘리지 않게.
        Vector2 inward = (center - screen).normalized;
        float push = onScreen ? 44f : 52f;
        Vector2 labelOffset = onScreen ? new Vector2(0f, 44f) : inward * push;
        distanceLabel.rectTransform.anchoredPosition = local + labelOffset;
    }
}
