using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 패시브 트리 에셋. 칸 목록과 화면 격자 크기를 들고 있다.
///
/// SkillCatalog과 같은 이유로 Resources에 하나만 둔다.
/// </summary>
[CreateAssetMenu(fileName = "PassiveTree", menuName = "Blob/Passive Tree")]
public class PassiveTree : ScriptableObject
{
    public const string ResourcePath = "PassiveTree";

    /// <summary>칸 에셋 폴더. 에디터 생성기가 여기에 만든다.</summary>
    public const string NodeFolder = "Assets/Data/ScriptableObjects/Passives";

    [SerializeField] private List<PassiveNode> nodes = new();

    [Tooltip("한 계열의 가로 칸 수. 계열마다 같은 격자를 쓴다.")]
    [Min(1)]
    [SerializeField] private int columns = 4;

    [Tooltip("한 계열의 세로 칸 수. 0행이 맨 아래다.")]
    [Min(1)]
    [SerializeField] private int rows = 4;

    public IReadOnlyList<PassiveNode> Nodes => nodes;
    public int Count => nodes.Count;
    public int Columns => Mathf.Max(1, columns);
    public int Rows => Mathf.Max(1, rows);

    /// <summary>그 계열의 칸만 골라 담는다.</summary>
    public List<PassiveNode> GetBranch(PassiveBranch branch, List<PassiveNode> result = null)
    {
        result ??= new List<PassiveNode>();
        result.Clear();

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].Branch == branch)
                result.Add(nodes[i]);
        }

        return result;
    }

    /// <summary>그 계열의 칸 수.</summary>
    public int CountIn(PassiveBranch branch)
    {
        int count = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].Branch == branch)
                count++;
        }

        return count;
    }

    public PassiveNode Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].Id == id)
                return nodes[i];
        }

        return null;
    }

    /// <summary>그 계열의 그 자리에 있는 칸. 없으면 null.</summary>
    public PassiveNode At(PassiveBranch branch, int column, int row)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            PassiveNode node = nodes[i];

            if (node != null && node.Branch == branch
                && node.Column == column && node.Row == row)
                return node;
        }

        return null;
    }

    public static PassiveTree Load() => Resources.Load<PassiveTree>(ResourcePath);

#if UNITY_EDITOR
    public void EditorSetNodes(List<PassiveNode> source, int columnCount, int rowCount)
    {
        nodes.Clear();

        if (source != null)
            nodes.AddRange(source);

        columns = Mathf.Max(1, columnCount);
        rows = Mathf.Max(1, rowCount);
    }
#endif
}
