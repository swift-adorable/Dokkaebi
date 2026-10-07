using System;
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
    public static int TotalWeight(IReadOnlyList<LootEntry> entries, Func<ItemDefinition, bool> allow = null,
                                  bool includeEmpty = true)
    {
        if (entries == null)
            return 0;

        int total = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (Allowed(entries[i], allow, includeEmpty))
                total += entries[i].weight;
        }

        return total;
    }

    /// <summary>
    /// 빈손을 다시 뽑을 확률 — 무언가 나올 확률 q를 q × bonus로 만든다 (1을 넘지 않는다).
    /// 빈손 확률 (1 − q) 중 p를 되돌리면 q + (1 − q)p = q × bonus → p = q(bonus − 1) / (1 − q).
    /// </summary>
    public static double RerollChance(IReadOnlyList<LootEntry> entries, Func<ItemDefinition, bool> allow, float bonus)
    {
        int total = TotalWeight(entries, allow);
        int items = TotalWeight(entries, allow, includeEmpty: false);

        if (total <= 0 || items <= 0 || items >= total || bonus <= 1f)
            return 0d;

        double q = (double)items / total;
        return Math.Min(1d, q * (bonus - 1d) / (1d - q));
    }

    /// <summary>
    /// 이번 판에 뽑을 수 있는 줄인가. allow가 막은 줄은 표에서 빠진 것처럼 친다 —
    /// 요리 재료는 그 장에서만 나온다(IngredientTable.DropsIn · 결정 2-73). 빈손 줄은 늘 남는다.
    /// </summary>
    private static bool Allowed(LootEntry entry, Func<ItemDefinition, bool> allow, bool includeEmpty = true)
        => entry.IsValid && (entry.IsEmptyRoll ? includeEmpty : allow == null || allow(entry.item));

    /// <summary>한 줄을 가중치로 뽑는다. 뽑을 것이 없으면 false.</summary>
    public static bool TryPick(IReadOnlyList<LootEntry> entries, Random random, out LootEntry picked,
                               Func<ItemDefinition, bool> allow = null, bool includeEmpty = true)
    {
        picked = default;

        int total = TotalWeight(entries, allow, includeEmpty);

        if (total <= 0)
            return false;

        random ??= new Random();

        int roll = random.Next(total);

        for (int i = 0; i < entries.Count; i++)
        {
            if (!Allowed(entries[i], allow, includeEmpty))
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
    /// rolls번 뽑아 상자에 담는다. 실제로 담은 칸 수를 돌려준다.
    /// 칸이 차면 더 담지 않는다. 「빈손」 줄은 칸을 쓰지 않는다.
    /// </summary>
    public static int Roll(
        IReadOnlyList<LootEntry> entries, int rolls, Random random, LootContainer into,
        Func<ItemDefinition, bool> allow = null, float findBonus = 1f)
    {
        if (into == null || rolls <= 0)
            return 0;

        random ??= new Random();

        int placed = 0;

        for (int i = 0; i < rolls && !into.IsFull; i++)
        {
            if (!TryPick(entries, random, out LootEntry entry, allow))
                break;

            // 패시브 「희귀 드롭」 — 빈손이 나온 판의 일부를 다시 뽑아 「무언가 나올 확률」을 findBonus배로 만든다.
            if (entry.IsEmptyRoll && findBonus > 1f
                && random.NextDouble() < RerollChance(entries, allow, findBonus)
                && TryPick(entries, random, out LootEntry again, allow, includeEmpty: false))
                entry = again;

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
