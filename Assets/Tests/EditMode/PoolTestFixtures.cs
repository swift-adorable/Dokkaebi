using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// IPoolable 호출 횟수를 기록하는 테스트 전용 컴포넌트.
    /// 풀이 생명주기 통지를 제대로 하는지 검증하는 데 쓴다.
    /// </summary>
    public class PoolableProbe : MonoBehaviour, IPoolable
    {
        public int SpawnedCount { get; private set; }
        public int DespawnedCount { get; private set; }

        public void OnSpawned() => SpawnedCount++;

        public void OnDespawned() => DespawnedCount++;
    }
}
