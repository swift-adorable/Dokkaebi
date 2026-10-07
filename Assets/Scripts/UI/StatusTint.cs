using UnityEngine;

/// <summary>【임시 표시】 상태의 색 (결정 2-77) — 탄 · 머리 위 글자 · 잔류물이 같은 색을 쓴다. 아트 때 바꾼다.</summary>
public static class StatusTint
{
    public static Color? Of(StatusEffectType status)
    {
        switch (status)
        {
            case StatusEffectType.Ignite:   return new Color(1f, 0.45f, 0.15f);
            case StatusEffectType.Poison:
            case StatusEffectType.Corrode:  return new Color(0.45f, 0.85f, 0.25f);
            case StatusEffectType.Chill:
            case StatusEffectType.Freeze:   return new Color(0.6f, 0.85f, 1f);
            case StatusEffectType.Shock:
            case StatusEffectType.Paralyze: return new Color(1f, 0.95f, 0.35f);
            case StatusEffectType.Bleed:    return new Color(0.85f, 0.15f, 0.2f);
            case StatusEffectType.Congeal:  return new Color(0.6f, 0.4f, 0.9f);
            default:                        return null;
        }
    }
}
