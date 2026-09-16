using System;
using System.Collections.Generic;

/// <summary>
/// 레벨업 시 제시할 Mutation 후보를 뽑는다.
///
/// 난수원을 인자로 받으므로 테스트에서 고정 시드로 결정적 검증이 가능하다.
/// </summary>
public static class MutationDraft
{
    private const int WeightCommon = 60;
    private const int WeightRare = 30;
    private const int WeightEpic = 10;

    /// <summary>등급별 추첨 가중치.</summary>
    public static int GetWeight(MutationRarity rarity)
    {
        return rarity switch
        {
            MutationRarity.Common => WeightCommon,
            MutationRarity.Rare => WeightRare,
            MutationRarity.Epic => WeightEpic,
            _ => WeightCommon
        };
    }

    /// <summary>
    /// 후보를 중복 없이 count개 뽑는다.
    ///
    /// 중첩 상한에 도달한 Mutation은 제외된다.
    /// 뽑을 수 있는 종류가 count보다 적으면 가능한 만큼만 반환한다.
    /// </summary>
    public static List<MutationDefinition> Draw(
        IReadOnlyList<MutationDefinition> catalog,
        MutationInventory inventory,
        int count,
        Random random,
        List<MutationDefinition> result = null)
    {
        result ??= new List<MutationDefinition>();
        result.Clear();

        if (catalog == null || count <= 0)
            return result;

        // 뽑을 수 있는 후보만 모은다.
        var candidates = new List<MutationDefinition>();

        for (int i = 0; i < catalog.Count; i++)
        {
            MutationDefinition definition = catalog[i];

            if (definition == null)
                continue;

            // 같은 에셋이 카탈로그에 두 번 들어가 있어도 한 번만 센다.
            if (candidates.Contains(definition))
                continue;

            if (inventory != null && !inventory.CanAdd(definition))
                continue;

            candidates.Add(definition);
        }

        random ??= new Random();

        // 가중치 추첨 후 목록에서 제거하여 중복을 막는다.
        while (result.Count < count && candidates.Count > 0)
        {
            int totalWeight = 0;

            for (int i = 0; i < candidates.Count; i++)
                totalWeight += GetWeight(candidates[i].Rarity);

            if (totalWeight <= 0)
                break;

            int roll = random.Next(totalWeight);
            int accumulated = 0;
            int pickedIndex = candidates.Count - 1;

            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += GetWeight(candidates[i].Rarity);

                if (roll < accumulated)
                {
                    pickedIndex = i;
                    break;
                }
            }

            result.Add(candidates[pickedIndex]);
            candidates.RemoveAt(pickedIndex);
        }

        return result;
    }
}
