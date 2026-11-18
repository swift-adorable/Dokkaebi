using UnityEngine;

/// <summary>장 하나의 빛깔 (결정 2-144).</summary>
public readonly struct ChapterMood
{
    public readonly string Name;
    /// <summary>해(방향광) 색 · 세기 · 그림자 짙기.</summary>
    public readonly Color Sun;
    public readonly float SunIntensity;
    public readonly float ShadowStrength;
    /// <summary>주변광 — 하늘 · 수평 · 땅.</summary>
    public readonly Color AmbientSky, AmbientEquator, AmbientGround;
    /// <summary>후처리 — 색온도(− 차갑게 · + 따뜻하게) · 채도 · 대비 · 노출.</summary>
    public readonly float Temperature, Saturation, Contrast, Exposure;
    /// <summary>나눠 칠하기 — 어두운 곳 · 밝은 곳에 다른 빛깔. Balance −는 어두운 쪽을 넓게.</summary>
    public readonly Color Shadows, Highlights;
    public readonly float Balance;
    public readonly float Vignette;

    public ChapterMood(string name, Color sun, float sunIntensity, float shadowStrength,
                       Color sky, Color equator, Color ground,
                       float temperature, float saturation, float contrast, float exposure,
                       Color shadows, Color highlights, float balance, float vignette)
    {
        Name = name; Sun = sun; SunIntensity = sunIntensity; ShadowStrength = shadowStrength;
        AmbientSky = sky; AmbientEquator = equator; AmbientGround = ground;
        Temperature = temperature; Saturation = saturation; Contrast = contrast; Exposure = exposure;
        Shadows = shadows; Highlights = highlights; Balance = balance; Vignette = vignette;
    }
}

/// <summary>
/// 【장마다 빛깔】 (결정 2-144 · 2026-11-18 사용자 「색감 통일 1단계」 · 「도깨비의 세계」 159장을 한 장씩 본 결과)
/// 참고 장면의 공통 — 밤이 절반(79/159): 짙은 남색 · 청록 바탕(#1a2030 ~ #2a3a6a)에 따뜻한 빛은 횃불 · 등불처럼 작은 점으로만.
/// 낮은 황토 흙(#8a6a48 ~ #b07a50) + 짙은 초록(#3a5a2a). 물은 늘 차가운 청록으로 대비. 진한 색은 좁게만.
/// 그래서 장마다 「어두운 곳의 빛깔(나눠 칠하기 그림자)」과 「밝은 곳의 빛깔」을 따로 준다 — 그림자는 차갑게, 빛은 따뜻하게가 기본.
/// 본문 장소: 0 대숲 밤길 · 1 비 오는 폐장터(겨울) · 2 대숲과 물레방아 · 3 약방골과 주작의 가마(여름) · 4 장승 벌판(가을) · 5 궁궐 문(겨울) · 6 꽃밭(결계 가장자리).
/// 날씨 빛깔(FogOverlayUI)은 그 위에 그대로 얹힌다. 소굴은 BunkerMood가 따로 맡는다. 수치는 [임시값] — 전후 화면을 나란히 보며 맞춘다.
/// </summary>
public static class ChapterMoodTable
{
    private static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;

    // 【1차 시험 뒤 고침】 처음 값은 너무 셌다 — 0장이 새까매 주인공이 안 보였고, 3장은 온통 주황, 5장은 짙은 남색, 물은 회색으로 죽었다.
    // 그래서 ① 노출은 깎지 않는다(밤도 형태가 보이게 — 어둠은 그림자 빛깔로만) ② 나눠 칠하기 색은 회색 쪽으로 반쯤 섞어 약하게
    // ③ 색온도는 ±8 안쪽 ④ 해 세기는 1.1 ~ 1.3.
    private static readonly ChapterMood[] moods =
    {
        // 0 대숲 밤길 — 밤 숲: 그림자만 남색으로, 형태는 보이게.
        new("대숲 밤길", C("#d4dcff"), 1.15f, 0.55f, C("#6a7aa0"), C("#4e5a5c"), C("#2e2a26"),
            -8f, -4f, 12f, 0.00f, C("#5a6a8c"), C("#c8b090"), -20f, 0.26f),
        // 1 비 오는 폐장터(겨울) — 차가운 해질녘: 눈에 푸른 그림자(회색 안개 X).
        new("비 오는 폐장터", C("#e6ecff"), 1.20f, 0.70f, C("#8e9cba"), C("#6c7482"), C("#44403c"),
            -8f, 4f, 16f, 0.00f, C("#64789c"), C("#d8c0a0"), -10f, 0.22f),
        // 2 대숲과 물레방아 — 맑은 낮: 황토 흙 · 짙은 초록 · 물은 청록 그대로.
        new("대숲과 물레방아", C("#fff4e0"), 1.30f, 0.62f, C("#b0bccc"), C("#82826c"), C("#4e4232"),
            3f, 4f, 10f, 0.00f, C("#5a7a74"), C("#d8c8a8"), 15f, 0.18f),
        // 3 약방골과 주작의 가마(여름) — 따뜻한 해질녘, 그림자는 보랏빛.
        new("약방골과 가마", C("#ffe2c0"), 1.25f, 0.65f, C("#9a90a4"), C("#86705e"), C("#46342a"),
            6f, 2f, 12f, 0.00f, C("#6a5a7c"), C("#e0b890"), 5f, 0.20f),
        // 4 장승 벌판(가을) — 금빛 해질녘.
        new("장승 벌판", C("#ffecc8"), 1.25f, 0.60f, C("#a4a4b0"), C("#8e7c64"), C("#4c3e2e"),
            4f, 2f, 10f, 0.00f, C("#6a5e74"), C("#dcc090"), 10f, 0.18f),
        // 5 궁궐 문(겨울) — 차가운 밤 궁궐: 쪽빛 그림자, 나무 · 사람은 보이게.
        new("궁궐 문", C("#dce2ff"), 1.15f, 0.60f, C("#76829e"), C("#5a6070"), C("#34323a"),
            -8f, 0f, 14f, 0.00f, C("#5a6890"), C("#d4ccbc"), -15f, 0.24f),
        // 6 꽃밭(결계 가장자리) — 새벽 꿈빛: 그림자는 보랏빛, 빛은 엷은 분홍.
        new("꽃밭", C("#fff0f4"), 1.25f, 0.55f, C("#b4aed0"), C("#8e8292"), C("#423646"),
            2f, 4f, 8f, 0.00f, C("#6e6290"), C("#e8c8c0"), 5f, 0.18f),
    };

    public static int Count => moods.Length;

    /// <summary>그 장의 빛깔. 표 밖이면 0장.</summary>
    public static ChapterMood For(int chapter) => moods[Mathf.Clamp(chapter, 0, moods.Length - 1)];
}
