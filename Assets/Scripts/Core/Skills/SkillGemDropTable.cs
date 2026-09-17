using System.Collections.Generic;
using Random = System.Random;

/// <summary>
/// 인자 드랍 추첨. (docs/Blob_Skill_System.md 11-3절)
///
/// 난수원을 인자로 받으므로 고정 시드로 결정적 검증이 가능하다.
/// 등급(Rarity) 개념이 없으므로 가중치를 쓰지 않고 균등 추첨한다.
///
/// MonoBehaviour 의존이 없는 순수 정적 클래스다. EditMode 테스트 대상.
/// </summary>
public static class SkillGemDropTable
{
    /// <summary>드랍 풀에서 하나를 균등 추첨한다. 풀이 비었으면 null.</summary>
    public static SkillDefinition Draw(IReadOnlyList<SkillDefinition> pool, Random random)
    {
        if (pool == null || pool.Count == 0)
            return null;

        random ??= new Random();

        return pool[random.Next(pool.Count)];
    }

    /// <summary>
    /// 레이드 시작 직후의 확정 드랍. 【부여 계열 Core만】 나온다.
    ///
    /// 왜 부여 계열이어야 하는가 —
    /// 기폭 계열 Core는 `투사체` 태그가 없다. 그것만 손에 쥐면
    /// 투사체 Support 12종이 통째로 죽어 빌드가 서지 않는다.
    /// 부여 계열은 전부 투사체 태그를 가지므로 어떤 Support든 갈 곳이 생긴다.
    /// </summary>
    public static SkillDefinition DrawFirstCore(IReadOnlyList<SkillDefinition> pool, Random random)
    {
        var candidates = Filter(pool, SkillCategory.Core, CoreFamily.Ailment);

        return Draw(candidates, random);
    }

    /// <summary>분류·계열로 거른다. 계열이 None이면 계열 조건을 보지 않는다.</summary>
    public static List<SkillDefinition> Filter(
        IReadOnlyList<SkillDefinition> pool,
        SkillCategory category,
        CoreFamily family = CoreFamily.None,
        List<SkillDefinition> result = null)
    {
        result ??= new List<SkillDefinition>();
        result.Clear();

        if (pool == null)
            return result;

        for (int i = 0; i < pool.Count; i++)
        {
            SkillDefinition definition = pool[i];

            if (definition == null || definition.Category != category)
                continue;

            if (family != CoreFamily.None && definition.Family != family)
                continue;

            result.Add(definition);
        }

        return result;
    }
}
