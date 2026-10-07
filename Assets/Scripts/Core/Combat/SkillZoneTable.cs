using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기폭 · 잔류물 수치 (Audit A5 · A6 · 결정 2-74). 전부 [임시값] — 사용자가 고른 추천안.
///
///   · 원소 작렬 — 상태가 걸린 적이 죽거나 최대 중첩이 되면 그 상태를 소모해 터진다. 피해 = 무기 한 발 × 1.2 × 중첩 · 반경 3m
///   · 충격파 — 4초마다 플레이어 둘레 4m · 2m 밀어냄 · 잔류물과 걸린 상태를 바로 터뜨린다
///   · 중력 붕괴 — 6초마다 가장 가까운 적 자리에 우물 3초 · 반경 3m · 끌어당김 + 응집
///   · 잔류물 — 반경 2m · 4초 · 0.5초마다 안의 적에게 상태 · 한 번에 8개까지 · **플레이어가 만든 것은 적에게만**
/// </summary>
public static class SkillZoneTable
{
    public const string ElementalBurstId = "core_elemental_burst";
    public const string GravityCollapseId = "core_gravity_collapse";
    public const string ShockwaveId = "core_shockwave";

    // ── 기폭
    public const float DefaultFuse = 0.25f;
    public const float DetonationCooldown = 0.2f;

    // ── 충격파
    public const float ShockwaveInterval = 4f;
    public const float ShockwaveRadius = 4f;
    public const float ShockwavePush = 2f;
    public const float ShockwavePushSeconds = 0.2f;

    // ── 중력 붕괴
    public const float GravityInterval = 6f;
    public const float GravityRadius = 3f;
    public const float GravityDuration = 3f;
    public const float GravityPullSpeed = 4f;
    public const float GravitySearchRange = 12f;

    // ── 잔류물
    public const float GroundRadius = 2f;
    public const float GroundDuration = 4f;
    public const float GroundTick = 0.5f;
    public const int MaxGrounds = 8;

    /// <summary>잔류물 안의 적에게 0.5초마다 거는 상태. 서리 장판은 동결이 아니라 냉기(둔화 · 누적).</summary>
    public static StatusEffectType ZoneStatus(GroundEffectType ground)
        => ground == GroundEffectType.FrostField ? StatusEffectType.Chill : GroundEffectTable.AppliesStatus(ground);

    /// <summary>
    /// 기폭할 상태를 고른다 — 터질 수 있는 것 중 중첩이 가장 많은 것. 같으면 점화 · 중독 · 동결 · 감전 · 출혈 순.
    /// 없으면 None.
    /// </summary>
    public static StatusEffectType PickDetonation(StatusEffectState status)
    {
        if (status == null)
            return StatusEffectType.None;

        StatusEffectType best = StatusEffectType.None;
        int bestStacks = 0;

        foreach (StatusEffectType type in Detonatable)
        {
            int stacks = StacksFor(status, type);

            if (stacks > bestStacks)
            {
                best = type;
                bestStacks = stacks;
            }
        }

        return best;
    }

    /// <summary>터뜨릴 때 칠 중첩 — 위험 상태(부식 · 마비 · 동결)는 원래 상태의 최대 중첩으로 친다.</summary>
    public static int StacksFor(StatusEffectState status, StatusEffectType type)
    {
        if (status == null)
            return 0;

        switch (type)
        {
            case StatusEffectType.Poison when status.Has(StatusEffectType.Corrode):
                return StatusEffectTable.Get(StatusEffectType.Poison).MaxStacks;
            case StatusEffectType.Shock when status.Has(StatusEffectType.Paralyze):
                return StatusEffectTable.Get(StatusEffectType.Shock).MaxStacks;
            case StatusEffectType.Freeze when status.Has(StatusEffectType.Freeze):
                return StatusEffectTable.Get(StatusEffectType.Chill).MaxStacks;
            default:
                return status.StacksOf(type);
        }
    }

    /// <summary>
    /// 원소 작렬이 「최대 중첩」으로 터지는가. 중독 · 감전 · 냉기는 최대 중첩에 닿는 순간 위험 상태(부식 · 마비 · 동결)로
    /// 넘어가므로(StatusEffectTable.ThresholdOf), **위험 상태가 걸린 것 = 최대 중첩에 닿은 것**으로 본다.
    /// detonateAs = 터뜨릴 상태 · consume = 지울 위험 상태 · fullStacks = 원래 상태의 최대 중첩(긴 퓨즈가 전부 쓴다).
    /// </summary>
    public static bool AtMaxStacks(StatusEffectState status, out StatusEffectType detonateAs,
                                   out StatusEffectType consume, out int fullStacks)
    {
        detonateAs = consume = StatusEffectType.None;
        fullStacks = 0;

        if (status == null)
            return false;

        if (status.Has(StatusEffectType.Corrode))
        {
            detonateAs = StatusEffectType.Poison;
            consume = StatusEffectType.Corrode;
        }
        else if (status.Has(StatusEffectType.Paralyze))
        {
            detonateAs = StatusEffectType.Shock;
            consume = StatusEffectType.Paralyze;
        }
        else if (status.Has(StatusEffectType.Freeze))
        {
            detonateAs = StatusEffectType.Freeze;
            consume = StatusEffectType.Freeze;
        }
        else
        {
            return false;
        }

        StatusEffectType source = detonateAs == StatusEffectType.Freeze ? StatusEffectType.Chill : detonateAs;
        fullStacks = StatusEffectTable.Get(source).MaxStacks;
        return true;
    }

    public static readonly StatusEffectType[] Detonatable =
    {
        StatusEffectType.Ignite, StatusEffectType.Poison, StatusEffectType.Freeze,
        StatusEffectType.Shock, StatusEffectType.Bleed
    };
}

/// <summary>바닥에 남은 잔류물 하나 — 순수 클래스.</summary>
public sealed class GroundZone
{
    public readonly GroundEffectType Type;
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float SourceDamage;
    public float Remaining { get; private set; }
    private float tick;

    public GroundZone(GroundEffectType type, Vector3 position, float radius, float duration, float sourceDamage)
    {
        Type = type;
        Position = position;
        Radius = Mathf.Max(0.1f, radius);
        Remaining = Mathf.Max(0.1f, duration);
        SourceDamage = Mathf.Max(1f, sourceDamage);
        tick = 0f; // 깔리자마자 한 번 건다
    }

    public bool IsExpired => Remaining <= 0f;

    /// <summary>시간을 보낸다. 이번에 상태를 걸 차례면 true.</summary>
    public bool Advance(float deltaTime)
    {
        Remaining -= deltaTime;
        tick -= deltaTime;

        if (tick > 0f || IsExpired)
            return false;

        tick += SkillZoneTable.GroundTick;
        return true;
    }

    public void Expire() => Remaining = 0f;

    public bool Contains(Vector3 point)
    {
        Vector3 d = point - Position;
        d.y = 0f;
        return d.sqrMagnitude <= Radius * Radius;
    }
}

/// <summary>잔류물 묶음 — 한 번에 8개까지. 넘치면 가장 오래된 것부터 사라진다.</summary>
public sealed class GroundZoneSet
{
    private readonly List<GroundZone> zones = new();

    public IReadOnlyList<GroundZone> Zones => zones;
    public int Count => zones.Count;

    /// <summary>깐다. 밀려난 잔류물이 있으면 돌려준다(화면에서 지우라고).</summary>
    public GroundZone Add(GroundZone zone, out GroundZone evicted)
    {
        evicted = null;

        if (zone == null || zone.Type == GroundEffectType.None)
            return null;

        if (zones.Count >= SkillZoneTable.MaxGrounds)
        {
            evicted = zones[0];
            zones.RemoveAt(0);
        }

        zones.Add(zone);
        return zone;
    }

    public void RemoveExpired(List<GroundZone> removed)
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            if (!zones[i].IsExpired)
                continue;

            removed?.Add(zones[i]);
            zones.RemoveAt(i);
        }
    }

    public void Clear() => zones.Clear();
}
