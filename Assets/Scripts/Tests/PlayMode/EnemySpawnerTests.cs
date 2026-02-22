using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using SurvivalRPG.Survival;

namespace SurvivalRPG.Tests.PlayMode
{
    public class EnemySpawnerTests
    {
        private GameObject _networkManagerObj;
        private NetworkManager _networkManager;
        private GameObject _spawnerObj;
        private EnemySpawner _spawner;
        private GameObject _enemyPrefab;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Setup NetworkManager
            _networkManager = NGOTestHelper.CreateNetworkManager();
            _networkManagerObj = _networkManager.gameObject;

            // Create enemy prefab
            _enemyPrefab = new GameObject("EnemyPrefab");
            _enemyPrefab.SetActive(false);
            _enemyPrefab.AddComponent<NetworkObject>();
            _enemyPrefab.AddComponent<EnemyAI>();
            _networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = _enemyPrefab });

            // Start Host
            _networkManager.StartHost();
            yield return null;

            // Setup Spawner
            _spawnerObj = new GameObject("EnemySpawner");
            _spawnerObj.AddComponent<NetworkObject>();
            _spawner = _spawnerObj.AddComponent<EnemySpawner>();

            // Assign prefab via reflection since it's private serialized
            var type = typeof(EnemySpawner);
            type.GetField("_enemyPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_spawner, _enemyPrefab);
            type.GetField("_spawnInterval", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_spawner, 0.1f);
            type.GetField("_maxEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_spawner, 2);

            _spawnerObj.GetComponent<NetworkObject>().Spawn();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            NGOTestHelper.CleanUp();
            if (_spawnerObj != null)
            {
                Object.Destroy(_spawnerObj);
            }
            if (_enemyPrefab != null)
            {
                Object.Destroy(_enemyPrefab);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemySpawner_SpawnsEnemiesUpToMax()
        {
            // Wait for spawns
            yield return new WaitForSeconds(0.5f);

            // Find spawned enemies
            EnemyAI[] spawnedEnemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);

            // Should be 2 enemies (max) + 1 prefab = 3, but prefab is inactive or not in scene?
            // Actually, FindObjectsOfType finds active objects. The prefab might be active if not instantiated properly, but let's count NetworkObjects with EnemyAI that are spawned.
            int spawnedCount = 0;
            foreach (var enemy in spawnedEnemies)
            {
                if (enemy.gameObject != _enemyPrefab && enemy.IsSpawned)
                {
                    spawnedCount++;
                }
            }

            Assert.AreEqual(2, spawnedCount, "Spawner should have spawned exactly 2 enemies.");
        }
    }
}
