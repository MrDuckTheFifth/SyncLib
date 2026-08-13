using Alta;
using Alta.Crafting;
using Alta.Inventory;
using Alta.Loot;
using Alta.Networking;
using Alta.Networking.Internal;
using Alta.Utilities;
using ATT_Workshop_Utilities;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Item = Alta.Inventory.Item;
using JointSlot = Alta.Crafting.JointSlot;
using JointInsert = Alta.Crafting.JointInsert;
using WorkshopChildEntity = ATT_Workshop_Utilities.ChildNetworkEntity;
using WorkshopCraftingPart = ATT_Workshop_Utilities.CraftingPart;
using WorkshopGrabPoint = ATT_Workshop_Utilities.GrabPoint;
using WorkshopItem = ATT_Workshop_Utilities.Item;
using WorkshopJointInsert = ATT_Workshop_Utilities.JointInsert;
using WorkshopJointSlot = ATT_Workshop_Utilities.JointSlot;
using WorkshopLootCategory = Enums.LootCategory;
using WorkshopLootValue = Enums.LootValue;

namespace SyncLib.Items {
    public class HashRegistryFile {
        public int FormatVersion = 1;

        public int NextHashId = 1;

        public List<ModHashRegistry> Mods = new List<ModHashRegistry>();
    }

    public class ModHashRegistry {
        public string ModId { get; set; }

        public string LastKnownVersion { get; set; }

        public bool Installed { get; set; }

        public Dictionary<string, int> ItemHashes { get; set; } = new Dictionary<string, int>();

        public ModHashRegistry(string modId, string version) {
            ModId = modId;
            LastKnownVersion = version;
            Installed = true;
        }
    }

    /// <summary>
    /// A class designed to assist in easily registering prefabs as NetworkPrefabs in Alta's systems for later use.
    /// </summary>
    public static class NetworkPrefabRegistry {
        public static List<NetworkPrefab> RegisteredCustomPrefabs = new List<NetworkPrefab>();

        public static Dictionary<Item, WorkshopItem> RegisteredCustomItems = new Dictionary<Item, WorkshopItem>();

        public static event Action OnItemsFinishedRegistering;

        internal static HashRegistryFile Registry { get; set; }

        internal static string clientSerializableJsonData { get; set; }

        private static string saveFilePath;

        internal static string jsonData = null;

        private static bool inRegistryProcess;

        /// <summary>
        /// Called when custom prefabs are being registered.
        /// </summary>
        public static event Action RegisterPrefabs;

        internal static bool recievedSyncData = false;

        private static ModHashRegistry GetOrCreateNewModHashIds(this MelonMod mod) {
            string id = $"{mod.Info.Name}.{mod.Info.Author}";

            var registry = Registry.Mods.FirstOrDefault(x => x.ModId == id);

            if (registry == null) {
                registry = new ModHashRegistry(
                    id,
                    mod.Info.Version
                );

                Registry.Mods.Add(registry);
            }

            registry.LastKnownVersion = mod.Info.Version;
            registry.Installed = true;

            return registry;
        }

        internal static void RefreshInstalledMods() {
            foreach (var mod in Registry.Mods)
                mod.Installed = false;

            foreach (var melon in MelonMod.RegisteredMelons) {
                string id = $"{melon.Info.Name}.{melon.Info.Author}";

                var registry =
                    Registry.Mods.FirstOrDefault(x => x.ModId == id);

                if (registry != null) {
                    registry.Installed = true;
                    registry.LastKnownVersion = melon.Info.Version;
                }
            }
        }

        internal static void ReadJsonFile() {
            if (!NetworkSceneManager.IsServer) {
                if (string.IsNullOrWhiteSpace(jsonData)) {
                    MelonLogger.Error("Client has no custom item registry.");

                    return;
                }

                Registry = JsonConvert.DeserializeObject<HashRegistryFile>(jsonData);

                return;
            }
            else {
                IAltaFolder serverFolder = AltaIO.ServersFolder;
                IAltaFolder serverIdFolder = serverFolder.GetSubfolder("-1");
                IAltaFolder SaveFolder = serverIdFolder.GetSubfolder("Save");
                saveFilePath = Path.Combine(SaveFolder.Path, "modHashIds.json");

                string path = null;

                if (saveFilePath != null) {
                    path = saveFilePath;
                }

                if (string.IsNullOrWhiteSpace(path)) {
                    MessageError(null, "Something went wrong while getting custom item data.");

                    return;
                }

                if (!File.Exists(path)) {
                    Registry = new HashRegistryFile();
                    return;
                }

                string json = File.ReadAllText(path);

                Registry = JsonConvert.DeserializeObject<HashRegistryFile>(json);

                RefreshInstalledMods();
            }
        }

        internal static bool hasRegistered = false;

        private static HashSet<int> takenHashIds;

        private static GameObject JointSlotReference;

        private static GameObject JointInsertReference;

        internal static void RegisterIntoGame() {
            if (hasRegistered)
                return;

            if (!NetworkSceneManager.IsServer && !recievedSyncData)
                return;

            MelonLogger.Msg("Registering custom prefabs...");

            takenHashIds = new HashSet<int>(SyncLib.ExistingHashIDs);

            ReadJsonFile();

            foreach (var mod in Registry.Mods) {
                foreach (var hash in mod.ItemHashes.Values) {
                    takenHashIds.Add(hash);
                }
            }

            HashedGeneralValue<Item>.CheckItems();

            // Yoinking the stick and flint's Joints to steal them for our own custom items.

            if (JointSlotReference is null) {
                // 10650 is the stick's HashedGeneralValue.
                NetworkPrefab stickPrefab = HashedGeneralValue<Item>.Get(10650).Prefab;

                GameObject jointSlotObject = stickPrefab.GetComponentInChildren<JointSlot>().gameObject;

                if (jointSlotObject != null) {
                    JointSlotReference = jointSlotObject;
                }
            }

            if (JointInsertReference is null) {
                // 42570 is the flint's HashedGeneralValue.
                NetworkPrefab flintPrefab = HashedGeneralValue<Item>.Get(42570).Prefab;

                GameObject jointInsertObject = flintPrefab.GetComponentInChildren<JointInsert>().gameObject;

                if (jointInsertObject != null) {
                    JointInsertReference = jointInsertObject;
                }
            }

            inRegistryProcess = true;

            RegisterPrefabs?.Invoke();

            inRegistryProcess = false;

            if (NetworkSceneManager.IsServer) {
                string modPrefabFilePath = saveFilePath;

                string json = JsonConvert.SerializeObject(Registry, Formatting.Indented);

                jsonData = json;

                File.WriteAllText(modPrefabFilePath, json);

                var clientData = new HashRegistryFile {
                    FormatVersion = Registry.FormatVersion,
                    Mods = Registry.Mods.Where(x => x.Installed).ToList()
                };

                string clientJson = JsonConvert.SerializeObject(clientData, Formatting.Indented);

                clientSerializableJsonData = clientJson;
            }

            hasRegistered = true;

            OnItemsFinishedRegistering?.Invoke();
        }

        internal static int GetNextAvailableHashId() {
            while (takenHashIds.Contains(Registry.NextHashId))
                Registry.NextHashId++;

            int id = Registry.NextHashId++;

            takenHashIds.Add(id);

            return id;
        }

        /// <summary>
        /// Runs all [GetComponent] related attributes that fields in components may have.
        /// </summary>
        /// <param name="comps"></param>
        public static void ExecuteAllGetComponentAtts(params Component[] comps) {
            foreach (var item in comps) {
                if (item is null)
                    continue;

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

                SyncLib.MessageBox(
                    SyncLib.GetActiveWindow(),
                    $"{from} {message}\n\n{extraMessage}",
                    "SyncLib - Error Occurred, (A Township Tale)",
                    0
                );

                Application.Quit();
            }
        }

        private static void SetValue(this Item item, Traverse traverse, string field, object value) {
            traverse.Field(field).SetValue(value);
        }

        private static T? FindScriptableObjectOfName<T>(string name, bool contains = false) where T : UnityEngine.Object {
            if (contains) {
                return Resources.FindObjectsOfTypeAll(typeof(T))
                    .FirstOrDefault(obj => obj.name.Contains(name.Replace('_', ' '))) as T;
            }
            else {
                return Resources.FindObjectsOfTypeAll(typeof(T))
                    .FirstOrDefault(obj => obj.name == name.Replace('_', ' ')) as T;
            }
        }

        private static Item CreateNewItem(string itemName, WorkshopItem settings) {
            Item item = ScriptableObject.CreateInstance<Item>();

            item.name = itemName;

            Traverse traverse = Traverse.Create(item);

            // Set Loot Category
            if (settings.LootCategory != WorkshopLootCategory.NoneOrCustom) {
                string LootCategory = settings.LootCategory.ToString().Replace('_', ' ');

                Category? category = FindScriptableObjectOfName<Category>(LootCategory);

                if(category != null) {
                    item.SetValue(traverse, "lootCategory", category);
                }
            }

            // Set Loot Value
            if (settings.LootValue != WorkshopLootValue.NoneOrCustom) {
                string LootValue = settings.LootValue.ToString();

                LootValue? lootValue = FindScriptableObjectOfName<LootValue>(LootValue, true);

                if (lootValue != null) {
                    item.SetValue(traverse, "lootValue", lootValue);
                }
            }

            // Set Pickup Tags
            if (settings.PickupTags != null && settings.PickupTags.Length > 0) {
                List<PickupTag> pickupTags = new List<PickupTag>();

                foreach (var tagEnum in settings.PickupTags) {
                    string tag = tagEnum.ToString().Replace('_', ' ');

                    PickupTag? pickupTag = FindScriptableObjectOfName<PickupTag>(tag);

                    if (pickupTag != null) {
                        pickupTags.Add(pickupTag);
                    }
                }

                if (pickupTags != null && pickupTags.Count > 0)
                    item.SetValue(traverse, "tags", pickupTags);
            }

            //item.SetValue(traverse, "glyph", settings.glyph);
            item.SetValue(traverse, "storeScale", settings.StoreScale);

            // I'm so sorry to who ever beared the burden of reading through this code

            if (settings.scaledPositioning is null) {
                settings.scaledPositioning = new ATT_Workshop_Utilities.DockPositioning();
            }

            Item.DockPositioning scaledDp = new Item.DockPositioning(
                settings.scaledPositioning.storePosition,
                settings.scaledPositioning.storeEuler
            );

            if (settings.normalPositioning is null) {
                settings.normalPositioning = new ATT_Workshop_Utilities.DockPositioning();
            }

            Item.DockPositioning normalDp = new Item.DockPositioning(
                settings.normalPositioning.storePosition,
                settings.normalPositioning.storeEuler
            );

            item.SetValue(traverse, "scaledPositioning", scaledDp);
            item.SetValue(traverse, "normalPositioning", normalDp);

            if (settings.customPositions is null) {
                settings.customPositions = new ATT_Workshop_Utilities.CustomDockPositioning[0];
            }

            Item.CustomDockPositioning[] cdp = new Item.CustomDockPositioning[settings.customPositions.Length];

            if (settings.customPositions.Length > 0) {
                for (int i = 0; i < settings.customPositions.Length; i++) {
                    ATT_Workshop_Utilities.CustomDockPositioning customDP = settings.customPositions[i];

                    cdp[i] = new Item.CustomDockPositioning(null, customDP.storePosition, customDP.storeEuler);
                }
            }

            if (settings.customPositionsWhenCrafted is null) {
                settings.customPositionsWhenCrafted = new ATT_Workshop_Utilities.CustomDockPositioning[0];
            }

            Item.CustomDockPositioning[] cdpwc = new Item.CustomDockPositioning[settings.customPositionsWhenCrafted.Length];

            if (settings.customPositionsWhenCrafted.Length > 0) {
                for (int i = 0; i < settings.customPositionsWhenCrafted.Length; i++) {
                    ATT_Workshop_Utilities.CustomDockPositioning customDP = settings.customPositionsWhenCrafted[i];

                    cdpwc[i] = new Item.CustomDockPositioning(null, customDP.storePosition, customDP.storeEuler);
                }
            }

            item.SetValue(traverse, "customPositions", cdp);
            item.SetValue(traverse, "customPositionsWhenCrafted", cdpwc);

            item.SetValue(traverse, "isStackable", settings.isStackable);
            item.SetValue(traverse, "isStackableWhenCrafted", settings.isStackableWhenCrafted);
            item.SetValue(traverse, "size", settings.size);
            item.SetValue(traverse, "dockedStackSize", settings.dockedStackSize);
            item.SetValue(traverse, "weight", settings.weight);
            item.SetValue(traverse, "isUsingShrunkenVisual", settings.isUsingShrunkenVisual);

            //if (settings.shrunkenVisual != null)
                //item.SetValue(traverse, "shrunkenVisual", settings.shrunkenVisual);

            item.SetValue(traverse, "isOverridingDefaultShrunkenScale", settings.isOverridingDefaultVisualScale);
            item.SetValue(traverse, "overrideShrunkenScale", settings.overrideShrunkenScale);
            item.SetValue(traverse, "isOverridingDefaultVisualScale", settings.isOverridingDefaultVisualScale);
            item.SetValue(traverse, "overrideScaleInsideVisual", settings.overrideScaleInsideVisual);
            item.SetValue(traverse, "isSnappingToHand", settings.isSnappingToHand);
            item.SetValue(traverse, "isAssistGrabBlocked", settings.isAssistGrabBlocked);

            RegisteredCustomItems.Add(item, settings);

            return item;
        }

        private static void ApplyPickupGrabPoints(this Pickup pickup) {
            Transform transform = pickup.transform;

            WorkshopGrabPoint[] wsGrabPoints = transform.GetComponentsInChildren<WorkshopGrabPoint>();

            GrabPoint[] grabPoints = new GrabPoint[wsGrabPoints.Length];

            for (int i = 0; i < wsGrabPoints.Length; i++) {
                WorkshopGrabPoint grabPoint = wsGrabPoints[i];

                GrabPoint gp = new GrabPoint() {
                    rotationMode = (RotationMode)grabPoint.rotationMode,
                };

                Traverse traverse = Traverse.Create(gp);

                traverse.Field("transform").SetValue(pickup.transform);
                traverse.Field("position").SetValue(grabPoint.transform.localPosition);
                traverse.Field("rotationEuler").SetValue(grabPoint.transform.localEulerAngles);
            }

            Traverse.Create(pickup).Field("grabPoints").SetValue(grabPoints);
        }

        internal static bool AttachNetworkComponentsToBase(this GameObject prefab, out NetworkPrefab networkprefab, out NetworkEntity entity) {
            networkprefab = null;
            entity = null;

            networkprefab = prefab.GetComponent<NetworkPrefab>();

            if (networkprefab is null)
                networkprefab = prefab.AddComponent<NetworkPrefab>();
            else {
                return false;
            }

            entity = prefab.GetComponent<NetworkEntity>();
            if (entity is null)
                entity = prefab.AddComponent<NetworkEntity>();
            else {
                return false;
            }

            return true;
        }

        private static ModHashRegistry GetHashOwner(int hash, ModHashRegistry owner) {
            foreach (var mod in Registry.Mods) {
                if (mod == owner)
                    continue;

                if (mod.ItemHashes.Values.Contains(hash))
                    return mod;
            }

            return null;
        }

        private static bool HasTightGrab(this Transform transform) {
            Transform[] objs = transform.GetComponentsInChildren<Transform>();

            // Layer 14 is the tight grab layer.
            return objs.Any(go => go.gameObject.layer == 14);
        }

        public static Item? RegisterCustomItem(this NetworkPrefab prefab) {
            WorkshopItem workshopItem = prefab.GetComponent<WorkshopItem>();

            if (workshopItem is null) {
                MelonLogger.Error("Please use ATT Workshop to register custom items.");

                return null;
            }

            Item item = CreateNewItem(prefab.name, workshopItem);

            item.Prefab = prefab;

            NetworkEntity entity = prefab.GetComponent<NetworkEntity>();

            Pickup pickup = prefab.GetComponent<Pickup>();

            if (pickup != null) {
                pickup.Item = item;
            }

            Traverse.Create(typeof(HashedGeneralValue<Item>))
                .Field("items")
                .GetValue<Dictionary<uint, Item>>()
                .Add(prefab.Hash, item);

            MelonLogger.Msg($"Successfully registered {prefab.name} as a custom item!");

            return item;
        }

        /// <summary>
        /// Registers this prefab as an item in Alta's sytems for later use.
        /// <br></br>
        /// <br></br>
        /// <b>BEWARE!:</b> If the client has this mod, but the server doesn't, this function WILL return null, please take care of that accordingly.
        /// <br></br>
        /// <br></br>
        /// The CustomPrefabId only matters to SyncLib, please do not change it after publishing your mod.
        /// <br></br>
        /// Your prefab is automatically assigned a Hash upon first registry per server.
        /// <br></br>
        /// <br></br>
        /// If you need to attach other classes or NetworkBehaviours to the NetworkPrefab, use the 'AdditionalClasses' parameter.
        /// </summary>
        public static NetworkPrefab? RegisterCustomPrefab(this MelonMod mod, GameObject prefab, string CustomPrefabId, params Type[] AdditionalComponents) {
            if (mod is null) {
                MessageError(mod, "Attempted to register a custom prefab. However the 'mod' parameter was null.");

                return null;
            }

            if (!inRegistryProcess) {
                MessageError(mod, "Attempted to register a custom prefab too late.", false);

                return null;
            }

            if (Registry is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the HashIds reference was null.");

                return null;
            }

            if (takenHashIds is null) {
                MessageError(mod, "Attempted to register a custom prefab. But the takenHashIds reference was null.");

                return null;
            }

            if (string.IsNullOrWhiteSpace(CustomPrefabId) || CustomPrefabId.Length < 3) {
                MessageError(mod, "Attempted to register a custom prefab with an invalid CustomPrefabId. Please make sure your CustomPrefabId has at least three characters.");

                return null;
            }

            CustomPrefabId = $"{CustomPrefabId} ({mod.Info.Name})";

            if (prefab is null) {
                MessageError(mod, "Attempted to register a custom prefab that was null.");

                return null;
            }

            bool success = prefab.AttachNetworkComponentsToBase(out NetworkPrefab networkprefab, out NetworkEntity entity);

            if (!success) {
                MessageError(mod, "Attempted to register a prefab that's already registered.", false);

                return null;
            }

            List<Component> allComps = new List<Component>();

            allComps.Add(networkprefab);
            allComps.Add(entity);

            //if (item != null) {
            //    WorkshopItem settings = RegisteredCustomItems[item];

            //    if (settings != null && settings._physicalMaterial != null) {
            //        PhysicalMaterialPart part = prefab.AddComponent<PhysicalMaterialPart>();

            //        Traverse.Create(part).Field("physicalMaterial").SetValue(settings._physicalMaterial);

            //        allComps.Add(part);
            //    }
            //}

            foreach (Type type in AdditionalComponents) {
                if (type is null)
                    continue;

                Component comp = prefab.AddComponent(type);

                if (comp is Pickup pickup) {
                    if (HasTightGrab(networkprefab.transform)) {
                        Traverse.Create(pickup).Field("isTightGrab").SetValue(true);
                    }

                    pickup.ApplyPickupGrabPoints();
                }

                allComps.Add(comp);
            }

            #region non-abstract wall of doom and despair

            if (prefab.GetComponent<Rigidbody>() != null) {
                allComps.Add(prefab.AddComponent<NetworkRigidbody>());
            }

            WorkshopChildEntity[] childEntities = entity.GetComponentsInChildren<WorkshopChildEntity>();

            foreach (WorkshopChildEntity childEntity in childEntities) {
                GameObject originalObject = childEntity.gameObject;

                NetworkEntity actualChildEntity = originalObject.AddComponent<NetworkEntity>();

                if (!childEntity.DoNotMarkAsChild) {
                    Traverse.Create(actualChildEntity).Field("Parent").SetValue(entity);
                }

                allComps.Add(actualChildEntity);
            }

            WorkshopCraftingPart[] craftingParts = entity.GetComponentsInChildren<WorkshopCraftingPart>();

            foreach (WorkshopCraftingPart craftingPart in craftingParts) {
                GameObject originalObject = craftingPart.gameObject;

                Type type = craftingPart.isAddon ?
                    typeof(AddonCraftingPart) :
                    typeof(Alta.Crafting.CraftingPart);

                Component comp = originalObject.AddComponent(type);

                allComps.Add(comp);
            }

            WorkshopJointSlot[] jointSlots = entity.GetComponentsInChildren<WorkshopJointSlot>();

            // I give up on keeping ts organized, as long as it works
            foreach (WorkshopJointSlot jointSlot in jointSlots) {
                GameObject originalObject = jointSlot.gameObject;

                GameObject JointSlotObj = GameObject.Instantiate(JointSlotReference);

                Transform originalTr = jointSlot.transform;
                Transform newTr = JointSlotObj.transform;

                newTr.parent = originalTr.parent;

                newTr.localPosition = originalTr.localPosition;
                newTr.localScale = originalTr.localScale;
                newTr.localRotation = originalTr.localRotation;

                JointSlot slot = JointSlotObj.GetComponent<JointSlot>();
                NetworkEntity slotEntity = JointSlotObj.GetComponent<NetworkEntity>();

                if (slot is null || slotEntity is null)
                    continue;

                slotEntity.OnValidate();

                ChildNetworkEntity childEntity = JointSlotObj.GetComponent<ChildNetworkEntity>();

                Traverse traverse = Traverse.Create(slot);

                if (childEntity != null && !childEntity.DoNotMarkAsChild) {
                    traverse.Field("Parent").SetValue(entity);
                }

                List<JointSlotType> types = new List<JointSlotType>();

                foreach (Enums.JointSlotType type in jointSlot.JointSlotTypes) {
                    string name = type.ToString();

                    JointSlotType? jointSlotType = FindScriptableObjectOfName<JointSlotType>(name);

                    if (jointSlotType != null) {
                        types.Add(jointSlotType);
                    }
                }

                traverse.Field("types").SetValue(types);

                if (slot.CraftingPart != null) {
                    traverse.Field("ConnectedPart").SetValue(slot.CraftingPart);
                }

                allComps.Add(slotEntity);

                originalObject.SetActive(false);
            }

            WorkshopJointInsert[] insertSlots = entity.GetComponentsInChildren<WorkshopJointInsert>();

            foreach (WorkshopJointInsert jointInsert in insertSlots) {
                GameObject originalObject = jointInsert.gameObject;

                GameObject JointInsertObj = GameObject.Instantiate(JointInsertReference);

                Transform originalTr = jointInsert.transform;
                Transform newTr = JointInsertObj.transform;

                newTr.parent = originalTr.parent;

                newTr.localPosition = originalTr.localPosition;
                newTr.localScale = originalTr.localScale;
                newTr.localRotation = originalTr.localRotation;

                JointInsert slot = JointInsertObj.GetComponent<JointInsert>();
                NetworkEntity slotEntity = JointInsertObj.GetComponent<NetworkEntity>();

                if (slot is null || slotEntity is null)
                    continue;

                slotEntity.OnValidate();

                ChildNetworkEntity childEntity = JointInsertObj.GetComponent<ChildNetworkEntity>();

                Traverse traverse = Traverse.Create(slot);

                if (childEntity != null && !childEntity.DoNotMarkAsChild) {
                    traverse.Field("Parent").SetValue(entity);
                }


                string name = jointInsert.InsertType.ToString();

                JointSlotType? jointSlotType = FindScriptableObjectOfName<JointSlotType>(name);

                if (jointSlotType != null) {
                    traverse.Field("type").SetValue(jointSlotType);
                }

                allComps.Add(slotEntity);

                originalObject.SetActive(false);
            }

            #endregion

            ExecuteAllGetComponentAtts(allComps.ToArray());

            ModHashRegistry hashIds = mod.GetOrCreateNewModHashIds();

            bool SetHashId(int HashId) {
                FieldInfo fieldInfoHash = typeof(NetworkPrefab).GetField("hash", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfoHash.SetValue(networkprefab, HashId);

                ModHashRegistry existingOwner = GetHashOwner(HashId, hashIds);

                if (existingOwner != null) {
                    MessageError(mod, $"Hash '{HashId}' for '{prefab.name}' is already registered by '{existingOwner.ModId}'", false);

                    return false;
                }

                takenHashIds.Add(HashId);

                networkprefab.Initialize();
                entity.OnValidate();

                return true;
            }

            if (hashIds is null) {
                MessageError(mod, "Attempted to register a custom prefab, but something unexpected happened.");

                return null;
            }

            bool alreadyHadId = false;

            foreach (var data in hashIds.ItemHashes) {
                if (data.Key == CustomPrefabId) {
                    bool done = SetHashId(data.Value);

                    if (!done)
                        return null;

                    alreadyHadId = true;

                    break;
                }
            }

            if (!alreadyHadId) {
                int nextAvaliable = GetNextAvailableHashId();

                if (nextAvaliable <= 0) {
                    MessageError(mod, "Attempted to register a custom prefab, but GetNextAvailableHashId returned an error.");

                    return null;
                }

                hashIds.ItemHashes.Add(CustomPrefabId, nextAvaliable);

                bool done = SetHashId(nextAvaliable);

                if (!done)
                    return null;
            }

            NetworkPrefab[] prefabArray = new NetworkPrefab[] { networkprefab };

            MethodInfo methodInfo = typeof(PrefabManager).GetMethod("AddToPrefabMap", BindingFlags.NonPublic | BindingFlags.Static);
            methodInfo.Invoke(null, new object[] { prefabArray });

            RegisteredCustomPrefabs.Add(networkprefab);

            MelonLogger.Msg($"Successfully registered {prefab.name} as a NetworkPrefab with HashId of '{networkprefab.Hash}'!");

            return networkprefab;
        }
    }
}