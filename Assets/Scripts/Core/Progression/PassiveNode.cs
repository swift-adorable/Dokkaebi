using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배우는 데 드는 재료 한 줄. 덕코프 스킬 표의 「필요물품」 열에 해당한다. [확인됨]
///
/// 크레딧만 쓰면 시간을 들이면 전부 열린다. 결정이 없다.
/// 재료를 요구하면 "이 메모리 코어를 팔까, 패시브에 쓸까"가 생긴다.
/// 파밍이 성장에 직접 닿는다. (docs/Blob_Passive_System.md 3절)
/// </summary>
[System.Serializable]
public struct PassiveMaterial
{
    public ItemDefinition item;

    [Min(1)]
    public int count;

    public PassiveMaterial(ItemDefinition item, int count)
    {
        this.item = item;
        this.count = Mathf.Max(1, count);
    }

    public bool IsValid => item != null && count > 0;
    public int ClampedCount => Mathf.Max(1, count);
}

/// <summary>
/// 패시브 한 칸. (docs/Blob_Passive_System.md)
///
/// 선행 조건을 id 문자열로 두는 이유 —
/// 에셋 간 상호 참조를 만들면 순환 참조와 GUID 꼬임이 생긴다.
/// 이 방식이라 【계열을 넘는 선행】도 자연스럽게 표현된다
/// (덕코프의 「식이요법 = 낚시 3 + 영양 관리 3」과 같은 교차 선행). [확인됨]
/// </summary>
[CreateAssetMenu(fileName = "Passive_", menuName = "Blob/Passive Node")]
public class PassiveNode : ScriptableObject
{
    [Header("식별")]
    [SerializeField] private string id = "passive_id";
    [SerializeField] private string displayName = "이름 없음";

    [TextArea(2, 3)]
    [SerializeField] private string description = string.Empty;

    [Header("계열")]
    [SerializeField] private PassiveBranch branch = PassiveBranch.Adapt;

    [Header("효과")]
    [SerializeField] private PassiveEffectType effect = PassiveEffectType.None;

    [Tooltip("효과의 크기. 해금형(제작대·시체 회수 등)은 무시된다.")]
    [SerializeField] private float value = 1f;

    [Header("조건")]
    [Tooltip("이 계정 레벨 미만에서는 배울 수 없다. 중개 계열은 무시된다.")]
    [Min(1)]
    [SerializeField] private int requiredAccountLevel = 1;

    [Tooltip("배우는 데 드는 크레딧.")]
    [Min(0)]
    [SerializeField] private int cost = 500;

    [Tooltip("배우는 데 드는 재료. 비어 있으면 크레딧만 든다.")]
    [SerializeField] private PassiveMaterial[] materials = new PassiveMaterial[0];

    [Tooltip("이것들을 전부 배운 뒤에야 배울 수 있다. 다른 계열의 id도 쓸 수 있다.")]
    [SerializeField] private string[] prerequisites = new string[0];

    [Header("배치 (트리 화면)")]
    [Tooltip("계열 안에서의 가로 위치. 0이 왼쪽.")]
    [Min(0)]
    [SerializeField] private int column = 0;

    [Tooltip("계열 안에서의 세로 위치. 0이 맨 아래. 위로 갈수록 커진다.")]
    [Min(0)]
    [SerializeField] private int row = 0;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;

    public PassiveBranch Branch => branch;
    public PassiveUnlockKind UnlockKind => PassiveBranchInfo.UnlockKind(branch);

    public PassiveEffectType Effect => effect;
    public float Value => value;

    public int RequiredAccountLevel => Mathf.Max(1, requiredAccountLevel);
    public int Cost => Mathf.Max(0, cost);

    public IReadOnlyList<PassiveMaterial> Materials => materials;
    public bool NeedsMaterials => materials != null && materials.Length > 0;

    public IReadOnlyList<string> Prerequisites => prerequisites;
    public bool IsRoot => prerequisites == null || prerequisites.Length == 0;

    public int Column => Mathf.Max(0, column);
    public int Row => Mathf.Max(0, row);

    /// <summary>화면에 보여 줄 효과 문구.</summary>
    public string EffectText => PassiveEffectInfo.Describe(effect, value);

    /// <summary>화면에 보여 줄 재료 문구. 재료가 없으면 빈 문자열.</summary>
    public string MaterialText
    {
        get
        {
            if (!NeedsMaterials)
                return string.Empty;

            var parts = new List<string>(materials.Length);

            for (int i = 0; i < materials.Length; i++)
            {
                if (!materials[i].IsValid)
                    continue;

                parts.Add($"{materials[i].item.DisplayName} ×{materials[i].ClampedCount}");
            }

            return string.Join(" · ", parts);
        }
    }

#if UNITY_EDITOR
    public void EditorSet(
        string nodeId, string name, string desc, PassiveBranch nodeBranch,
        PassiveEffectType effectType, float effectValue,
        int level, int nodeCost, PassiveMaterial[] nodeMaterials,
        string[] prereq, int col, int r)
    {
        id = nodeId;
        displayName = name;
        description = desc;
        branch = nodeBranch;
        effect = effectType;
        value = effectValue;
        requiredAccountLevel = level;
        cost = nodeCost;
        materials = nodeMaterials ?? new PassiveMaterial[0];
        prerequisites = prereq ?? new string[0];
        column = col;
        row = r;
    }
#endif
}
