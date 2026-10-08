using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 【전체 지도】 (결정 2-90 · 2-94) — 미니맵을 누르거나 M. 떠 있는 동안 게임이 멈춘다(결정 2-92).
///
/// 【모양 — 2026-10-08 사용자 참고 그림】 화면 가득 누런 양피지 지도 · 가장자리는 어둡게 번진다.
///   · 위: 「‹ 지도 › 1장 › 비 오는 폐장터」 · 목표 한 줄 · 표시 수 · 닫기
///   · 왼쪽 세로 줄: 표시 거르기(길목 · 마커 · 퀘스트 · 전리품 · 쓰러진 자리) · 목표로 · 마커 목록 · 맨 아래 달 · 날씨
///   · 아래: − ─── + 확대 막대 · 내 자리로
///   · 아이콘 아래 이름표 · 퀘스트 구역은 테두리 + 가운데 「!」
/// 【확대 · 이동】 배율 1(장 하나가 다 보인다) ~ 4 · 열면 2배로 내 자리가 가운데 · 끌어서 이동 · 휠 · 두 손가락 확대.
/// 【보이는 것】 내 자리 · 바라보는 쪽 · 찾은 길목 · 금줄 · 퀘스트 구역 · 내 마커 · 쓰러진 자리 · (옛 지도) 전리품.
///   구역 이름은 적지 않는다 · 가 보지 않은 땅은 어둡다 · 적 · 보스 · 조각 · 방은 그리지 않는다 (Map_System 4절)
/// 【마커】 두 번 누르면 찍는다(장마다 5개) · 마커를 두 번 누르면 지운다 — 지도 위에만 있다.
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
    private static readonly Color FogTint = new(0.55f, 0.42f, 0.30f, 0.72f);

    private const float TopBarHeight = 84f;
    private const float LeftBarWidth = 116f;

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
    private RectTransform outlineLayer;
    private RectTransform iconLayer;
    private RectTransform playerArrow;
    private Text breadcrumb;
    private Text objectiveLabel;
    private Text markerCount;
    private Text moonLabel;
    private Text weatherLabel;
    private Slider zoomSlider;
    private GameObject markerList;
    private RectTransform markerListRows;
    private readonly Image[] filterButtons = new Image[5];
    private readonly List<MapIcon> icons = new();
    private readonly List<GameObject> builtForMap = new();
    private MapRuntime drawnFor;

    // ── 보는 자리 ─────────────────────────────────────────────────────
    private float zoom = MapView.OpenZoom;
    private Vector2 center = new(0.5f, 0.5f);
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
        Image dim = UIFactory.CreatePanel("Dim", canvas.transform, Backdrop, Vector2.zero, Vector2.one, 0);
        dim.raycastTarget = true;   // 지도 밖을 눌러도 조준이 돌지 않게
        root = dim.gameObject;
        GamePause.Register(root);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)

        RectTransform safe = UIFactory.CreateRegion("SafeArea", root.transform, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        BuildViewport(safe);
        BuildTopBar(safe);
        BuildLeftBar(safe);
        BuildZoomBar(safe);
        BuildMarkerList(safe);

        MapMemory.MarkersChanged += OnMarkersChanged;
        root.SetActive(false);
    }

    private void BuildViewport(RectTransform safe)
    {
        viewport = UIFactory.CreateRegion("Viewport", safe, Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(LeftBarWidth, 0f);
        viewport.offsetMax = new Vector2(0f, -TopBarHeight);
        viewport.gameObject.AddComponent<RectMask2D>();

        Image catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0.01f);
        catcher.raycastTarget = true;
        viewport.gameObject.AddComponent<MapInput>().owner = this;

        content = UIFactory.CreateRegion("Content", viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        content.pivot = new Vector2(0.5f, 0.5f);

        mapImage = CreateRaw("MapImage", content);
        fogImage = CreateRaw("FogImage", content);
        fogImage.color = FogTint;
        outlineLayer = UIFactory.CreateRegion("QuestOutlines", content, Vector2.zero, Vector2.one);

        RawImage vignette = CreateRaw("Vignette", viewport);
        vignette.texture = VignetteTexture();
        vignette.color = new Color(Backdrop.r, Backdrop.g, Backdrop.b, 1f);

        iconLayer = UIFactory.CreateRegion("Icons", viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        Text arrow = UIFactory.CreateLabel(iconLayer, "▲", 34, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, MinimapUI.PlayerColor);
        arrow.rectTransform.sizeDelta = new Vector2(40f, 40f);
        arrow.raycastTarget = false;
        arrow.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        playerArrow = arrow.rectTransform;

        Text hint = UIFactory.CreateLabel(viewport, "두 번 눌러 표시 · 표시를 두 번 누르면 지운다 · 끌어서 이동", 18,
            FontStyle.Normal, new Vector2(1f, 0f), new Vector2(1f, 0f), TextAnchor.LowerRight, InkDim);
        hint.rectTransform.pivot = new Vector2(1f, 0f);
        hint.rectTransform.sizeDelta = new Vector2(620f, 30f);
        hint.rectTransform.anchoredPosition = new Vector2(-18f, 12f);
        hint.raycastTarget = false;
    }

    private void BuildTopBar(RectTransform safe)
    {
        RectTransform bar = UIFactory.CreateRegion("TopBar", safe, new Vector2(0f, 1f), new Vector2(1f, 1f));
        bar.offsetMin = new Vector2(0f, -TopBarHeight);
        Image back = bar.gameObject.AddComponent<Image>();
        back.color = BarColor;

        Button backButton = UIFactory.CreateButton(bar, "‹", new Vector2(0f, 0f), new Vector2(0f, 1f), ButtonOff, Hide, 44);
        Inset((RectTransform)backButton.transform, 10f, LeftBarWidth - 10f);

        breadcrumb = UIFactory.CreateLabel(bar, "", 32, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(0.55f, 1f), TextAnchor.MiddleLeft, Ink);
        breadcrumb.rectTransform.offsetMin = new Vector2(LeftBarWidth + 14f, 0f);
        breadcrumb.horizontalOverflow = HorizontalWrapMode.Overflow;

        objectiveLabel = UIFactory.CreateLabel(bar, "", 20, FontStyle.Normal,
            new Vector2(0.45f, 0f), new Vector2(0.84f, 1f), TextAnchor.MiddleRight, QuestColor);
        objectiveLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);

        markerCount = UIFactory.CreateLabel(bar, "", 22, FontStyle.Normal,
            new Vector2(0.84f, 0f), new Vector2(0.93f, 1f), TextAnchor.MiddleCenter, InkDim);

        Button close = UIFactory.CreateButton(bar, "✕", new Vector2(1f, 0f), new Vector2(1f, 1f), ButtonOff, Hide, 34);
        var closeRect = (RectTransform)close.transform;
        closeRect.offsetMin = new Vector2(-96f, 10f);
        closeRect.offsetMax = new Vector2(-12f, -10f);
    }

    private static void Inset(RectTransform rect, float pad, float width)
    {
        rect.offsetMin = new Vector2(pad, pad);
        rect.offsetMax = new Vector2(width, -pad);
    }

    private void BuildLeftBar(RectTransform safe)
    {
        RectTransform bar = UIFactory.CreateRegion("LeftBar", safe, new Vector2(0f, 0f), new Vector2(0f, 1f));
        bar.offsetMin = Vector2.zero;
        bar.offsetMax = new Vector2(LeftBarWidth, -TopBarHeight);
        Image back = bar.gameObject.AddComponent<Image>();
        back.color = BarColor;

        float y = 10f;
        AddFilter(bar, Layer.Extraction, UISprites.Glyph.Circle, "", ExtractionDirector.Name, MinimapUI.ExtractionColor, ref y);
        AddFilter(bar, Layer.Marker, UISprites.Glyph.Diamond, "", "마커", MinimapUI.MarkerColor, ref y);
        AddFilter(bar, Layer.Quest, UISprites.Glyph.Diamond, "!", "퀘스트", QuestColor, ref y);
        AddFilter(bar, Layer.Loot, UISprites.Glyph.Square, "", "전리품", MapLootMarks.BundleColor, ref y);
        AddFilter(bar, Layer.Fallen, UISprites.Glyph.Circle, "", "쓰러진 자리", MapLootMarks.FallenColor, ref y);

        y += 14f;
        AddTool(bar, UISprites.Glyph.Triangle, "", "목표로", QuestColor, GoToObjective, ref y);
        AddTool(bar, UISprites.Glyph.Square, "", "마커 목록", MinimapUI.MarkerColor, ToggleMarkerList, ref y);

        // 맨 아래 — 오늘 밤의 달 · 날씨 (참고 그림의 시간 · 날씨 자리).
        moonLabel = UIFactory.CreateLabel(bar, "", 20, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(1f, 0f), TextAnchor.MiddleCenter, Ink);
        moonLabel.rectTransform.offsetMin = new Vector2(4f, 58f);
        moonLabel.rectTransform.offsetMax = new Vector2(-4f, 92f);

        weatherLabel = UIFactory.CreateLabel(bar, "", 18, FontStyle.Normal,
            new Vector2(0f, 0f), new Vector2(1f, 0f), TextAnchor.MiddleCenter, InkDim);
        weatherLabel.rectTransform.offsetMin = new Vector2(4f, 14f);
        weatherLabel.rectTransform.offsetMax = new Vector2(-4f, 56f);
    }

    private const float ToolHeight = 84f;
    private const float ToolGap = 6f;

    private void AddFilter(RectTransform bar, Layer layer, UISprites.Glyph shape, string inner, string label, Color color, ref float y)
    {
        Layer captured = layer;
        Image button = AddTool(bar, shape, inner, label, color, () =>
        {
            shown[(int)captured] = !shown[(int)captured];
            Refresh();
        }, ref y);

        filterButtons[(int)layer] = button;
    }

    private Image AddTool(RectTransform bar, UISprites.Glyph shape, string inner, string label, Color color,
                          UnityEngine.Events.UnityAction action, ref float y)
    {
        Image image = UIFactory.CreatePanel($"Tool_{label}", bar, ButtonOff,
            new Vector2(0f, 1f), new Vector2(1f, 1f), UIFactory.Radius);
        image.rectTransform.offsetMin = new Vector2(10f, -(y + ToolHeight));
        image.rectTransform.offsetMax = new Vector2(-10f, -y);
        y += ToolHeight + ToolGap;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        Image icon = UIFactory.CreatePanel("Shape", image.transform, color,
            new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), 0);
        icon.sprite = UISprites.Of(shape);
        icon.rectTransform.sizeDelta = new Vector2(30f, 30f);
        icon.raycastTarget = false;

        if (!string.IsNullOrEmpty(inner))
        {
            Text t = UIFactory.CreateLabel(icon.transform, inner, 20, FontStyle.Bold,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, new Color(0.08f, 0.07f, 0.05f, 1f));
            t.raycastTarget = false;
        }

        Text l = UIFactory.CreateLabel(image.transform, label, 18, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(1f, 0.4f), TextAnchor.MiddleCenter, Ink);
        l.raycastTarget = false;
        l.horizontalOverflow = HorizontalWrapMode.Overflow;

        return image;
    }

    private void BuildZoomBar(RectTransform safe)
    {
        RectTransform bar = UIFactory.CreateRegion("ZoomBar", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = new Vector2(660f, 80f);
        bar.anchoredPosition = new Vector2(LeftBarWidth * 0.5f, 52f);

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

    private void BuildMarkerList(RectTransform safe)
    {
        Image panel = UIFactory.CreatePanel("MarkerList", safe, BarColor, new Vector2(0f, 1f), new Vector2(0f, 1f));
        panel.rectTransform.pivot = new Vector2(0f, 1f);
        panel.rectTransform.sizeDelta = new Vector2(300f, 60f + MapTable.MaxMarkers * 58f);
        panel.rectTransform.anchoredPosition = new Vector2(LeftBarWidth + 8f, -(TopBarHeight + 10f + 6 * (ToolHeight + ToolGap) + 14f));

        Text title = UIFactory.CreateLabel(panel.transform, "내 마커", 22, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, Ink);
        title.rectTransform.offsetMin = new Vector2(0f, -50f);

        markerListRows = UIFactory.CreateRegion("Rows", panel.transform, Vector2.zero, Vector2.one);
        markerListRows.offsetMax = new Vector2(0f, -54f);

        markerList = panel.gameObject;
        markerList.SetActive(false);
    }

    private static RawImage CreateRaw(string name, Transform parent)
    {
        RectTransform rect = UIFactory.CreateRegion(name, parent, Vector2.zero, Vector2.one);
        var raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    // ── 열고 닫기 ─────────────────────────────────────────────────────

    private void Show()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        if (drawnFor != runtime)
            BuildForMap(runtime);

        mapImage.texture = runtime.EnsureParchment();
        fogImage.texture = runtime.FogTexture;

        root.SetActive(true);
        markerList.SetActive(false);
        SetHudHidden(true);

        // 열면 2배로 내 자리가 가운데.
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
        breadcrumb.text = chapter != null
            ? $"지도  ›  {chapter.Number}장  ›  {chapter.Place}"
            : "지도";

        // 구역 이름 · 「(닫힘)」은 적지 않는다 — 장 하나가 한 맵으로 보이게 (2026-10-08 사용자).
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

        // 금줄이 걷혔으면 양피지도 새로 — 열려 있을 때만 바로 그린다(닫혀 있으면 다음에 열 때).
        if (IsOpen)
            mapImage.texture = runtime.EnsureParchment();
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
        Refresh();
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

        if (Shown(Layer.Quest))
            foreach (ZoneArea area in QuestAreas(runtime))
            {
                Vector3 mid = new(area.Bounds.center.x, 0f, area.Bounds.center.y);
                PlaceIcon(ref used, runtime, mid, size, UISprites.Glyph.Diamond, "!", null, QuestColor, 46);
            }

        ExtractionDirector extraction = ExtractionDirector.Instance;
        if (Shown(Layer.Extraction) && extraction != null && extraction.Points != null)
            for (int i = 0; i < extraction.Points.Length; i++)
                if (extraction.IsDiscovered(i))
                    PlaceIcon(ref used, runtime, extraction.Points[i], size, UISprites.Glyph.Circle, "",
                        ExtractionDirector.Name, MinimapUI.ExtractionColor, 30);

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
            playerArrow.gameObject.SetActive(InView(at));
            playerArrow.anchoredPosition = at;
            playerArrow.localRotation = Quaternion.Euler(0f, 0f, -runtime.Player.eulerAngles.y);
            playerArrow.SetAsLastSibling();
        }
        else
        {
            playerArrow.gameObject.SetActive(false);
        }

        markerCount.text = $"표시 {markers.Count}/{MapTable.MaxMarkers}";

        RaidConditions night = RaidManager.Current;
        moonLabel.text = night.lunar ? MoonTable.Name(night.moon) : string.Empty;
        weatherLabel.text = night.weather.Chapter > 0
            ? $"{WeatherTable.SeasonName(night.weather.Season)}\n{night.weather.Name}"
            : night.weather.Name;
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
