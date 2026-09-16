using System;

/// <summary>
/// Mutation 태그 — v5 §4 확정 14종.
///
/// [System.Flags]인 이유: 선택창을 열 때마다 전체 풀을 태그로 필터링한다.
/// 문자열 리스트 비교는 매 선택마다 GC Alloc과 O(n*m) 비교를 만든다.
/// 비트 마스크는 비교 1회로 끝난다. (마스터 프롬프트 10-10)
///
/// 폐기된 태그: 소환수 / 지속형(공전체·설치물) / 근접. 재도입 금지. (10-5)
/// </summary>
[Flags]
public enum MutationTag
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

    /// <summary>효과범위 — 범위 피해를 준다</summary>
    AreaOfEffect = 1 << 6,

    /// <summary>지대 — 바닥에 장판을 남긴다</summary>
    Zone = 1 << 7,

    /// <summary>기폭장치 — 다른 것을 터뜨릴 수 있다</summary>
    Detonator = 1 << 8,

    /// <summary>지속시간 — 시간 개념이 있다</summary>
    Duration = 1 << 9,

    /// <summary>유지형 — Nucleus를 점유한다</summary>
    Persistent = 1 << 10,

    /// <summary>발동 — 다른 것을 트리거한다</summary>
    Trigger = 1 << 11,

    /// <summary>조건부 — 발동 조건이 있다</summary>
    Conditional = 1 << 12,

    /// <summary>청산 — 상태이상을 소모한다</summary>
    Consuming = 1 << 13
}

/// <summary>태그 비트 연산 헬퍼. 순수 함수만 두어 EditMode 테스트 대상이 된다.</summary>
public static class MutationTagExtensions
{
    /// <summary>v5 §4 확정 태그 개수. 테스트가 이 값으로 정의 누락을 감지한다.</summary>
    public const int DefinedTagCount = 14;

    /// <summary>피해 유형 5종 마스크.</summary>
    public const MutationTag ElementMask =
        MutationTag.Fire | MutationTag.Cold | MutationTag.Lightning |
        MutationTag.Chaos | MutationTag.Physical;

    /// <summary>
    /// 태그 게이팅의 핵심 판정.
    /// required가 None이면 요구 조건이 없으므로 항상 true다.
    /// </summary>
    public static bool ContainsAll(this MutationTag tags, MutationTag required)
    {
        return (tags & required) == required;
    }

    /// <summary>하나라도 겹치는지.</summary>
    public static bool ContainsAny(this MutationTag tags, MutationTag any)
    {
        return (tags & any) != MutationTag.None;
    }

    /// <summary>켜진 비트 개수. 디버그 표기와 테스트에 쓴다.</summary>
    public static int Count(this MutationTag tags)
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
