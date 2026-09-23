using UnityEngine;

/// <summary>왜 쓸 수 없었는가.</summary>
public enum ConsumableError
{
    None = 0,

    /// <summary>소모품이 아니다.</summary>
    NotConsumable = 1,

    /// <summary>분류만 있고 값이 비어 있다. (강화·저항 — 아직 축이 없다)</summary>
    NoEffect = 2,

    /// <summary>이미 가득하거나, 풀 상태가 걸려 있지 않다. 써도 버리는 셈이다.</summary>
    NothingToDo = 3,

    /// <summary>죽어 있다.</summary>
    Dead = 4,

    /// <summary>내구도를 다 썼다. 빈 구급상자다.</summary>
    Empty = 5
}

/// <summary>
/// 쓰기 직전의 내 상태. 【MonoBehaviour를 모르게 두려고 값으로 받는다.】
/// 이 덕분에 소모품 판정 전체를 EditMode에서 돌릴 수 있다.
/// </summary>
public readonly struct ConsumableSubject
{
    public readonly int Health;
    public readonly int MaxHealth;
    public readonly bool IsDead;

    public readonly float Water;
    public readonly float MaxWater;
    public readonly float Energy;
    public readonly float MaxEnergy;

    /// <summary>이 소모품이 푸는 상태가 실제로 걸려 있는가.</summary>
    public readonly bool HasCureTarget;

    /// <summary>
    /// 지금 이 칸에 남은 내구도. 내구도가 없는 물건이면 그냥 0을 넣는다 —
    /// 판정은 UseCost가 0인지로 갈리므로 이 값을 보지 않는다.
    /// </summary>
    public readonly int Durability;

    public ConsumableSubject(int health, int maxHealth, bool isDead,
                             float water, float maxWater,
                             float energy, float maxEnergy,
                             bool hasCureTarget, int durability = 0)
    {
        Durability = durability;
        Health = health;
        MaxHealth = maxHealth;
        IsDead = isDead;
        Water = water;
        MaxWater = maxWater;
        Energy = energy;
        MaxEnergy = maxEnergy;
        HasCureTarget = hasCureTarget;
    }
}

/// <summary>실제로 무엇이 일어나는가. 적용 전에 전부 정해 둔다.</summary>
public readonly struct ConsumableOutcome
{
    public readonly ConsumableError Error;

    /// <summary>실제로 들어갈 회복량. 이미 가득한 만큼은 깎여 있다.</summary>
    public readonly int Heal;

    /// <summary>덜어 낼 중첩 수. Cure가 None이면 0이다.</summary>
    public readonly int CureStacks;

    /// <summary>
    /// 이번에 닳을 내구도. 0이면 아이템 하나가 통째로 없어진다는 뜻이다.
    /// </summary>
    public readonly int UseCost;

    public readonly float Water;
    public readonly float Energy;
    public readonly float WaterCost;
    public readonly float EnergyCost;

    public readonly StatusEffectType Cure;

    /// <summary>걸릴 이로운 상태. None이면 없다.</summary>
    public readonly StatusEffectType Grant;

    public bool Ok => Error == ConsumableError.None;

    public ConsumableOutcome(ConsumableError error, int heal = 0,
                             float water = 0f, float energy = 0f,
                             float waterCost = 0f, float energyCost = 0f,
                             StatusEffectType cure = StatusEffectType.None,
                             int cureStacks = 0, int useCost = 0,
                             StatusEffectType grant = StatusEffectType.None)
    {
        Grant = grant;
        CureStacks = cureStacks;
        UseCost = useCost;
        Error = error;
        Heal = heal;
        Water = water;
        Energy = energy;
        WaterCost = waterCost;
        EnergyCost = energyCost;
        Cure = cure;
    }
}

/// <summary>
/// 소모품 하나를 쓰면 무슨 일이 일어나는지 계산한다.
/// MonoBehaviour 의존이 없는 순수 클래스다.
/// (docs/Blob_Consumable_System.md)
///
/// 【적용과 계산을 나누는 이유】 「이미 체력이 가득한데 구급상자를 썼다」가
/// 가장 흔한 사고다. 계산을 먼저 끝내 두면 그 경우를 쓰기 전에 막을 수
/// 있고, 같은 판정을 화면(회색 처리)과 실행이 나눠 쓸 수 있다.
/// </summary>
public static class ConsumableUse
{
    /// <summary>
    /// 쓸 수 있는지, 쓰면 얼마가 들어가는지.
    ///
    /// 【대가는 한 푼도 깎지 않는다.】 수분이 5밖에 없는데 대가가 15면
    /// 0까지만 깎이고 탈수에 걸린다. 모자라다고 사용을 막지는 않는다 —
    /// 덕코프의 주사약이 그렇고, 「무리해서 쓴다」가 선택지여야 한다.
    /// </summary>
    public static ConsumableOutcome Evaluate(ItemDefinition definition, in ConsumableSubject subject)
    {
        if (definition == null || definition.Kind != ItemKind.Consumable)
            return new ConsumableOutcome(ConsumableError.NotConsumable);

        ConsumableEffect effect = definition.Consumable;

        if (effect == null || effect.IsEmpty)
            return new ConsumableOutcome(ConsumableError.NoEffect);

        if (subject.IsDead)
            return new ConsumableOutcome(ConsumableError.Dead);

        // 빈 구급상자. 무게만 먹고 있으므로 버리라고 말해 주는 편이 낫다.
        if (effect.Charged && subject.Durability < effect.UseCost)
            return new ConsumableOutcome(ConsumableError.Empty);

        int heal = Mathf.Clamp(effect.Heal, 0, Mathf.Max(0, subject.MaxHealth - subject.Health));

        float water = Mathf.Clamp(effect.Water, 0f, Mathf.Max(0f, subject.MaxWater - subject.Water));
        float energy = Mathf.Clamp(effect.Energy, 0f, Mathf.Max(0f, subject.MaxEnergy - subject.Energy));

        bool cures = effect.Cures && subject.HasCureTarget;

        // 【이로운 상태는 늘 성립한다.】 이미 걸려 있어도 다시 걸면
        // 지속시간이 처음으로 돌아간다 — 그것이 「덮어쓴다」(문서 6절)이다.
        bool grants = effect.Grants;

        // 【들어갈 것이 하나도 없으면 쓰지 않는다.】 가방에서 한 개가
        // 사라졌는데 아무 일도 안 일어나면 그것은 버린 것이지 쓴 것이 아니다.
        if (heal == 0 && water <= 0f && energy <= 0f && !cures && !grants)
            return new ConsumableOutcome(ConsumableError.NothingToDo);

        return new ConsumableOutcome(ConsumableError.None, heal, water, energy,
            effect.WaterCost, effect.EnergyCost,
            cures ? effect.Cure : StatusEffectType.None,
            cures ? effect.CureStacks : 0,
            effect.UseCost,
            effect.Grant);
    }

    /// <summary>화면과 결과 줄이 함께 쓰는 한국어 사유.</summary>
    public static string Explain(ConsumableError error)
    {
        switch (error)
        {
            case ConsumableError.None:          return "사용했습니다.";
            case ConsumableError.NotConsumable: return "쓸 수 있는 물건이 아닙니다.";
            case ConsumableError.NoEffect:      return "아직 효과가 들어 있지 않습니다.";
            case ConsumableError.NothingToDo:   return "지금은 써도 채울 것이 없습니다.";
            case ConsumableError.Dead:          return "죽어 있어 쓸 수 없습니다.";
            case ConsumableError.Empty:         return "다 썼습니다. 빈 통입니다.";
            default:                            return "쓸 수 없습니다.";
        }
    }
}
