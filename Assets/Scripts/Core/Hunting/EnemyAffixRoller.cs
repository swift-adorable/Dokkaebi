using System;
using System.Collections.Generic;

/// <summary>
/// 등급에 맞춰 속성을 뽑는다. (docs/Dokkaebi_Hunting_System.md 2·3절)
///
/// 【System.Random을 받는 이유】
/// 같은 씨앗이면 같은 결과가 나와야 테스트가 성립한다.
/// UnityEngine.Random은 전역 상태라 EditMode에서 재현이 안 된다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다.
/// </summary>
public static class EnemyAffixRoller
{
    /// <summary>
    /// 속성을 뽑아 into에 채운다. 기존 내용은 지운다.
    /// </summary>
    /// <returns>실제로 붙은 개수. 조합 금지에 걸리면 요청보다 적을 수 있다.</returns>
    public static int Roll(EnemyRarity rarity, Random random, List<EnemyAffix> into)
    {
        if (into == null)
            return 0;

        into.Clear();

        if (random == null)
            return 0;

        EnemyRarityTable.AffixRange(rarity, out int min, out int max);

        if (max <= 0)
            return 0;

        int want = random.Next(min, max + 1);

        if (want <= 0)
            return 0;

        // 후보를 섞어서 앞에서부터 담는다. 매번 무작위 인덱스를 다시 뽑으면
        // 이미 고른 것을 계속 다시 만나 몇 번 돌지 알 수 없다.
        List<EnemyAffix> pool = BuildPool();

        Shuffle(pool, random);

        for (int i = 0; i < pool.Count && into.Count < want; i++)
        {
            if (EnemyAffixTable.CanAdd(into, pool[i]))
                into.Add(pool[i]);
        }

        return into.Count;
    }

    /// <summary>지금 붙일 수 있는 속성만 모은다. (「과민」은 빠진다)</summary>
    private static List<EnemyAffix> BuildPool()
    {
        IReadOnlyList<EnemyAffix> every = EnemyAffixTable.Every;

        var pool = new List<EnemyAffix>(every.Count);

        for (int i = 0; i < every.Count; i++)
        {
            if (EnemyAffixTable.IsEnabled(every[i]))
                pool.Add(every[i]);
        }

        return pool;
    }

    /// <summary>피셔-예이츠. 뒤에서부터 한 번만 훑는다.</summary>
    private static void Shuffle(IList<EnemyAffix> list, Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);

            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
