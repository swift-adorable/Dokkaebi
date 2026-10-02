using System;
using UnityEngine;

/// <summary>
/// 속성 저항 배율 묶음. 1.0이 기본이고, 0.5면 절반만 받고, 2.0이면 두 배로 받는다.
///
/// MonoBehaviour 의존이 없는 순수 구조체다. EditMode 테스트 대상.
/// (docs/Dokkaebi_Combat_Baseline.md 3-2절)
/// </summary>
[Serializable]
public struct ElementalResistances
{
    public float physical;
    public float fire;
    public float cold;
    public float lightning;
    public float chaos;

    /// <summary>전부 1.0인 기본값. 구조체의 기본값(전부 0)과 다르므로 반드시 이걸 쓴다.</summary>
    public static ElementalResistances Default => new ElementalResistances
    {
        physical = 1f, fire = 1f, cold = 1f, lightning = 1f, chaos = 1f
    };

    /// <summary>기계형 — 번개 2배, 카오스 완전 면역.</summary>
    public static ElementalResistances Mechanical
    {
        get
        {
            ElementalResistances r = Default;
            r.lightning = 2f;
            r.chaos = 0f;
            return r;
        }
    }

    /// <summary>허깨비 · 무주귀 — 물리 0.66배, 화염 1.5배.</summary>
    public static ElementalResistances Settled
    {
        get
        {
            ElementalResistances r = Default;
            r.physical = 0.66f;
            r.fire = 1.5f;
            return r;
        }
    }

    public float Get(DamageElement element)
    {
        switch (element)
        {
            case DamageElement.Fire: return fire;
            case DamageElement.Cold: return cold;
            case DamageElement.Lightning: return lightning;
            case DamageElement.Chaos: return chaos;
            default: return physical;
        }
    }

    public void Set(DamageElement element, float value)
    {
        switch (element)
        {
            case DamageElement.Fire: fire = value; break;
            case DamageElement.Cold: cold = value; break;
            case DamageElement.Lightning: lightning = value; break;
            case DamageElement.Chaos: chaos = value; break;
            default: physical = value; break;
        }
    }

    /// <summary>
    /// 한 속성에 저항 속성 하나를 얹는다. 나머지 속성은 건드리지 않는다.
    ///
    /// 【TakeLowest로 대신할 수 없다.】
    /// 몬스터 속성 「경화」는 물리 하나만 0.5로 만든다. 이걸 구현하려고
    /// Default(전부 1.0)를 만들어 TakeLowest에 넣으면, 손대지 않아야 할
    /// 다른 속성까지 1.0으로 눌린다 — 절굿공이귀의 전기 2배, 허깨비의 화염 1.5배가
    /// 속성 하나 붙었다는 이유로 사라진다. 유형이 요구하던 답이 지워진다.
    ///
    /// TakeLowest는 【묶음 대 묶음】(레이드 특성 vs 개체)을 겹칠 때 쓴다.
    /// 이 함수는 【한 속성】을 겹칠 때 쓴다. 둘은 다른 연산이다.
    /// </summary>
    public void LowerTo(DamageElement element, float multiplier)
    {
        float current = Get(element);

        // 【약점을 저항으로 뒤집지 않는다.】
        // 그냥 multiplier로 덮으면 절굿공이귀의 전기 2배가 0.5배가 되어,
        // 「얘는 전기로 잡아라」라는 유형의 답이 속성 하나로 사라진다.
        // 전기 들고 간 사람에게는 공략법이 없어지는 것과 같다.
        //
        // 대신 곱하고, multiplier를 바닥으로 삼는다.
        //   전기 2.0  + 절연성 → 1.0  (약점이 줄 뿐 사라지지 않는다)
        //   물리 1.0  + 경화   → 0.5  (기존과 같다)
        //   물리 0.66 + 경화   → 0.5  (0.33이 되지 않는다 — 곱연산 금지)
        //   이미 0.4  + 경화   → 0.4  (더 약해지지 않는다)
        //
        // 마지막 Min이 없으면 이미 잘 저항하던 적이 속성을 얻고
        // 오히려 물러지는 일이 생긴다.
        float softened = Mathf.Min(current, Mathf.Max(current * multiplier, multiplier));

        Set(element, softened);
    }

    /// <summary>
    /// 저항을 겹칠 때는 곱하지 않고 【가장 낮은 배율 하나만】 남긴다.
    ///
    /// 곱연산으로 중첩하면 방어형 속성 2개만으로 공략 불가가 된다.
    /// (사냥 문서 3절 — 조합 금지 규칙과 같은 사상)
    /// </summary>
    public static ElementalResistances TakeLowest(in ElementalResistances a, in ElementalResistances b)
    {
        return new ElementalResistances
        {
            physical = a.physical < b.physical ? a.physical : b.physical,
            fire = a.fire < b.fire ? a.fire : b.fire,
            cold = a.cold < b.cold ? a.cold : b.cold,
            lightning = a.lightning < b.lightning ? a.lightning : b.lightning,
            chaos = a.chaos < b.chaos ? a.chaos : b.chaos
        };
    }
}
