using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 좌하단의 체력 · 수분 · 에너지. (docs/Blob_Survival_System.md 7절)
///
/// 【덕코프와 같은 모양으로 둔다.】
///   하트 + 긴 막대 하나 · 그 오른쪽에 물방울 · 번개 원형 게이지 둘.
///
/// 처음에는 가로 막대 셋을 세로로 쌓고 왼쪽에 「체력 / 수분 / 에너지」를
/// 글자로 적었다. 좁은 폭에서 글자가 두 줄로 접혔고, 그 자리는 막대가
/// 써야 할 자리였다. **표식은 도형으로 둔다** — 한 번 배우면 글자보다 빠르다.
///
/// 【체력만 길게 두는 이유】 체력은 초 단위로 변하고 수분·에너지는 십수 분에
/// 걸쳐 변한다. 급한 축에 넓은 면적을 준다. 원형 게이지는 「얼마나 남았나」를
/// 한눈에 보여 주되 자리를 적게 먹는다.
/// </summary>
public class SurvivalHudUI : MonoBehaviour
{
    private static SurvivalHudUI instance;

    // ── 치수 (캔버스 픽셀) ────────────────────────────────────────────

    private const float Margin = 16f;

    private const float HeartSize = 44f;
    private const float BarWidth = 250f;
    private const float BarHeight = 34f;
    private const float GaugeSize = 58f;
    private const float Gap = 10f;

    private const float PanelWidth =
        HeartSize + Gap + BarWidth + Gap + GaugeSize + Gap + GaugeSize;

    private const float PanelHeight = 60f;

    private RectTransform root;

    private Image healthFill;
    private Image waterFill;
    private Image energyFill;

    private Image heartIcon;
    private Image waterIcon;
    private Image energyIcon;

    private Text healthLabel;
    private Text waterLabel;
    private Text energyLabel;

    private Health health;

    /// <summary>가방·전리품 화면이 열려 있으면 감춘다.</summary>
    private bool hiddenByScreen;

    public static SurvivalHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<SurvivalHudUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        // 가방 화면(1000)보다 아래. 화면이 열리면 그 판이 이 위를 덮는다.
        Canvas canvas = UIFactory.CreateCanvas("SurvivalHudCanvas (Runtime)", 800);

        instance = canvas.gameObject.AddComponent<SurvivalHudUI>();
        instance.Build(canvas);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// 화면(가방 · 전리품)이 열리고 닫힐 때 불린다.
    ///
    /// 가방 화면은 좌하단에 소지 중량 카드를 둔다 — 같은 자리다.
    /// 둘이 겹쳐서 어느 막대가 무엇인지 읽을 수 없었다.
    /// 덕코프도 가방을 열면 생존 게이지 대신 소지 중량을 보여 준다.
    /// </summary>
    public static void SetHiddenByScreen(bool hidden)
    {
        if (instance == null)
            return;

        instance.hiddenByScreen = hidden;

        if (instance.root != null)
            instance.root.gameObject.SetActive(!hidden);
    }

    // ────────────────────────────────── 생성

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        GameObject panel = UIFactory.CreateChild("SurvivalHud", safe);

        root = panel.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        root.anchoredPosition = new Vector2(Margin, Margin);

        float cursor = 0f;

        heartIcon = BuildIcon("Heart", cursor, HeartSize, UISprites.Glyph.Heart,
            UIPalette.HealthBar);

        cursor += HeartSize + Gap;

        BuildHealthBar(cursor);

        cursor += BarWidth + Gap;

        BuildGauge("Water", cursor, UISprites.Glyph.Drop, UIPalette.WaterBar,
            out waterIcon, out waterFill, out waterLabel);

        cursor += GaugeSize + Gap;

        BuildGauge("Energy", cursor, UISprites.Glyph.Bolt, UIPalette.EnergyBar,
            out energyIcon, out energyFill, out energyLabel);
    }

    /// <summary>표식 하나. 가로 위치는 픽셀, 세로는 가운데 정렬이다.</summary>
    private Image BuildIcon(string name, float left, float size,
                            UISprites.Glyph glyph, Color color)
    {
        GameObject go = UIFactory.CreateChild($"{name}Icon", root);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = new Vector2(left, 0f);

        var image = go.AddComponent<Image>();
        image.sprite = UISprites.Of(glyph);
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }

    private void BuildHealthBar(float left)
    {
        GameObject go = UIFactory.CreateChild("HealthBar", root);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(BarWidth, BarHeight);
        rect.anchoredPosition = new Vector2(left, 0f);

        var track = go.AddComponent<Image>();
        track.color = UIPalette.Inset;
        track.sprite = UISprites.Rounded(UIFactory.Radius);
        track.type = Image.Type.Sliced;
        track.raycastTarget = false;

        UIFactory.CreateOutline(track, UIPalette.EdgeSoft, UIFactory.Radius, 2);

        // 채움은 왼쪽에 붙어 오른쪽 끝(anchorMax.x)만 움직인다.
        healthFill = UIFactory.CreatePanel("HealthFill", go.transform,
            UIPalette.HealthBar, Vector2.zero, Vector2.one, UIFactory.Radius);

        UIFactory.Inset(healthFill.rectTransform, 3f);

        healthFill.raycastTarget = false;

        healthLabel = UIFactory.CreateLabel(go.transform, string.Empty, 20, FontStyle.Bold,
            new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleCenter,
            UIPalette.TextOnGlass);
    }

    /// <summary>
    /// 원형 게이지. 【안쪽에서 시계 방향으로 찬다.】
    /// Image.Type.Filled + Radial360이면 한 장의 원 스프라이트로 그려진다.
    /// </summary>
    private void BuildGauge(string name, float left, UISprites.Glyph glyph, Color color,
                            out Image icon, out Image fill, out Text label)
    {
        GameObject go = UIFactory.CreateChild($"{name}Gauge", root);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(GaugeSize, GaugeSize);
        rect.anchoredPosition = new Vector2(left, 0f);

        int radius = Mathf.RoundToInt(GaugeSize * 0.5f);

        // 뒤판 — 남은 양이 0이어도 자리는 보인다.
        var back = go.AddComponent<Image>();
        back.sprite = UISprites.Rounded(radius);
        back.type = Image.Type.Simple;
        back.color = UIPalette.Inset;
        back.raycastTarget = false;

        // 고리 — 채워지는 부분.
        GameObject fillObject = UIFactory.CreateChild("Fill", go.transform);

        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fill = fillObject.AddComponent<Image>();
        fill.sprite = UISprites.RoundedOutline(radius, Mathf.Max(4, radius / 6));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.color = color;
        fill.raycastTarget = false;

        // 표식은 고리 안쪽 가운데.
        GameObject iconObject = UIFactory.CreateChild("Icon", go.transform);

        var iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.24f, 0.30f);
        iconRect.anchorMax = new Vector2(0.76f, 0.82f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        icon = iconObject.AddComponent<Image>();
        icon.sprite = UISprites.Of(glyph);
        icon.color = color;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // 숫자는 아래쪽에 작게. 고리와 표식을 가리지 않는다.
        label = UIFactory.CreateLabel(go.transform, string.Empty, 16, FontStyle.Bold,
            new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.30f), TextAnchor.MiddleCenter,
            UIPalette.TextOnGlass);
    }

    // ────────────────────────────────── 갱신

    /// <summary>
    /// 【매 프레임 읽는다.】 이벤트로 받지 않는 이유 —
    /// Health에는 피해 알림만 있고 회복·최대치 변경에는 없다. 이벤트를 붙이면
    /// 「회복약을 먹었는데 막대가 안 움직인다」가 생긴다.
    /// 막대 세 개를 읽는 비용은 무시할 수준이다.
    /// </summary>
    private void LateUpdate()
    {
        if (root == null || hiddenByScreen)
            return;

        RefreshHealth();
        RefreshSurvival();
    }

    private void RefreshHealth()
    {
        if (health == null)
            health = FindPlayerHealth();

        if (health == null)
        {
            SetHorizontal(healthFill, healthLabel, 0f, "—", UIPalette.HealthBar, false);
            return;
        }

        float ratio = health.Normalized;

        bool warn = ratio <= SurvivalTable.HealthWarnRatio;

        SetHorizontal(healthFill, healthLabel, ratio, $"{health.Current} / {health.Max}",
            UIPalette.HealthBar, warn);

        if (heartIcon != null)
            heartIcon.color = warn ? UIPalette.Warning : UIPalette.HealthBar;
    }

    private void RefreshSurvival()
    {
        if (!PlayerSurvival.HasInstance)
        {
            SetRadial(waterFill, waterIcon, waterLabel, 0f, "—", UIPalette.WaterBar, false);
            SetRadial(energyFill, energyIcon, energyLabel, 0f, "—", UIPalette.EnergyBar, false);
            return;
        }

        SurvivalState state = PlayerSurvival.Instance.State;

        // 【바닥나면 경고색으로 고정된다.】 지금 페널티를 받고 있다는 표시다.
        SetRadial(waterFill, waterIcon, waterLabel, state.WaterRatio,
            Mathf.CeilToInt(state.Water).ToString(), UIPalette.WaterBar,
            state.WaterRatio <= SurvivalTable.WarnRatio);

        SetRadial(energyFill, energyIcon, energyLabel, state.EnergyRatio,
            Mathf.CeilToInt(state.Energy).ToString(), UIPalette.EnergyBar,
            state.EnergyRatio <= SurvivalTable.WarnRatio);
    }

    private static void SetHorizontal(Image fill, Text label, float ratio, string text,
                                      Color normal, bool warn)
    {
        if (fill != null)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            fill.color = warn ? UIPalette.Warning : normal;
            fill.gameObject.SetActive(ratio > 0f);
        }

        if (label != null)
            label.text = text;
    }

    private static void SetRadial(Image fill, Image icon, Text label, float ratio,
                                  string text, Color normal, bool warn)
    {
        Color color = warn ? UIPalette.Warning : normal;

        if (fill != null)
        {
            fill.fillAmount = Mathf.Clamp01(ratio);
            fill.color = color;
        }

        if (icon != null)
            icon.color = color;

        if (label != null)
            label.text = text;
    }

    /// <summary>
    /// 플레이어의 Health. 씬에 놓이므로 찾아서 쓴다.
    /// 한 번 잡으면 들고 있는다 — 매 프레임 찾지 않는다.
    /// </summary>
    private static Health FindPlayerHealth()
    {
        var controller = FindAnyObjectByType<BlobController>(FindObjectsInactive.Exclude);

        return controller != null ? controller.GetComponent<Health>() : null;
    }
}
