using System.Collections.Generic;

/// <summary>
/// 도감 — 「이 인자가 존재한다는 것을 안다」는 영구 기록.
/// (docs/Blob_Skill_System.md 11-2절)
///
/// 【도감은 보유 목록이 아니다.】 해금 기록이다.
///   도감에 있다  →  드랍 풀에 들어온다 · 제작할 수 있다.  영구.
///   인자 실물     →  가방에 든 아이템.                    죽으면 잃는다.
///
/// 덕코프의 설계도와 같은 구조다. 설계도는 영구, 만든 물건은 죽으면 잃는다. [확인됨]
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public class SkillCodex
{
    private readonly HashSet<string> unlocked = new();

    public int Count => unlocked.Count;

    public IReadOnlyCollection<string> UnlockedIds => unlocked;

    public bool IsUnlocked(string id)
    {
        return !string.IsNullOrEmpty(id) && unlocked.Contains(id);
    }

    public bool IsUnlocked(SkillDefinition definition)
    {
        return definition != null && IsUnlocked(definition.Id);
    }

    /// <summary>해금한다. 이미 해금돼 있었으면 false.</summary>
    public bool Unlock(SkillDefinition definition)
    {
        return definition != null && unlocked.Add(definition.Id);
    }

    /// <summary>세이브 복원용. id 문자열로 직접 해금한다.</summary>
    public bool Unlock(string id)
    {
        return !string.IsNullOrEmpty(id) && unlocked.Add(id);
    }

    /// <summary>
    /// 해금된 것만 골라 드랍 풀을 만든다.
    ///
    /// 요구 레벨로 거르지 않는다. 11-3절 — 각성 레벨에 미달하는 인자도
    /// 【주울 수는 있다】. 끼우지 못할 뿐이다. 그래야 "레벨을 올려야 끼운다"는
    /// 압박이 생기고, 소켓 개방이 레벨업 보상으로 성립한다.
    /// </summary>
    public List<SkillDefinition> BuildDropPool(
        IReadOnlyList<SkillDefinition> all, List<SkillDefinition> result = null)
    {
        result ??= new List<SkillDefinition>();
        result.Clear();

        if (all == null)
            return result;

        for (int i = 0; i < all.Count; i++)
        {
            SkillDefinition definition = all[i];

            if (definition == null || !IsUnlocked(definition))
                continue;

            result.Add(definition);
        }

        return result;
    }

    /// <summary>전부 해금한다. 세이브가 붙기 전의 임시 경로이자 테스트 도우미다.</summary>
    public void UnlockAll(IReadOnlyList<SkillDefinition> all)
    {
        if (all == null)
            return;

        for (int i = 0; i < all.Count; i++)
            Unlock(all[i]);
    }

    public void Clear() => unlocked.Clear();
}
