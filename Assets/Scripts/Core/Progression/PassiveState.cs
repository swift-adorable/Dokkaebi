using System.Collections.Generic;

/// <summary>배울 수 없는 이유. UI가 그대로 문구로 바꾼다.</summary>
public enum PassiveError
{
    None = 0,

    /// <summary>칸이 없다.</summary>
    NoSuchNode,

    /// <summary>이미 배웠다.</summary>
    AlreadyLearned,

    /// <summary>선행 칸을 아직 배우지 않았다.</summary>
    MissingPrerequisite,

    /// <summary>계정 레벨이 부족하다.</summary>
    LevelTooLow,

    /// <summary>크레딧이 부족하다.</summary>
    NotEnoughCredits
}

/// <summary>
/// 배운 패시브의 상태. 【계정 축의 영구 성장이다. 죽어도 잃지 않는다.】
/// (docs/Blob_Progression_System.md — 각성 / 계정 2축)
///
/// 각성 레벨은 런마다 초기화되고 소켓을 연다.
/// 계정 레벨은 영구하고 패시브를 연다. 둘을 섞지 않는다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public class PassiveState
{
    private readonly HashSet<string> learned = new();

    public int Count => learned.Count;

    public IReadOnlyCollection<string> LearnedIds => learned;

    public bool IsLearned(string id) => !string.IsNullOrEmpty(id) && learned.Contains(id);

    public bool IsLearned(PassiveNode node) => node != null && IsLearned(node.Id);

    /// <summary>배울 수 있는지. 이유까지 돌려준다.</summary>
    public PassiveError CanLearn(PassiveNode node, int accountLevel, int credits)
    {
        if (node == null)
            return PassiveError.NoSuchNode;

        if (IsLearned(node))
            return PassiveError.AlreadyLearned;

        // 선행을 레벨보다 먼저 본다. 「윗칸부터 눌러 보는」 조작에서
        // "레벨이 부족합니다"보다 "먼저 아래를 배우십시오"가 더 쓸모 있는 안내다.
        IReadOnlyList<string> prerequisites = node.Prerequisites;

        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!IsLearned(prerequisites[i]))
                return PassiveError.MissingPrerequisite;
        }

        if (accountLevel < node.RequiredAccountLevel)
            return PassiveError.LevelTooLow;

        if (credits < node.Cost)
            return PassiveError.NotEnoughCredits;

        return PassiveError.None;
    }

    /// <summary>배운다. 실제로 든 비용을 돌려준다. 배우지 못하면 0.</summary>
    public int TryLearn(PassiveNode node, int accountLevel, int credits)
    {
        if (CanLearn(node, accountLevel, credits) != PassiveError.None)
            return 0;

        learned.Add(node.Id);

        return node.Cost;
    }

    /// <summary>세이브 복원용.</summary>
    public void Restore(IEnumerable<string> ids)
    {
        learned.Clear();

        if (ids == null)
            return;

        foreach (string id in ids)
        {
            if (!string.IsNullOrEmpty(id))
                learned.Add(id);
        }
    }

    /// <summary>배운 칸들이 주는 효과의 합.</summary>
    public float Total(PassiveTree tree, PassiveEffectType effect)
    {
        if (tree == null || effect == PassiveEffectType.None)
            return 0f;

        float total = 0f;

        IReadOnlyList<PassiveNode> nodes = tree.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            PassiveNode node = nodes[i];

            if (node == null || node.Effect != effect || !IsLearned(node))
                continue;

            total += node.Value;
        }

        return total;
    }

    /// <summary>해금형 효과가 켜졌는지.</summary>
    public bool HasUnlock(PassiveTree tree, PassiveEffectType effect)
    {
        if (tree == null)
            return false;

        IReadOnlyList<PassiveNode> nodes = tree.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            PassiveNode node = nodes[i];

            if (node != null && node.Effect == effect && IsLearned(node))
                return true;
        }

        return false;
    }

    public void Clear() => learned.Clear();
}

/// <summary>PassiveError를 유저에게 보여 줄 한국어 문구로 바꾼다.</summary>
public static class PassiveErrorText
{
    public static string Describe(PassiveError error)
    {
        switch (error)
        {
            case PassiveError.None:                 return string.Empty;
            case PassiveError.NoSuchNode:           return "없는 항목입니다.";
            case PassiveError.AlreadyLearned:       return "이미 배웠습니다.";
            case PassiveError.MissingPrerequisite:  return "선행 항목을 먼저 배워야 합니다.";
            case PassiveError.LevelTooLow:          return "계정 레벨이 부족합니다.";
            case PassiveError.NotEnoughCredits:     return "크레딧이 부족합니다.";
            default:                                return "배울 수 없습니다.";
        }
    }
}
