using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 Mutation 정의 목록을 담는 에셋.
///
/// 왜 Resources.LoadAll이 아니라 카탈로그 에셋인가:
/// v5 §10-10이 정의 에셋의 위치를 Assets/Data/ScriptableObjects/Mutations/ 로 확정했다.
/// 이 경로는 Resources 하위가 아니므로 Resources.LoadAll이 닿지 않는다.
/// 카탈로그 에셋 하나만 Resources에 두고, 그 안의 목록은 에디터 도구가
/// 폴더를 스캔해 자동으로 채운다(MutationCatalogBuilder).
/// → 에셋을 추가할 때 Inspector에 드래그하는 것을 잊는 실패 지점이 생기지 않는다. (프롬프트 5-4)
/// </summary>
[CreateAssetMenu(fileName = "MutationCatalog", menuName = "Blob/Mutation Catalog")]
public class MutationCatalog : ScriptableObject
{
    /// <summary>Resources 하위 경로. 확장자 없이 쓴다.</summary>
    public const string ResourcePath = "MutationCatalog";

    /// <summary>정의 에셋이 저장되는 폴더. 에디터 빌더가 이 폴더를 스캔한다.</summary>
    public const string DefinitionFolder = "Assets/Data/ScriptableObjects/Mutations";

    [Tooltip("MutationCatalogBuilder가 자동으로 채운다. 직접 편집하지 않는다.")]
    [SerializeField] private List<MutationDefinition> definitions = new();

    public IReadOnlyList<MutationDefinition> Definitions => definitions;

    public int Count => definitions.Count;

    /// <summary>id로 정의를 찾는다. 없으면 null.</summary>
    public MutationDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null && definitions[i].Id == id)
                return definitions[i];
        }

        return null;
    }

    /// <summary>카테고리별로 걸러 담는다. 호출부가 리스트를 재사용해 GC를 피한다.</summary>
    public List<MutationDefinition> GetByCategory(
        MutationCategory category, List<MutationDefinition> result = null)
    {
        result ??= new List<MutationDefinition>();
        result.Clear();

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null && definitions[i].Category == category)
                result.Add(definitions[i]);
        }

        return result;
    }

    /// <summary>Resources에서 카탈로그를 불러온다. 없으면 null.</summary>
    public static MutationCatalog Load()
    {
        return Resources.Load<MutationCatalog>(ResourcePath);
    }

#if UNITY_EDITOR
    /// <summary>에디터 빌더 전용. 런타임에서는 호출되지 않는다.</summary>
    public void EditorSetDefinitions(List<MutationDefinition> source)
    {
        definitions.Clear();

        if (source != null)
            definitions.AddRange(source);
    }
#endif
}
