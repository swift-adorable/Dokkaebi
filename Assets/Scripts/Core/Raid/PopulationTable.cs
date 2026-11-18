using UnityEngine;

/// <summary>
/// 【구역에 미리 세우는 적 수】 (결정 2-140 · 2026-11-18) — 판이 열릴 때 정해진 수만큼 세워 두고, 다 쓰러뜨리면 더 나오지 않는다.
///   · 기준: 0장 0-1 넓이(약 99 × 90m)에 18마리. 장마다 +3. 넓이의 0.75제곱에 비례(결정 2-149 — 맵 2배 뒤 사용자 「적 · 상자 수 늘려줘」:
///     제곱근이면 넓이 4배에 2배뿐이라 듬성했다). 8 ~ 48마리
///   · 출발 자리 22m 안 · 길목 8m 안에는 세우지 않는다. 무리끼리 10m 띄운다
/// 수치는 [임시값] — 플레이해 보며 맞춘다.
/// </summary>
public static class PopulationTable
{
    public const float ReferenceArea = 99f * 90f;
    public const int BaseCount = 18;
    public const int PerChapter = 3;
    public const int Min = 8, Max = 48;
    /// <summary>넓이 지수 — 0.5(제곱근)면 넓은 맵이 듬성하고, 1이면 휴대폰에 너무 많다.</summary>
    public const float AreaPower = 0.75f;
    public const float MinFromStart = 22f;
    public const float MinFromExtraction = 8f;
    public const float PackGap = 10f;

    public static int Count(int chapter, float area)
        => Mathf.Clamp(Mathf.RoundToInt((BaseCount + PerChapter * Mathf.Max(0, chapter)) * Mathf.Pow(Mathf.Max(1f, area) / ReferenceArea, AreaPower)), Min, Max);
}
