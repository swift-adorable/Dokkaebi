using UnityEngine;

/// <summary>발동 구슬을 충전하는 일.</summary>
public enum TriggerEvent
{
    None = 0,
    ShockInflicted = 1,     // 감전이 새로 걸렸다
    FreezeInflicted = 2,    // 동결이 새로 걸렸다
    IgniteInflicted = 3,    // 점화가 새로 걸렸다
    KilledIgnited = 4,      // 점화된 적을 처치했다
    Critical = 5            // 치명타가 들어갔다
}

/// <summary>
/// 【모였다 터지는 구슬 · 번지는 구슬】 (결정 2-102 · 본문 부록 「구슬의 갈래」).
///
/// 발동 구슬 다섯 — 정해진 일이 일어날 때마다 에너지가 차고, 100%가 되면 저절로 터진 뒤 0으로 돌아간다.
/// 확률로 터지지 않는다. 발동 구슬이 일으킨 결과는 충전하지 않는다 (PlayerHitEvents.Suppress).
///
///   감전 시 시전   감전을 걸 때마다 8%   → 화면 안의 감전된 적 모두에게 낙뢰
///   점화 시 시전   점화된 적 처치마다 10% → 지닌 핵심 구슬을 둘레 적 모두에게
///   동결 시 시전   동결을 걸 때마다 7%   → 둘레 적 모두 동결 · 5초 동안 동결된 적이 죽으면 옆의 적이 동결
///   치명타 시 시전 치명타마다 6%         → 치명타를 맞은 적 자리에서 지닌 핵심 구슬을 한 번 더
///   원소 상태 이상 점화 · 동결 · 감전마다 5% → 화면 안 적에게 걸린 원소 상태를 한꺼번에 기폭
///
/// 전령 다섯 — 그 상태가 걸린 적을 처치하면 그 자리에서 터져 둘레로 번진다. 터져서 죽은 적이
/// 또 그 상태였으면 다시 터진다(연쇄). 수치는 전부 [임시값].
/// </summary>
public static class TriggerGemTable
{
    public const string OnShock = "meta_cast_on_shock";
    public const string OnIgnite = "meta_cast_on_ignite";
    public const string OnFreeze = "meta_cast_on_freeze";
    public const string OnCrit = "meta_cast_on_crit";
    public const string OnAilment = "meta_cast_on_elemental_ailment";

    /// <summary>점화 시 시전 — 「둘레 적 모두」의 반경(m).</summary>
    public const float AroundRadius = 6f;

    /// <summary>치명타 시 시전 — 맞은 적 자리 둘레(m).</summary>
    public const float CritRadius = 2.5f;

    /// <summary>동결 시 시전 — 동결시키는 반경(m).</summary>
    public const float FreezeRadius = 6f;

    /// <summary>동결 시 시전 — 동결이 옮는 시간(초)과 거리(m).</summary>
    public const float FreezeSpreadSeconds = 5f;
    public const float FreezeSpreadRadius = 2.5f;

    /// <summary>감전 시 시전 — 낙뢰 한 번의 피해 = 지금 무기 한 발 × 이 값.</summary>
    public const float StrikeDamageScale = 1.5f;

    /// <summary>카메라가 없을 때 「화면 안」을 대신하는 반경(m).</summary>
    public const float ScreenFallbackRadius = 18f;

    /// <summary>전령이 터질 때까지의 틈(초) — 연쇄가 한 프레임에 몰리지 않게.</summary>
    public const float HeraldDelay = 0.1f;

    /// <summary>이 발동 구슬이 이 일에 몇 %(0~1) 차는가. 상관없는 일이면 0.</summary>
    public static float ChargePer(string metaId, TriggerEvent e)
    {
        switch (metaId)
        {
            case OnShock:   return e == TriggerEvent.ShockInflicted ? 0.08f : 0f;
            case OnIgnite:  return e == TriggerEvent.KilledIgnited ? 0.10f : 0f;
            case OnFreeze:  return e == TriggerEvent.FreezeInflicted ? 0.07f : 0f;
            case OnCrit:    return e == TriggerEvent.Critical ? 0.06f : 0f;
            case OnAilment:
                return e == TriggerEvent.IgniteInflicted || e == TriggerEvent.FreezeInflicted
                    || e == TriggerEvent.ShockInflicted ? 0.05f : 0f;
            default:        return 0f;
        }
    }

    /// <summary>새로 걸린 상태 → 충전 사건.</summary>
    public static TriggerEvent FromInflicted(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Shock:  return TriggerEvent.ShockInflicted;
            case StatusEffectType.Freeze: return TriggerEvent.FreezeInflicted;
            case StatusEffectType.Ignite: return TriggerEvent.IgniteInflicted;
            default:                      return TriggerEvent.None;
        }
    }

    // ────────────────────────────────── 전령

    /// <summary>전령이 터질 때 둘레에 거는 상태. 재 = 점화 · 얼음 = 동결 · 역병 = 중독. 천둥 · 피는 걸지 않는다.</summary>
    public static StatusEffectType HeraldSpread(StatusEffectType condition)
    {
        switch (condition)
        {
            case StatusEffectType.Ignite: return StatusEffectType.Ignite;
            case StatusEffectType.Freeze: return StatusEffectType.Freeze;
            case StatusEffectType.Poison: return StatusEffectType.Poison;
            default:                      return StatusEffectType.None;
        }
    }

    /// <summary>
    /// 전령 폭발의 중첩 — 피의 전령만 「축적된 출혈량에 비례」해 쌓인 출혈 전부를 쓴다. 나머지는 1.
    /// </summary>
    public static int HeraldStacks(StatusEffectType condition, int stacksAtDeath)
        => condition == StatusEffectType.Bleed ? Mathf.Max(1, stacksAtDeath) : 1;

    /// <summary>죽은 적에게 전령의 조건 상태가 걸려 있었는가. 감전 · 중독 · 동결은 넘어간 위험 상태(마비 · 부식)도 친다.</summary>
    public static bool HeraldMatches(StatusEffectType condition, StatusEffectState status)
    {
        if (status == null || condition == StatusEffectType.None)
            return false;

        if (status.Has(condition))
            return true;

        switch (condition)
        {
            case StatusEffectType.Shock:  return status.Has(StatusEffectType.Paralyze);
            case StatusEffectType.Poison: return status.Has(StatusEffectType.Corrode);
            default:                      return false;
        }
    }
}

/// <summary>
/// 발동 구슬 하나의 에너지. 순수 로직 — 판마다 새로 시작한다(저장하지 않는다).
/// 끼운 구슬이 바뀌면 0으로 돌아간다.
/// </summary>
public sealed class TriggerCharge
{
    public string MetaId { get; private set; }

    /// <summary>0 ~ 1.</summary>
    public float Energy { get; private set; }

    public int Percent => Mathf.Clamp(Mathf.FloorToInt(Energy * 100f + 0.0001f), 0, 100);

    /// <summary>끼운 발동 구슬을 맞춘다. 다른 구슬이면 0부터.</summary>
    public void Bind(string metaId)
    {
        if (MetaId == metaId)
            return;

        MetaId = metaId;
        Energy = 0f;
    }

    /// <summary>사건 하나를 더한다. 100%에 닿으면 true를 돌려주고 0으로 돌아간다.</summary>
    public bool Add(TriggerEvent e)
    {
        if (string.IsNullOrEmpty(MetaId))
            return false;

        float add = TriggerGemTable.ChargePer(MetaId, e);
        if (add <= 0f)
            return false;

        Energy += add;
        if (Energy < 1f - 0.0001f)
            return false;

        Energy = 0f;
        return true;
    }
}
