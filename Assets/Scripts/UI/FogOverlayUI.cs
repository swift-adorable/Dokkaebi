using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【날씨 덮개】 (결정 2-91 · 사용자 결정 「내 둘레 밖을 안개로」 · 「간단한 입자로 지금」)
///
///   · 짙은 안개 — 밝히기가 모자라면 내 둘레(BaseSight 18m × ×0.7 / ×0.45) 밖을 안개로 덮는다.
///     둘레는 땅 위의 원을 화면에 비춘 타원 — 가장자리는 부드럽게 흐린다
///   · 날씨마다 화면 전체에 옅은 빛깔(흐림 · 비 · 흙비 · 폭염 · 독안개 · 꽃비)
/// HUD(800~) 아래 · 월드 위(790). 누르는 것을 막지 않는다. [임시 — 아트 때 바꾼다]
/// </summary>
public class FogOverlayUI : MonoBehaviour
{
    private static FogOverlayUI instance;

    public static readonly Color FogColor = new(0.56f, 0.59f, 0.63f, 0.94f);

    private RectTransform root;
    private Image tint;
    private RawImage hole;
    private Image left, right, top, bottom;
    private static Texture2D holeTexture;

    public static FogOverlayUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("FogOverlayCanvas (Runtime)", 790);
        Object.Destroy(canvas.GetComponent<GraphicRaycaster>());
        instance = canvas.gameObject.AddComponent<FogOverlayUI>();
        instance.Build(canvas);
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        root = (RectTransform)canvas.transform;

        tint = Plain("Tint");
        tint.rectTransform.anchorMin = Vector2.zero;
        tint.rectTransform.anchorMax = Vector2.one;
        tint.rectTransform.offsetMin = Vector2.zero;
        tint.rectTransform.offsetMax = Vector2.zero;

        var holeRect = UIFactory.CreateRegion("SightHole", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        hole = holeRect.gameObject.AddComponent<RawImage>();
        hole.texture = HoleTexture();
        hole.color = FogColor;
        hole.raycastTarget = false;

        left = Plain("FogLeft");
        right = Plain("FogRight");
        top = Plain("FogTop");
        bottom = Plain("FogBottom");

        foreach (Image side in new[] { left, right, top, bottom })
            side.color = FogColor;

        SetFog(false);
    }

    private Image Plain(string name)
    {
        RectTransform rect = UIFactory.CreateRegion(name, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void LateUpdate()
    {
        bool raid = !SceneFlow.InBunker && RaidManager.HasInstance;
        RaidWeather weather = raid ? RaidManager.Current.weather : RaidWeather.Calm;

        tint.color = TintOf(weather);
        tint.enabled = tint.color.a > 0.001f;

        PlayerWeather player = PlayerWeather.Current;
        Camera cam = Camera.main;
        float scale = PlayerWeather.SightScale;

        bool fog = raid && player != null && cam != null && scale < 0.999f;
        SetFog(fog);

        if (fog)
            PlaceHole(cam, player.transform.position, WeatherTable.BaseSight * scale);
    }

    private void SetFog(bool on)
    {
        hole.enabled = on;
        left.enabled = right.enabled = top.enabled = bottom.enabled = on;
    }

    /// <summary>땅 위 반경 radius의 원을 화면에 비춰 구멍을 놓고, 나머지를 넷으로 덮는다.</summary>
    private void PlaceHole(Camera cam, Vector3 center, float radius)
    {
        Vector3 rightDir = Flat(cam.transform.right);
        Vector3 forwardDir = Flat(cam.transform.forward);

        Vector2 c = ToLocal(cam, center);
        Vector2 r = ToLocal(cam, center + rightDir * radius);
        Vector2 f = ToLocal(cam, center + forwardDir * radius);
        Vector2 b = ToLocal(cam, center - forwardDir * radius);

        float rx = Mathf.Max(8f, Mathf.Abs(r.x - c.x));
        float yTop = Mathf.Max(f.y, b.y);
        float yBottom = Mathf.Min(f.y, b.y);
        if (yTop - yBottom < 16f) { yTop = c.y + 8f; yBottom = c.y - 8f; }

        float xMin = c.x - rx, xMax = c.x + rx;

        Vector2 size = root.rect.size;
        float W = size.x * 0.5f + 50f, H = size.y * 0.5f + 50f;

        Set(hole.rectTransform, xMin, yBottom, xMax, yTop);
        Set(left.rectTransform, -W, -H, xMin, H);
        Set(right.rectTransform, xMax, -H, W, H);
        Set(top.rectTransform, xMin, yTop, xMax, H);
        Set(bottom.rectTransform, xMin, -H, xMax, yBottom);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    private Vector2 ToLocal(Camera cam, Vector3 world)
    {
        Vector3 screen = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        return local;
    }

    private static void Set(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(xMin, yMin);
        rect.offsetMax = new Vector2(Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
    }

    /// <summary>날씨마다 화면 전체에 거는 옅은 빛깔 [임시].</summary>
    public static Color TintOf(RaidWeather w)
    {
        if (w.IsFlowerRain)
            return new Color(1f, 0.78f, 0.88f, 0.06f);

        int sev = Mathf.Max(1, w.Severity);

        switch (w.Hazard)
        {
            case WeatherHazard.Cold: return new Color(0.72f, 0.84f, 1f, 0.07f * sev);
            case WeatherHazard.Dust: return new Color(0.58f, 0.44f, 0.26f, 0.13f * sev);
            case WeatherHazard.Heat:
                return w.IsMiasma ? new Color(0.36f, 0.62f, 0.22f, 0.2f) : new Color(1f, 0.55f, 0.2f, 0.09f * sev);
            case WeatherHazard.Fog: return new Color(0.72f, 0.74f, 0.77f, 0.12f * sev);
        }

        return w.Slot switch
        {
            WeatherSlot.Cloudy => new Color(0.08f, 0.1f, 0.14f, 0.12f),
            WeatherSlot.Precipitation => w.Snow
                ? new Color(0.85f, 0.9f, 1f, 0.07f)
                : new Color(0.06f, 0.1f, 0.18f, w.IsMonsoon ? 0.24f : 0.17f),
            _ => Color.clear,
        };
    }

    /// <summary>가운데가 비고 가장자리로 갈수록 짙어지는 원 — 구석은 꽉 찬다.</summary>
    private static Texture2D HoleTexture()
    {
        if (holeTexture != null)
            return holeTexture;

        const int n = 128;
        holeTexture = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "SightHole (Runtime)"
        };

        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d));
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        holeTexture.SetPixels32(pixels);
        holeTexture.Apply();
        return holeTexture;
    }
}
