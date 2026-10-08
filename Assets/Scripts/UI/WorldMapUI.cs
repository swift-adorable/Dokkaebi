using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 【전체 지도】 (결정 2-90) — 미니맵을 누르거나 M. 지도가 떠 있어도 게임은 흐른다(덕코프처럼).
///
///   · 보이는 것: 내 자리 · 바라보는 쪽 · 찾은 길목 · 금줄 · 진행 중인 퀘스트 구역 테두리 · 내 마커
///   · 구역 이름은 적지 않는다 — 맵 하나로 보이게 (2026-10-08 사용자)
///   · 가 보지 않은 땅은 어둡다 · 닫힌 구역은 더 어둡다 · 적은 보이지 않는다 (Map_System 4절)
///   · 두 번 누르면 마커를 찍는다(장마다 5개) · 마커를 두 번 누르면 지운다 — 마커는 지도 위에만 있다
/// </summary>
public class WorldMapUI : MonoBehaviour, IPointerClickHandler
{
    private static WorldMapUI instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private static readonly Color QuestOutline = new(1f, 0.85f, 0.45f, 0.95f);

    private GameObject root;
    private Text title;
    private Text markerCount;
    private RectTransform mapArea;
    private AspectRatioFitter fitter;
    private RawImage mapImage;
    private RawImage fogImage;
    private RectTransform overlay;
    private RectTransform playerArrow;
    private readonly List<Text> icons = new();
    private readonly List<GameObject> builtForMap = new();
    private MapRuntime drawnFor;

    private float lastTapTime = -10f;
    private Vector2 lastTapPosition;

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

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        // 덮개는 거의 불투명하게 — 뒤의 퀵슬롯 · 게임 화면이 지도 글자와 겹쳐 읽히지 않게.
        var dimColor = UIPalette.Dim;
        dimColor.a = 0.96f;
        Image dim = UIFactory.CreatePanel("Dim", safe, dimColor, new Vector2(-0.2f, -0.2f), new Vector2(1.2f, 1.2f), 0);
        dim.raycastTarget = true;   // 지도 밖을 눌러도 조준이 돌지 않게
        root = dim.gameObject;
        GamePause.Register(root);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)

        RectTransform body = UIFactory.Inset(UIFactory.CreateRegion("Body", root.transform,
            new Vector2(1f / 7f, 1f / 7f), new Vector2(6f / 7f, 6f / 7f)), UIFactory.Gap);
        // 덮개가 안전 영역의 -0.2 ~ 1.2(1.4배)라 덮개의 1/7 ~ 6/7 = 안전 영역 전체.

        title = UIFactory.CreateLabel(body, "", 34, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(0.6f, 1f), TextAnchor.MiddleLeft, UIPalette.Text);
        title.rectTransform.offsetMin = new Vector2(0f, -56f);

        markerCount = UIFactory.CreateLabel(body, "", 24, FontStyle.Normal,
            new Vector2(0.4f, 1f), new Vector2(0.86f, 1f), TextAnchor.MiddleRight, UIPalette.TextDim);
        markerCount.rectTransform.offsetMin = new Vector2(0f, -56f);
        markerCount.rectTransform.offsetMax = new Vector2(-16f, 0f);

        Button close = UIFactory.CreateButton(body, "닫기", new Vector2(0.88f, 1f), new Vector2(1f, 1f),
            UIPalette.Header, Hide, 28);
        ((RectTransform)close.transform).offsetMin = new Vector2(0f, -56f);

        Text hint = UIFactory.CreateLabel(body, "두 번 눌러 표시 · 표시를 두 번 누르면 지운다 · 적은 지도에 나오지 않는다", 22,
            FontStyle.Normal, new Vector2(0f, 0f), new Vector2(1f, 0f), TextAnchor.MiddleCenter, UIPalette.TextDim);
        hint.rectTransform.offsetMax = new Vector2(0f, 36f);

        RectTransform holder = UIFactory.CreateRegion("MapHolder", body, Vector2.zero, Vector2.one);
        holder.offsetMin = new Vector2(0f, 46f);
        holder.offsetMax = new Vector2(0f, -66f);

        mapArea = UIFactory.CreateRegion("Map", holder, Vector2.zero, Vector2.one);
        fitter = mapArea.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        Image frameBack = mapArea.gameObject.AddComponent<Image>();
        frameBack.color = new Color(0f, 0f, 0f, 0.6f);
        frameBack.raycastTarget = true;   // 두 번 누르기를 받는다 (IPointerClickHandler는 이 캔버스 루트)

        mapImage = CreateRaw("MapImage", mapArea);
        fogImage = CreateRaw("FogImage", mapArea);
        overlay = UIFactory.CreateRegion("Overlay", mapArea, Vector2.zero, Vector2.one);

        Text arrow = UIFactory.CreateLabel(overlay, "▲", 30, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, MinimapUI.PlayerColor);
        arrow.rectTransform.sizeDelta = new Vector2(34f, 34f);
        arrow.raycastTarget = false;
        arrow.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        playerArrow = arrow.rectTransform;

        MapMemory.MarkersChanged += OnMarkersChanged;
        root.SetActive(false);
    }

    private static RawImage CreateRaw(string name, Transform parent)
    {
        RectTransform rect = UIFactory.CreateRegion(name, parent, Vector2.zero, Vector2.one);
        var raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    private void Show()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        if (drawnFor != runtime)
            BuildForMap(runtime);

        root.SetActive(true);
        SetHudHidden(true);
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

    /// <summary>장 맵이 바뀌었을 때 — 그림 · 구역 이름 · 퀘스트 테두리를 새로.</summary>
    private void BuildForMap(MapRuntime runtime)
    {
        drawnFor = runtime;

        foreach (GameObject go in builtForMap)
            if (go != null) Destroy(go);
        builtForMap.Clear();

        ChapterMap map = runtime.Map;
        fitter.aspectRatio = map.HalfSize.x / map.HalfSize.y;
        mapImage.texture = runtime.MapTexture;
        fogImage.texture = runtime.FogTexture;

        ChapterDefinition chapter = StoryTable.Chapter(map.Chapter);
        title.text = chapter != null ? $"{chapter.Title} — 지도" : "지도";

        List<string> questZones = QuestZones.InChapter(StoryManager.Quests, map.Chapter);

        foreach (ZoneArea area in map.Areas)
        {
            Vector2 min = runtime.UvOf(new Vector3(area.Bounds.xMin, 0f, area.Bounds.yMin));
            Vector2 max = runtime.UvOf(new Vector3(area.Bounds.xMax, 0f, area.Bounds.yMax));

            // 구역 이름 · 「(닫힘)」은 적지 않는다 — 장 하나가 한 맵으로 보이게 (2026-10-08 사용자).
            if (!questZones.Contains(area.ZoneId))
                continue;

            Image outline = UIFactory.CreatePanel($"Quest_{area.ZoneId}", overlay, QuestOutline, min, max, 0);
            outline.sprite = UISprites.RoundedOutline(UIFactory.RadiusLarge, 4);
            outline.type = Image.Type.Sliced;
            outline.raycastTarget = false;
            builtForMap.Add(outline.gameObject);
        }

        runtime.Redrawn -= OnRedrawn;
        runtime.Redrawn += OnRedrawn;
        playerArrow.SetAsLastSibling();
    }

    private void OnRedrawn()
    {
        if (MapRuntime.Current != null)
        {
            mapImage.texture = MapRuntime.Current.MapTexture;
            fogImage.texture = MapRuntime.Current.FogTexture;
        }
    }

    private void OnMarkersChanged(int chapter)
    {
        if (IsOpen)
            Refresh();
    }

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

        Refresh();
    }

    private void Refresh()
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null)
            return;

        Vector2 size = overlay.rect.size;
        int used = 0;

        if (runtime.Player != null)
        {
            playerArrow.gameObject.SetActive(true);
            playerArrow.anchoredPosition = Local(runtime, runtime.Player.position, size);
            playerArrow.localRotation = Quaternion.Euler(0f, 0f, -runtime.Player.eulerAngles.y);
        }
        else
        {
            playerArrow.gameObject.SetActive(false);
        }

        ExtractionDirector extraction = ExtractionDirector.Instance;
        if (extraction != null && extraction.Points != null)
            for (int i = 0; i < extraction.Points.Length; i++)
                if (extraction.IsDiscovered(i))
                    Place(ref used, $"● {ExtractionDirector.Name}", MinimapUI.ExtractionColor,
                        Local(runtime, extraction.Points[i], size), 24);

        foreach (MapLootMarks.Mark m in MapLootMarks.Collect())
            Place(ref used, MapLootMarks.Label(m.kind), MapLootMarks.ColorOf(m.kind),
                Local(runtime, m.position, size), m.kind == MapLootMarks.Kind.Fallen ? 24 : 22);

        IReadOnlyList<Vector2> markers = MapMemory.Markers(runtime.Chapter);
        for (int i = 0; i < markers.Count; i++)
            Place(ref used, $"◆{i + 1}", MinimapUI.MarkerColor,
                Local(runtime, new Vector3(markers[i].x, 0f, markers[i].y), size), 26);

        for (int i = used; i < icons.Count; i++)
            icons[i].gameObject.SetActive(false);

        markerCount.text = $"표시 {markers.Count}/{MapTable.MaxMarkers}";
        playerArrow.SetAsLastSibling();
    }

    /// <summary>월드 자리 → 지도 위 자리 (가운데 기준 픽셀).</summary>
    private static Vector2 Local(MapRuntime runtime, Vector3 world, Vector2 size)
    {
        Vector2 uv = runtime.UvOf(world);
        return new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
    }

    private void Place(ref int used, string glyph, Color color, Vector2 local, int fontSize)
    {
        while (icons.Count <= used)
        {
            Text t = UIFactory.CreateLabel(overlay, "", 24, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            t.rectTransform.sizeDelta = new Vector2(160f, 34f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            icons.Add(t);
        }

        Text icon = icons[used++];
        icon.text = glyph;
        icon.color = color;
        icon.fontSize = fontSize;
        icon.rectTransform.anchoredPosition = local;
        icon.gameObject.SetActive(true);
    }

    // ── 두 번 누르기 — 마커 찍기 · 지우기 ───────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        MapRuntime runtime = MapRuntime.Current;
        if (runtime == null || !IsOpen)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapArea, eventData.position,
                eventData.pressEventCamera, out Vector2 local))
            return;

        Rect r = mapArea.rect;
        var uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
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
}
