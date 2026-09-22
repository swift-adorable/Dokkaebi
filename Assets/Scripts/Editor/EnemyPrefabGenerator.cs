using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 프리팹 생성기. (로드맵 6-P)
///
/// 【왜 만들었나】
/// 씬에는 근접 적 하나뿐이었다. 근접만 있으면 전투는 「붙는다 / 뒤로 뺀다」
/// 두 동작으로 끝나고, 사거리·각도·엄폐 같은 판단이 전부 죽는다.
/// 무엇보다 【원거리 적이 없으면 플레이어의 방어 기술을 확인할 수 없다】 —
/// 예비동작을 보고 대시로 피하는 것도, 거리 절반 규칙(×0.5)도.
///
/// 수치는 docs/Blob_Combat_Baseline.md 5절 「적 유형」 표를 그대로 쓴다.
/// 7단계에서 유형 9종을 데이터(ScriptableObject)로 옮기면 이 생성기는 지운다.
/// 지금은 「플레이 검증을 할 수 있는 최소한」이 목적이다.
/// </summary>
public static class EnemyPrefabGenerator
{
    private const string Source = "Assets/Prefabs/Enemy.prefab";
    private const string RangedPath = "Assets/Prefabs/EnemyRanged.prefab";
    private const string BulletPath = "Assets/Prefabs/Bullet.prefab";

    // 자전체 — Combat_Baseline 5절 3행. 요구하는 답은 「각도」다.
    public const int SpitterHealth = 30;
    public const int SpitterDamage = 10;
    public const int SpitterArmourPenetration = 1;
    public const float SpitterAttackRange = 9f;
    public const float SpitterPreferredDistance = 7f;

    [MenuItem("Blob/Enemy/원거리 적 프리팹 생성")]
    public static void GenerateRanged()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);

        if (source == null)
        {
            Debug.LogError($"[EnemyPrefab] {Source}을 찾지 못했습니다.");
            return;
        }

        var bullet = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);

        if (bullet == null)
        {
            Debug.LogError($"[EnemyPrefab] {BulletPath}을 찾지 못했습니다.");
            return;
        }

        GameObject instance = Object.Instantiate(source);

        try
        {
            instance.name = "EnemyRanged";

            Apply(instance, bullet);

            PrefabUtility.SaveAsPrefabAsset(instance, RangedPath);

            AssetDatabase.SaveAssets();

            Debug.Log($"[EnemyPrefab] 자전체(원거리)를 만들었습니다 — "
                      + $"체력 {SpitterHealth} · 피해 {SpitterDamage} · 관통 {SpitterArmourPenetration} "
                      + $"· 사거리 {SpitterAttackRange}m → {RangedPath}");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void Apply(GameObject instance, GameObject bullet)
    {
        var health = new SerializedObject(instance.GetComponent<Health>());
        health.FindProperty("maxHealth").intValue = SpitterHealth;
        health.ApplyModifiedPropertiesWithoutUndo();

        var attack = new SerializedObject(instance.GetComponent<EnemyAttack>());
        attack.FindProperty("attackKind").enumValueIndex = (int)EnemyAttackKind.Ranged;
        attack.FindProperty("damage").intValue = SpitterDamage;
        attack.FindProperty("armourPenetration").intValue = SpitterArmourPenetration;
        attack.FindProperty("attackRange").floatValue = SpitterAttackRange;
        attack.FindProperty("preferredDistance").floatValue = SpitterPreferredDistance;
        attack.FindProperty("projectilePrefab").objectReferenceValue = bullet;
        attack.ApplyModifiedPropertiesWithoutUndo();

        // 원거리는 거리를 유지한다. 두뇌의 유지 거리도 같이 맞춘다.
        var brain = new SerializedObject(instance.GetComponent<EnemyBrain>());
        brain.FindProperty("preferredDistance").floatValue = SpitterPreferredDistance;
        brain.ApplyModifiedPropertiesWithoutUndo();

        // 눈에 보이게 구분한다. 색이 다르지 않으면 어느 쪽이 쏘는지 알 수 없다.
        if (instance.TryGetComponent(out MeshRenderer renderer))
        {
            instance.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);

            var material = new Material(renderer.sharedMaterial) { color = new Color(0.85f, 0.55f, 0.2f) };

            AssetDatabase.CreateAsset(material, "Assets/Art/Meterials/EnemyRanged.mat");

            renderer.sharedMaterial = material;
        }
    }
}
