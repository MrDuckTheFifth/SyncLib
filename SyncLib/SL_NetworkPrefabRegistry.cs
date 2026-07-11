using Alta.Networking;
using Alta.Networking.Internal;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SyncLib {
    /// <summary>
    /// A class designed to assist in easily registering prefabs as NetworkPrefabs in Alta's systems for later use.
    /// </summary>
    public class SL_NetworkPrefabRegistry : MonoBehaviour {
        public static List<NetworkPrefab> RegisteredCustomPrefabs = new List<NetworkPrefab>();

        internal static Dictionary<string, Dictionary<string, int>> HashIds { get; set; }
        internal static string jsonData = null;

        public static EntityManager entityManager = null;

        /// <summary>
        /// Called when prefabs are avaliable to be registered by SyncLib.
        /// </summary>
        public static Action RegisterPrefabs;

        internal static bool recievedSyncData = false;

        private static Dictionary<string, int> GetOrCreateNewModHashIds(string modId) {
            if (!HashIds.TryGetValue(modId, out Dictionary<string, int> prefabIds)) {
                prefabIds = new Dictionary<string, int>();
                HashIds.Add(modId, prefabIds);
            }

            return prefabIds;
        }

        private static void ReadJsonFile() {
            if (NetworkSceneManager.IsServer && jsonData is null) {
                string modPrefabFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "modHashIDs.json"));

                if (!File.Exists(modPrefabFilePath)) {
                    HashIds = new Dictionary<string, Dictionary<string, int>>();

                    return;
                }

                jsonData = File.ReadAllText(modPrefabFilePath);
            }

            try {
                Dictionary<string, Dictionary<string, int>>? jsonArray = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(jsonData);

                if (jsonArray is null) {
                    MelonLogger.Error("Failed to read Json modHashIDs file.");

                    // Quitting just so any save data on the server won't get lost.
                    if (NetworkSceneManager.IsServer)
                        Application.Quit();

                    return;
                }

                HashIds = jsonArray;
            }
            catch (Exception ex) {
                MelonLogger.Error("Failed to read Json modHashIDs file.");

                // Quitting just so any save data on the server won't get lost.
                if (NetworkSceneManager.IsServer)
                    Application.Quit();

                return;
            }
        }

        internal static bool hasRegistered = false;

        private static HashSet<int> takenHashIds;

        internal static void RegisterIntoGame() {
            if (hasRegistered)
                return;

            takenHashIds = new HashSet<int>(SyncLib.ExistingHashIDs);

            MelonLogger.Msg("Registering custom prefabs...");

            ReadJsonFile();

            entityManager = Traverse.Create(NetworkSceneManager.Current).Field("entityManager").GetValue<EntityManager>();

            RegisterPrefabs?.Invoke();

            if (NetworkSceneManager.IsServer) {
                string modPrefabFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "modHashIDs.json"));

                string jsonArray = JsonConvert.SerializeObject(HashIds, Formatting.Indented);

                File.WriteAllText(modPrefabFilePath, jsonArray);
            }

            hasRegistered = true;
        }

        internal static int getNextAvaliableHashId() {
            if (takenHashIds is null)
                return -1;

            int nextAvaliable = 1;
            while (takenHashIds.Contains(nextAvaliable)) {
                nextAvaliable++;
            }

            return nextAvaliable;
        }

        /// <summary>
        /// Registers a prefab as a NetworkPrefab in Alta's sytems for later use.
        /// <br></br>
        /// <br></br>
        /// The SyncLibPrefabId only matters to SyncLib, please do not change it after publishing your mod.
        /// <br></br>
        /// Your mod is automatically assigned a Hash upon first registry per server.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static NetworkPrefab? RegisterPrefab(MelonMod mod, GameObject prefab, string SyncLibPrefabId) {
            if (HashIds is null) {
                MelonLogger.Error("SyncLib attempted to register a custom prefab. But the HashIds reference was null.");

                if (NetworkSceneManager.IsServer) {
                    Application.Quit();
                }

                return null;
            }

            if (takenHashIds is null) {
                MelonLogger.Error("SyncLib attempted to register a custom prefab. But the takenHashIds reference was null.");

                if (NetworkSceneManager.IsServer) {
                    Application.Quit();
                }

                return null;
            }

            if (entityManager is null) {
                MelonLogger.Error("SyncLib attempted to register a custom prefab. But the entityManager reference was null.");

                if (NetworkSceneManager.IsServer) {
                    Application.Quit();
                }

                return null;
            }

            if (mod is null) {
                MelonLogger.Error("SyncLib attempted to register a custom prefab. However the 'mod' parameter was null.");

                if (NetworkSceneManager.IsServer) {
                    Application.Quit();
                }

                return null;
            }

            if (string.IsNullOrWhiteSpace(SyncLibPrefabId) || SyncLibPrefabId.Length < 3) {
                MelonLogger.Error("SyncLib attempted to register a custom prefab with an invalid SyncLibPrefabId. Please make sure your SyncLibPrefabId has at least three characters.");

                if (NetworkSceneManager.IsServer) {
                    Application.Quit();
                }

                return null;
            }

            if (prefab is null) {
                MelonLogger.Error("SyncLib attempted to register a custom prefab that was null.");

                if (NetworkSceneManager.IsServer) {
                    Application.Quit();
                }

                return null;
            }

            NetworkPrefab networkprefab = prefab.GetComponent<NetworkPrefab>();

            if (networkprefab is null)
                networkprefab = prefab.AddComponent<NetworkPrefab>();
            else {
                MelonLogger.Error("You cannot register a prefab that already has a NetworkPrefab.");
                    return null;
            }

            NetworkEntity entity = prefab.GetComponent<NetworkEntity>();
            if (entity is null)
                entity = prefab.AddComponent<NetworkEntity>();
            else {
                MelonLogger.Error("You cannot register a prefab that already has a NetworkEntity.");
                    return null;
            }

            FieldInfo fieldInfoNP = typeof(NetworkPrefab).GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfoNP == null)
                throw new Exception("Couldn't find NetworkPrefab.entity");

            fieldInfoNP.SetValue(networkprefab, entity);

            FieldInfo fieldInfoNE = typeof(NetworkEntity).GetField("prefab", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfoNE == null)
                throw new Exception("Couldn't find NetworkEntity.prefab");

            fieldInfoNE.SetValue(entity, networkprefab);

            networkprefab.Initialize();

            Dictionary<string, int>? hashIds = GetOrCreateNewModHashIds(mod.ID);

            if (hashIds is null) {
                MelonLogger.Error("SyncLib attempted to register a custom prefab, but something unexpected happened.");
                    return null;
            }

            void SetHashId(int HashId) {
                FieldInfo fieldInfoHash = typeof(NetworkPrefab).GetField("hash", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfoHash.SetValue(networkprefab, HashId);
            }

            bool alreadyContainedID = false;

            foreach (var item in hashIds) {
                string syncLibPrefabId = item.Key;
                int hashId = item.Value;

                if (syncLibPrefabId == SyncLibPrefabId) {
                    if (hashId > 0) {
                        SetHashId(hashId);

                        alreadyContainedID = true;

                        break;
                    }
                }
            }

            if (!alreadyContainedID) {
                int nextAvaliable = getNextAvaliableHashId();

                if (nextAvaliable <= 0) {
                    MelonLogger.Error("SyncLib attempted to register a custom prefab, but getNextAvaliableHashId returned an error.");
                        return null;
                }

                takenHashIds.Add(nextAvaliable);

                hashIds.Add(SyncLibPrefabId, nextAvaliable);

                SetHashId(nextAvaliable);
            }

            NetworkPrefab[] prefabArray = new NetworkPrefab[] { networkprefab };

            MethodInfo methodInfo = typeof(PrefabManager).GetMethod("AddToPrefabMap", BindingFlags.NonPublic | BindingFlags.Static);
            methodInfo.Invoke(null, new object[] { prefabArray });

            return networkprefab;
        }
    }
}