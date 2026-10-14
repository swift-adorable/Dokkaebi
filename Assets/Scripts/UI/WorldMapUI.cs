using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 【전체 지도】 (결정 2-90 · 2-94) — 미니맵을 누르거나 M. 떠 있는 동안 게임이 멈춘다(결정 2-92).
///
/// 【모양 — 2026-10-13 사용자 참고 그림 (결정 2-121)】 장면을 위에서 찍은 지도가 흐린 게임 화면 위에 떠 있다.
///   · 그림: 구운 지도 사진(MapPhotoBaker) — 없으면 양피지. 가장자리는 번져 사라진다(UISoftEdge) · 뒤는 흐린 게임 화면(ScreenBlur)
///   · 위 가운데: 큰 제목(장소) · 그 위 작은 「n장」 · 아래 목표 한 줄 / 왼쪽 위 ‹ · 오른쪽 위 표시 수 · ✕
///   · 왼쪽 아래 「지도 표시」 판: 거르기(길목 · 마커 · 퀘스트 · 전리품 · 쓰러진 자리) · 목표로 · 마커 목록 · 도움말 한 줄
///   · 오른쪽: 진행 중 퀘스트(위) · 나침반 · 달 · 날씨 · 확대 막대(아래)
///   · 지도 위: 가 본 구역의 이름(흰 글씨 · 검은 테) · 아이콘 아래 이름표 · 내 자리는 파란 동그라미 + 「나」 · 퀘스트 구역은 테두리 + 「!」
/// 【확대 · 이동】 배율 1(장 하나가 다 보인다) ~ 4 · 열면 배율 1 (결정 2-122) · 끌어서 이동 · 휠 · 두 손가락 확대.
/// 【보이는 것】 내 자리 · 바라보는 쪽 · 찾은 길목 · 금줄 · 퀘스트 구역 · 내 마커 · 쓰러진 자리 · (옛 지도) 전리품.
///   구역 이름은 【가 본 구역만】 적는다(사용자 2026-10-13 — 2026-10-08의 「적지 않는다」를 바꿨다) · 가 보지 않은 땅은 어둡다 ·
///   적 · 보스 · 조각은 그리지 않는다 (Map_System 4절)
/// 【마커】 두 번 누르면 찍는다(장마다 5개) · 마커를 두 번 누르면 지운다 — 지도 위에만 있다.
/// 【진행 중 퀘스트】 (결정 2-120) 오른쪽 위 목록 — 퀘스트마다 다음 목표 한 줄, 누르면 그 구역으로 지도가 옮겨 간다.
///   「퀘스트」 거르기를 끄면 목록도 감춘다.
/// </summary>
public class WorldMapUI : MonoBehaviour
{
    private static WorldMapUI instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    // ── 빛깔 [임시 — 아트 때] ─────────────────────────────────────────
    private static readonly Color Backdrop = new(0.10f, 0.085f, 0.065f, 0.985f);
    private static readonly Color BarColor = new(0.07f, 0.06f, 0.045f, 0.86f);
    private static readonly Color ButtonOn = new(0.40f, 0.32f, 0.22f, 0.95f);
    private static readonly Color ButtonOff = new(0.15f, 0.125f, 0.095f, 0.92f);
    private static readonly Color Ink = new(0.96f, 0.92f, 0.82f, 1f);
    private static readonly Color InkDim = new(0.72f, 0.65f, 0.53f, 1f);
    private static readonly Color QuestColor = new(0.42f, 0.86f, 0.86f, 1f);
    private static readonly Color FogTint = new(0.08f, 0.10f, 0.12f, 0.58f);
    private static readonly Color BlurTint = new(0.55f, 0.55f, 0.58f, 1f);
    private static readonly Color PlayerBlue = new(0.30f, 0.56f, 0.95f, 1f);

    /// <summary>위 제목 칸의 높이 — 진행 중 퀘스트 목록이 그 아래에서 시작한다.</summary>
    private const float TopBarHeight = 132f;
    private const float TitleHeight = 150f;

    // 왼쪽 아래 「지도 표시」 판
    private const float LegendPad = 14f;
    private const float LegendHeight = 140f;
    private const float ToolSize = 84f;
    private const float ToolGap = 8f;

    private const float CompassSize = 96f;

    /// <summary>구역 이름을 구역 가운데에서 위로 띄우는 높이(화소).</summary>
    private const float ZoneLabelLift = 44f;

    /// <summary>사진 · 덮개 · 안개 가장자리가 번지는 폭 (그림 한 변의 비율).</summary>
    private const float EdgeFeather = 0.07f;

    /// <summary>표시 거르기 — 켜고 끈 것은 이 실행 동안 남는다.</summary>
    private enum Layer { Extraction = 0, Marker = 1, Quest = 2, Loot = 3, Fallen = 4 }

    private static readonly bool[] shown = { true, true, true, true, true };

    private static bool Shown(Layer layer) => shown[(int)layer];

    // ── 만든 것 ───────────────────────────────────────────────────────
    private Canvas canvas;
    private GameObject root;
    private RectTransform viewport;
    private RectTransform content;
    private RawImage mapImage;
    private RawImage fogImage;
    private RawImage closedImage;
    private RawImage blurImage;
    private RenderTexture blurTexture;
    private RectTransform zoneLabelLayer;
    private readonly List<Text> zoneLabels = new();
    private RectTransform legend;
    private RectTransform outlineLayer;
    private RectTransform iconLayer;
    private RectTransform playerMarker;
    private RectTransform playerHeading;
    private Text chapterLabel;
    private Text titleLabel;
    private Text objectiveLabel;
    private Text markerCount;
    private Text moonLabel;
    private Text weatherLabel;
    private Slider zoomSlider;
    private GameObject markerList;
    private RectTransform markerListRows;
    private RectTransform questPanel;
    private RectTransform questRows;
    private bool hasQuestRows;
    private readonly Image[] filterButtons = new Image[5];
    private readonly List<MapIcon> icons = new();
    private readonly List<GameObject> builtForMap = new();
    private MapRuntime drawnFor;

    // ── 보는 자리 ─────────────────────────────────────────────────────
    private float zoom = MapView.OpenZoom;
    private Vector2 center = new(0.5f, 0.5f);

    // 【매 프레임 다시 그리지 않는다】 (결정 2-100) — 지도가 떠 있는 동안 게임은 멈춰 있다(2-92).
    // 끌거나 확대할 때만 바로, 그 밖에는 0.25초에 한 번. 퀘스트 구역은 열 때 · 다시 그릴 때만 센다.
    private const float IdleRefresh = 0.25f;
    private Vector2 lastCenter = new(-1f, -1f);
    private float lastZoom = -1f;
    private float nextRefresh;
    private List<ZoneArea> questCache;
    private float aspect = 1f;

    private float lastTapTime = -10f;
    private Vector2 lastTapPosition;
    private bool draggedSinceDown;
    private float pinchDistance;

    public static void Open()
    {
        if (MapRuntime.Current == null)
            return;

        EnsureInstance().Show();
    }

    public static void Close()
    {
        if (instance != null)
            instance.Hide();
    }

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private static WorldMapUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        // 가방 화면(1000) 위.
        Canvas canvas = UIFactory.CreateCanvas("WorldMapCanvas (Runtime)", 1050);
        instance = canvas.gameObject.AddComponent<WorldMapUI>();
        instance.Build(canvas);
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        MapMemory.MarkersChanged -= OnMarkersChanged;
    }

    // ── 만들기 ────────────────────────────────────────────────────────

    private void Build(Canvas host)
    {
        canvas = host;

        // 화면 끝까지 덮는다(노치 밖도). 글자 · 단추는 안전 영역 안에.
        // 맨 뒤는 어두운 판 — 그 위에 흐린 게임 화면(ScreenBlur)이 찍히면 깔린다.
        Image dim = UIFactory.CreatePanel("Dim", canvas.transform, Backdrop, Vector2.zero, Vector2.one, 0);
        dim.raycastTarget = true;   // 지도 밖을 눌러도 조준이 돌지 않게
        root = dim.gameObject;
        GamePause.Register(root);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)

        blurImage = CreateRaw("Blur", root.transform);
        blurImage.color = BlurTint;
        blurImage.enabled = false;

        RectTransform safe = UIFactory.CreateRegion("SafeArea", root.transform, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        BuildViewport(safe);
        BuildTitle(safe);
        BuildLegend(safe);
        BuildRightColumn(safe);
        BuildMarkerList(safe);
        BuildQuestList(safe);

        MapMemory.MarkersChanged += OnMarkersChanged;
        root.SetActive(false);
    }

    /// <summary>글자를 지도 위에서 읽히게 — 검은 테 + 아래 그림자 (참고 그림의 흰 글씨).</summary>
    private static Text Inked(Text label, float outline = 2f)
    {
        var o = label.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(outline, -outline);
        var s = label.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.55f);
        s.effectDistance = new Vector2(0f, -outline * 2f);
        return label;
    }

    private void BuildViewport(RectTransform safe)
    {
        // 【화면 전체가 지도다】 — 위 · 왼쪽 띠를 걷었다. 단추들은 지도 위에 떠 있다.
        viewport = UIFactory.CreateRegion("Viewport", safe, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();

        Image catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0.01f);
        catcher.raycastTarget = true;
        viewport.gameObject.AddComponent<MapInput>().owner = this;

        content = UIFactory.CreateRegion("Content", viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        content.pivot = new Vector2(0.5f, 0.5f);

        // 사진 · 닫힌 곳 덮개 · 안개 — 셋 다 가장자리가 번진다 (UISoftEdge).
        Material soft = ScreenBlur.SoftEdge(EdgeFeather);
        mapImage = CreateRaw("MapImage", content);
        closedImage = CreateRaw("ClosedOverlay", content);
        fogImage = CreateRaw("FogImage", content);
        fogImage.color = FogTint;
        mapImage.material = soft;
        closedImage.material = soft;
        fogImage.material = soft;

        outlineLayer = UIFactory.CreateRegion("QuestOutlines", content, Vector2.zero, Vector2.one);

        RawImage vignette = CreateRaw("Vignette", viewport);
        vignette.texture = VignetteTexture();
        vignette.color = new Color(0f, 0f, 0f, 0.55f);

        // 구역 이름이 아이콘보다 아래 — 아이콘의 이름표를 가리지 않게.
        zoneLabelLayer = UIFactory.CreateRegion("ZoneNames", viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        iconLayer = UIFactory.CreateRegion("Icons", viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        BuildPlayerMarker();
    }

    /// <summary>내 자리 — 파란 동그라미(흰 테) · 바라보는 쪽으로 도는 작은 세모 · 아래 「나」 (참고 그림의 「자신 / 나」).</summary>
    private void BuildPlayerMarker()
    {
        playerMarker = UIFactory.CreateRegion("Player", iconLayer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        playerMarker.sizeDelta = new Vector2(44f, 44f);

        playerHeading = UIFactory.CreateRegion("Heading", playerMarker, Vector2.zero, Vector2.one);
        Image tip = UIFactory.CreatePanel("Tip", playerHeading, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0);
        tip.sprite = UISprites.Of(UISprites.Glyph.Triangle);
        tip.rectTransform.sizeDelta = new Vector2(20f, 16f);
        tip.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        tip.raycastTarget = false;

        Image ring = UIFactory.CreatePanel("Ring", playerMarker, Color.white, Vector2.zero, Vector2.one, 0);
        ring.sprite = UISprites.Of(UISprites.Glyph.Circle);
        ring.raycastTarget = false;
        ring.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

        Image dot = UIFactory.CreatePanel("Dot", playerMarker, PlayerBlue, Vector2.zero, Vector2.one, 0);
        dot.sprite = UISprites.Of(UISprites.Glyph.Circle);
        dot.rectTransform.offsetMin = new Vector2(4f, 4f);
        dot.rectTransform.offsetMax = new Vector2(-4f, -4f);
        dot.raycastTarget = false;

        Text me = Inked(UIFactory.CreateLabel(playerMarker, "나", 22, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), TextAnchor.UpperCenter, Color.white));
        me.rectTransform.pivot = new Vector2(0.5f, 1f);
        me.rectTransform.sizeDelta = new Vector2(80f, 28f);
        me.rectTransform.anchoredPosition = new Vector2(0f, -4f);
    }

    /// <summary>위 — 가운데 큰 제목(장소) · 그 위 「n장」 · 아래 목표 한 줄 / 왼쪽 ‹ · 오른쪽 표시 수 · ✕.</summary>
    private void BuildTitle(RectTransform safe)
    {
        RectTransform bar = UIFactory.CreateRegion("Title", safe, new Vector2(0f, 1f), new Vector2(1f, 1f));
        bar.offsetMin = new Vector2(0f, -TitleHeight);

        // 위쪽만 살짝 어둡게 — 띠가 아니라 그늘 (제목이 지도 위에서도 읽히게).
        RawImage shade = CreateRaw("Shade", bar);
        shade.texture = TopShadeTexture();
        shade.color = new Color(0f, 0f, 0f, 0.6f);

        chapterLabel = Inked(UIFactory.CreateLabel(bar, "", 24, FontStyle.Bold,
            new Vector2(0.2f, 0.70f), new Vector2(0.8f, 0.94f), TextAnchor.MiddleCenter, InkDim));

        titleLabel = Inked(UIFactory.CreateLabel(bar, "", 58, FontStyle.Bold,
            new Vector2(0.15f, 0.26f), new Vector2(0.85f, 0.74f), TextAnchor.MiddleCenter, Color.white), 3f);
        titleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

        objectiveLabel = Inked(UIFactory.CreateLabel(bar, "", 22, FontStyle.Bold,
            new Vector2(0.15f, 0f), new Vector2(0.85f, 0.26f), TextAnchor.MiddleCenter, QuestColor));

        Button backButton = RoundButton(bar, "‹", new Vector2(0f, 1f), new Vector2(18f, -18f), Hide, 44);
        backButton.name = "Back";

        Button close = RoundButton(bar, "✕", new Vector2(1f, 1f), new Vector2(-18f, -18f), Hide, 34);
        close.name = "Close";

        markerCount = Inked(UIFactory.CreateLabel(bar, "", 22, FontStyle.Bold,
            new Vector2(1f, 1f), new Vector2(1f, 1f), TextAnchor.MiddleRight, Ink));
        markerCount.rectTransform.pivot = new Vector2(1f, 1f);
        markerCount.rectTransform.sizeDelta = new Vector2(200f, 72f);
        markerCount.rectTransform.anchoredPosition = new Vector2(-108f, -18f);
    }

    /// <summary>둥근 단추 하나 — 지도 위에 떠 있는 ‹ · ✕.</summary>
    private static Button RoundButton(Transform parent, string text, Vector2 anchor, Vector2 position,
                                      UnityEngine.Events.UnityAction action, int fontSize)
    {
        Button button = UIFactory.CreateButton(parent, text, anchor, anchor, ButtonOff, action, fontSize, 36);
        var rect = (RectTransform)button.transform;
        rect.pivot = anchor;
        rect.sizeDelta = new Vector2(72f, 72f);
        rect.anchoredPosition = position;
        return button;
    }

    /// <summary>
    /// 왼쪽 아래 「지도 표시」 판 (참고 그림) — 거르기 다섯 · 목표로 · 마커 목록을 한 줄로, 위에 이름과 도움말 한 줄.
    /// </summary>
    private void BuildLegend(RectTransform safe)
    {
        const int tools = 7;
        float width = LegendPad * 2f + tools * ToolSize + (tools - 1) * ToolGap;

        Image panel = UIFactory.CreatePanel("Legend", safe, BarColor, Vector2.zero, Vector2.zero, UIFactory.RadiusLarge);
        panel.rectTransform.pivot = Vector2.zero;
        panel.rectTransform.sizeDelta = new Vector2(width, LegendHeight);
        panel.rectTransform.anchoredPosition = new Vector2(18f, 18f);
        legend = panel.rectTransform;

        Text title = UIFactory.CreateLabel(panel.transform, "지도 표시", 22, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(0f, 1f), TextAnchor.MiddleLeft, Ink);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(120f, 40f);
        title.rectTransform.anchoredPosition = new Vector2(LegendPad, -4f);

        Text hint = UIFactory.CreateLabel(panel.transform, "두 번 눌러 표시 · 표시를 두 번 누르면 지운다 · 끌어서 이동", 16,
            FontStyle.Normal, new Vector2(0f, 1f), new Vector2(1f, 1f), TextAnchor.MiddleLeft, InkDim);
        hint.rectTransform.pivot = new Vector2(0f, 1f);
        hint.rectTransform.offsetMin = new Vector2(LegendPad + 116f, -44f);
        hint.rectTransform.offsetMax = new Vector2(-LegendPad, -4f);

        float x = LegendPad;
        AddFilter(panel.transform, Layer.Extraction, UISprites.Glyph.Circle, "", ExtractionDirector.Name, MinimapUI.ExtractionColor, ref x);
        AddFilter(panel.transform, Layer.Marker, UISprites.Glyph.Diamond, "", "마커", MinimapUI.MarkerColor, ref x);
        AddFilter(panel.transform, Layer.Quest, UISprites.Glyph.Diamond, "!", "퀘스트", QuestColor, ref x);
        AddFilter(panel.transform, Layer.Loot, UISprites.Glyph.Square, "", "전리품", MapLootMarks.BundleColor, ref x);
        AddFilter(panel.transform, Layer.Fallen, UISprites.Glyph.Circle, "", "쓰러진 자리", MapLootMarks.FallenColor, ref x);
        AddTool(panel.transform, UISprites.Glyph.Triangle, "", "목표로", QuestColor, GoToObjective, ref x);
        AddTool(panel.transform, UISprites.Glyph.Square, "", "마커 목록", MinimapUI.MarkerColor, ToggleMarkerList, ref x);
    }

    private void AddFilter(Transform bar, Layer layer, UISprites.Glyph shape, string inner, string label, Color color, ref float x)
    {
        Layer captured = layer;
        Image button = AddTool(bar, shape, inner, label, color, () =>
        {
            shown[(int)captured] = !shown[(int)captured];
            Refresh();
        }, ref x);

        filterButtons[(int)layer] = button;
    }

    private Image AddTool(Transform bar, UISprites.Glyph shape, string inner, string label, Color color,
                          UnityEngine.Events.UnityAction action, ref float x)
    {
        Image image = UIFactory.CreatePanel($"Tool_{label}", bar, ButtonOff, Vector2.zero, Vector2.zero, UIFactory.Radius);
        image.rectTransform.pivot = Vector2.zero;
        image.rectTransform.sizeDelta = new Vector2(ToolSize, ToolSize);
        image.rectTransform.anchoredPosition = new Vector2(x, LegendPad);
        x += ToolSize + ToolGap;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        Image icon = UIFactory.CreatePanel("Shape", image.transform, color,
            new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), 0);
        icon.sprite = UISprites.Of(shape);
        icon.rectTransform.sizeDelta = new Vector2(32f, 32f);
        icon.raycastTarget = false;
        icon.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

        if (!string.IsNullOrEmpty(inner))
        {
            Text t = UIFactory.CreateLabel(icon.transform, inner, 20, FontStyle.Bold,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, new Color(0.08f, 0.07f, 0.05f, 1f));
            t.raycastTarget = false;
        }

        Text l = UIFactory.CreateLabel(image.transform, label, 16, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(1f, 0.36f), TextAnchor.MiddleCenter, Ink);
        l.raycastTarget = false;
        l.horizontalOverflow = HorizontalWrapMode.Overflow;

        return image;
    }

    /// <summary>오른쪽 아래 — 나침반(북쪽이 늘 위) · 달 · 날씨 · 확대 막대 · 내 자리.</summary>
    private void BuildRightColumn(RectTransform safe)
    {
        // 확대 막대 — 오른쪽 아래.
        RectTransform bar = UIFactory.CreateRegion("ZoomBar", safe, new Vector2(1f, 0f), new Vector2(1f, 0f));
        bar.pivot = new Vector2(1f, 0f);
        bar.sizeDelta = new Vector2(560f, 80f);
        bar.anchoredPosition = new Vector2(-18f, 18f);

        Image back = bar.gameObject.AddComponent<Image>();
        back.sprite = UISprites.Rounded(UIFactory.RadiusLarge);
        back.type = Image.Type.Sliced;
        back.color = BarColor;

        Button minus = UIFactory.CreateButton(bar, "−", new Vector2(0f, 0f), new Vector2(0f, 1f), ButtonOff,
            () => ZoomBy(-MapView.ZoomStep), 36);
        Inset((RectTransform)minus.transform, 10f, 74f);

        zoomSlider = UIFactory.CreateIntSlider(bar, new Vector2(0f, 0f), new Vector2(1f, 1f), 0, 100, 33);
        var sliderRect = (RectTransform)zoomSlider.transform;
        sliderRect.offsetMin = new Vector2(88f, 12f);
        sliderRect.offsetMax = new Vector2(-196f, -12f);
        Tint(zoomSlider);
        zoomSlider.onValueChanged.AddListener(v =>
            SetZoom(Mathf.Lerp(MapView.MinZoom, MapView.MaxZoom, v / 100f), Vector2.zero));

        Button plus = UIFactory.CreateButton(bar, "+", new Vector2(1f, 0f), new Vector2(1f, 1f), ButtonOff,
            () => ZoomBy(MapView.ZoomStep), 36);
        var plusRect = (RectTransform)plus.transform;
        plusRect.offsetMin = new Vector2(-180f, 10f);
        plusRect.offsetMax = new Vector2(-116f, -10f);

        Button me = UIFactory.CreateButton(bar, "내 자리", new Vector2(1f, 0f), new Vector2(1f, 1f), ButtonOn,
            CenterOnPlayer, 22);
        var meRect = (RectTransform)me.transform;
        meRect.offsetMin = new Vector2(-106f, 10f);
        meRect.offsetMax = new Vector2(-10f, -10f);

        // 나침반 — 확대 막대 위 오른쪽. 지도는 늘 북쪽이 위라 바늘이 돌지 않는다.
        RectTransform compass = UIFactory.CreateRegion("Compass", safe, new Vector2(1f, 0f), new Vector2(1f, 0f));
        compass.pivot = new Vector2(1f, 0f);
        compass.sizeDelta = new Vector2(CompassSize, CompassSize);
        compass.anchoredPosition = new Vector2(-30f, 118f);

        Image ring = UIFactory.CreatePanel("Ring", compass, new Color(1f, 1f, 1f, 0.85f), Vector2.zero, Vector2.one, 0);
        ring.sprite = UISprites.RoundedOutline(Mathf.RoundToInt(CompassSize * 0.5f), 3);
        ring.type = Image.Type.Sliced;
        ring.raycastTarget = false;

        Image needle = UIFactory.CreatePanel("North", compass, new Color(0.92f, 0.25f, 0.22f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0);
        needle.sprite = UISprites.Of(UISprites.Glyph.Triangle);
        needle.rectTransform.sizeDelta = new Vector2(22f, 22f);
        needle.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        needle.raycastTarget = false;

        Image hub = UIFactory.CreatePanel("Hub", compass, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0);
        hub.sprite = UISprites.Of(UISprites.Glyph.Circle);
        hub.rectTransform.sizeDelta = new Vector2(12f, 12f);
        hub.raycastTarget = false;

        Text north = Inked(UIFactory.CreateLabel(compass, "북", 18, FontStyle.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), TextAnchor.LowerCenter, Ink));
        north.rectTransform.pivot = new Vector2(0.5f, 0f);
        north.rectTransform.sizeDelta = new Vector2(40f, 24f);
        north.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        // 달 · 날씨 — 나침반 왼쪽 (참고 그림의 시간 · 날씨 자리).
        moonLabel = Inked(UIFactory.CreateLabel(safe, "", 22, FontStyle.Bold,
            new Vector2(1f, 0f), new Vector2(1f, 0f), TextAnchor.LowerRight, Ink));
        moonLabel.rectTransform.pivot = new Vector2(1f, 0f);
        moonLabel.rectTransform.sizeDelta = new Vector2(260f, 30f);
        moonLabel.rectTransform.anchoredPosition = new Vector2(-(46f + CompassSize), 166f);

        weatherLabel = Inked(UIFactory.CreateLabel(safe, "", 18, FontStyle.Normal,
            new Vector2(1f, 0f), new Vector2(1f, 0f), TextAnchor.UpperRight, InkDim));
        weatherLabel.rectTransform.pivot = new Vector2(1f, 1f);
        weatherLabel.rectTransform.sizeDelta = new Vector2(260f, 50f);
        weatherLabel.rectTransform.anchoredPosition = new Vector2(-(46f + CompassSize), 164f);
    }

    private static void Inset(RectTransform rect, float pad, float width)
    {
        rect.offsetMin = new Vector2(pad, pad);
        rect.offsetMax = new Vector2(width, -pad);
    }

    private static void Tint(Slider slider)
    {
        if (slider.fillRect != null && slider.fillRect.TryGetComponent(out Image fill))
            fill.color = new Color(0.78f, 0.64f, 0.42f, 1f);

        if (slider.handleRect != null && slider.handleRect.TryGetComponent(out Image handle))
            handle.color = Ink;

        foreach (Image image in slider.GetComponentsInChildren<Image>(true))
            if (image.gameObject.name == "Background")
                image.color = new Color(0.22f, 0.18f, 0.13f, 1f);
    }

    /// <summary>마커 목록 — 「지도 표시」 판 바로 위에 뜬다.</summary>
    private void BuildMarkerList(RectTransform safe)
    {
        Image panel = UIFactory.CreatePanel("MarkerList", safe, BarColor, Vector2.zero, Vector2.zero, UIFactory.RadiusLarge);
        panel.rectTransform.pivot = Vector2.zero;
        panel.rectTransform.sizeDelta = new Vector2(300f, 60f + MapTable.MaxMarkers * 58f);
        panel.rectTransform.anchoredPosition = new Vector2(18f, 18f + LegendHeight + 10f);

        Text title = UIFactory.CreateLabel(panel.transform, "내 마커", 22, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, Ink);
        title.rectTransform.offsetMin = new Vector2(0f, -50f);

        markerListRows = UIFactory.CreateRegion("Rows", panel.transform, Vector2.zero, Vector2.one);
        markerListRows.offsetMax = new Vector2(0f, -54f);

        markerList = panel.gameObject;
        markerList.SetActive(false);
    }

    // ── 진행 중 퀘스트 (결정 2-120) ──────────────────────────────────

    private const float QuestPanelWidth = 400f;
    private const float QuestRowHeight = 84f;
    private const float QuestRowGap = 6f;
    private const int MaxQuestRows = 6;

    private void BuildQuestList(RectTransform safe)
    {
        Image panel = UIFactory.CreatePanel("QuestList", safe, BarColor, new Vector2(1f, 1f), new Vector2(1f, 1f));
        panel.rectTransform.pivot = new Vector2(1f, 1f);
        panel.rectTransform.anchoredPosition = new Vector2(-12f, -(TopBarHeight + 10f));
        panel.rectTransform.sizeDelta = new Vector2(QuestPanelWidth, 60f);

        Text title = UIFactory.CreateLabel(panel.transform, "진행 중 퀘스트", 22, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, Ink);
        title.rectTransform.offsetMin = new Vector2(0f, -48f);

        questRows = UIFactory.CreateRegion("Rows", panel.transform, Vector2.zero, Vector2.one);
        questRows.offsetMax = new Vector2(0f, -50f);

        questPanel = panel.rectTransform;
        questPanel.gameObject.SetActive(false);
    }

    /// <summary>목록을 새로 — 열 때만 (떠 있는 동안 게임이 멈추므로 퀘스트가 바뀌지 않는다).</summary>
    private void FillQuestList(MapRuntime runtime)
    {
        UIFactory.ClearChildren(questRows);

        QuestTracker tracker = StoryManager.Quests;
        List<QuestPlace> places = QuestZones.ActiveList(tracker, runtime.Map.Chapter);
        hasQuestRows = places.Count > 0;

        int shownRows = Mathf.Min(places.Count, MaxQuestRows);
        bool more = places.Count > MaxQuestRows;

        for (int i = 0; i < shownRows; i++)
            AddQuestRow(runtime, tracker, places[i], i);

        if (more)
        {
            Text rest = UIFactory.CreateLabel(questRows, $"외 {places.Count - MaxQuestRows}개 — 퀘스트 창(J)에서", 18,
                FontStyle.Normal, new Vector2(0f, 1f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, InkDim);
            rest.rectTransform.offsetMin = new Vector2(10f, -(shownRows * (QuestRowHeight + QuestRowGap) + 34f));
            rest.rectTransform.offsetMax = new Vector2(-10f, -(shownRows * (QuestRowHeight + QuestRowGap)));
        }

        float height = 56f + shownRows * (QuestRowHeight + QuestRowGap) + (more ? 40f : 4f);
        questPanel.sizeDelta = new Vector2(QuestPanelWidth, height);
    }

    private void AddQuestRow(MapRuntime runtime, QuestTracker tracker, QuestPlace place, int index)
    {
        Image row = UIFactory.CreatePanel($"Quest_{index}", questRows, place.HasPlace ? ButtonOff : new Color(ButtonOff.r, ButtonOff.g, ButtonOff.b, 0.55f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), UIFactory.Radius);
        row.rectTransform.offsetMin = new Vector2(10f, -(index * (QuestRowHeight + QuestRowGap) + QuestRowHeight));
        row.rectTransform.offsetMax = new Vector2(-10f, -(index * (QuestRowHeight + QuestRowGap)));

        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row;
        QuestPlace captured = place;
        button.onClick.AddListener(() => GoToQuest(captured));

        // 갈 곳이 있으면 「!」 마름모 — 지도 위 퀘스트 표시와 같은 모양.
        Image mark = UIFactory.CreatePanel("Mark", row.transform, place.HasPlace ? QuestColor : InkDim,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 0);
        mark.sprite = UISprites.Of(UISprites.Glyph.Diamond);
        mark.rectTransform.sizeDelta = new Vector2(30f, 30f);
        mark.rectTransform.anchoredPosition = new Vector2(26f, 0f);
        mark.raycastTarget = false;
        Text bang = UIFactory.CreateLabel(mark.transform, "!", 18, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, new Color(0.08f, 0.07f, 0.05f, 1f));
        bang.raycastTarget = false;

        Text title = UIFactory.CreateLabel(row.transform, QuestHudText.Title(place.Quest), 20, FontStyle.Bold,
            new Vector2(0f, 0.5f), new Vector2(1f, 1f), TextAnchor.LowerLeft, Ink);
        title.rectTransform.offsetMin = new Vector2(50f, 0f);
        title.rectTransform.offsetMax = new Vector2(-10f, -6f);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;

        string objective = QuestHudText.Objective(tracker, place.Quest);
        Text line = UIFactory.CreateLabel(row.transform,
            place.HasPlace ? objective : $"{objective} · 이 장 밖", 17, FontStyle.Normal,
            new Vector2(0f, 0f), new Vector2(1f, 0.5f), TextAnchor.UpperLeft, place.HasPlace ? QuestColor : InkDim);
        line.rectTransform.offsetMin = new Vector2(50f, 6f);
        line.rectTransform.offsetMax = new Vector2(-10f, 0f);
        line.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    /// <summary>그 퀘스트의 다음 목표 구역으로 지도를 옮긴다. 이 장에 없으면 위 줄에 알린다.</summary>
    private void GoToQuest(QuestPlace place)
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null || place.Quest == null)
            return;

        string objective = QuestHudText.Objective(StoryManager.Quests, place.Quest);

        if (!place.HasPlace)
        {
            objectiveLabel.text = $"{objective} — 이 장에는 갈 곳이 없다";
            return;
        }

        foreach (ZoneArea area in runtime.Map.Areas)
        {
            if (area.ZoneId != place.ZoneId)
                continue;

            shown[(int)Layer.Quest] = true;
            objectiveLabel.text = $"목표 — {objective}";
            FocusOn(new Vector3(area.Bounds.center.x, 0f, area.Bounds.center.y), 2f);
            return;
        }

        objectiveLabel.text = $"{objective} — 이 장에는 갈 곳이 없다";
    }

    private static RawImage CreateRaw(string name, Transform parent)
    {
        RectTransform rect = UIFactory.CreateRegion(name, parent, Vector2.zero, Vector2.one);
        var raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    // ── 열고 닫기 ─────────────────────────────────────────────────────

    /// <summary>흐린 뒤 화면을 찍는 중 — 그동안 또 열지 않는다.</summary>
    private bool opening;

    private void Show()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null || opening)
            return;

        // HUD를 먼저 감추고, 이번 프레임 끝에서 화면을 찍어 흐리게 깐 다음 연다 (결정 2-121).
        opening = true;
        SetHudHidden(true);
        StartCoroutine(ScreenBlur.Capture(shot =>
        {
            opening = false;
            ScreenBlur.Release(ref blurTexture);
            blurTexture = shot;
            blurImage.texture = shot;
            blurImage.enabled = shot != null;

            if (MapRuntime.Current == runtime)
                ShowNow(runtime);
            else
                SetHudHidden(false);
        }));
    }

    private void ShowNow(MapRuntime runtime)
    {
        if (drawnFor != runtime)
            BuildForMap(runtime);

        ApplyPicture(runtime);
        fogImage.texture = runtime.FogTexture;
        questCache = null;

        root.SetActive(true);
        markerList.SetActive(false);
        FillQuestList(runtime);

        // 열면 장 하나가 다 보인다 (결정 2-122 — 참고 그림). 확대하면 내 자리 쪽에서 시작한다.
        zoom = MapView.OpenZoom;
        center = runtime.Player != null ? runtime.UvOf(runtime.Player.position) : new Vector2(0.5f, 0.5f);
        objectiveLabel.text = string.Empty;
        Canvas.ForceUpdateCanvases();
        ApplyView();
        Refresh();
    }

    private void Hide()
    {
        if (root == null || !root.activeSelf)
            return;

        root.SetActive(false);
        SetHudHidden(false);
        blurImage.texture = null;
        blurImage.enabled = false;
        ScreenBlur.Release(ref blurTexture);
    }

    /// <summary>지도 그림 — 사진이 있으면 사진 + 닫힌 곳 덮개, 없으면 양피지(덮개 없이 — 양피지가 이미 어둡게 그린다).</summary>
    private void ApplyPicture(MapRuntime runtime)
    {
        mapImage.texture = runtime.MapPicture;
        closedImage.texture = runtime.EnsureClosedOverlay();
        closedImage.enabled = closedImage.texture != null;
    }

    private static void SetHudHidden(bool hidden)
    {
        SurvivalHudUI.SetHiddenByScreen(hidden);
        QuestHudUI.SetHiddenByScreen(hidden);
        ExtractionHudUI.SetHiddenByScreen(hidden);
        AmmoHudUI.SetHiddenByScreen(hidden);
        ReloadButtonUI.SetHiddenByScreen(hidden);
        InventoryScreenUI.SetHudSuppressed(hidden);
    }

    /// <summary>장 맵이 바뀌었을 때 — 그림 · 제목 · 퀘스트 테두리를 새로.</summary>
    private void BuildForMap(MapRuntime runtime)
    {
        drawnFor = runtime;

        foreach (GameObject go in builtForMap)
            if (go != null) Destroy(go);
        builtForMap.Clear();

        ChapterMap map = runtime.Map;
        aspect = map.HalfSize.x / map.HalfSize.y;

        ChapterDefinition chapter = StoryTable.Chapter(map.Chapter);
        chapterLabel.text = chapter != null ? $"{chapter.Number}장" : string.Empty;
        titleLabel.text = chapter != null ? chapter.Place : "지도";
        // 퀘스트 구역 테두리 (구역 이름은 Refresh가 가 본 곳만 적는다 — 결정 2-121).
        foreach (ZoneArea area in QuestAreas(runtime))
        {
            Vector2 min = runtime.UvOf(new Vector3(area.Bounds.xMin, 0f, area.Bounds.yMin));
            Vector2 max = runtime.UvOf(new Vector3(area.Bounds.xMax, 0f, area.Bounds.yMax));

            Image outline = UIFactory.CreatePanel($"Quest_{area.ZoneId}", outlineLayer, QuestColor, min, max, 0);
            outline.sprite = UISprites.RoundedOutline(UIFactory.RadiusLarge, 4);
            outline.type = Image.Type.Sliced;
            outline.raycastTarget = false;
            builtForMap.Add(outline.gameObject);
        }

        runtime.Redrawn -= OnRedrawn;
        runtime.Redrawn += OnRedrawn;
    }

    /// <summary>진행 중인 퀘스트의 다음 목표 구역들 (이 장).</summary>
    private static List<ZoneArea> QuestAreas(MapRuntime runtime)
    {
        var result = new List<ZoneArea>();
        List<string> zones = QuestZones.InChapter(StoryManager.Quests, runtime.Map.Chapter);

        foreach (ZoneArea area in runtime.Map.Areas)
            if (zones.Contains(area.ZoneId))
                result.Add(area);

        return result;
    }

    private void OnRedrawn()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        fogImage.texture = runtime.FogTexture;
        questCache = null;

        // 금줄이 걷혔으면 그림(양피지 · 사진 덮개)도 새로 — 열려 있을 때만 바로(닫혀 있으면 다음에 열 때).
        if (IsOpen)
            ApplyPicture(runtime);
    }

    private void OnMarkersChanged(int chapter)
    {
        if (IsOpen)
        {
            Refresh();
            if (markerList.activeSelf)
                FillMarkerList();
        }
    }

    // ── 매 프레임 ─────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (!IsOpen)
            return;

        if (MapRuntime.Current == null)
        {
            Hide();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        HandlePinch();
        ApplyView();

        if (center != lastCenter || !Mathf.Approximately(zoom, lastZoom) || Time.unscaledTime >= nextRefresh)
        {
            lastCenter = center;
            lastZoom = zoom;
            nextRefresh = Time.unscaledTime + IdleRefresh;
            Refresh();
        }
    }

    private Vector2 ContentSize => MapView.FitSize(viewport.rect.size, aspect) * zoom;

    private void ApplyView()
    {
        Vector2 size = ContentSize;
        center = MapView.ClampCenter(center, viewport.rect.size, size);

        content.sizeDelta = size;
        content.anchoredPosition = Vector2.Scale(new Vector2(0.5f, 0.5f) - center, size);

        zoomSlider.SetValueWithoutNotify(Mathf.InverseLerp(MapView.MinZoom, MapView.MaxZoom, zoom) * 100f);
    }

    private void Refresh()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        Vector2 size = ContentSize;
        int used = 0;

        for (int i = 0; i < filterButtons.Length; i++)
            if (filterButtons[i] != null)
                filterButtons[i].color = shown[i] ? ButtonOn : ButtonOff;

        outlineLayer.gameObject.SetActive(Shown(Layer.Quest));
        questPanel.gameObject.SetActive(Shown(Layer.Quest) && hasQuestRows);

        PlaceZoneNames(runtime, size);

        if (Shown(Layer.Quest))
            foreach (ZoneArea area in questCache ??= QuestAreas(runtime))
            {
                Vector3 mid = new(area.Bounds.center.x, 0f, area.Bounds.center.y);
                PlaceIcon(ref used, runtime, mid, size, UISprites.Glyph.Diamond, "!", null, QuestColor, 46);
            }

        ExtractionDirector extraction = ExtractionDirector.Instance;
        if (Shown(Layer.Extraction) && extraction != null && extraction.Points != null)
            for (int i = 0; i < extraction.Points.Length; i++)
                if (extraction.IsDiscovered(i))
                    PlaceIcon(ref used, runtime, extraction.Points[i], size, UISprites.Glyph.Circle, "",
                        ExtractionDirector.Name, MinimapUI.ExtractionColor, 38);

        foreach (MapLootMarks.Mark m in MapLootMarks.Collect())
        {
            bool fallen = m.kind == MapLootMarks.Kind.Fallen;
            if (!Shown(fallen ? Layer.Fallen : Layer.Loot))
                continue;

            PlaceIcon(ref used, runtime, m.position, size, ShapeOf(m.kind), fallen ? "!" : "",
                fallen ? FallenStash.Name : null, MapLootMarks.ColorOf(m.kind), fallen ? 34 : 22);
        }

        IReadOnlyList<Vector2> markers = MapMemory.Markers(runtime.Chapter);
        if (Shown(Layer.Marker))
            for (int i = 0; i < markers.Count; i++)
                PlaceIcon(ref used, runtime, new Vector3(markers[i].x, 0f, markers[i].y), size,
                    UISprites.Glyph.Diamond, (i + 1).ToString(), null, MinimapUI.MarkerColor, 36);

        for (int i = used; i < icons.Count; i++)
            icons[i].root.SetActive(false);

        if (runtime.Player != null)
        {
            Vector2 at = MapView.ToView(runtime.UvOf(runtime.Player.position), center, size);
            playerMarker.gameObject.SetActive(InView(at));
            playerMarker.anchoredPosition = at;
            playerHeading.localRotation = Quaternion.Euler(0f, 0f, -runtime.Player.eulerAngles.y);
            playerMarker.SetAsLastSibling();
        }
        else
        {
            playerMarker.gameObject.SetActive(false);
        }

        SetText(markerCount, $"표시 {markers.Count}/{MapTable.MaxMarkers}");

        RaidConditions night = RaidManager.Current;
        SetText(moonLabel, night.lunar ? MoonTable.Name(night.moon) : string.Empty);
        SetText(weatherLabel, night.weather.Chapter > 0
            ? $"{WeatherTable.SeasonName(night.weather.Season)}\n{night.weather.Name}"
            : night.weather.Name);
    }

    /// <summary>
    /// 【가 본 구역의 이름】 (결정 2-121 · 사용자 2026-10-13) — 구역마다 그 땅 가운데에 흰 글씨 · 검은 테.
    /// 들어가 본 구역(EnterEvent를 본 것)만 — 가 보지 않은 곳의 이름을 미리 알려 주지 않는다. 이름은 본문 그대로(StoryTable).
    /// </summary>
    private void PlaceZoneNames(MapRuntime runtime, Vector2 size)
    {
        int used = 0;

        foreach ((string zoneId, Vector3 middle) in ZoneCenters(runtime.Map))
        {
            if (!StoryManager.Progress.HasSeen(StoryTable.EnterEvent(zoneId)))
                continue;

            ZoneDefinition zone = StoryTable.Zone(zoneId);
            if (zone == null)
                continue;

            Vector2 at = MapView.ToView(runtime.UvOf(middle), center, size);
            if (!InView(at))
                continue;

            while (zoneLabels.Count <= used)
            {
                Text t = Inked(UIFactory.CreateLabel(zoneLabelLayer, "", 30, FontStyle.Bold,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, Color.white), 2.5f);
                t.rectTransform.sizeDelta = new Vector2(360f, 44f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                zoneLabels.Add(t);
            }

            Text label = zoneLabels[used++];
            label.gameObject.SetActive(true);
            SetText(label, zone.Name);
            // 구역 가운데에는 퀘스트 「!」가 설 수 있다 — 이름은 그 위로 비켜 선다.
            label.rectTransform.anchoredPosition = at + new Vector2(0f, ZoneLabelLift);
        }

        for (int i = used; i < zoneLabels.Count; i++)
            zoneLabels[i].gameObject.SetActive(false);
    }

    /// <summary>구역마다 한 자리 — 구역이 땅 여럿이면 그 땅들을 감싼 사각형의 가운데.</summary>
    private static List<(string, Vector3)> ZoneCenters(ChapterMap map)
    {
        var bounds = new Dictionary<string, Rect>();
        var order = new List<string>();

        foreach (ZoneArea area in map.Areas)
        {
            if (bounds.TryGetValue(area.ZoneId, out Rect r))
            {
                bounds[area.ZoneId] = Rect.MinMaxRect(Mathf.Min(r.xMin, area.Bounds.xMin), Mathf.Min(r.yMin, area.Bounds.yMin),
                    Mathf.Max(r.xMax, area.Bounds.xMax), Mathf.Max(r.yMax, area.Bounds.yMax));
            }
            else
            {
                bounds[area.ZoneId] = area.Bounds;
                order.Add(area.ZoneId);
            }
        }

        var list = new List<(string, Vector3)>(order.Count);
        foreach (string id in order)
            list.Add((id, new Vector3(bounds[id].center.x, 0f, bounds[id].center.y)));

        return list;
    }

    /// <summary>같은 글이면 넣지 않는다 — 넣을 때마다 글자 메시를 다시 만든다.</summary>
    private static void SetText(Text label, string text)
    {
        if (label != null && label.text != text)
            label.text = text;
    }

    private bool InView(Vector2 at)
    {
        Vector2 half = viewport.rect.size * 0.5f + new Vector2(30f, 30f);
        return Mathf.Abs(at.x) <= half.x && Mathf.Abs(at.y) <= half.y;
    }

    // ── 아이콘 (위에 그림 · 아래 이름표) ─────────────────────────────────

    private sealed class MapIcon
    {
        public GameObject root;
        public RectTransform rect;
        public Image shape;
        public Text inner;
        public Text label;
    }

    private static UISprites.Glyph ShapeOf(MapLootMarks.Kind kind) => kind switch
    {
        MapLootMarks.Kind.Bundle => UISprites.Glyph.Square,
        MapLootMarks.Kind.Corpse => UISprites.Glyph.Cross,
        _ => UISprites.Glyph.Circle,
    };

    private void PlaceIcon(ref int used, MapRuntime runtime, Vector3 world, Vector2 size,
                           UISprites.Glyph shape, string inner, string label, Color color, float shapeSize)
    {
        Vector2 at = MapView.ToView(runtime.UvOf(world), center, size);
        if (!InView(at))
            return;

        while (icons.Count <= used)
            icons.Add(CreateIcon());

        MapIcon icon = icons[used++];
        icon.root.SetActive(true);
        icon.rect.anchoredPosition = at;

        icon.rect.sizeDelta = new Vector2(shapeSize, shapeSize);
        icon.shape.sprite = UISprites.Of(shape);
        icon.shape.color = color;
        icon.inner.text = inner ?? string.Empty;
        icon.inner.fontSize = Mathf.RoundToInt(shapeSize * 0.55f);

        bool hasLabel = !string.IsNullOrEmpty(label);
        icon.label.gameObject.SetActive(hasLabel);
        if (hasLabel)
        {
            icon.label.text = label;
            icon.label.color = Ink;
        }
    }

    private MapIcon CreateIcon()
    {
        RectTransform rect = UIFactory.CreateRegion("Icon", iconLayer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.sizeDelta = new Vector2(44f, 44f);

        // 그림자 — 양피지 위에서 도형이 떠 보이게.
        Image shadow = UIFactory.CreatePanel("Shadow", rect, new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one, 0);
        shadow.sprite = UISprites.Of(UISprites.Glyph.Circle);
        shadow.rectTransform.offsetMin = new Vector2(-3f, -5f);
        shadow.rectTransform.offsetMax = new Vector2(3f, 1f);
        shadow.raycastTarget = false;

        Image shape = UIFactory.CreatePanel("Shape", rect, Color.white, Vector2.zero, Vector2.one, 0);
        shape.raycastTarget = false;
        shape.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.07f, 0.04f, 0.9f);

        Text inner = UIFactory.CreateLabel(rect, "", 20, FontStyle.Bold, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter,
            new Color(0.08f, 0.07f, 0.05f, 1f));
        inner.raycastTarget = false;
        inner.horizontalOverflow = HorizontalWrapMode.Overflow;
        inner.verticalOverflow = VerticalWrapMode.Overflow;

        // 이름표 — 참고 그림처럼 아이콘 바로 아래 · 검은 테두리 흰 글자.
        Text label = UIFactory.CreateLabel(rect, "", 20, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), TextAnchor.UpperCenter, Ink);
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.sizeDelta = new Vector2(220f, 28f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.raycastTarget = false;
        label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

        return new MapIcon { root = rect.gameObject, rect = rect, shape = shape, inner = inner, label = label };
    }

    // ── 확대 · 이동 ───────────────────────────────────────────────────

    private void ZoomBy(float step) => SetZoom(zoom + step, Vector2.zero);

    /// <summary>검증 도구 — 열린 지도를 한 칸 확대한다.</summary>
    public static void DebugZoomIn()
    {
        if (IsOpen)
            instance.ZoomBy(MapView.ZoomStep);
    }

    /// <summary>검증 도구 — 장 하나가 다 보이게 끝까지 줄인다.</summary>
    public static void DebugZoomOut()
    {
        if (IsOpen)
            instance.SetZoom(MapView.MinZoom, Vector2.zero);
    }

    /// <summary>배율을 바꾼다 — anchorView(화면 가운데 기준)가 가리킨 곳을 그대로 붙든다.</summary>
    private void SetZoom(float target, Vector2 anchorView)
    {
        float next = MapView.ClampZoom(target);
        if (Mathf.Approximately(next, zoom))
            return;

        Vector2 before = ContentSize;
        zoom = next;
        center = MapView.ZoomAround(center, anchorView, before, ContentSize);
        ApplyView();
        Refresh();
    }

    private void CenterOnPlayer()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime?.Player == null)
            return;

        center = runtime.UvOf(runtime.Player.position);
        ApplyView();
        Refresh();
    }

    private void FocusOn(Vector3 world, float minZoom)
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        zoom = Mathf.Max(zoom, MapView.ClampZoom(minZoom));
        center = runtime.UvOf(world);
        ApplyView();
        Refresh();
    }

    private void GoToObjective()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        QuestTracker tracker = StoryManager.Quests;
        QuestDefinition tracked = tracker?.Tracked();
        List<ZoneArea> areas = QuestAreas(runtime);

        if (areas.Count == 0)
        {
            objectiveLabel.text = tracked != null
                ? $"{QuestHudText.Objective(tracker, tracked)} — 이 장에는 갈 곳이 없다"
                : "지금 따라가는 퀘스트가 없다";
            return;
        }

        shown[(int)Layer.Quest] = true;
        objectiveLabel.text = tracked != null ? $"목표 — {QuestHudText.Objective(tracker, tracked)}" : "목표";
        FocusOn(new Vector3(areas[0].Bounds.center.x, 0f, areas[0].Bounds.center.y), 2f);
    }

    private void ToggleMarkerList()
    {
        bool open = !markerList.activeSelf;
        markerList.SetActive(open);
        if (open)
            FillMarkerList();
    }

    private void FillMarkerList()
    {
        UIFactory.ClearChildren(markerListRows);

        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        IReadOnlyList<Vector2> markers = MapMemory.Markers(runtime.Chapter);

        if (markers.Count == 0)
        {
            UIFactory.CreateLabel(markerListRows, "찍은 표시가 없다.\n지도를 두 번 눌러 찍는다.", 18, FontStyle.Normal,
                Vector2.zero, Vector2.one, TextAnchor.UpperCenter, InkDim);
            return;
        }

        for (int i = 0; i < markers.Count; i++)
        {
            var world = new Vector3(markers[i].x, 0f, markers[i].y);
            float distance = runtime.Player != null
                ? Vector3.Distance(new Vector3(runtime.Player.position.x, 0f, runtime.Player.position.z), world)
                : 0f;

            Button row = UIFactory.CreateButton(markerListRows, $"◆{i + 1}   {distance:0}m",
                new Vector2(0f, 1f), new Vector2(1f, 1f), ButtonOff, () => FocusOn(world, 2f), 22);
            var rect = (RectTransform)row.transform;
            rect.offsetMin = new Vector2(10f, -(i * 58f + 52f));
            rect.offsetMax = new Vector2(-10f, -(i * 58f));
        }
    }

    // ── 입력 ──────────────────────────────────────────────────────────

    private float ScaleFactor => canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

    private void HandlePinch()
    {
        if (Input.touchCount != 2)
        {
            pinchDistance = 0f;
            return;
        }

        Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
        float distance = Vector2.Distance(a.position, b.position);

        if (pinchDistance > 0f && distance > 0f)
        {
            Vector2 mid = (a.position + b.position) * 0.5f;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, mid, null, out Vector2 local))
                SetZoom(zoom * distance / pinchDistance, local - viewport.rect.center);
        }

        pinchDistance = distance;
        draggedSinceDown = true;
    }

    private void OnPointerDown() => draggedSinceDown = false;

    private void OnDrag(PointerEventData eventData)
    {
        if (Input.touchCount >= 2)
            return;

        draggedSinceDown = true;
        Vector2 size = ContentSize;
        Vector2 delta = eventData.delta / ScaleFactor;
        center -= new Vector2(size.x > 0f ? delta.x / size.x : 0f, size.y > 0f ? delta.y / size.y : 0f);
        ApplyView();
    }

    private void OnScroll(PointerEventData eventData)
    {
        float scroll = eventData.scrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                eventData.pressEventCamera, out Vector2 local))
            local = viewport.rect.center;

        SetZoom(zoom * (scroll > 0f ? 1.15f : 1f / 1.15f), local - viewport.rect.center);
    }

    /// <summary>두 번 누르기 — 마커 찍기 · 지우기.</summary>
    private void OnClick(PointerEventData eventData)
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null || !IsOpen || draggedSinceDown)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                eventData.pressEventCamera, out Vector2 local))
            return;

        Vector2 uv = MapView.ToUv(local - viewport.rect.center, center, ContentSize);
        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
            return;

        bool second = Time.unscaledTime - lastTapTime <= MapTable.DoubleTapSeconds
                      && (eventData.position - lastTapPosition).sqrMagnitude <= 48f * 48f;

        lastTapTime = second ? -10f : Time.unscaledTime;
        lastTapPosition = eventData.position;

        if (!second)
            return;

        Vector3 world = runtime.WorldOfUv(uv);
        var at = new Vector2(world.x, world.z);

        if (MapMemory.RemoveMarkerNear(runtime.Chapter, at, MapTable.MarkerPickRadius))
            return;

        MarkerError error = MapMemory.AddMarker(runtime.Chapter, at);
        if (error != MarkerError.None)
            StoryDialogueUI.ShowBanner(MapMemory.Explain(error), 2f);
    }

    /// <summary>지도 칸이 받는 손짓 — 끌기 · 휠 · 누르기를 지도에 넘긴다.</summary>
    private sealed class MapInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler,
                                    IPointerClickHandler, IScrollHandler
    {
        public WorldMapUI owner;

        public void OnPointerDown(PointerEventData e) => owner.OnPointerDown();
        public void OnBeginDrag(PointerEventData e) => owner.draggedSinceDown = true;
        public void OnDrag(PointerEventData e) => owner.OnDrag(e);
        public void OnPointerClick(PointerEventData e) => owner.OnClick(e);
        public void OnScroll(PointerEventData e) => owner.OnScroll(e);
    }

    // ── 가장자리 번짐 ─────────────────────────────────────────────────

    private static Texture2D topShade;

    /// <summary>위가 짙고 아래로 사라지는 세로 그늘 — 제목 뒤.</summary>
    private static Texture2D TopShadeTexture()
    {
        if (topShade != null)
            return topShade;

        const int n = 32;
        topShade = new Texture2D(1, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "MapTopShade (Runtime)"
        };

        var pixels = new Color32[n];
        for (int y = 0; y < n; y++)
        {
            float t = (y + 0.5f) / n;   // 0 = 아래, 1 = 위
            pixels[y] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0f, 1f, t) * 255f));
        }

        topShade.SetPixels32(pixels);
        topShade.Apply();
        return topShade;
    }

    private static Texture2D vignette;

    /// <summary>가운데는 비고 가장자리로 갈수록 짙어지는 판 — 참고 그림의 어둡게 번지는 끝.</summary>
    private static Texture2D VignetteTexture()
    {
        if (vignette != null)
            return vignette;

        const int n = 64;
        vignette = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "MapVignette (Runtime)"
        };

        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = Mathf.Abs((x + 0.5f) / n * 2f - 1f);
            float dy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
            float d = Mathf.Max(dx, dy) * 0.6f + Mathf.Sqrt(dx * dx + dy * dy) * 0.4f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.05f, d)) * 0.92f;
            pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }

        vignette.SetPixels32(pixels);
        vignette.Apply();
        return vignette;
    }
}
