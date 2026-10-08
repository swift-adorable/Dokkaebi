using UnityEngine;

/// <summary>
/// 【날씨 입자】 (결정 2-91 · 사용자 결정 「간단한 입자로 지금」) — 비 · 장마비 · 눈 · 눈보라(한파) ·
/// 흙비 · 독안개 티끌 · 꽃비. 플레이어 머리 위 상자에서 내린다. 화면 빛깔과 안개는 FogOverlayUI가 맡는다.
/// 해를 주지 않는다 — 보기만이다. [임시 — 아트 때 바꾼다]
/// </summary>
public class WeatherEffects : MonoBehaviour
{
    public const string MaterialPath = "Weather/WeatherParticle";

    private const float Height = 14f;
    private const float Area = 40f;

    private ParticleSystem system;
    private Transform follow;
    private RaidWeather shown;
    private bool built;

    /// <summary>판 씬에 하나 — 없으면 만든다.</summary>
    public static void Ensure(Transform player)
    {
        var existing = FindAnyObjectByType<WeatherEffects>();
        if (existing != null)
        {
            existing.follow = player;
            return;
        }

        var go = new GameObject("WeatherEffects (Runtime)");
        go.AddComponent<WeatherEffects>().follow = player;
        FogOverlayUI.EnsureInstance();
    }

    /// <summary>입자 한 벌의 모양.</summary>
    public struct Spec
    {
        public bool on;
        public Color color;
        public float rate;
        public float fall;      // 아래로 (m/s)
        public float drift;     // 옆으로 (m/s)
        public float size;
        public bool streak;     // 빗줄기처럼 늘인다
    }

    public static Spec SpecOf(RaidWeather w)
    {
        if (w.IsFlowerRain)
            return new Spec { on = true, color = new Color(1f, 0.72f, 0.84f, 0.9f), rate = 70f * w.Severity, fall = 1.4f, drift = 1.2f, size = 0.22f };

        switch (w.Hazard)
        {
            case WeatherHazard.Cold:
                return new Spec { on = true, color = new Color(1f, 1f, 1f, 0.9f), rate = 260f * w.Severity, fall = 3.5f, drift = 3f * w.Severity, size = 0.16f };
            case WeatherHazard.Dust:
                return new Spec { on = true, color = new Color(0.62f, 0.48f, 0.3f, 0.55f), rate = 220f * w.Severity, fall = 2.5f, drift = 5f, size = 0.2f };
            case WeatherHazard.Heat:
                return w.IsMiasma
                    ? new Spec { on = true, color = new Color(0.55f, 0.85f, 0.35f, 0.4f), rate = 60f, fall = 0.4f, drift = 0.4f, size = 0.35f }
                    : default;
            case WeatherHazard.Fog:
                return default;
        }

        if (w.Slot != WeatherSlot.Precipitation)
            return default;

        if (w.Snow)
            return new Spec { on = true, color = new Color(1f, 1f, 1f, 0.85f), rate = 150f, fall = 2.2f, drift = 0.6f, size = 0.13f };

        return new Spec
        {
            on = true, color = new Color(0.72f, 0.8f, 0.95f, 0.5f),
            rate = w.IsMonsoon ? 700f : 380f, fall = 20f, drift = w.IsMonsoon ? 2f : 0.5f,
            size = 0.05f, streak = true
        };
    }

    private void LateUpdate()
    {
        RaidWeather w = RaidManager.HasInstance && !SceneFlow.InBunker ? RaidManager.Current.weather : RaidWeather.Calm;

        if (!built || w.Slot != shown.Slot || w.Chapter != shown.Chapter || w.Snow != shown.Snow)
            Rebuild(w);

        if (follow != null)
            transform.position = follow.position + Vector3.up * Height;
    }

    private void Rebuild(RaidWeather w)
    {
        built = true;
        shown = w;

        if (system != null)
            Destroy(system.gameObject);
        system = null;

        Spec spec = SpecOf(w);
        if (!spec.on)
            return;

        var go = new GameObject("Particles");
        go.transform.SetParent(transform, false);
        system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float life = Height / Mathf.Max(0.1f, spec.fall) + 0.2f;

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = true;
        main.startLifetime = life;
        main.startSpeed = 0f;
        main.startSize = spec.size;
        main.startColor = spec.color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.CeilToInt(spec.rate * life * 1.2f);
        main.scalingMode = ParticleSystemScalingMode.Shape;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = spec.rate;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Area, 0.5f, Area);

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(spec.drift * 0.6f, spec.drift);
        velocity.y = new ParticleSystem.MinMaxCurve(-spec.fall * 1.1f, -spec.fall * 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(-spec.drift * 0.3f, spec.drift * 0.3f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = SharedMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (spec.streak)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
        }

        system.Play();
    }

    private static Material material;

    /// <summary>Resources의 재료(에디터 메뉴로 만든다) — 없으면 그 자리에서 만든다.</summary>
    private static Material SharedMaterial()
    {
        if (material != null)
            return material;

        material = Resources.Load<Material>(MaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        material = new Material(shader) { name = "WeatherParticle (Runtime)" };
        SetupTransparent(material);
        return material;
    }

    /// <summary>URP 입자 재료를 반투명으로 — 에디터 생성기도 쓴다.</summary>
    public static void SetupTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetTexture("_BaseMap", SoftDot());
    }

    private static Texture2D softDot;

    private static Texture2D SoftDot()
    {
        if (softDot != null)
            return softDot;

        const int n = 32;
        softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0f, 1f, a) * 255f));
        }
        softDot.SetPixels32(pixels);
        softDot.Apply();
        return softDot;
    }

    public static Texture2D SoftDotTexture() => SoftDot();
}
