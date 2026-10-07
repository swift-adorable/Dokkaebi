/// <summary>
/// 【고목 뿌리 샘가】 소굴 안의 샘. 호리병 물을 떠 간다 (결정 2-73).
///
///   · 파밍을 한 번 다녀올 때마다(한 밤) **8개**로 차오른다. 떠 가지 않은 물은 쌓이지 않고 넘친다.
///   · 떠 간 호리병 물은 마셔도 되고(수분 20), 부뚜막에서 요리의 물로 써도 된다.
///   · 하룻밤 쉬기([미구현])가 생기면 그때도 차오른다.
/// 개수는 [임시값].
/// </summary>
public static class SpringTable
{
    public const string Name = "샘가";

    /// <summary>호리병 물 — 옛 「샘물」 · 들판의 「맑은 물」(water_bottle)을 합쳤다.</summary>
    public const string WaterId = "con_water";

    /// <summary>한 밤에 차오르는 개수.</summary>
    public const int PerNight = 8;
}

public enum SpringError
{
    None = 0,

    /// <summary>오늘 밤 샘이 말랐다 — 다음 파밍을 다녀오면 다시 찬다.</summary>
    Dry = 1,

    BagFull = 2
}

/// <summary>샘에 남은 물. MonoBehaviour 없는 순수 클래스 — EditMode 테스트 대상.</summary>
public sealed class SpringState
{
    public int Remaining { get; private set; } = SpringTable.PerNight;

    public void Refill() => Remaining = SpringTable.PerNight;

    /// <summary>세이브에서 되살린다. 범위 밖이면 잘라 낸다.</summary>
    public void Restore(int saved)
        => Remaining = saved < 0 ? 0 : saved > SpringTable.PerNight ? SpringTable.PerNight : saved;

    /// <summary>한 병 떠서 가방에 넣는다.</summary>
    public SpringError Draw(Inventory bag, ItemDefinition water)
    {
        if (Remaining <= 0)
            return SpringError.Dry;

        if (bag == null || water == null || !bag.CanAdd(water, 1))
            return SpringError.BagFull;

        bag.TryAdd(water, 1);
        Remaining--;
        return SpringError.None;
    }

    public static string Explain(SpringError error)
    {
        switch (error)
        {
            case SpringError.Dry:     return "샘이 말랐다. 파밍을 다녀오면 다시 차오른다.";
            case SpringError.BagFull: return "가방에 자리가 없다.";
            default:                  return string.Empty;
        }
    }
}
