using Alta.Inventory;
using Alta.Loot;
using Alta.Networking;
using Alta.Networking.Internal;
using Alta.PAGaC;
using Alta.Pages;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static SyncLib.Prefabs.Enums;

namespace SyncLib.Prefabs {
    public class ItemSettings {
        public string Description;

        public Glyph glyph;

        /// <summary>
        /// When scaled in dock
        /// </summary>
        public Vector3 StoreScale = new Vector3(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// When scaled in dock
        /// </summary>
        public DockPositioning scaledPositioning;

        /// <summary>
        /// When not scaled in a dock
        /// </summary>
        public DockPositioning normalPositioning;

        /// <summary>
        /// a null value for PickupDockSettings will act as a default if no specific setting is found
        /// </summary>
        public Alta.Inventory.Item.CustomDockPositioning[] customPositions;

        /// <summary>
        /// a null value for PickupDockSettings will act as a default if no specific setting is found
        /// </summary>
        public Alta.Inventory.Item.CustomDockPositioning[] customPositionsWhenCrafted;

        public bool isStackable;

        public bool isStackableWhenCrafted;

        public bool destroyWhenDocked;

        public int size;

        public int dockedStackSize = 30;

        public float weight = 1f;

        public Enums.LootValue LootValue;

        public LootCategory LootCategory;

        public Enums.PickupTag[] PickupTags;

        /// <summary>
        /// Leave as None to use default sound clip
        /// </summary>
        public SoundEffect overridePickUpSoundEffectType = SoundEffect.None;

        /// <summary>
        /// Leave as None to use default sound clip
        /// </summary>
        public SoundEffect overrideLetGoSoundEffectType = SoundEffect.None;

        public bool isUsingShrunkenVisual;

        public PooledObjectDefinition shrunkenVisual;

        public bool isOverridingShrunkenScale;

        public float overrideShrunkenScale = 1f;

        public bool isOverridingDefaultVisualScale;

        public float overrideScaleInsideVisual = 1f;

        public bool isSnappingToHand;

        public bool isAssistGrabBlocked;
    }

    /// <summary>
    /// A class designed to assist in easily registering prefabs as NetworkPrefabs in Alta's systems for later use.
    /// </summary>
    
    // The reason this class doesn't have many funny comments is because I was dead locked in on this for like 4 days.

    //                                           holy crap Geometry Dash reference ^^
    public static class NetworkPrefabRegistry {
        public static List<NetworkPrefab> RegisteredCustomPrefabs = new List<NetworkPrefab>();

        internal static Dictionary<string, Dictionary<string, int>> HashIds { get; set; }
        internal static string jsonData = null;

        public static EntityManager entityManager = null;

        private static bool inRegistryProcess;

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

        internal static void ReadJsonFile() {
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

            if (!NetworkSceneManager.IsServer && !recievedSyncData)
                return;

            takenHashIds = new HashSet<int>(SyncLib.ExistingHashIDs);

            MelonLogger.Msg("Registering custom prefabs...");

            ReadJsonFile();

            entityManager = Traverse.Create(NetworkSceneManager.Current).Field("entityManager").GetValue<EntityManager>();

            inRegistryProcess = true;

            RegisterPrefabs?.Invoke();

            inRegistryProcess = false;

            if (NetworkSceneManager.IsServer) {
                string modPrefabFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "modHashIDs.json"));

                string jsonArray = JsonConvert.SerializeObject(HashIds, Formatting.Indented);

                jsonData = jsonArray;

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

        private static void ExecuteAllGetComponentAtts(params Component[] comps) {
            foreach (var item in comps) {
                Type type = item.GetType();

                List<FieldInfo> allFields = new List<FieldInfo>();

                while (type != null) {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    allFields.AddRange(fields);

                    type = type.BaseType;
                }

                foreach (FieldInfo field in allFields) {
                    PrefabApplyAttribute att = field.GetCustomAttribute<PrefabApplyAttribute>();

                    if (att is null)
                        continue;

                    att.HandleApply(item, field, item.gameObject);
                }
            }
        }

        private static void MessageError(MelonMod mod, string message, bool doMessageBox = true) {
            string from = mod != null ? $"[FROM: '{mod.Info.Name}']" : "[FROM: 'Unknown']";

            MelonLogger.Error($"{from} {message}");

            if (doMessageBox) {
                string extraMessage = NetworkSceneManager.IsServer ? 
                    "The server will be closed to prevent data corruption." :
                    "A Township Tale will be closed to prevent any further errors.";

                SyncLib.MessageBox(SyncLib.GetActiveWindow(), $"{from} {message}\n\n{extraMessage}",
                    "SyncLib - Error Occurred, (A Township Tale)", 0);

                Application.Quit();
            }
        }

        private static void SetValue(this Item item, string field, object value) {
            Traverse.Create(item).Field(field).SetValue(value);
        }
             
        public static Item CreateNewItem(string itemName, ItemSettings settings) {
            Item item = ScriptableObject.CreateInstance<Item>();

            item.name = itemName;

            item.SetValue("Description", settings.Description);

            // Set Loot Category
            if (settings.LootCategory != LootCategory.None) {
                string LootCategory = settings.LootCategory.ToString().Replace('_', ' ');

                Category category = Resources.FindObjectsOfTypeAll<Category>()
                    .FirstOrDefault(c => c.name == LootCategory);

                item.SetValue("lootCategory", category);
            }

            // Set Loot Value
            if (settings.LootValue != Enums.LootValue.None) {
                string LootValue = settings.LootValue.ToString();

                LootValue lootValue = Resources.FindObjectsOfTypeAll<LootValue>()
                    .FirstOrDefault(c => c.name.Contains(LootValue));

                item.SetValue("lootValue", lootValue);
            }

            // Set Pickup Tags
            if (settings.PickupTags != null && settings.PickupTags.Length > 0) {
                List<Alta.Inventory.PickupTag> pickupTags = new List<Alta.Inventory.PickupTag>();

                foreach (var tagEnum in settings.PickupTags) {
                    string tag = tagEnum.ToString().Replace('_', ' ');

                    Alta.Inventory.PickupTag pickupTag = Resources.FindObjectsOfTypeAll<Alta.Inventory.PickupTag>()
                        .FirstOrDefault(c => c.name == tag);

                    pickupTags.Add(pickupTag);
                }

                if (pickupTags != null && pickupTags.Count > 0)
                    item.SetValue("tags", pickupTags);
            }

            item.SetValue("glyph", settings.glyph);
            item.SetValue("storeScale", settings.StoreScale);
            item.SetValue("scaledPositioning", settings.scaledPositioning);
            item.SetValue("normalPositioning", settings.normalPositioning);

            if(settings.customPositions != null && settings.customPositions.Length > 0)
                item.SetValue("customPositions", settings.customPositions);

            if (settings.customPositionsWhenCrafted != null && settings.customPositionsWhenCrafted.Length > 0)
                item.SetValue("customPositionsWhenCrafted", settings.customPositionsWhenCrafted);

            item.SetValue("isStackable", settings.isStackable);
            item.SetValue("isStackableWhenCrafted", settings.isStackableWhenCrafted);
            item.SetValue("size", settings.size);
            item.SetValue("dockedStackSize", settings.dockedStackSize);
            item.SetValue("weight", settings.weight);
            item.SetValue("isUsingShrunkenVisual", settings.isUsingShrunkenVisual);

            if (settings.shrunkenVisual != null)
                item.SetValue("shrunkenVisual", settings.shrunkenVisual);

            item.SetValue("isOverridingDefaultShrunkenScale", settings.isOverridingDefaultVisualScale);
            item.SetValue("overrideShrunkenScale", settings.overrideShrunkenScale);
            item.SetValue("isOverridingDefaultVisualScale", settings.isOverridingDefaultVisualScale);
            item.SetValue("overrideScaleInsideVisual", settings.overrideScaleInsideVisual);
            item.SetValue("isSnappingTohand", settings.isSnappingToHand);
            item.SetValue("isAssistGrabBlocked", settings.isAssistGrabBlocked);

            return item;
        }

        /// <summary>
        /// Registers a prefab as a NetworkPrefab in Alta's sytems for later use.
        /// <br></br>
        /// <br></br>
        /// <b>BEWARE!:</b> If the client has this mod that you are using to register an item, but the server doesn't, this function WILL return null, please take care of that accordingly.
        /// <br></br>
        /// <br></br>
        /// The SyncLibPrefabId only matters to SyncLib, please do not change it after publishing your mod.
        /// <br></br>
        /// Your prefab is automatically assigned a Hash upon first registry per server.
        /// <br></br>
        /// <br></br>
        /// If you need to attach other classes or NetworkBehaviours to the NetworkPrefab, use the 'AdditionalClasses' parameter.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static NetworkPrefab? RegisterPrefab(MelonMod mod, GameObject prefab, string SyncLibPrefabId, Item item, params Type[] AdditionalClasses) {
            if (mod is null) {
                MessageError(mod, "Attempted to register a custom prefab. However the 'mod' parameter was null.");

                return null;
            }

            if (!inRegistryProcess) {
                MessageError(mod, "Attempted to register a custom prefab too late.", false);

                return null;
            }

            if (HashIds is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the HashIds reference was null.");

                return null;
            }

            if (takenHashIds is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the takenHashIds reference was null.");

                return null;
            }

            if (entityManager is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the entityManager reference was null.");

                return null;
            }

            if (string.IsNullOrWhiteSpace(SyncLibPrefabId) || SyncLibPrefabId.Length < 3) {
                MessageError(mod, "Attempted to register a custom prefab with an invalid SyncLibPrefabId. Please make sure your SyncLibPrefabId has at least three characters.");

                return null;
            }

            if (prefab is null) {
                MessageError(mod, "Attempted to register a custom prefab that was null.");

                return null;
            }

            if (!NetworkSceneManager.IsServer) {
                string modRequired = "Unknown";
                bool issueFound = false;

                foreach (var hash in HashIds) {
                    if (issueFound) {
                        string version = modRequired.Replace($"{mod.Info.Name}.{mod.Info.Author}.", "");

                        SyncLib.MessageBox(SyncLib.GetActiveWindow(), $"The server requires a mod that the client doesn't have." +
                            $"\n\nPlease make sure you install '{modRequired}' version '{version}' before joining this server.", "SyncLib - Mod incompatibility, (A Township Tale)", 0);

                        Application.Quit();

                        return null;
                    }

                    foreach (var melonMod in MelonMod.RegisteredMelons) {
                        string modInfo = $"{melonMod.Info.Name}.{melonMod.Info.Author}.{melonMod.Info.Version}";

                        if (modInfo == hash.Key) {

                            issueFound = false;

                            break;
                        }
                        else {
                            modRequired = modInfo;

                            issueFound = true;
                        }
                    }
                }
            }

            NetworkPrefab networkprefab = prefab.GetComponent<NetworkPrefab>();

            if (networkprefab is null)
                networkprefab = prefab.AddComponent<NetworkPrefab>();
            else {
                MessageError(mod, "Attempted to register a prefab that already has a NetworkPrefab.", false);

                return null;
            }

            item?.SetValue("prefab", networkprefab);

            NetworkEntity entity = prefab.GetComponent<NetworkEntity>();
            if (entity is null)
                entity = prefab.AddComponent<NetworkEntity>();
            else {
                MessageError(mod, "Attempted to register a prefab that already has a NetworkEntity.", false);

                return null;
            }

            ExecuteAllGetComponentAtts(entity, networkprefab);

            FieldInfo fieldInfoNP = typeof(NetworkPrefab).GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfoNP == null)
                throw new Exception("Couldn't find NetworkPrefab.entity");

            fieldInfoNP.SetValue(networkprefab, entity);

            FieldInfo fieldInfoNE = typeof(NetworkEntity).GetField("prefab", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfoNE == null)
                throw new Exception("Couldn't find NetworkEntity.prefab");

            fieldInfoNE.SetValue(entity, networkprefab);

            List<Component> allComps = new List<Component>();
            foreach (Type type in AdditionalClasses) {
                Component comp = prefab.AddComponent(type);

                if (type == typeof(Pickup) && item != null) {
                    Pickup p = comp as Pickup;
                    
                    p.Item = item;
                }

                allComps.Add(comp);
            }

            ExecuteAllGetComponentAtts(allComps.ToArray());

            networkprefab.Initialize();

            void SetHashId(int HashId) {
                FieldInfo fieldInfoHash = typeof(NetworkPrefab).GetField("hash", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfoHash.SetValue(networkprefab, HashId);
            }

            Dictionary<string, int>? hashIds = GetOrCreateNewModHashIds($"{mod.Info.Name}.{mod.Info.Author}.{mod.Info.Version}");

            if (hashIds is null) {
                MessageError(mod, "Attempted to register a custom prefab, but something unexpected happened.");

                return null;
            }

            bool alreadyHadId = false;

            foreach (var data in hashIds) {
                if (data.Key == SyncLibPrefabId) {
                    SetHashId(data.Value);

                    alreadyHadId = true;

                    break;
                }
            }

            if (!alreadyHadId) {
                int nextAvaliable = getNextAvaliableHashId();

                if (nextAvaliable <= 0) {
                    MessageError(mod, "Attempted to register a custom prefab, but getNextAvaliableHashId returned an error.");

                    return null;
                }

                takenHashIds.Add(nextAvaliable);

                hashIds.Add(SyncLibPrefabId, nextAvaliable);

                SetHashId(nextAvaliable);
            }

            NetworkPrefab[] prefabArray = new NetworkPrefab[] { networkprefab };

            MethodInfo methodInfo = typeof(PrefabManager).GetMethod("AddToPrefabMap", BindingFlags.NonPublic | BindingFlags.Static);
            methodInfo.Invoke(null, new object[] { prefabArray });

            RegisteredCustomPrefabs.Add(networkprefab);

            MelonLogger.Msg($"Successfully registered {SyncLibPrefabId}!");

            return networkprefab;
        }
    }
}