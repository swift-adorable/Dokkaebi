using System;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 하나의 프리팹에 대한 GameObject 풀.
///
/// 풀 자료구조 자체는 Unity 내장 <see cref="ObjectPool{T}"/>를 사용한다.
/// 직접 구현하지 않는 이유는 설계 이유 항목에 정리해두었다.
/// 이 클래스는 GameObject 특유의 처리(활성화, 위치 지정, IPoolable 통지)만 담당한다.
/// </summary>
public class GameObjectPool
{
    private readonly GameObject prefab;
    private readonly Transform parent;
    private readonly ObjectPool<GameObject> pool;

    /// <summary>풀 안에서 대기 중인 개수.</summary>
    public int CountInactive => pool.CountInactive;

    /// <summary>현재 사용 중인 개수.</summary>
    public int CountActive => pool.CountActive;

    /// <summary>지금까지 실제로 Instantiate된 총 개수.</summary>
    public int CountAll => pool.CountAll;

    public GameObjectPool(GameObject prefab, Transform parent, int defaultCapacity, int maxSize)
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));

        if (defaultCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(defaultCapacity), "0 이상이어야 합니다.");

        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "1 이상이어야 합니다.");

        this.prefab = prefab;
        this.parent = parent;

        pool = new ObjectPool<GameObject>(
            createFunc: CreateInstance,
            actionOnGet: null,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyInstance,
            collectionCheck: false,   // 중복 반납은 PooledObject.IsSpawned로 이미 막는다.
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);
    }

    /// <summary>풀에서 인스턴스를 꺼내 지정한 위치/회전으로 배치하고 활성화한다.</summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject instance = pool.Get();

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        var pooled = instance.GetComponent<PooledObject>();
        if (pooled != null)
            pooled.SetSpawned(true);

        NotifySpawned(instance);

        return instance;
    }

    /// <summary>인스턴스를 풀로 되돌린다.</summary>
    public void Release(GameObject instance)
    {
        if (instance == null)
            return;

        pool.Release(instance);
    }

    /// <summary>미리 생성해 첫 사용 시의 프레임 스파이크를 없앤다.</summary>
    public void Prewarm(int count)
    {
        if (count <= 0)
            return;

        var buffer = new GameObject[count];

        for (int i = 0; i < count; i++)
            buffer[i] = pool.Get();

        for (int i = 0; i < count; i++)
            pool.Release(buffer[i]);
    }

    /// <summary>풀에 대기 중인 인스턴스를 모두 파괴한다.</summary>
    public void Clear()
    {
        pool.Clear();
    }

    private GameObject CreateInstance()
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
        instance.name = $"{prefab.name} (Pooled)";

        var pooled = instance.GetComponent<PooledObject>();
        if (pooled == null)
            pooled = instance.AddComponent<PooledObject>();

        // 프리팹을 직접 수정하지 않아도 되도록 런타임에 식별표를 붙인다.
        pooled.Bind(prefab);

        instance.SetActive(false);

        return instance;
    }

    private void OnRelease(GameObject instance)
    {
        if (instance == null)
            return;

        NotifyDespawned(instance);

        var pooled = instance.GetComponent<PooledObject>();
        if (pooled != null)
            pooled.SetSpawned(false);

        instance.SetActive(false);

        if (parent != null)
            instance.transform.SetParent(parent, false);
    }

    private void OnDestroyInstance(GameObject instance)
    {
        if (instance == null)
            return;

#if UNITY_EDITOR
        // 에디트 모드(단위 테스트 등)에서는 Destroy가 허용되지 않는다.
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            return;
        }
#endif

        UnityEngine.Object.Destroy(instance);
    }

    private static void NotifySpawned(GameObject instance)
    {
        var poolables = instance.GetComponentsInChildren<IPoolable>(true);

        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnSpawned();
    }

    private static void NotifyDespawned(GameObject instance)
    {
        var poolables = instance.GetComponentsInChildren<IPoolable>(true);

        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnDespawned();
    }
}
