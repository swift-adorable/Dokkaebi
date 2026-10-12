using System;
using UnityEngine;

/// <summary>
/// 【플레이어가 적에게 일으킨 일】 (결정 2-102) — 발동 구슬(모였다 터지는 구슬)이 이것을 듣고 충전된다.
///
///   · Inflicted — 적에게 점화 · 동결 · 감전이 【새로】 걸렸다 (이미 걸려 있던 것을 다시 건 것은 세지 않는다)
///   · Critical  — 플레이어의 타격이 적에게 치명타로 들어갔다
///
/// 【발동 구슬이 일으킨 결과는 발동 구슬을 충전하지 않는다】 (Skill_System 7절 — 무한 루프 차단).
/// 발동 효과를 실행하는 동안 Suppress로 감싸면 그 사이의 보고는 버려진다.
/// </summary>
public static class PlayerHitEvents
{
    public static event Action<Health, StatusEffectType> Inflicted;
    public static event Action<Health> Critical;

    private static int suppressDepth;

    /// <summary>지금 발동 구슬이 효과를 실행하는 중인가 — 이 사이의 일은 충전하지 않는다.</summary>
    public static bool IsSuppressed => suppressDepth > 0;

    public static void BeginSuppress() => suppressDepth++;

    public static void EndSuppress()
    {
        if (suppressDepth > 0)
            suppressDepth--;
    }

    /// <summary>충전에 세는 상태인가 — 원소 상태 셋.</summary>
    public static bool IsTracked(StatusEffectType type)
        => type == StatusEffectType.Ignite || type == StatusEffectType.Freeze || type == StatusEffectType.Shock;

    public static void ReportInflicted(Health target, StatusEffectType type)
    {
        if (suppressDepth > 0 || !IsTracked(type))
            return;

        Inflicted?.Invoke(target, type);
    }

    public static void ReportCritical(Health target)
    {
        if (suppressDepth > 0)
            return;

        Critical?.Invoke(target);
    }

    /// <summary>도메인 다시 불러오기를 끈 에디터에서 남은 구독 · 깊이를 지운다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics()
    {
        Inflicted = null;
        Critical = null;
        suppressDepth = 0;
    }
}
