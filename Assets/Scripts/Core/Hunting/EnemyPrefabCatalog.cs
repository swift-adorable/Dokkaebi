using UnityEngine;

/// <summary>
/// 유형 9종의 프리팹 참조. (로드맵 7-F · 7-G)
///
/// 【왜 카탈로그 에셋인가】
/// 프리팹은 Assets/Prefabs에 있어 Resources.LoadAll이 닿지 않는다.
/// 스포너(씬 오브젝트)에 아홉 칸을 손으로 끌어다 넣으면, 씬을 새로 만들 때마다
/// 다시 끌어야 하고 하나를 빠뜨려도 조용히 그 유형만 안 나온다.
///
/// PlaytestCatalog · SkillCatalog와 같은 방식이다 — 참조만 모은 에셋 하나를
/// Resources에 두고, 「Blob/Enemy/유형 프리팹 카탈로그 생성」이 채운다.
/// </summary>
[CreateAssetMenu(fileName = "EnemyPrefabCatalog", menuName = "Blob/Enemy Prefab Catalog")]
public class EnemyPrefabCatalog : ScriptableObject
{
    public const string ResourcePath = "EnemyPrefabCatalog";

    [Tooltip("EnemyArchetype 순서대로. 비어 있으면 그 유형은 나오지 않는다.")]
    [SerializeField] private GameObject[] prefabs = new GameObject[EnemyArchetypeTable.Count];

    /// <summary>그 유형의 프리팹. 없으면 null.</summary>
    public GameObject Get(EnemyArchetype archetype)
    {
        int index = (int)archetype;

        return prefabs != null && index >= 0 && index < prefabs.Length
            ? prefabs[index]
            : null;
    }

    /// <summary>실제로 채워진 유형의 수. 카탈로그가 비었는지 검사할 때 쓴다.</summary>
    public int FilledCount
    {
        get
        {
            if (prefabs == null)
                return 0;

            int count = 0;

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                    count++;
            }

            return count;
        }
    }

    public static EnemyPrefabCatalog Load() => Resources.Load<EnemyPrefabCatalog>(ResourcePath);

#if UNITY_EDITOR
    public void EditorSet(GameObject[] source)
    {
        prefabs = source;
    }
#endif
}
