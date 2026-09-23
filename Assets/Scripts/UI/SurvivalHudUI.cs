using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 좌상단의 체력 · 수분 · 에너지. (docs/Blob_Survival_System.md 7절)
///
/// 【덕코프와 같은 모양으로 둔다.】
///   하트 + 긴 막대 하나 · 그 오른쪽에 물방울 · 번개 원형 게이지 둘.
///
/// 처음에는 가로 막대 셋을 세로로 쌓고 왼쪽에 「체력 / 수분 / 에너지」를
/// 글자로 적었다. 좁은 폭에서 글자가 두 줄로 접혔고, 그 자리는 막대가
/// 써야 할 자리였다. **마커는 도형으로 둔다** — 한 번 배우면 글자보다 빠르다.
///
/// 【왜 좌상단인가】 처음에는 덕코프를 따라 좌하단에 뒀다. 모바일에서는
/// 왼쪽 아래가 이동 조이스틱의 자리라, 엄지와 손바닥이 게이지를 통째로
/// 가렸다. 정작 급할 때 체력이 안 보였다. 위쪽 두 구석 중 오른쪽은
/// 장비·스킬·패시브 버튼줄이 쓰므로 왼쪽으로 올린다.
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

    // ── 상태이상 줄 ───────────────────────────────────────────────────
    // 게이지 판 **바로 아래로** 쌓는다. 위로 쌓으면 화면 밖으로 나간다 —
    // 판이 이미 화면 맨 위에 붙어 있기 때문이다. 옆에 두면 게이지가 밀린다.

    private const float StatusRowHeight = 30f;
    private const float StatusRowGap = 4f;
    private const float StatusWidth = 176f;

    /// <summary>한 번에 보여 주는 줄 수. 넘치면 오래된 것부터 잘린다.</summary>
    private const int MaxStatusRows = 6;

    private RectTransform statusColumn;

    private readonly List<StatusRow> statusRows = new();

    /// <summary>줄 하나를 이루는 조각들. 매 프레임 만들지 않고 재사용한다.</summary>
    private readonly struct StatusRow
    {
        public readonly GameObject Root;
        public readonly Image Back;
        public readonly Image Fill;
        public readonly Text Label;

        public StatusRow(GameObject root, Image back, Image fill, Text label)
        {
            Root = root;
            Back = back;
            Fill = fill;
            Label = label;
        }
    }

    /// <summary>가방·전리품 화면이 열려 있으면 감춘다.</summary>
    private bool hiddenByScreen;

    /// <summary>StatusEffectType의 개수. None을 뺀 나머지를 훑는다.</summary>
    private static readonly int StatusEffectCount =
        System.Enum.GetValues(typeof(StatusEffectType)).Length;

    /// <summary>이번 프레임에 보일 상태. 매 프레임 새로 만들지 않는다.</summary>
    private readonly List<StatusEffectType> visible = new();

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
    /// 가방 화면은 판을 화면 전체에 깔고 자기 수치(소지 중량 · 크레딧)를
    /// 띄운다. 그 위에 생존 게이지가 겹쳐 있으면 어느 막대가 무엇인지
    /// 읽을 수 없다. 덕코프도 가방을 열면 생존 게이지를 걷어낸다.
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
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        root.anchoredPosition = new Vector2(Margin, -Margin);

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

        BuildStatusColumn();
    }

    /// <summary>
    /// 상태이상 줄이 쌓이는 자리. 게이지 판 **바로 아래**에서 아래로 자란다.
    ///
    /// 【줄을 미리 만들어 두고 켰다 끈다.】 매 프레임 Destroy/Instantiate를
    /// 하면 상태가 자주 바뀌는 교전 중에 쓰레기가 쏟아진다.
    /// </summary>
    private void BuildStatusColumn()
    {
        GameObject columnObject = UIFactory.CreateChild("Status", root);

        // 판의 왼쪽 **아래** 모서리에 걸고, 위쪽을 기준점으로 삼아 아래로 자란다.
        statusColumn = columnObject.GetComponent<RectTransform>();
        statusColumn.anchorMin = new Vector2(0f, 0f);
        statusColumn.anchorMax = new Vector2(0f, 0f);
        statusColumn.pivot = new Vector2(0f, 1f);
        statusColumn.sizeDelta = new Vector2(StatusWidth, 0f);
        statusColumn.anchoredPosition = new Vector2(0f, -6f);

        for (int i = 0; i < MaxStatusRows; i++)
            statusRows.Add(BuildStatusRow(i));
    }

    private StatusRow BuildStatusRow(int index)
    {
        GameObject rowObject = UIFactory.CreateChild($"Status_{index}", statusColumn);

        var rect = rowObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, StatusRowHeight);
        rect.anchoredPosition = new Vector2(0f, -index * (StatusRowHeight + StatusRowGap));

        var back = rowObject.AddComponent<Image>();
        back.color = UIPalette.Inset;
        back.sprite = UISprites.Rounded(UIFactory.Radius);
        back.type = Image.Type.Sliced;
        back.raycastTarget = false;

        // 남은 시간은 채움으로 보여 준다. 숫자만 있으면 「곧 풀리는지」를
        // 읽어야 알 수 있는데, 교전 중에는 읽을 겨를이 없다.
        Image fill = UIFactory.CreatePanel("Fill", rowObject.transform,
            UIPalette.Text, Vector2.zero, Vector2.one, UIFactory.Radius);

        fill.raycastTarget = false;

        Text label = UIFactory.CreateLabel(rowObject.transform, string.Empty, 19,
            FontStyle.Bold, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        rowObject.SetActive(false);

        return new StatusRow(rowObject, back, fill, label);
    }

    /// <summary>마커 하나. 가로 위치는 픽셀, 세로는 가운데 정렬이다.</summary>
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

        // 마커는 고리 안쪽 가운데.
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

        // 숫자는 아래쪽에 작게. 고리와 마커를 가리지 않는다.
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
        RefreshStatus();
    }

    /// <summary>
    /// 걸려 있는 상태이상을 줄로 세운다.
    ///
    /// 【위험 상태를 맨 위로 올린다.】 동결·마비는 지금 못 움직인다는 뜻이고,
    /// 부식은 회복약이 반만 듣는다는 뜻이다. 중독 3중첩보다 먼저 보여야 한다.
    /// </summary>
    private void RefreshStatus()
    {
        if (health == null)
        {
            HideStatusFrom(0);
            return;
        }

        StatusEffectState state = health.Status;

        visible.Clear();

        for (int i = 1; i < StatusEffectCount; i++)
        {
            var type = (StatusEffectType)i;

            if (state.Has(type))
                visible.Add(type);
        }

        // 위험 상태 먼저, 그다음 남은 시간이 짧은 것 먼저 —
        // 곧 풀릴 것이 위에 있어야 「기다릴까 약을 쓸까」를 정할 수 있다.
        visible.Sort((a, b) =>
        {
            bool criticalA = StatusEffectNames.IsCritical(a);
            bool criticalB = StatusEffectNames.IsCritical(b);

            if (criticalA != criticalB)
                return criticalA ? -1 : 1;

            return state.RemainingOf(a).CompareTo(state.RemainingOf(b));
        });

        int count = Mathf.Min(visible.Count, MaxStatusRows);

        for (int i = 0; i < count; i++)
        {
            StatusEffectType type = visible[i];

            StatusRow row = statusRows[i];

            row.Root.SetActive(true);

            Color color = UIPalette.ForStatus(type);

            int stacks = state.StacksOf(type);
            float remaining = state.RemainingOf(type);

            row.Label.text = stacks > 1
                ? $"{StatusEffectNames.Of(type)} {stacks}    {remaining:0.0}s"
                : $"{StatusEffectNames.Of(type)}    {remaining:0.0}s";

            // 채움은 지속시간 기준으로 줄어든다. 갱신되면 다시 찬다.
            float duration = Mathf.Max(0.01f, StatusEffectTable.Get(type).Duration);

            row.Fill.rectTransform.anchorMax =
                new Vector2(Mathf.Clamp01(remaining / duration), 1f);

            // 위험 상태는 바탕까지 물들인다. 줄 하나가 아니라 띠로 보여야 한다.
            row.Fill.color = UIPalette.Glassify(color, 0.55f);
            row.Back.color = StatusEffectNames.IsCritical(type)
                ? UIPalette.Glassify(color, 0.22f)
                : UIPalette.Inset;
        }

        HideStatusFrom(count);
    }

    private void HideStatusFrom(int index)
    {
        for (int i = index; i < statusRows.Count; i++)
        {
            if (statusRows[i].Root.activeSelf)
                statusRows[i].Root.SetActive(false);
        }
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
