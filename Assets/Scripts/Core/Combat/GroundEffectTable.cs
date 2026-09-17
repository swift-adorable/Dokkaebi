/// <summary>
/// 상태이상 → 잔류물 대응표. (docs/Blob_Skill_System.md 4절)
///
/// 상태를 지닌 채 죽은 적이 바닥에 무엇을 남기는지 정한다.
/// 순수 함수만 두어 EditMode에서 검증한다.
/// </summary>
public static class GroundEffectTable
{
    /// <summary>이 상태를 지닌 채 죽으면 남는 잔류물. 없으면 None.</summary>
    public static GroundEffectType FromStatus(StatusEffectType status)
    {
        switch (status)
        {
            case StatusEffectType.Ignite: return GroundEffectType.FireZone;
            case StatusEffectType.Poison: return GroundEffectType.ToxicSwamp;
            case StatusEffectType.Freeze: return GroundEffectType.FrostField;
            case StatusEffectType.Bleed: return GroundEffectType.BloodZone;

            // 감전은 잔류물을 남기지 않는다. 증폭 전용 상태이기 때문이다.
            // 응집은 중력 붕괴 Core가 직접 우물을 만들므로 사망 시 생성이 중복이다.
            default: return GroundEffectType.None;
        }
    }

    /// <summary>진입한 대상에게 이 잔류물이 거는 상태. 없으면 None.</summary>
    public static StatusEffectType AppliesStatus(GroundEffectType ground)
    {
        switch (ground)
        {
            case GroundEffectType.FireZone: return StatusEffectType.Ignite;
            case GroundEffectType.ToxicSwamp: return StatusEffectType.Poison;
            case GroundEffectType.BloodZone: return StatusEffectType.Bleed;
            case GroundEffectType.GravityWell: return StatusEffectType.Congeal;

            // 서리 장판은 상태를 걸지 않는다. 이동 속도를 늦추고 냉기 누적을 가속할 뿐이다.
            // 동결을 바로 걸면 장판 하나로 무리 전체가 굳어 버린다.
            default: return StatusEffectType.None;
        }
    }
}
