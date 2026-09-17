using System.Collections.Generic;
using Random = System.Random;

/// <summary>
/// 레벨업 시 제시할 Skill 후보를 뽑는다. (v5 §10-9 — 3장)
///
/// 난수원을 인자로 받으므로 테스트에서 고정 시드로 결정적 검증이 가능하다.
///
/// ※ v5에는 등급(Rarity) 개념이 없다. 가중치 추첨을 쓰지 않고 균등 추첨한다.
///    희소성은 요구 레벨(Lv 1~13)이 대신한다. 레벨이 오르면서 풀이 넓어진다.
/// </summary>
public static class SkillDraft
{
    /// <summary>
    /// 후보에서 중복 없이 count개를 균등 추첨한다.
    /// 뽑을 수 있는 종류가 count보다 적으면 가능한 만큼만 반환한다.
    /// </summary>
    public static List<SkillDefinition> DrawFrom(
        IReadOnlyList<SkillDefinition> candidates,
        int count,
        Random random,
        List<SkillDefinition> result = null)
    {
        result ??= new List<SkillDefinition>();
        result.Clear();

        if (candidates == null || count <= 0)
            return result;

        // 원본을 건드리지 않기 위해 복사본에서 뽑아 제거한다.
        var pool = new List<SkillDefinition>(candidates.Count);

        for (int i = 0; i < candidates.Count; i++)
        {
            SkillDefinition definition = candidates[i];

            if (definition == null || pool.Contains(definition))
                continue;

            pool.Add(definition);
        }

        random ??= new Random();

        while (result.Count < count && pool.Count > 0)
        {
            int index = random.Next(pool.Count);

            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    /// <summary>적재 → 필터 → 추첨을 한 번에 수행한다.</summary>
    public static List<SkillDefinition> Draw(
        IReadOnlyList<SkillDefinition> loadout,
        RunSkillState state,
        int playerLevel,
        int count,
        Random random,
        List<SkillDefinition> result = null)
    {
        List<SkillDefinition> candidates =
            SkillSelectionPool.Build(loadout, state, playerLevel);

        return DrawFrom(candidates, count, random, result);
    }
}
