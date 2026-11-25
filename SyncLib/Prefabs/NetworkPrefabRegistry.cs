using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ATT_Workshop_Utilities;
using Alta;
using Alta.Inventory;
using Alta.Loot;
using Alta.Networking;
using Alta.Utilities;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using Alta.Crafting;

namespace SyncLib.Items {
    public static class NetworkPrefabRegistry {
        public static Dictionary<int, NetworkPrefab> RegisteredCustomPrefabs = new Dictionary<int, NetworkPrefab>();

        /// <summary>
        /// Lookup from a mod's CustomPrefabId to HashId.
        /// 
        /// <br></br>
        /// <br></br>
        /// 
        /// The syntax is "CustomPrefabId (ModName)" to avoid conflicts between mods.
        /// 
        /// <br></br>
        /// <br></br>
        /// 
        /// EXAMPLE:
        /// <br></br>
        ///     "HangGlider (Hang Glider)"
        /// </summary>
        public static Dictionary<string, int> RegisteredCustomPrefabIdsLookup = new Dictionary<string, int>();

        public static Dictionary<Alta.Inventory.Item, ATT_Workshop_Utilities.Item> RegisteredCustomItems = new Dictionary<Alta.Inventory.Item, ATT_Workshop_Utilities.Item>();

        private static string saveFilePath;

        internal static string jsonData = null;

        private static bool inRegistryProcess;

        internal static bool recievedSyncData = false;

        internal static bool hasRegistered = false;

        private static HashSet<int> takenHashIds;

        internal static GameObject JointSlotReference;

        internal static GameObject JointInsertReference;

        internal static HashRegistryFile Registry { get; set; }

        internal static string clientSerializableJsonData { get; set; }

        public static event Action OnItemsFinishedRegistering;

        public static event Action RegisterPrefabs;

        private static ModHashRegistry GetOrCreateNewModHashIds(this MelonMod mod) {
            string id = mod.Info.Name + "." + mod.Info.Author;
            ModHashRegistry modHashRegistry = Registry.Mods.FirstOrDefault((ModHashRegistry x) => x.ModId == id);

            if (modHashRegistry == null) {
                modHashRegistry = new ModHashRegistry(id, mod.Info.Version);
                Registry.Mods.Add(modHashRegistry);
            }

            modHashRegistry.LastKnownVersion = mod.Info.Version;
            modHashRegistry.Installed = true;
            return modHashRegistry;
        }

        internal static void RefreshInstalledMods() {
            foreach (ModHashRegistry mod in Registry.Mods) {
                mod.Installed = false;
            }
            foreach (MelonMod registeredMelon in MelonTypeBase<MelonMod>.RegisteredMelons) {
                string id = registeredMelon.Info.Name + "." + registeredMelon.Info.Author;
                ModHashRegistry modHashRegistry = Registry.Mods.FirstOrDefault((ModHashRegistry x) => x.ModId == id);

                if (modHashRegistry != null) {
                    modHashRegistry.Installed = true;
                    modHashRegistry.LastKnownVersion = registeredMelon.Info.Version;
                }
            }
        }

        internal static void ReadJsonFile() {
            if (!NetworkSceneManager.IsServer) {
                if (string.IsNullOrWhiteSpace(jsonData)) {
                    MelonLogger.Error("Client has no custom item registry.");
                }
                else {
                    Registry = JsonConvert.DeserializeObject<HashRegistryFile>(jsonData);
                }
                return;
            }

            saveFilePath = Path.Combine(AltaIO.ServersFolder.GetSubfolder("-1").GetSubfolder("Save").Path, "modHashIds.json");
            string text = saveFilePath;

            if (string.IsNullOrWhiteSpace(text)) {
                MessageError(SyncLib.instance, "Something went wrong while getting custom item data.");
                return;
            }
            if (!File.Exists(text)) {
                Registry = new HashRegistryFile();
                return;
            }

            Registry = JsonConvert.DeserializeObject<HashRegistryFile>(File.ReadAllText(text));

            RefreshInstalledMods();
        }

        internal static void RegisterIntoGame() {
            if (hasRegistered || (!NetworkSceneManager.IsServer && !recievedSyncData)) {
                return;
            }

            MelonLogger.Msg("Registering custom prefabs...");

            takenHashIds = new HashSet<int>(SyncLib.ExistingHashIDs);

            ReadJsonFile();

            foreach (ModHashRegistry mod in Registry.Mods) {
                foreach (int value in mod.ItemHashes.Values) {
                    takenHashIds.Add(value);
                }
            }

            HashedGeneralValue<Alta.Inventory.Item>.CheckItems();

            if ((object)JointSlotReference == null) {
                GameObject gameObject = HashedGeneralValue<Alta.Inventory.Item>.Get(10650u).Prefab.GetComponentInChildren<Alta.Crafting.JointSlot>().gameObject;
                if (gameObject != null) {
                    JointSlotReference = gameObject;
                }
            }
            if ((object)JointInsertReference == null) {
                GameObject gameObject2 = HashedGeneralValue<Alta.Inventory.Item>.Get(42570u).Prefab.GetComponentInChildren<Alta.Crafting.JointInsert>().gameObject;
                if (gameObject2 != null) {
                    JointInsertReference = gameObject2;
                }
            }

            inRegistryProcess = true;
            NetworkPrefabRegistry.RegisterPrefabs?.Invoke();
            inRegistryProcess = false;

            if (!NetworkSceneManager.IsServer) {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                foreach (ModHashRegistry mod2 in Registry.Mods) {
                    foreach (int value2 in mod2.ItemHashes.Values) {
                        if (!RegisteredCustomPrefabs.ContainsKey(value2)) {
                            dictionary[mod2.ModId] = mod2.LastKnownVersion;
                        }
                    }
                }
                if (dictionary.Count > 0) {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine("Optional item mods unavailable; their items will be hidden:\n");
                    foreach (KeyValuePair<string, string> item in dictionary) {
                        stringBuilder.AppendLine("- " + item.Key + " (VERSION: " + item.Value + ")");
                    }
                    string message = stringBuilder.ToString();
                    MelonLogger.Warning(message);
                }
            }
            else {
                File.WriteAllText(saveFilePath, jsonData = JsonConvert.SerializeObject(Registry, Formatting.Indented));
                clientSerializableJsonData = JsonConvert.SerializeObject(new HashRegistryFile {
                    FormatVersion = Registry.FormatVersion,
                    Mods = Registry.Mods.Where((ModHashRegistry x) => x.Installed).ToList()
                }, Formatting.Indented);
            }

            hasRegistered = true;

            ClientPrefabSupport.ReportRegisteredPrefabs();

            NetworkPrefabRegistry.OnItemsFinishedRegistering?.Invoke();
        }

        internal static int GetNextAvailableHashId() {
            while (takenHashIds.Contains(Registry.NextHashId)) {
                Registry.NextHashId++;
            }
            int num = Registry.NextHashId++;
            takenHashIds.Add(num);
            return num;
        }

        public static void ExecuteAllGetComponentAtts(params Component[] comps) {
            if (comps == null) {
                return;
            }
            foreach (Component component in comps) {

                if (component == null) {
                    continue;
                }
                Type type = component.GetType();
                List<FieldInfo> list = new List<FieldInfo>();

                while (type != null && type != typeof(MonoBehaviour) && type != typeof(Component)) {
                    FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    list.AddRange(fields);
                    type = type.BaseType;
                }
                foreach (FieldInfo item in list) {
                    item.GetCustomAttribute<PrefabApplyAttribute>()?.HandleApply(component, item, component.gameObject);
                }
            }
        }

        private static void MessageError(MelonMod mod, string message, bool doMessageBox = true) {
            string text = ((mod != null) ? ("[FROM: '" + mod.Info.Name + "']") : "[FROM: 'Unknown']");
            MelonLogger.Error(text + " " + message);
            if (doMessageBox) {
                string text2 = (NetworkSceneManager.IsServer ? "The server will be closed to prevent data corruption." : "A Township Tale will be closed to prevent any further errors.");
                SyncLib.MessageBox(SyncLib.GetActiveWindow(), text + " " + message + "\n\n" + text2, "SyncLib - Error Occurred, (A Township Tale)", 0u);
                Application.Quit();
            }
        }

        private static void SetValue(this Alta.Inventory.Item item, Traverse traverse, string field, object value) {
            traverse.Field(field).SetValue(value);
        }

        public static T? FindScriptableObjectOfName<T>(string name, bool contains = false) where T : UnityEngine.Object {
            name = Regex.Replace(name, "(?<=[a-z0-9])(?<!_)([A-Z])", " $1");
            name = name.Replace("__", "-");
            if (contains) {
                return Resources.FindObjectsOfTypeAll(typeof(T)).FirstOrDefault((UnityEngine.Object obj) => obj.name.Contains(name)) as T;
            }
            return Resources.FindObjectsOfTypeAll(typeof(T)).FirstOrDefault((UnityEngine.Object obj) => obj.name == name) as T;
        }

        private static Alta.Inventory.Item CreateNewItem(string itemName, ATT_Workshop_Utilities.Item settings) {
            Alta.Inventory.Item item = ScriptableObject.CreateInstance<Alta.Inventory.Item>();
            item.name = itemName;
            Traverse traverse = Traverse.Create(item);

            if (settings.LootCategory != Enums.LootCategory.NoneOrCustom) {
                Category category = FindScriptableObjectOfName<Category>(settings.LootCategory.ToString());
                if (category != null) {
                    item.SetValue(traverse, "lootCategory", category);
                }
            }

            if (settings.LootValue != Enums.LootValue.NoneOrCustom) {
                LootValue lootValue = FindScriptableObjectOfName<LootValue>(settings.LootValue.ToString(), contains: true);
                if (lootValue != null) {
                    item.SetValue(traverse, "lootValue", lootValue);
                }
            }

            if (settings.PickupTags != null && settings.PickupTags.Length != 0) {
                List<PickupTag> list = new List<PickupTag>();
                Enums.PickupTag[] pickupTags = settings.PickupTags;
                for (int i = 0; i < pickupTags.Length; i++) {
                    Enums.PickupTag pickupTag = pickupTags[i];
                    PickupTag pickupTag2 = FindScriptableObjectOfName<PickupTag>(pickupTag.ToString());
                    if (pickupTag2 != null) {
                        list.Add(pickupTag2);
                    }
                }
                if (list != null && list.Count > 0) {
                    item.SetValue(traverse, "tags", list);
                }
            }

            item.SetValue(traverse, "storeScale", settings.StoreScale);
            if (settings.scaledPositioning == null) {
                settings.scaledPositioning = new ATT_Workshop_Utilities.DockPositioning();
            }

            Alta.Inventory.Item.DockPositioning value = new Alta.Inventory.Item.DockPositioning(settings.scaledPositioning.storePosition, settings.scaledPositioning.storeEuler);
            if (settings.normalPositioning == null) {
                settings.normalPositioning = new ATT_Workshop_Utilities.DockPositioning();
            }

            Alta.Inventory.Item.DockPositioning value2 = new Alta.Inventory.Item.DockPositioning(settings.normalPositioning.storePosition, settings.normalPositioning.storeEuler);
            item.SetValue(traverse, "scaledPositioning", value);
            item.SetValue(traverse, "normalPositioning", value2);
            if (settings.customPositions == null) {
                settings.customPositions = new CustomDockPositioning[0];
            }

            Alta.Inventory.Item.CustomDockPositioning[] array = new Alta.Inventory.Item.CustomDockPositioning[settings.customPositions.Length];
            if (settings.customPositions.Length != 0) {
                for (int j = 0; j < settings.customPositions.Length; j++) {
                    CustomDockPositioning customDockPositioning = settings.customPositions[j];
                    array[j] = new Alta.Inventory.Item.CustomDockPositioning(null, customDockPositioning.storePosition, customDockPositioning.storeEuler);
                }
            }

            if (settings.customPositionsWhenCrafted == null) {
                settings.customPositionsWhenCrafted = new CustomDockPositioning[0];
            }

            Alta.Inventory.Item.CustomDockPositioning[] array2 = new Alta.Inventory.Item.CustomDockPositioning[settings.customPositionsWhenCrafted.Length];
            if (settings.customPositionsWhenCrafted.Length != 0) {
                for (int k = 0; k < settings.customPositionsWhenCrafted.Length; k++) {
                    CustomDockPositioning customDockPositioning2 = settings.customPositionsWhenCrafted[k];
                    array2[k] = new Alta.Inventory.Item.CustomDockPositioning(null, customDockPositioning2.storePosition, customDockPositioning2.storeEuler);
                }
            }

            item.SetValue(traverse, "customPositions", array);
            item.SetValue(traverse, "customPositionsWhenCrafted", array2);
            item.SetValue(traverse, "isStackable", settings.isStackable);
            item.SetValue(traverse, "isStackableWhenCrafted", settings.isStackableWhenCrafted);
            item.SetValue(traverse, "size", settings.size);
            item.SetValue(traverse, "dockedStackSize", settings.dockedStackSize);
            item.SetValue(traverse, "weight", settings.weight);
            item.SetValue(traverse, "isUsingShrunkenVisual", settings.isUsingShrunkenVisual);
            item.SetValue(traverse, "isOverridingDefaultShrunkenScale", settings.isOverridingDefaultVisualScale);
            item.SetValue(traverse, "overrideShrunkenScale", settings.overrideShrunkenScale);
            item.SetValue(traverse, "isOverridingDefaultVisualScale", settings.isOverridingDefaultVisualScale);
            item.SetValue(traverse, "overrideScaleInsideVisual", settings.overrideScaleInsideVisual);
            item.SetValue(traverse, "isSnappingToHand", settings.isSnappingToHand);
            item.SetValue(traverse, "isAssistGrabBlocked", settings.isAssistGrabBlocked);
            RegisteredCustomItems.Add(item, settings);
            return item;
        }

        internal static void AttachNetworkComponentsToBase(this GameObject prefab, out NetworkPrefab networkprefab, out NetworkEntity entity) {
            networkprefab = null;
            entity = null;
            networkprefab = prefab.GetComponent<NetworkPrefab>();

            if ((object)networkprefab == null) {
                networkprefab = prefab.AddComponent<NetworkPrefab>();
            }

            entity = prefab.GetComponent<NetworkEntity>();

            if ((object)entity == null) {
                entity = prefab.AddComponent<NetworkEntity>();
            }
        }

        private static ModHashRegistry GetHashOwner(int hash, ModHashRegistry owner) {
            foreach (ModHashRegistry mod in Registry.Mods) {
                if (mod != owner && mod.ItemHashes.Values.Contains(hash)) {
                    return mod;
                }
            }

            return null;
        }

        [Obsolete("This method is obsolete. SyncLib will now automatically detect and register custom items in \"RegisterCustomPrefab\".\nThis method will now simply return the existing item attached to the network prefab..")]
        public static Alta.Inventory.Item? RegisterCustomItem(this NetworkPrefab prefab) {
            return prefab.Entity.CommonPickup.Item;
        }

        private static Alta.Inventory.Item? _internalRegisterCustomItem(NetworkPrefab prefab) {
            ATT_Workshop_Utilities.Item component = prefab.GetComponent<ATT_Workshop_Utilities.Item>();

            if (component == null) {
                return null;
            }

            if (component.PhysicalMaterial != Enums.PhysicalMaterial.NoneOrCustom) {
                PhysicalMaterial? physicalMaterial = FindScriptableObjectOfName<PhysicalMaterial>(component.PhysicalMaterial.ToString());

                if (physicalMaterial != null) {
                    PhysicalMaterialPart physicalMaterialPart = prefab.gameObject.AddComponent<PhysicalMaterialPart>();

                    Traverse.Create(physicalMaterialPart).Field("physicalMaterial").SetValue(physicalMaterial);

                    ExecuteAllGetComponentAtts(physicalMaterialPart);
                }
            }

            Alta.Inventory.Item item = CreateNewItem(prefab.name, component);
            item.Prefab = prefab;

            Traverse.Create(typeof(HashedGeneralValue<Alta.Inventory.Item>)).Field("items").GetValue<Dictionary<uint, Alta.Inventory.Item>>().Add(prefab.Hash, item);

            Traverse.Create(item).Field("hash").SetValue((int)prefab.Hash);

            MelonLogger.Msg("Successfully registered " + prefab.name + " as a custom item!");

            return item;
        }

        public static NetworkPrefab? RegisterCustomPrefab(this MelonMod mod, GameObject prefab, string CustomPrefabId, params Type[] AdditionalComponents) {
            if (mod == null) {
                MessageError(mod, "Attempted to register a custom prefab. However the 'mod' parameter was null.");
                return null;
            }

            if (!inRegistryProcess) {
                MessageError(mod, "Attempted to register a custom prefab too late.", doMessageBox: false);
                return null;
            }

            if (Registry == null) {
                MessageError(mod, "Attempted to register a custom prefab. But the HashIds reference was null.");
                return null;
            }

            if (takenHashIds == null) {
                MessageError(mod, "Attempted to register a custom prefab. But the takenHashIds reference was null.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(CustomPrefabId) || CustomPrefabId.Length < 3) {
                MessageError(mod, "Attempted to register a custom prefab with an invalid CustomPrefabId. Please make sure your CustomPrefabId has at least three characters.");
                return null;
            }

            CustomPrefabId = CustomPrefabId + " (" + mod.Info.Name + ")";

            if (!NetworkSceneManager.IsServer && !Registry.Mods.Any((ModHashRegistry m) => m.ItemHashes.Any<KeyValuePair<string, int>>((KeyValuePair<string, int> h) => h.Key == CustomPrefabId))) {
                return null;
            }

            if ((object)prefab == null) {
                MessageError(mod, "Attempted to register a custom prefab that was null.");
                return null;
            }

            prefab.AttachNetworkComponentsToBase(out NetworkPrefab networkprefab, out NetworkEntity entity);

            List<Component> allComps = new List<Component>();

            allComps.Add(networkprefab);
            allComps.Add(entity);

            foreach (Type type in AdditionalComponents) {
                if ((object)type != null) {
                    Component item = prefab.AddComponent(type);
                    allComps.Add(item);
                }
            }

            if (prefab.GetComponent<Rigidbody>() != null) {
                allComps.Add(prefab.AddComponent<NetworkRigidbody>());
            }

            ModHashRegistry hashIds = mod.GetOrCreateNewModHashIds();

            if (hashIds == null) {
                MessageError(mod, "Attempted to register a custom prefab, but something unexpected happened.");
                return null;
            }

            bool flag = false;

            foreach (KeyValuePair<string, int> itemHash in hashIds.ItemHashes) {
                if (itemHash.Key == CustomPrefabId) {
                    if (!SetHashId(itemHash.Value)) {
                        return null;
                    }
                    flag = true;
                    break;
                }
            }

            if (!flag && NetworkSceneManager.IsServer) {
                int nextAvailableHashId = GetNextAvailableHashId();

                if (nextAvailableHashId <= 0) {
                    MessageError(mod, "Attempted to register a custom prefab, but GetNextAvailableHashId returned an error.");
                    return null;
                }

                hashIds.ItemHashes.Add(CustomPrefabId, nextAvailableHashId);

                if (!SetHashId(nextAvailableHashId)) {
                    return null;
                }
            }

            Alta.Inventory.Item? itemComp = _internalRegisterCustomItem(networkprefab);
            WorkshopApplier.ApplyAll(entity, networkprefab, itemComp, ref allComps);

            ExecuteAllGetComponentAtts(allComps.ToArray());

            NetworkPrefab[] array = new NetworkPrefab[1] { networkprefab };
            typeof(PrefabManager).GetMethod("AddToPrefabMap", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[1] { array });

            RegisteredCustomPrefabs.Add((int)networkprefab.Hash, networkprefab);
            RegisteredCustomPrefabIdsLookup.Add(CustomPrefabId, (int)networkprefab.Hash);

            MelonLogger.Msg($"Successfully registered {prefab.name} as a NetworkPrefab with HashId of '{networkprefab.Hash}'!");

            return networkprefab;

            bool SetHashId(int HashId) {
                typeof(NetworkPrefab).GetField("hash", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(networkprefab, HashId);
                ModHashRegistry hashOwner = GetHashOwner(HashId, hashIds);

                if (hashOwner != null) {
                    MessageError(mod, $"Hash '{HashId}' for '{prefab.name}' is already registered by '{hashOwner.ModId}'", doMessageBox: false);
                    return false;
                }

                takenHashIds.Add(HashId);

                networkprefab.Initialize();
                entity.OnValidate();

                return true;
            }
        }
    }
}