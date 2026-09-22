using UnityEditor;
using UnityEngine;

/// <summary>
/// 유형 데이터(7-A·B)와 진영 어그로(7-C)를 실제 프리팹에 붙인다. (로드맵 7-F 일부)
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

    private const string Folder = "Assets/Prefabs";

    /// <summary>유형별 프리팹 이름. 이미 있는 둘은 이름이 달라 따로 잡는다.</summary>
    private static string PathOf(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Scav:   return MeleePath;
            case EnemyArchetype.Dynamo: return RangedPath;
            default:                    return $"{Folder}/Enemy{archetype}.prefab";
        }
    }

    [MenuItem("Blob/Enemy/유형·진영 배선")]
    public static void Wire()
    {
        // 이미 있는 둘 — 이름을 바꾸지 않는다. 씬과 스폰기가 이 경로를 참조한다.
        Attach(MeleePath, EnemyArchetype.Scav);
        Attach(RangedPath, EnemyArchetype.Dynamo);

        // 나머지 일곱은 근접/원거리 중 맞는 쪽을 본떠 만든다.
        //
        // 【본뜨는 쪽을 표가 정한다.】
        // 원거리형을 근접 프리팹에서 만들면 투사체 참조가 비어 있어
        // 「쏘는데 아무것도 안 나간다」가 된다. 손으로 고르지 않는다.
        foreach (EnemyArchetype archetype in (EnemyArchetype[])System.Enum.GetValues(typeof(EnemyArchetype)))
        {
            if (archetype == EnemyArchetype.Scav || archetype == EnemyArchetype.Dynamo)
                continue;

            CreateFrom(archetype);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyArchetypeWiring] 배선 완료 — 유형 {EnemyArchetypeTable.Count}종");
    }

    /// <summary>프리팹에 EnemyIdentity·EnemyAggro를 붙이고 유형을 지정한다.</summary>
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
    /// 유형 표의 수치를 프리팹에 베껴 넣는다. 손으로 적지 않는다.
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

        var movement = root.GetComponent<EnemyMovement>();

        if (movement != null)
        {
            var so = new SerializedObject(movement);
            so.FindProperty("moveSpeed").floatValue = stats.moveSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var attack = root.GetComponent<EnemyAttack>();

        if (attack != null)
        {
            var so = new SerializedObject(attack);
            so.FindProperty("damage").intValue = stats.damage;
            so.FindProperty("armourPenetration").intValue = stats.armourPenetration;
            so.FindProperty("attackKind").intValue =
                (int)(stats.ranged ? EnemyAttackKind.Ranged : EnemyAttackKind.Melee);
            so.FindProperty("appliedStatus").intValue = (int)stats.appliedStatus;
            so.FindProperty("windupDuration").floatValue = stats.windupDuration;
            so.FindProperty("attackCooldown").floatValue = stats.attackCooldown;

            // 근접은 유지 거리를 쓰지 않는다. 0으로 덮으면 붙기 전에 멈춘다.
            if (stats.ranged)
                so.FindProperty("preferredDistance").floatValue = stats.preferredDistance;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 이 유형의 프리팹을 만든다. 이미 있으면 덮어쓴다.
    ///
    /// 원거리형은 원거리 프리팹을, 근접형은 근접 프리팹을 본뜬다 —
    /// 투사체 참조가 거기에 들어 있다.
    /// </summary>
    private static void CreateFrom(EnemyArchetype archetype)
    {
        EnemyArchetypeStats stats = EnemyArchetypeTable.Of(archetype);

        string sourcePath = stats.ranged ? RangedPath : MeleePath;
        string targetPath = PathOf(archetype);

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

        if (source == null)
        {
            Debug.LogError($"[EnemyArchetypeWiring] {sourcePath}을 찾지 못했습니다.");
            return;
        }

        GameObject instance = Object.Instantiate(source);

        try
        {
            instance.name = $"Enemy{archetype}";

            PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }

        Attach(targetPath, archetype);
    }
}
