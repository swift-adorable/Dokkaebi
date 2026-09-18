using System.Collections.Generic;
using Random = System.Random;

/// <summary>
/// 전리품 추첨. 난수원을 인자로 받으므로 고정 시드로 결정적 검증이 가능하다.
///
/// MonoBehaviour 의존이 없는 순수 정적 클래스다. EditMode 테스트 대상.
/// </summary>
public static class LootRoller
{
    /// <summary>표의 가중치 합. 0이면 아무것도 나오지 않는다.</summary>
    public static int TotalWeight(IReadOnlyList<LootEntry> entries)
    {
        if (entries == null)
            return 0;

        int total = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsValid)
                total += entries[i].weight;
        }

        return total;
    }

    /// <summary>한 줄을 가중치로 뽑는다. 뽑을 것이 없으면 false.</summary>
    public static bool TryPick(IReadOnlyList<LootEntry> entries, Random random, out LootEntry picked)
    {
        picked = default;

        int total = TotalWeight(entries);

        if (total <= 0)
            return false;

        random ??= new Random();

        int roll = random.Next(total);

        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].IsValid)
                continue;

            roll -= entries[i].weight;

            if (roll >= 0)
                continue;

            picked = entries[i];

            return true;
        }

        // 부동소수가 아니라 정수 누산이므로 여기 도달하지 않는다. 방어적으로만 둔다.
        return false;
    }

    /// <summary>
    /// rolls번 뽑아 컨테이너에 담는다. 실제로 담은 칸 수를 돌려준다.
    /// 칸이 차면 더 담지 않는다. 「빈손」 줄은 칸을 쓰지 않는다.
    /// </summary>
    public static int Roll(
        IReadOnlyList<LootEntry> entries, int rolls, Random random, LootContainer into)
    {
        if (into == null || rolls <= 0)
            return 0;

        random ??= new Random();

        int placed = 0;

        for (int i = 0; i < rolls && !into.IsFull; i++)
        {
            if (!TryPick(entries, random, out LootEntry entry))
                break;

            if (entry.IsEmptyRoll)
                continue;

            int count = entry.ClampedMin == entry.ClampedMax
                ? entry.ClampedMin
                : random.Next(entry.ClampedMin, entry.ClampedMax + 1);

            if (into.TryPut(entry.item, count))
                placed++;
        }

        return placed;
    }
}
