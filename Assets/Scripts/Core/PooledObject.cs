using UnityEngine;

/// <summary>
/// 풀에서 생성된 인스턴스에 자동으로 붙는 식별표.
///
/// 이 컴포넌트가 있으면 인스턴스 스스로 "나는 어느 프리팹에서 나왔고
/// 지금 사용 중인가"를 알 수 있다. 덕분에 호출부가 프리팹 참조를 들고
/// 다니지 않아도 반납할 수 있고, 중복 반납도 막을 수 있다.
/// </summary>
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    /// <summary>이 인스턴스를 만들어낸 원본 프리팹.</summary>
    public GameObject SourcePrefab { get; private set; }

    /// <summary>현재 사용 중(풀 밖에 나와 있음)인지 여부.</summary>
    public bool IsSpawned { get; private set; }

    /// <summary>PoolManager가 호출한다. 직접 호출하지 않는다.</summary>
    public void Bind(GameObject prefab)
    {
        SourcePrefab = prefab;
    }

    /// <summary>PoolManager가 호출한다. 직접 호출하지 않는다.</summary>
    public void SetSpawned(bool spawned)
    {
        IsSpawned = spawned;
    }

    /// <summary>자기 자신을 풀로 반납한다.</summary>
    public bool Despawn()
    {
        if (!PoolManager.HasInstance)
            return false;

        return PoolManager.Instance.Despawn(gameObject);
    }
}
