using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【미니맵】 (결정 2-90) — 오른쪽 위 · 북쪽 고정(카메라와 같은 방향) · 플레이어 둘레 MinimapRange(36m).
///
///   · 보이는 것: 내 자리 · 바라보는 쪽(화살표) · 찾은 길목 · 내가 찍은 마커 · 금줄(그림에 그려져 있다)
///   · 가 보지 않은 땅은 어둡다(안개) · 적은 보이지 않는다 (Map_System 4절)
///   · 범위 밖의 길목 · 마커는 테두리에 점으로 그쪽을 가리킨다
///   · 누르면 전체 지도 (PC: M)
/// </summary>
public class MinimapUI : MonoBehaviour
{
    private static MinimapUI instance;

    public static readonly Color ExtractionColor = new(0.45f, 0.95f, 0.55f, 1f);
    public static readonly Color MarkerColor = new(1f, 0.85f, 0.35f, 1f);
    public static readonly Color PlayerColor = new(1f, 1f, 1f, 1f);

    private const float Inner = 6f;      // 틀 안쪽 여백
    private const float EdgeInset = 9f;  // 범위 밖 점을 테두리 안쪽으로

    private RectTransform frame;
    private RectTransform view;
    private RawImage mapImage;
    private RawImage fogImage;
    private RectTransform playerArrow;
    private readonly List<Text> icons = new();
    private Text weatherLine;
    private float weatherRefresh;
    private bool hiddenByScreen;

    public static MinimapUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        // 철수 HUD(805) 바로 위 · 가방 화면(1000) 아래.
        Canvas canvas = UIFactory.CreateCanvas("MinimapCanvas (Runtime)", 806);
        instance = canvas.gameObject.AddComponent<MinimapUI>();
        instance.Build(canvas);
        return instance;
    }

    public static void SetHiddenByScreen(bool hidden)
    {
        if (instance == null)
            return;

        instance.hiddenByScreen = hidden;
        instance.frame.gameObject.SetActive(!hidden);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        Image back = UIFactory.CreateGlass("Minimap", safe, UIPalette.Panel, Vector2.one, Vector2.one);
        frame = back.rectTransform;
        frame.pivot = Vector2.one;
        frame.sizeDelta = new Vector2(HudLayout.MinimapSize, HudLayout.MinimapSize);
        frame.anchoredPosition = new Vector2(-HudLayout.Margin, -HudLayout.Margin);

        var button = back.gameObject.AddComponent<Button>();
        button.targetGraphic = back;
        button.onClick.AddListener(WorldMapUI.Open);

        view = UIFactory.Inset(UIFactory.CreateRegion("View", frame, Vector2.zero, Vector2.one), Inner);
        view.gameObject.AddComponent<RectMask2D>();

        mapImage = CreateRaw("Map", view);
        fogImage = CreateRaw("Fog", view);

        Text arrow = UIFactory.CreateLabel(view, "▲", 26, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, PlayerColor);
        arrow.raycastTarget = false;
        arrow.rectTransform.sizeDelta = new Vector2(30f, 30f);
        arrow.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        playerArrow = arrow.rectTransform;

        Text hint = UIFactory.CreateLabel(frame, "지도", 18, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(1f, 0f), TextAnchor.LowerRight, UIPalette.TextDim);
        hint.rectTransform.offsetMin = new Vector2(0f, 6f);
        hint.rectTransform.offsetMax = new Vector2(-10f, 30f);
        hint.raycastTarget = false;

        // 달 · 날씨 · 막이 한 줄 (결정 2-91) — 미니맵 위쪽 띠.
        Image strip = UIFactory.CreatePanel("WeatherStrip", frame, UIPalette.NameStrip,
            new Vector2(0f, 1f), new Vector2(1f, 1f), radius: 0);
        strip.raycastTarget = false;
        strip.rectTransform.offsetMin = new Vector2(Inner, -Inner - 30f);
        strip.rectTransform.offsetMax = new Vector2(-Inner, -Inner);

        weatherLine = UIFactory.CreateLabel(strip.transform, string.Empty, 16, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextOnGlass);
        weatherLine.raycastTarget = false;
        weatherLine.supportRichText = true;
        weatherLine.rectTransform.offsetMin = new Vector2(4f, 0f);
        weatherLine.rectTransform.offsetMax = new Vector2(-4f, 0f);
        weatherLine.resizeTextForBestFit = true;
        weatherLine.resizeTextMinSize = 10;
        weatherLine.resizeTextMaxSize = 16;
    }

    /// <summary>미니맵 띠에 적을 한 줄 — 「보름 · 한파 · 방한 0/2 · 추위 23」.</summary>
    public static string WeatherText(RaidConditions c, int protection, int deficit, float cold)
    {
        var parts = new List<string>(4);

        if (c.lunar)
            parts.Add(MoonTable.Name(c.moon));

        RaidWeather w = c.weather;
        parts.Add(w.Name);

        if (w.Hazard != WeatherHazard.None)
        {
            string guard = $"{WeatherTable.ProtectionName(w.Protection)} {protection}/{w.Severity}";
            parts.Add(deficit > 0 ? $"<color=#F29E66>{guard}</color>" : guard);
        }

        if (cold >= 1f)
            parts.Add($"<color=#A8D2FF>추위 {Mathf.FloorToInt(cold)}</color>");

        return string.Join(" · ", parts);
    }

    private static RawImage CreateRaw(string name, Transform parent)
    {
        RectTransform rect = UIFactory.CreateRegion(name, parent, Vector2.zero, Vector2.one);
        var raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
            WorldMapUI.Toggle();
    }

    private void LateUpdate()
    {
        MapRuntime runtime = MapRuntime.Current;
        bool show = runtime != null && runtime.Player != null && !hiddenByScreen && !WorldMapUI.IsOpen;
        frame.gameObject.SetActive(show);

        if (!show)
            return;

        weatherRefresh -= Time.unscaledDeltaTime;
        if (weatherRefresh <= 0f)
        {
            weatherRefresh = 0.25f;
            PlayerWeather pw = PlayerWeather.Current;
            weatherLine.text = WeatherText(RaidManager.Current,
                pw != null ? pw.Protection : 0, pw != null ? pw.Deficit : 0, pw != null ? pw.ColdStacks : 0f);
        }

        Vector3 center = runtime.Player.position;
        ChapterMap map = runtime.Map;

        // 플레이어가 가운데 — 그림에서 반경만큼 잘라 보인다.
        Vector2 uv = runtime.UvOf(center);
        float uw = MapTable.MinimapRange / map.HalfSize.x;   // 지름 / 맵 폭 = 2R / 2hx
        float vh = MapTable.MinimapRange / map.HalfSize.y;
        var rect = new Rect(uv.x - uw * 0.5f, uv.y - vh * 0.5f, uw, vh);

        mapImage.texture = runtime.MapTexture;
        mapImage.uvRect = rect;
        fogImage.texture = runtime.FogTexture;
        fogImage.uvRect = rect;

        playerArrow.localRotation = Quaternion.Euler(0f, 0f, -runtime.Player.eulerAngles.y);

        int used = 0;
        float half = view.rect.width * 0.5f;

        ExtractionDirector extraction = ExtractionDirector.Instance;
        if (extraction != null && extraction.Points != null)
            for (int i = 0; i < extraction.Points.Length; i++)
                if (extraction.IsDiscovered(i))
                    Place(ref used, "●", ExtractionColor, extraction.Points[i], center, half, 22);

        // 쓰러진 자리는 범위 밖이면 테두리 점으로, 전리품(옛 지도)은 범위 안의 것만 (결정 2-93).
        foreach (MapLootMarks.Mark m in MapLootMarks.Collect())
        {
            bool fallen = m.kind == MapLootMarks.Kind.Fallen;
            if (!fallen && !InRange(m.position, center))
                continue;

            Place(ref used, MapLootMarks.Glyph(m.kind), MapLootMarks.ColorOf(m.kind), m.position, center, half,
                fallen ? 24 : 18);
        }

        IReadOnlyList<Vector2> markers = MapMemory.Markers(runtime.Chapter);
        for (int i = 0; i < markers.Count; i++)
            Place(ref used, (i + 1).ToString(), MarkerColor, new Vector3(markers[i].x, 0f, markers[i].y), center, half, 20);

        for (int i = used; i < icons.Count; i++)
            icons[i].gameObject.SetActive(false);
    }

    private static bool InRange(Vector3 world, Vector3 center)
        => Mathf.Abs(world.x - center.x) <= MapTable.MinimapRange
           && Mathf.Abs(world.z - center.z) <= MapTable.MinimapRange;

    /// <summary>점 하나 — 범위 밖이면 테두리에 붙여 그쪽을 가리킨다.</summary>
    private void Place(ref int used, string glyph, Color color, Vector3 world, Vector3 center, float half, int size)
    {
        Text icon = Icon(used++);
        icon.text = glyph;
        icon.color = color;
        icon.fontSize = size;

        Vector2 local = new Vector2(world.x - center.x, world.z - center.z) / MapTable.MinimapRange * half;
        float limit = half - EdgeInset;
        float k = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y)) / limit;
        if (k > 1f)
            local /= k;

        icon.rectTransform.anchoredPosition = local;
        icon.gameObject.SetActive(true);
    }

    private Text Icon(int index)
    {
        while (icons.Count <= index)
        {
            Text t = UIFactory.CreateLabel(view, "", 20, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            t.rectTransform.sizeDelta = new Vector2(28f, 28f);
            t.raycastTarget = false;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            icons.Add(t);
        }

        // 화살표가 늘 맨 위.
        playerArrow.SetAsLastSibling();
        return icons[index];
    }
}
