using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 좌하단의 체력 · 수분 · 에너지. (docs/Blob_Survival_System.md 7절)
///
/// 【셋을 붙여 둔다.】 떨어뜨려 놓으면 「지금 위험한 게 어느 쪽인가」를
/// 두 군데를 봐야 안다. 덕코프도 같은 자리에 한 덩어리로 둔다.
///
/// 체력은 크게, 수분·에너지는 그 아래 작게 — 순서가 곧 급한 순서다.
/// 체력은 순식간에 0이 되고 수분·에너지는 십수 분에 걸쳐 준다.
/// </summary>
public class SurvivalHudUI : MonoBehaviour
{
    private static SurvivalHudUI instance;

    // ── 치수 (캔버스 픽셀) ────────────────────────────────────────────

    private const float PanelWidth = 360f;
    private const float PanelHeight = 108f;
    private const float Margin = 14f;

    private const float HealthBarHeight = 36f;
    private const float SmallBarHeight = 22f;
    private const float BarGap = 8f;

    /// <summary>막대 왼쪽의 표식 자리. 「체력」·물방울·번개가 들어간다.</summary>
    private const float MarkWidth = 34f;

    private RectTransform root;

    private Image healthFill;
    private Image waterFill;
    private Image energyFill;

    private Text healthLabel;
    private Text waterLabel;
    private Text energyLabel;

    private Health health;

    public static SurvivalHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<SurvivalHudUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        // 가방 화면(1000)보다 아래. 가방을 열면 그 판이 이 위를 덮는다.
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

        // 세로로 세 줄. 위에서부터 체력 · 수분 · 에너지.
        // 정규화 값으로 나누면 세 줄의 높이가 제각각이 되므로 픽셀로 계산한다.
        float total = HealthBarHeight + SmallBarHeight * 2f + BarGap * 2f;

        float healthTop = 1f;
        float healthBottom = healthTop - HealthBarHeight / total;

        float waterTop = healthBottom - BarGap / total;
        float waterBottom = waterTop - SmallBarHeight / total;

        float energyTop = waterBottom - BarGap / total;
        float energyBottom = energyTop - SmallBarHeight / total;

        BuildBar("Health", healthBottom, healthTop, UIPalette.HealthBar, "체력", 22,
            out healthFill, out healthLabel);

        BuildBar("Water", waterBottom, waterTop, UIPalette.WaterBar, "수분", 18,
            out waterFill, out waterLabel);

        BuildBar("Energy", energyBottom, energyTop, UIPalette.EnergyBar, "에너지", 18,
            out energyFill, out energyLabel);
    }

    private void BuildBar(string name, float bottom, float top, Color color,
                          string mark, int fontSize, out Image fill, out Text label)
    {
        RectTransform row = UIFactory.CreateRegion($"{name}Row", root,
            new Vector2(0f, bottom), new Vector2(1f, top));

        // 표식은 왼쪽 고정 폭. 아트가 들어오면 아이콘이 이 자리를 가져간다.
        float markRatio = MarkWidth / PanelWidth;

        UIFactory.CreateLabel(row, mark, fontSize, FontStyle.Bold,
            Vector2.zero, new Vector2(markRatio, 1f), TextAnchor.MiddleLeft,
            UIPalette.TextDim);

        Image track = UIFactory.CreatePanel($"{name}Track", row, UIPalette.Inset,
            new Vector2(markRatio + 0.015f, 0.10f), new Vector2(1f, 0.90f),
            UIFactory.Radius);

        UIFactory.CreateOutline(track, UIPalette.EdgeSoft, UIFactory.Radius, 2);

        // 채움은 트랙 안에서 왼쪽에 붙어 오른쪽 끝(anchorMax.x)만 움직인다.
        fill = UIFactory.CreatePanel($"{name}Fill", track.transform, color,
            Vector2.zero, Vector2.one, UIFactory.Radius);

        UIFactory.Inset(fill.rectTransform, 3f);

        label = UIFactory.CreateLabel(track.transform, string.Empty,
            Mathf.Max(15, fontSize - 3), FontStyle.Bold,
            new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), TextAnchor.MiddleRight,
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
        if (root == null)
            return;

        RefreshHealth();
        RefreshSurvival();
    }

    private void RefreshHealth()
    {
        if (health == null || health.gameObject == null)
            health = FindPlayerHealth();

        if (health == null)
        {
            SetBar(healthFill, healthLabel, 0f, "—", UIPalette.HealthBar, false);
            return;
        }

        float ratio = health.Normalized;

        SetBar(healthFill, healthLabel, ratio, $"{health.Current} / {health.Max}",
            UIPalette.HealthBar, ratio <= SurvivalTable.HealthWarnRatio);
    }

    private void RefreshSurvival()
    {
        if (!PlayerSurvival.HasInstance)
        {
            SetBar(waterFill, waterLabel, 0f, "—", UIPalette.WaterBar, false);
            SetBar(energyFill, energyLabel, 0f, "—", UIPalette.EnergyBar, false);
            return;
        }

        SurvivalState state = PlayerSurvival.Instance.State;

        // 【바닥나면 경고색으로 고정된다.】 지금 페널티를 받고 있다는 표시다.
        SetBar(waterFill, waterLabel, state.WaterRatio,
            Mathf.CeilToInt(state.Water).ToString(), UIPalette.WaterBar,
            state.WaterRatio <= SurvivalTable.WarnRatio);

        SetBar(energyFill, energyLabel, state.EnergyRatio,
            Mathf.CeilToInt(state.Energy).ToString(), UIPalette.EnergyBar,
            state.EnergyRatio <= SurvivalTable.WarnRatio);
    }

    private static void SetBar(Image fill, Text label, float ratio, string text,
                               Color normal, bool warn)
    {
        if (fill != null)
        {
            RectTransform rect = fill.rectTransform;

            rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);

            fill.color = warn ? UIPalette.Warning : normal;

            // 0이면 채움이 없어도 자리는 남는다. 색만으로 상태를 말하게 둔다.
            fill.gameObject.SetActive(ratio > 0f);
        }

        if (label != null)
            label.text = text;
    }

    /// <summary>
    /// 플레이어의 Health. 씬에 프리팹으로 놓이므로 찾아서 쓴다.
    /// 매 프레임 찾지 않도록 한 번 잡으면 들고 있는다.
    /// </summary>
    private static Health FindPlayerHealth()
    {
        var controller = FindAnyObjectByType<BlobController>(FindObjectsInactive.Exclude);

        return controller != null ? controller.GetComponent<Health>() : null;
    }
}
