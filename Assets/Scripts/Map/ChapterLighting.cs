using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 【장마다 빛깔을 건다】 (결정 2-144) — 장 맵이 뜨면 ChapterMoodTable의 그 장 빛깔로 해 · 주변광 · 후처리를 바꾼다.
/// 씬에 구운 공통 빛(SceneArtDresser)은 그대로 두고, 우선순위가 높은 전역 Volume 하나를 런타임에 얹는다 — 끄면 원래대로.
/// ZoneMap.Awake가 부른다(StoryScenery가 Start에서 해 세기를 읽기 전에). 「장 빛깔 켜기/끄기」(디버그)로 전후를 같은 자리에서 비교한다.
/// </summary>
public class ChapterLighting : MonoBehaviour
{
    public static ChapterLighting Current { get; private set; }
    public static bool Enabled { get; private set; } = true;

    private Volume volume;
    private Light sun;
    private Color baseSun;
    private float baseIntensity, baseShadow;
    private Color baseSky, baseEquator, baseGround;
    private AmbientMode baseMode;
    private ChapterMood mood;

    public static void Apply(GameObject host, int chapter)
    {
        var lighting = host.GetComponent<ChapterLighting>() ?? host.AddComponent<ChapterLighting>();
        lighting.Setup(ChapterMoodTable.For(chapter));
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    private void Setup(ChapterMood m)
    {
        Current = this;
        mood = m;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            if (l.type == LightType.Directional)
            {
                sun = l;
                break;
            }

        if (sun != null)
        {
            baseSun = sun.color;
            baseIntensity = sun.intensity;
            baseShadow = sun.shadowStrength;
        }
        baseMode = RenderSettings.ambientMode;
        baseSky = RenderSettings.ambientSkyColor;
        baseEquator = RenderSettings.ambientEquatorColor;
        baseGround = RenderSettings.ambientGroundColor;

        var go = new GameObject("ChapterMoodVolume (Runtime)");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = $"ChapterMood_{m.Name}";

        var white = profile.Add<WhiteBalance>(true);
        white.temperature.Override(m.Temperature);

        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(m.Saturation);
        color.contrast.Override(m.Contrast);
        color.postExposure.Override(m.Exposure);

        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(m.Shadows);
        split.highlights.Override(m.Highlights);
        split.balance.Override(m.Balance);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(m.Vignette);
        vignette.smoothness.Override(0.45f);

        volume.sharedProfile = profile;
        SetEnabled(Enabled);
        GameLogger.Log($"[ChapterLighting] {m.Name}");
    }

    /// <summary>켜고 끈다 — 전후 비교용(디버그). 끄면 씬에 구운 빛으로 돌아간다.</summary>
    public static void SetEnabled(bool on)
    {
        Enabled = on;
        ChapterLighting c = Current;
        if (c == null)
            return;

        c.volume.weight = on ? 1f : 0f;
        if (c.sun != null)
        {
            c.sun.color = on ? c.mood.Sun : c.baseSun;
            c.sun.intensity = on ? c.mood.SunIntensity : c.baseIntensity;
            c.sun.shadowStrength = on ? c.mood.ShadowStrength : c.baseShadow;
        }
        RenderSettings.ambientMode = on ? AmbientMode.Trilight : c.baseMode;
        RenderSettings.ambientSkyColor = on ? c.mood.AmbientSky : c.baseSky;
        RenderSettings.ambientEquatorColor = on ? c.mood.AmbientEquator : c.baseEquator;
        RenderSettings.ambientGroundColor = on ? c.mood.AmbientGround : c.baseGround;
    }
}
