using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform player;

    [Header("Spawn")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnRadius = 10f;

    private float nextSpawnTime;

    private void Update()
    {
        if (!GameManager.Instance.IsPlaying())
            return;

        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();

            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private void SpawnEnemy()
    {
        Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnRadius;

        Vector3 spawnPosition = player.position + new Vector3(
            randomCircle.x,
            0f,
            randomCircle.y
        );

        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
    }
}