using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 활성 적을 추적하고 정리하는 관리자.
///
/// 2단계에서 남겨둔 리스크를 여기서 해소한다.
/// 적이 화면 밖으로 멀어져도 풀에 반납되지 않으면 풀이 고갈되고,
/// 보이지도 않는 적이 계속 연산을 소비한다.
/// </summary>
public class EnemyManager : Singleton<EnemyManager>
{
    [Header("Limits")]
    [Tooltip("동시에 존재할 수 있는 최대 적 수. 모바일 프레임 방어선이다.")]
    [SerializeField] private int maxActiveEnemies = 60;

    [Tooltip("플레이어로부터 이 거리를 넘으면 자동으로 풀에 반납한다.")]
    [SerializeField] private float despawnDistance = 40f;

    [Header("Performance")]
    [Tooltip("거리 검사 주기(초). 매 프레임 검사할 필요가 없다.")]
    [SerializeField] private float cullInterval = 0.5f;

    private readonly List<EnemyController> activeEnemies = new();
    private Transform playerTransform;
    private float nextCullTime;

    /// <summary>현재 살아 있는 적 수.</summary>
    public int ActiveCount => activeEnemies.Count;

    /// <summary>적을 더 생성할 수 있는지.</summary>
    public bool CanSpawn => activeEnemies.Count < maxActiveEnemies;

    /// <summary>읽기 전용 활성 적 목록. 밀집 회피 계산에 쓰인다.</summary>
    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;

    /// <summary>인스턴스를 보장한다. 씬 배치를 강제하지 않는다.</summary>
    public static EnemyManager EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<EnemyManager>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("EnemyManager (Runtime)").AddComponent<EnemyManager>();
    }

    /// <summary>추격 대상을 지정한다. 스포너가 시작 시 한 번 호출한다.</summary>
    public void SetPlayer(Transform player)
    {
        playerTransform = player;
    }

    /// <summary>플레이어 Transform. 없으면 자동 탐색을 시도한다.</summary>
    public Transform PlayerTransform
    {
        get
        {
            if (playerTransform == null)
            {
                var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);

                if (movement != null)
                    playerTransform = movement.transform;
            }

            return playerTransform;
        }
    }

    public void Register(EnemyController enemy)
    {
        if (enemy == null || activeEnemies.Contains(enemy))
            return;

        activeEnemies.Add(enemy);
    }

    public void Unregister(EnemyController enemy)
    {
        if (enemy == null)
            return;

        activeEnemies.Remove(enemy);
    }

    private void Update()
    {
        if (Time.time < nextCullTime)
            return;

        nextCullTime = Time.time + cullInterval;

        CullDistantEnemies();
    }

    /// <summary>플레이어에게서 너무 멀어진 적을 풀로 되돌린다.</summary>
    private void CullDistantEnemies()
    {
        Transform player = PlayerTransform;

        if (player == null)
            return;

        float sqrLimit = despawnDistance * despawnDistance;

        // 뒤에서부터 순회해야 제거 중 인덱스가 밀리지 않는다.
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = activeEnemies[i];

            if (enemy == null)
            {
                activeEnemies.RemoveAt(i);
                continue;
            }

            float sqrDistance = (enemy.transform.position - player.position).sqrMagnitude;

            if (sqrDistance > sqrLimit)
                enemy.ReturnToPool();
        }
    }

    protected override void OnDestroy()
    {
        activeEnemies.Clear();

        base.OnDestroy();
    }
}
