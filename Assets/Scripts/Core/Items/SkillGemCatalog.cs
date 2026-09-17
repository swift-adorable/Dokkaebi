using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인자 아이템 목록. SkillDefinition(규칙) ↔ ItemDefinition(실물)을 잇는다.
///
/// 스킬이 실물 아이템이 되면서 두 개의 에셋이 필요해졌다.
///   SkillDefinition — 무엇을 하는가 (53종, 그대로)
///   ItemDefinition  — 무게·가치·가방 칸 (인자 1종당 1개)
///
/// 둘을 한 에셋으로 합치지 않은 이유 — SkillDefinition은 소켓에 끼워진 뒤
/// 아이템으로서의 성질을 전혀 쓰지 않고, ItemDefinition은 장비·전리품과
/// 같은 인벤토리 규칙을 공유해야 한다. 합치면 양쪽 다 오염된다.
///
/// SkillCatalog와 같은 이유로 Resources에 카탈로그 하나만 둔다.
/// </summary>
[CreateAssetMenu(fileName = "SkillGemCatalog", menuName = "Blob/Skill Gem Catalog")]
public class SkillGemCatalog : ScriptableObject
{
    /// <summary>Resources 하위 경로. 확장자 없이 쓴다.</summary>
    public const string ResourcePath = "SkillGemCatalog";

    /// <summary>인자 아이템 에셋 폴더. 에디터 빌더가 이 폴더를 스캔한다.</summary>
    public const string DefinitionFolder = "Assets/Data/ScriptableObjects/Items/Gems";

    [Tooltip("SkillGemCatalogBuilder가 자동으로 채운다. 직접 편집하지 않는다.")]
    [SerializeField] private List<ItemDefinition> gems = new();

    public IReadOnlyList<ItemDefinition> Gems => gems;

    public int Count => gems.Count;

    /// <summary>스킬 정의에 대응하는 인자 아이템을 찾는다. 없으면 null.</summary>
    public ItemDefinition Find(SkillDefinition skill)
    {
        if (skill == null)
            return null;

        for (int i = 0; i < gems.Count; i++)
        {
            if (gems[i] != null && gems[i].Skill == skill)
                return gems[i];
        }

        return null;
    }

    public static SkillGemCatalog Load()
    {
        return Resources.Load<SkillGemCatalog>(ResourcePath);
    }

#if UNITY_EDITOR
    public void EditorSetGems(List<ItemDefinition> source)
    {
        gems.Clear();

        if (source != null)
            gems.AddRange(source);
    }
#endif
}
