using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리팹별 오브젝트 풀을 관리하는 중앙 창구.
///
/// 호출부는 Instantiate/Destroy 대신 Spawn/Despawn만 쓴다.
/// 풀이 없으면 자동으로 만들어주므로 사전 등록이 필요 없다.
/// </summary>
public class PoolManager : Singleton<PoolManager>
{
    [Header("Pool Defaults")]
    [Tooltip("풀 생성 시 내부 컬렉션의 초기 용량")]
    [SerializeField] private int defaultCapacity = 16;

    [Tooltip("풀에 보관할 최대 개수. 초과분은 반납 시 파괴된다.")]
    [SerializeField] private int maxSize = 256;

    [Tooltip("풀링된 오브젝트를 이 오브젝트 하위로 모아 Hierarchy를 정리한다.")]
    [SerializeField] private bool groupUnderManager = true;

    private readonly Dictionary<GameObject, GameObjectPool> poolsByPrefab = new();

    /// <summary>생성된 풀의 개수.</summary>
    public int PoolCount => poolsByPrefab.Count;

    /// <summary>
    /// 인스턴스를 보장한다. 씬에 없으면 런타임에 생성한다.
    ///
    /// 씬 배치를 강제하면 '씬에 넣는 것을 잊어 런타임에 터지는' 실패 지점이 생긴다.
    /// 모바일 입력 UI와 같은 이유로 코드가 스스로 보장하도록 한다.
    /// </summary>
    public static PoolManager EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<PoolManager>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        var managerObject = new GameObject("PoolManager (Runtime)");

        return managerObject.AddComponent<PoolManager>();
    }

    /// <summary>
    /// 프리팹으로부터 인스턴스를 꺼낸다. 해당 프리팹의 풀이 없으면 새로 만든다.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            GameLogger.Error("[PoolManager] Spawn에 null 프리팹이 전달되었습니다.");
            return null;
        }

        return GetOrCreatePool(prefab).Get(position, rotation);
    }

    /// <summary>인스턴스를 원래 풀로 되돌린다. 풀 소속이 아니면 false를 반환한다.</summary>
    public bool Despawn(GameObject instance)
    {
        if (instance == null)
            return false;

        var pooled = instance.GetComponent<PooledObject>();

        if (pooled == null || pooled.SourcePrefab == null)
        {
            GameLogger.Warning($"[PoolManager] 풀 소속이 아닌 오브젝트입니다: {instance.name}");
            return false;
        }

        // 중복 반납 방어. 같은 프레임에 두 번 반납되면 풀이 오염된다.
        if (!pooled.IsSpawned)
            return false;

        if (!poolsByPrefab.TryGetValue(pooled.SourcePrefab, out GameObjectPool pool))
        {
            GameLogger.Warning($"[PoolManager] 원본 프리팹의 풀을 찾을 수 없습니다: {instance.name}");
            return false;
        }

        pool.Release(instance);

        return true;
    }

    /// <summary>미리 생성해둔다. 첫 발사/첫 스폰 시의 프레임 스파이크를 없앤다.</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        GetOrCreatePool(prefab).Prewarm(count);

        GameLogger.Log($"[PoolManager] Prewarm: {prefab.name} x{count}");
    }

    /// <summary>특정 프리팹의 풀 상태를 조회한다. 없으면 false.</summary>
    public bool TryGetPool(GameObject prefab, out GameObjectPool pool)
    {
        if (prefab == null)
        {
            pool = null;
            return false;
        }

        return poolsByPrefab.TryGetValue(prefab, out pool);
    }

    /// <summary>모든 풀을 비운다. 씬 전환 시 호출한다.</summary>
    public void ClearAll()
    {
        foreach (GameObjectPool pool in poolsByPrefab.Values)
            pool.Clear();

        poolsByPrefab.Clear();
    }

    private GameObjectPool GetOrCreatePool(GameObject prefab)
    {
        if (poolsByPrefab.TryGetValue(prefab, out GameObjectPool existing))
            return existing;

        Transform parent = groupUnderManager ? CreateGroup(prefab.name) : null;

        var pool = new GameObjectPool(prefab, parent, defaultCapacity, maxSize);

        poolsByPrefab.Add(prefab, pool);

        GameLogger.Log($"[PoolManager] 새 풀 생성: {prefab.name}");

        return pool;
    }

    private Transform CreateGroup(string prefabName)
    {
        var group = new GameObject($"Pool - {prefabName}");
        group.transform.SetParent(transform, false);

        return group.transform;
    }

    protected override void OnDestroy()
    {
        ClearAll();

        base.OnDestroy();
    }
}
