using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.IO;
using ByteWar.Core;

namespace ByteWar.Building
{
    // ── Serializable data containers ──────────────────────────────────────────

    [Serializable]
    public struct BuildingSaveEntry
    {
        public int PieceType;
        public float PosX, PosY, PosZ;
        public float RotX, RotY, RotZ;
        public ulong PlacedByClientId;
    }

    [Serializable]
    public class WorldSaveData
    {
        public List<BuildingSaveEntry> Buildings = new();
        public string SavedAtUtc;
    }

    // ── WorldPersistence component ────────────────────────────────────────────

    /// <summary>
    /// Server-only component that saves/loads all placed <see cref="BuildingPiece"/>
    /// instances to a JSON file on the host machine.
    /// Attach to the NetworkManager (or a persistent server-owned object).
    /// </summary>
    public class WorldPersistence : NetworkBehaviour, IPersistable
    {
        [Header("Configuration")]
        [SerializeField] private string _saveFileName = "world_save.json";

        [Header("Building Prefab Registry")]
        [SerializeField] private GameObject _foundationPrefab;
        [SerializeField] private GameObject _wallPrefab;
        [SerializeField] private GameObject _floorPrefab;
        [SerializeField] private GameObject _rampPrefab;
        [SerializeField] private GameObject _roof26Prefab;
        [SerializeField] private GameObject _stairsPrefab;
        [SerializeField] private GameObject _polePrefab;
        [SerializeField] private GameObject _beamPrefab;
        [SerializeField] private GameObject _angledWallPrefab;
        [SerializeField] private GameObject _doorFramePrefab;
        [SerializeField] private GameObject _windowPrefab;
        [SerializeField] private GameObject _halfWallPrefab;

        /// <summary>Full path to the save file (platform-specific).</summary>
        public string SaveFilePath => Path.Combine(Application.persistentDataPath, _saveFileName);

        /// <summary>Raised after a successful load. Parameter = number of buildings restored.</summary>
        public event Action<int> OnWorldLoaded;

        /// <summary>Raised after a successful save. Parameter = number of buildings saved.</summary>
        public event Action<int> OnWorldSaved;

        // ── IPersistable ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string Serialize()
        {
            var pieces = FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);
            var data = new WorldSaveData { SavedAtUtc = DateTime.UtcNow.ToString("o") };
            foreach (var piece in pieces)
            {
                if (piece == null || !piece.IsSpawned) continue;
                Vector3 pos = piece.transform.position;
                Vector3 euler = piece.transform.eulerAngles;
                data.Buildings.Add(new BuildingSaveEntry
                {
                    PieceType = (int)piece.PieceType,
                    PosX = pos.x,
                    PosY = pos.y,
                    PosZ = pos.z,
                    RotX = euler.x,
                    RotY = euler.y,
                    RotZ = euler.z,
                    PlacedByClientId = piece.PlacedByClientId.Value,
                });
            }
            return JsonUtility.ToJson(data, prettyPrint: true);
        }

        /// <inheritdoc/>
        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            var save = JsonUtility.FromJson<WorldSaveData>(data);
            if (save?.Buildings == null) return;
            Debug.Log($"[WorldPersistence] IPersistable.Deserialize: {save.Buildings.Count} entries.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes all <see cref="BuildingPiece"/> instances in the scene to disk.
        /// Server-only.
        /// </summary>
        public void SaveWorld()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[WorldPersistence] SaveWorld called on client — ignoring.");
                return;
            }

            var pieces = FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);
            var data = new WorldSaveData
            {
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            foreach (var piece in pieces)
            {
                if (piece == null || !piece.IsSpawned) continue;
                Vector3 pos = piece.transform.position;
                Vector3 euler = piece.transform.eulerAngles;
                data.Buildings.Add(new BuildingSaveEntry
                {
                    PieceType = (int)piece.PieceType,
                    PosX = pos.x,
                    PosY = pos.y,
                    PosZ = pos.z,
                    RotX = euler.x,
                    RotY = euler.y,
                    RotZ = euler.z,
                    PlacedByClientId = piece.PlacedByClientId.Value,
                });
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SaveFilePath, json);
            Debug.Log($"[WorldPersistence] Saved {data.Buildings.Count} buildings to '{SaveFilePath}'.");
            OnWorldSaved?.Invoke(data.Buildings.Count);
        }

        /// <summary>
        /// Loads buildings from disk and spawns them as networked objects.
        /// Server-only.
        /// </summary>
        public void LoadWorld()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[WorldPersistence] LoadWorld called on client — ignoring.");
                return;
            }

            if (!File.Exists(SaveFilePath))
            {
                Debug.Log($"[WorldPersistence] No save file found at '{SaveFilePath}'. Starting fresh.");
                OnWorldLoaded?.Invoke(0);
                return;
            }

            string json = File.ReadAllText(SaveFilePath);
            WorldSaveData data;
            try
            {
                data = JsonUtility.FromJson<WorldSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldPersistence] Failed to deserialize save file: {ex.Message}");
                OnWorldLoaded?.Invoke(0);
                return;
            }

            if (data == null || data.Buildings == null)
            {
                Debug.LogWarning("[WorldPersistence] Save data is empty or null.");
                OnWorldLoaded?.Invoke(0);
                return;
            }

            int spawned = 0;
            foreach (var entry in data.Buildings)
            {
                GameObject prefab = GetPrefabForType((BuildingPieceType)entry.PieceType);
                if (prefab == null)
                {
                    Debug.LogWarning($"[WorldPersistence] No prefab for piece type {entry.PieceType}; skipping.");
                    continue;
                }

                Vector3 pos = new(entry.PosX, entry.PosY, entry.PosZ);
                Quaternion rot = Quaternion.Euler(entry.RotX, entry.RotY, entry.RotZ);
                GameObject go = Instantiate(prefab, pos, rot);

                var piece = go.GetComponent<BuildingPiece>();
                if (piece != null)
                {
                    piece.PlacedByClientId.Value = entry.PlacedByClientId;
                }

                var netObj = go.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                    spawned++;
                }
                else
                {
                    Debug.LogError($"[WorldPersistence] Loaded building missing NetworkObject — destroying.");
                    Destroy(go);
                }
            }

            Debug.Log($"[WorldPersistence] Loaded {spawned}/{data.Buildings.Count} buildings from '{SaveFilePath}' (saved at {data.SavedAtUtc}).");
            OnWorldLoaded?.Invoke(spawned);
        }

        /// <summary>Deletes the save file if it exists.</summary>
        public void DeleteSave()
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                Debug.Log($"[WorldPersistence] Deleted save file at '{SaveFilePath}'.");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private GameObject GetPrefabForType(BuildingPieceType type)
        {
            return type switch
            {
                BuildingPieceType.Foundation => _foundationPrefab,
                BuildingPieceType.Wall => _wallPrefab,
                BuildingPieceType.Floor => _floorPrefab,
                BuildingPieceType.Ramp => _rampPrefab,
                BuildingPieceType.Roof26 => _roof26Prefab,
                BuildingPieceType.Stairs => _stairsPrefab,
                BuildingPieceType.Pole => _polePrefab,
                BuildingPieceType.Beam => _beamPrefab,
                BuildingPieceType.AngledWall => _angledWallPrefab,
                BuildingPieceType.DoorFrame => _doorFramePrefab,
                BuildingPieceType.Window => _windowPrefab,
                BuildingPieceType.HalfWall => _halfWallPrefab,
                _ => null,
            };
        }

        public void SetBuildingPrefabs(GameObject foundation, GameObject wall, GameObject floor = null, GameObject ramp = null, GameObject roof26 = null, GameObject stairs = null, GameObject pole = null, GameObject beam = null, GameObject angledWall = null, GameObject doorFrame = null, GameObject window = null, GameObject halfWall = null)
        {
            _foundationPrefab = foundation;
            _wallPrefab = wall;
            _floorPrefab = floor;
            _rampPrefab = ramp;
            _roof26Prefab = roof26;
            _stairsPrefab = stairs;
            _polePrefab = pole;
            _beamPrefab = beam;
            _angledWallPrefab = angledWall;
            _doorFramePrefab = doorFrame;
            _windowPrefab = window;
            _halfWallPrefab = halfWall;
        }
    }
}
