using System;

/// <summary>
/// Skill 태그 — v5 §4 확정 14종.
///
/// [System.Flags]인 이유: 선택창을 열 때마다 전체 풀을 태그로 필터링한다.
/// 문자열 리스트 비교는 매 선택마다 GC Alloc과 O(n*m) 비교를 만든다.
/// 비트 마스크는 비교 1회로 끝난다. (마스터 프롬프트 10-10)
///
/// 폐기된 태그: 소환수 / 지속형(공전체·설치물) / 근접. 재도입 금지. (10-5)
/// </summary>
[Flags]
public enum SkillTag
{
    None = 0,

    /// <summary>투사체 — 탄환을 발사한다</summary>
    Projectile = 1 << 0,

    /// <summary>화염 — 피해 유형</summary>
    Fire = 1 << 1,

    /// <summary>냉기 — 피해 유형</summary>
    Cold = 1 << 2,

    /// <summary>번개 — 피해 유형</summary>
    Lightning = 1 << 3,

    /// <summary>카오스 — 피해 유형</summary>
    Chaos = 1 << 4,

    /// <summary>물리 — 피해 유형</summary>
    Physical = 1 << 5,

    /// <summary>효과 범위 — 범위 피해를 준다</summary>
    AreaOfEffect = 1 << 6,

    /// <summary>잔류물 — 바닥에 장판을 남긴다 (poe2db 실제 태그명)</summary>
    Zone = 1 << 7,

    /// <summary>기폭 장치 — 다른 것을 터뜨릴 수 있다</summary>
    Detonator = 1 << 8,

    /// <summary>지속시간 — 시간 개념이 있다</summary>
    Duration = 1 << 9,

    /// <summary>유지형 — 전령 계열이 가진다. 동시 장착 1개 제한을 받는다</summary>
    Persistent = 1 << 10,

    /// <summary>발동 — 다른 것을 트리거한다</summary>
    Trigger = 1 << 11,

    /// <summary>조건부 — 발동 조건이 있다</summary>
    Conditional = 1 << 12,

    /// <summary>청산 — 상태이상을 소모한다</summary>
    Consuming = 1 << 13
}

/// <summary>태그 비트 연산 헬퍼. 순수 함수만 두어 EditMode 테스트 대상이 된다.</summary>
public static class SkillTagExtensions
{
    /// <summary>v5 §4 확정 태그 개수. 테스트가 이 값으로 정의 누락을 감지한다.</summary>
    public const int DefinedTagCount = 14;

    /// <summary>피해 유형 5종 마스크.</summary>
    public const SkillTag ElementMask =
        SkillTag.Fire | SkillTag.Cold | SkillTag.Lightning |
        SkillTag.Chaos | SkillTag.Physical;

    /// <summary>
    /// 태그 게이팅의 핵심 판정.
    /// required가 None이면 요구 조건이 없으므로 항상 true다.
    /// </summary>
    public static bool ContainsAll(this SkillTag tags, SkillTag required)
    {
        return (tags & required) == required;
    }

    /// <summary>하나라도 겹치는지.</summary>
    public static bool ContainsAny(this SkillTag tags, SkillTag any)
    {
        return (tags & any) != SkillTag.None;
    }

    /// <summary>v5 §4 표기 순서대로의 한글 태그명. 인덱스는 비트 순서와 일치한다.</summary>
    private static readonly string[] KoreanNames =
    {
        "투사체", "화염", "냉기", "번개", "카오스", "물리",
        "효과 범위", "잔류물", "기폭 장치", "지속시간", "유지형", "발동", "조건부", "청산"
    };

    /// <summary>
    /// 태그를 "투사체 · 화염" 형태의 한글 문자열로 만든다.
    ///
    /// 선택창을 여는 순간에만 호출된다(게임이 멈춰 있는 시점).
    /// 매 프레임 호출되는 경로가 아니므로 문자열 할당이 문제되지 않는다.
    /// </summary>
    public static string ToKoreanString(this SkillTag tags, string separator = " · ")
    {
        if (tags == SkillTag.None)
            return string.Empty;

        var builder = new System.Text.StringBuilder(48);

        for (int i = 0; i < KoreanNames.Length; i++)
        {
            if (((int)tags & (1 << i)) == 0)
                continue;

            if (builder.Length > 0)
                builder.Append(separator);

            builder.Append(KoreanNames[i]);
        }

        return builder.ToString();
    }

    /// <summary>켜진 비트 개수. 디버그 표기와 테스트에 쓴다.</summary>
    public static int Count(this SkillTag tags)
    {
        int value = (int)tags;
        int count = 0;

        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }
}
