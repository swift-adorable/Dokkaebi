using System.Collections.Generic;

/// <summary>배울 수 없는 이유. UI가 그대로 문구로 바꾼다.</summary>
public enum PassiveError
{
    None = 0,
    NoSuchNode,
    AlreadyLearned,

    /// <summary>선행 칸을 아직 배우지 않았다.</summary>
    MissingPrerequisite,

    /// <summary>계정 레벨이 부족하다.</summary>
    LevelTooLow,

    /// <summary>크레딧이 부족하다.</summary>
    NotEnoughCredits,

    /// <summary>필요물품이 부족하다.</summary>
    MissingMaterials,

    /// <summary>계열이 아직 발견되지 않았다. (역행)</summary>
    BranchUndiscovered
}

/// <summary>배울 수 있는지 판단하는 데 필요한 바깥 상태 전부.</summary>
public struct PassiveContext
{
    /// <summary>계정 레벨.</summary>
    public int accountLevel;

    /// <summary>보유 크레딧.</summary>
    public int credits;

    /// <summary>재료를 꺼낼 곳. null이면 재료 검사를 생략한다.</summary>
    public Inventory materials;

    /// <summary>역행 계열을 발견했는지.</summary>
    public bool discoveredRegression;

    public PassiveContext(int accountLevel, int credits,
                          Inventory materials = null, bool discoveredRegression = false)
    {
        this.accountLevel = accountLevel;
        this.credits = credits;
        this.materials = materials;
        this.discoveredRegression = discoveredRegression;
    }

    /// <summary>해당 계열이 지금 보이는지.</summary>
    public bool IsBranchVisible(PassiveBranch branch)
    {
        return branch != PassiveBranch.Regression || discoveredRegression;
    }
}

/// <summary>
/// 배운 패시브의 상태. 【계정 축의 영구 성장이다. 죽어도 잃지 않는다.】
/// (docs/Blob_Passive_System.md)
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

    /// <summary>
    /// 배울 수 있는지. 이유까지 돌려준다.
    ///
    /// 검사 순서에 뜻이 있다 —
    ///  1. 계열이 보이는가   (역행은 조우 전까지 존재 자체를 모른다)
    ///  2. 선행을 배웠는가   (「먼저 아래를 배우십시오」가 가장 쓸모 있는 안내다)
    ///  3. 레벨              (기다리면 해결된다)
    ///  4. 크레딧            (팔면 해결된다)
    ///  5. 재료              (나가서 구해야 한다 — 가장 무거운 요구라 마지막)
    /// </summary>
    public PassiveError CanLearn(PassiveNode node, in PassiveContext context)
    {
        if (node == null)
            return PassiveError.NoSuchNode;

        if (IsLearned(node))
            return PassiveError.AlreadyLearned;

        if (!context.IsBranchVisible(node.Branch))
            return PassiveError.BranchUndiscovered;

        IReadOnlyList<string> prerequisites = node.Prerequisites;

        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!IsLearned(prerequisites[i]))
                return PassiveError.MissingPrerequisite;
        }

        // 중개 계열은 레벨을 보지 않는다. 돈만 있으면 연다. (덕코프 블랙마켓과 같다)
        if (node.UnlockKind != PassiveUnlockKind.CreditsOnly &&
            context.accountLevel < node.RequiredAccountLevel)
        {
            return PassiveError.LevelTooLow;
        }

        if (context.credits < node.Cost)
            return PassiveError.NotEnoughCredits;

        if (!HasMaterials(node, context.materials))
            return PassiveError.MissingMaterials;

        return PassiveError.None;
    }

    /// <summary>필요물품을 전부 가지고 있는지. 재료 창고가 null이면 검사를 생략한다.</summary>
    public static bool HasMaterials(PassiveNode node, Inventory source)
    {
        if (node == null || !node.NeedsMaterials || source == null)
            return true;

        IReadOnlyList<PassiveMaterial> materials = node.Materials;

        for (int i = 0; i < materials.Count; i++)
        {
            if (!materials[i].IsValid)
                continue;

            if (source.CountOf(materials[i].item) < materials[i].ClampedCount)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 배운다. 실제로 든 크레딧을 돌려준다. 배우지 못하면 0.
    /// 【재료도 여기서 실제로 소모된다.】 검사와 소모가 갈라지면
    /// "검사는 통과했는데 재료가 안 빠지는" 상태가 조용히 생긴다.
    /// </summary>
    public int TryLearn(PassiveNode node, in PassiveContext context)
    {
        if (CanLearn(node, in context) != PassiveError.None)
            return 0;

        ConsumeMaterials(node, context.materials);

        learned.Add(node.Id);

        return node.Cost;
    }

    private static void ConsumeMaterials(PassiveNode node, Inventory source)
    {
        if (node == null || !node.NeedsMaterials || source == null)
            return;

        IReadOnlyList<PassiveMaterial> materials = node.Materials;

        for (int i = 0; i < materials.Count; i++)
        {
            if (materials[i].IsValid)
                source.Remove(materials[i].item, materials[i].ClampedCount);
        }
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

    /// <summary>그 계열에서 배운 칸 수. 화면의 진척 표시에 쓴다.</summary>
    public int CountIn(PassiveTree tree, PassiveBranch branch)
    {
        if (tree == null)
            return 0;

        int count = 0;

        IReadOnlyList<PassiveNode> nodes = tree.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].Branch == branch && IsLearned(nodes[i]))
                count++;
        }

        return count;
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
            case PassiveError.None:                return string.Empty;
            case PassiveError.NoSuchNode:          return "없는 항목입니다.";
            case PassiveError.AlreadyLearned:      return "이미 배웠습니다.";
            case PassiveError.MissingPrerequisite: return "선행 항목을 먼저 배워야 합니다.";
            case PassiveError.LevelTooLow:         return "계정 레벨이 부족합니다.";
            case PassiveError.NotEnoughCredits:    return "크레딧이 부족합니다.";
            case PassiveError.MissingMaterials:    return "필요물품이 부족합니다.";
            case PassiveError.BranchUndiscovered:  return "아직 발견하지 못한 계열입니다.";
            default:                               return "배울 수 없습니다.";
        }
    }
}
