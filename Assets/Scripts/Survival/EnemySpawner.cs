using UnityEngine;
using Unity.Netcode;

namespace SurvivalRPG.Survival
{
    public class EnemySpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private float _spawnInterval = 5f;
        [SerializeField] private int _maxEnemies = 3;

        private int _currentEnemies = 0;
        private float _lastSpawnTime;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                EnemyAI.OnEnemyDied += HandleEnemyDied;
                Debug.Log($"[EnemySpawner] Started spawner at {transform.position}. MaxEnemies={_maxEnemies}");
            }
        }

        public override void OnNetworkDespawn()
        {
            EnemyAI.OnEnemyDied -= HandleEnemyDied;
        }

        private void HandleEnemyDied(EnemyAI enemy)
        {
            _currentEnemies = Mathf.Max(0, _currentEnemies - 1);
            Debug.Log($"[EnemySpawner] Enemy '{enemy.gameObject.name}' died. Active enemies: {_currentEnemies}/{_maxEnemies}");
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            if (_currentEnemies < _maxEnemies && Time.time - _lastSpawnTime >= _spawnInterval)
            {
                SpawnEnemy();
            }
        }

        private void SpawnEnemy()
        {
            if (_enemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawner] Enemy prefab is not assigned!");
                return;
            }

            Vector3 spawnPos = transform.position + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
            if (Terrain.activeTerrain != null)
            {
                float y = Terrain.activeTerrain.SampleHeight(spawnPos)
                        + Terrain.activeTerrain.transform.position.y + 0.5f;
                spawnPos.y = y;
            }

            GameObject enemyObj = Instantiate(_enemyPrefab, spawnPos, Quaternion.identity);
            enemyObj.SetActive(true);

            NetworkObject netObj = enemyObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                _currentEnemies++;
                _lastSpawnTime = Time.time;
                Debug.Log($"[EnemySpawner] Spawned enemy at {spawnPos}. Active enemies: {_currentEnemies}/{_maxEnemies}");
            }
            else
            {
                Debug.LogError("[EnemySpawner] Enemy prefab is missing a NetworkObject component!");
                Destroy(enemyObj);
            }
        }
    }
}
