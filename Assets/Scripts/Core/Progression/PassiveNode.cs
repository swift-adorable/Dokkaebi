using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 패시브 한 칸. (스크린샷의 「스킬 강화」 한 칸에 해당한다)
///
/// 선행 조건을 id 문자열로 두는 이유 —
/// 에셋 간 상호 참조를 만들면 순환 참조와 GUID 꼬임이 생긴다.
/// SkillDefinition의 상호 배타 목록과 같은 방식이다.
/// </summary>
[CreateAssetMenu(fileName = "Passive_", menuName = "Blob/Passive Node")]
public class PassiveNode : ScriptableObject
{
    [Header("식별")]
    [SerializeField] private string id = "passive_id";
    [SerializeField] private string displayName = "이름 없음";

    [TextArea(2, 3)]
    [SerializeField] private string description = string.Empty;

    [Header("효과")]
    [SerializeField] private PassiveEffectType effect = PassiveEffectType.None;

    [Tooltip("효과의 크기. 해금형(시체 회수·제작대)은 무시된다.")]
    [SerializeField] private float value = 1f;

    [Header("조건")]
    [Tooltip("이 계정 레벨 미만에서는 배울 수 없다. 스크린샷의 「LEVEL 5」 가로선이 이것이다.")]
    [Min(1)]
    [SerializeField] private int requiredAccountLevel = 1;

    [Tooltip("배우는 데 드는 크레딧.")]
    [Min(0)]
    [SerializeField] private int cost = 500;

    [Tooltip("이것들을 전부 배운 뒤에야 배울 수 있다. 비어 있으면 뿌리 칸이다.")]
    [SerializeField] private string[] prerequisites = new string[0];

    [Header("배치 (트리 화면)")]
    [Tooltip("열. 0이 가장 왼쪽.")]
    [Min(0)]
    [SerializeField] private int column = 0;

    [Tooltip("행. 0이 가장 아래. 위로 갈수록 커진다.")]
    [Min(0)]
    [SerializeField] private int row = 0;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;

    public PassiveEffectType Effect => effect;
    public float Value => value;

    public int RequiredAccountLevel => Mathf.Max(1, requiredAccountLevel);
    public int Cost => Mathf.Max(0, cost);

    public IReadOnlyList<string> Prerequisites => prerequisites;

    public int Column => Mathf.Max(0, column);
    public int Row => Mathf.Max(0, row);

    /// <summary>뿌리 칸인지. 선행 조건이 없으면 바로 배울 수 있다.</summary>
    public bool IsRoot => prerequisites == null || prerequisites.Length == 0;

    /// <summary>화면에 보여 줄 효과 문구.</summary>
    public string EffectText => PassiveEffectInfo.Describe(effect, value);

#if UNITY_EDITOR
    public void EditorSet(
        string nodeId, string name, string desc,
        PassiveEffectType effectType, float effectValue,
        int level, int nodeCost, string[] prereq, int col, int r)
    {
        id = nodeId;
        displayName = name;
        description = desc;
        effect = effectType;
        value = effectValue;
        requiredAccountLevel = level;
        cost = nodeCost;
        prerequisites = prereq ?? new string[0];
        column = col;
        row = r;
    }
#endif
}
