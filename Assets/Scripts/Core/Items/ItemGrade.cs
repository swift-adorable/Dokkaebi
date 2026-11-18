using UnityEngine;

/// <summary>
/// 【물건 등급 — 가치로 매긴다】 (결정 2-142 · 2026-11-18 사용자 「덕코프 모드 중 넣을 만한 것 적용」)
/// 덕코프 최다 구독 모드 「아이템 가치 희귀도 표시」(223만)를 따랐다 — 칸에 등급 색, 뒤질 때 등급별 소리.
/// 등급(tier)이 아니라 【가치(BaseValue, 한 개 값)】로 매긴다 — tier 0이 절반이 넘어 tier로는 색이 거의 다 같다.
/// 한 개 값으로 매기는 이유: 묶음 값으로 매기면 탄 한 무더기가 금빛이 된다.
///
///   0 흰 · 1 초록 · 2 파랑 · 3 보라 · 4 금 · 5 붉은
/// [임시값] 경계는 지금 물건 277종의 가치 분포(중앙 300, 상위 10% 9000)를 보고 정했다.
/// </summary>
public static class ItemGrade
{
    public const int Count = 6;

    /// <summary>이 값 이상이면 다음 등급 [임시값].</summary>
    private static readonly int[] Floors = { 0, 100, 300, 1000, 3000, 10000 };

    private static readonly string[] Names = { "흔함", "쓸만함", "좋음", "귀함", "보배", "전설" };

    private static readonly Color[] Colors =
    {
        new(0.80f, 0.80f, 0.78f, 1f),   // 흰
        new(0.42f, 0.82f, 0.42f, 1f),   // 초록
        new(0.36f, 0.62f, 0.98f, 1f),   // 파랑
        new(0.72f, 0.45f, 0.95f, 1f),   // 보라
        new(1.00f, 0.78f, 0.25f, 1f),   // 금
        new(1.00f, 0.36f, 0.30f, 1f),   // 붉은
    };

    public static int Of(int value)
    {
        int grade = 0;
        for (int i = 1; i < Floors.Length; i++)
            if (value >= Floors[i])
                grade = i;
        return grade;
    }

    public static int Of(ItemDefinition definition) => definition == null ? 0 : Of(definition.BaseValue);

    public static Color ColorOf(int grade) => Colors[Mathf.Clamp(grade, 0, Count - 1)];

    public static string NameOf(int grade) => Names[Mathf.Clamp(grade, 0, Count - 1)];

    /// <summary>
    /// 뒤지는 데 걸리는 시간(초) — 좋은 것일수록 오래 걸린다 (덕코프와 같은 방향) [임시값].
    /// 흔함 0.35초 … 전설 1.35초. 상자 하나(1~4개)를 다 보는 데 대개 2초 안팎.
    /// </summary>
    public static float SearchSeconds(int grade) => 0.35f + 0.2f * Mathf.Clamp(grade, 0, Count - 1);
}
