using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 검증용 지급 품목 목록. (로드맵 6-P)
///
/// 【왜 카탈로그 에셋인가】
/// 지급 메뉴는 원래 에디터의 AssetDatabase로 에셋을 찾았다.
/// AssetDatabase는 **빌드에 존재하지 않는다.** 그래서 실기(iOS) 빌드에서는
/// 검증 도구가 통째로 없는 것과 같았고, 「장비가 지급되지 않는다」로 나타났다.
///
/// 아이템은 Assets/Data/... 아래에 있어 Resources.LoadAll이 닿지 않는다.
/// SkillCatalog와 같은 방식으로, 참조만 모은 에셋 하나를 Resources에 둔다.
/// 목록은 「Blob/Playtest/검증 카탈로그 생성」이 만든다.
/// </summary>
[CreateAssetMenu(fileName = "PlaytestCatalog", menuName = "Blob/Playtest Catalog")]
public class PlaytestCatalog : ScriptableObject
{
    public const string ResourcePath = "PlaytestCatalog";

    [Header("출격 한 벌")]
    [SerializeField] private List<ItemDefinition> starterKit = new();
    [SerializeField] private List<ItemDefinition> endgameKit = new();

    [Header("묶음")]
    [SerializeField] private List<ItemDefinition> weapons = new();
    [SerializeField] private List<ItemDefinition> imprints = new();
    [SerializeField] private List<ItemDefinition> keyImprints = new();
    [SerializeField] private List<ItemDefinition> coreGems = new();
    [SerializeField] private List<ItemDefinition> supportGems = new();
    [SerializeField] private List<ItemDefinition> checklistGems = new();

    [Header("과중량 유발")]
    [SerializeField] private ItemDefinition bulkMaterial;

    public IReadOnlyList<ItemDefinition> StarterKit => starterKit;
    public IReadOnlyList<ItemDefinition> EndgameKit => endgameKit;
    public IReadOnlyList<ItemDefinition> Weapons => weapons;
    public IReadOnlyList<ItemDefinition> Imprints => imprints;
    public IReadOnlyList<ItemDefinition> KeyImprints => keyImprints;
    public IReadOnlyList<ItemDefinition> CoreGems => coreGems;
    public IReadOnlyList<ItemDefinition> SupportGems => supportGems;
    public IReadOnlyList<ItemDefinition> ChecklistGems => checklistGems;
    public ItemDefinition BulkMaterial => bulkMaterial;

    public static PlaytestCatalog Load() => Resources.Load<PlaytestCatalog>(ResourcePath);

#if UNITY_EDITOR
    public void EditorSet(
        List<ItemDefinition> starter, List<ItemDefinition> endgame,
        List<ItemDefinition> weaponList, List<ItemDefinition> imprintList,
        List<ItemDefinition> keyImprintList, List<ItemDefinition> cores,
        List<ItemDefinition> supports, List<ItemDefinition> checklist,
        ItemDefinition bulk)
    {
        starterKit = starter;
        endgameKit = endgame;
        weapons = weaponList;
        imprints = imprintList;
        keyImprints = keyImprintList;
        coreGems = cores;
        supportGems = supports;
        checklistGems = checklist;
        bulkMaterial = bulk;
    }
#endif
}
