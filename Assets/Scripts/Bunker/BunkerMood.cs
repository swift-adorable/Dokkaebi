using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【소굴 빛】 — 벙커(고목 아래 소굴)는 어둡게, 불 둘레만 밝게.
///   · 해(방향 빛) · 주변광을 낮추고 차갑게 — 나뭇잎 사이로 새는 빛 정도
///   · 따뜻한 불빛: 가게마다 앞 등불(BuildingSprite 가 붙인다) · 곳간 · 출발 자리. 샘은 푸른 빛, 플레이어 곁 작은 불
///   · 씬에만 바꾼다 — 장면을 다시 불러오면 원래 값으로 돌아오므로 구역 장면에는 번지지 않는다
/// 숫자는 화면을 보며 맞춘다.
/// </summary>
public static class BunkerMood
{
    public static float SunIntensity = 0.7f;
    public static Color SunColor = new(0.62f, 0.7f, 0.85f);
    public static Color AmbientSky = new(0.36f, 0.38f, 0.45f);
    public static Color AmbientEquator = new(0.29f, 0.26f, 0.23f);
    public static Color AmbientGround = new(0.15f, 0.12f, 0.10f);

    public static readonly Color Fire = new(1f, 0.68f, 0.38f);
    public static readonly Color SpringGlow = new(0.45f, 0.75f, 1f);

    private const string RootName = "Bunker Mood Lights";

    public static void Apply()
    {
        if (GameObject.Find(RootName) != null)
            return;

        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            if (l.type == LightType.Directional)
            {
                l.intensity = SunIntensity;
                l.color = SunColor;
            }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;

        var root = new GameObject(RootName).transform;
        Lamp(root, "Stash", BunkerLayout.StashPosition + new Vector3(0f, 1f, -1.5f), Fire, 7f, 8f);
        Lamp(root, "Spring", BunkerLayout.SpringPosition + new Vector3(0f, 0.8f, -0.8f), SpringGlow, 6f, 6f);
        Lamp(root, "Departure", new Vector3(0f, 1f, -BunkerLayout.RoomDepth * 0.5f + 2.5f), Fire, 7f, 7f);
        Lamp(root, "Gate", new Vector3(0f, 1f, BunkerLayout.RoomDepth * 0.5f - 2.5f), Fire, 7f, 7f);

        // 플레이어 곁 작은 불 — 어두운 소굴에서도 캐릭터가 보이게.
        var player = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        if (player != null)
            Lamp(player.transform, "Player", player.transform.position + new Vector3(0f, 1.2f, -0.6f), new Color(1f, 0.85f, 0.65f), 5f, 5f);
    }

    /// <summary>따뜻한 점 빛 하나 — 그림자 없음(그림판 그림자가 어지러워지지 않게).</summary>
    public static Light Lamp(Transform parent, string name, Vector3 at, Color color, float range, float intensity)
    {
        var go = new GameObject($"Lamp_{name}");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }
}
