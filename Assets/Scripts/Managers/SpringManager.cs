/// <summary>
/// 고목 뿌리 샘가의 물을 들고 있는다 (결정 2-73). 세이브에 담긴다(SaveData.spring).
/// 상점 재고처럼 파밍이 끝날 때(철수 · 사망) 다시 찬다.
/// </summary>
public static class SpringManager
{
    private static SpringState state;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => state = null;

    public static SpringState State => state ??= new SpringState();

    public static int Remaining => State.Remaining;

    /// <summary>【파밍이 끝났다 = 한 밤이 지났다】 — 샘이 다시 찬다.</summary>
    public static void RefillAfterRun()
    {
        State.Refill();
        GameLogger.Log($"[Spring] 샘이 다시 찼습니다 ({SpringTable.PerNight}).");
    }

    /// <summary>호리병 물 한 병을 떠서 가방에 넣는다.</summary>
    public static SpringError Draw()
    {
        ItemDefinition water = ItemCatalog.Load()?.Find(SpringTable.WaterId);
        SpringError error = State.Draw(PlayerInventory.EnsureInstance().Bag, water);

        if (error == SpringError.None)
            PlayerInventory.Instance.RefreshCapacity();

        return error;
    }

    public static int Capture() => State.Remaining;

    public static void Restore(int saved) => State.Restore(saved);

    public static void Reset() => state = null;
}
