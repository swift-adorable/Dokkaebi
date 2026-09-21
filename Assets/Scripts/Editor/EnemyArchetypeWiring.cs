using UnityEditor;
using UnityEngine;

/// <summary>
/// 원형 데이터(7-A·B)와 진영 어그로(7-C)를 실제 프리팹에 붙인다. (로드맵 7-F 일부)
///
/// 【왜 지금 당겨서 하는가】
/// 7-A(데이터) · 7-B(적용) · 7-C(어그로)를 전부 「붙일 곳 없이」 쌓아 놨다.
/// 세 층을 다 만든 뒤 한 번에 처음 돌리면, 문제가 나도 어느 층인지 모른다.
/// 여기서 한 번 돌려 보고 다음 층으로 간다.
///
/// 【수치는 프리팹에도 같이 쓴다 — 다만 표에서 베껴 넣는다】
/// 런타임은 EnemyIdentity가 스폰마다 EnemyArchetypeTable의 값으로 덮어쓰므로
/// 프리팹 값은 사실 쓰이지 않는다. 그래도 비워 두지 않는 이유는,
/// 인스펙터를 연 사람이 스캐브 프리팹에서 체력 20을 봐야 하기 때문이다.
/// 압착기 프리팹에 체력 20이 적혀 있으면 그 사람은 틀린 정보를 읽는다.
///
/// 두 곳에 수치가 있으면 언젠가 갈라진다 — 그래서 손으로 적지 않고
/// 표에서 베껴 넣고, EnemyPrefabTests가 둘이 같은지 강제한다.
/// (마스터 프롬프트 7-8 — 드리프트를 테스트로 막는다)
///
/// ⚠️ 모달 대화상자를 띄우지 않는다. MCP 자동화가 그 자리에서 멈춘다.
/// </summary>
public static class EnemyArchetypeWiring
{
    private const string MeleePath = "Assets/Prefabs/Enemy.prefab";
    private const string RangedPath = "Assets/Prefabs/EnemyRanged.prefab";
    private const string CrusherPath = "Assets/Prefabs/EnemyCrusher.prefab";

    [MenuItem("Blob/Enemy/원형·진영 배선")]
    public static void Wire()
    {
        // 스캐브(야생) · 자전체(실험체) — 이미 있는 둘.
        Attach(MeleePath, EnemyArchetype.Scav);
        Attach(RangedPath, EnemyArchetype.Dynamo);

        // 【세 번째가 없으면 난전을 볼 수 없다.】
        // 스캐브(야생)와 자전체(실험체)만으로도 서로 적대하지만,
        // 압착기(시설)를 더해야 「세 진영이 얽힌다」가 실제로 나온다.
        // 압착기는 전기 2배 / 카오스 면역이라 속성 판단도 같이 확인된다.
        CreateCrusher();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[EnemyArchetypeWiring] 배선 완료 — "
                  + "Enemy=스캐브(야생) · EnemyRanged=자전체(실험체) · EnemyCrusher=압착기(시설)");
    }

    /// <summary>프리팹에 EnemyIdentity·EnemyAggro를 붙이고 원형을 지정한다.</summary>
    private static void Attach(string path, EnemyArchetype archetype)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError($"[EnemyArchetypeWiring] {path}을 찾지 못했습니다.");
            return;
        }

        // 프리팹 에셋은 직접 고치지 않는다. 내용을 열어 고치고 저장한다.
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            var identity = root.GetComponent<EnemyIdentity>() ?? root.AddComponent<EnemyIdentity>();

            var so = new SerializedObject(identity);
            so.FindProperty("archetype").intValue = (int)archetype;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<EnemyAggro>() == null)
                root.AddComponent<EnemyAggro>();

            WriteStats(root, archetype);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[EnemyArchetypeWiring] {path} → {EnemyArchetypeTable.Name(archetype)}");
    }

    /// <summary>
    /// 원형 표의 수치를 프리팹에 베껴 넣는다. 손으로 적지 않는다.
    /// </summary>
    private static void WriteStats(GameObject root, EnemyArchetype archetype)
    {
        EnemyArchetypeStats stats = EnemyArchetypeTable.Of(archetype);

        var health = root.GetComponent<Health>();

        if (health != null)
        {
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").intValue = stats.health;
            so.FindProperty("headArmour").floatValue = stats.armour;
            so.FindProperty("bodyArmour").floatValue = stats.armour;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var attack = root.GetComponent<EnemyAttack>();

        if (attack != null)
        {
            var so = new SerializedObject(attack);
            so.FindProperty("damage").intValue = stats.damage;
            so.FindProperty("armourPenetration").intValue = stats.armourPenetration;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>근접 프리팹을 본떠 압착기를 만든다. 시설 진영의 대표다.</summary>
    private static void CreateCrusher()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(MeleePath);

        if (source == null)
        {
            Debug.LogError($"[EnemyArchetypeWiring] {MeleePath}을 찾지 못했습니다.");
            return;
        }

        GameObject instance = Object.Instantiate(source);

        try
        {
            instance.name = "EnemyCrusher";

            // 느리다 — 「느리고 단단」이 압착기의 정체성이다. (문서 1절)
            // 체력·피해·방어도는 바로 아래 Attach가 표에서 베껴 넣는다.
            var movement = instance.GetComponent<EnemyMovement>();

            if (movement != null)
            {
                var so = new SerializedObject(movement);
                so.FindProperty("moveSpeed").floatValue = 1.4f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(instance, CrusherPath);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }

        Attach(CrusherPath, EnemyArchetype.Crusher);
    }
}
