using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MelonLoader;
using SyncLib.Items;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

[assembly: MelonInfo(typeof(SyncLib.SyncLib), "SyncLib", "1.0.4", "MrDuckTheFifth")]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace SyncLib {
    public class SyncLib : MelonMod {
        internal static SyncLib instance;

        [DllImport("user32.dll")]
        internal static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int MessageBox(
            IntPtr hWnd,
            string text,
            string caption,
            uint type
        );

        public static MessageType JsonSync = (MessageType)18;

        private static int[] _existingHashIDs;

        public static int[] ExistingHashIDs => _existingHashIDs;

        public override void OnEarlyInitializeMelon() {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SyncLib.Dependencies.ATT Workshop Utilities.dll")) {
                if (stream == null)
                    return;

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                Assembly.Load(data);

                MelonLogger.Msg("Successfully loaded workshop utilities!");
            }
        }

        public override void OnInitializeMelon() {
            base.OnInitializeMelon();

            instance = this;

            using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SyncLib.Prefabs.ExistingPrefabIDs.txt")) {
                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream)) {
                    string text = reader.ReadToEnd();

                    string[] list = text
                        .Replace("\r", "")
                        .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    _existingHashIDs = new int[list.Length];

                    for (int i = 0; i < list.Length; i++) {
                        if (int.TryParse(list[i], out int value)) {
                            _existingHashIDs[i] = value;
                        }
                        else {
                            LoggerInstance.Error($"Failed to parse '{list[i]}' from ExistingPrefabIDs.txt.");
                        }
                    }
                }
            }

            //ExampleItemRegistry.Awake();
        }

        public override void OnLateInitializeMelon() {
            base.OnLateInitializeMelon();

            if (_existingHashIDs is null) {
                LoggerInstance.Error("Something went wrong while reading ExistingPrefabIDs.");

                return;
            }

            //Player.LocalPlayerSet += LocalPlayerSet;
        }

        internal static void JsonSerialize(Connection connection, Alta.Serialization.Stream stream) {
            string json = NetworkPrefabRegistry.clientSerializableJsonData;

            if (NetworkSceneManager.IsServer) {
                MelonLogger.Msg($"Sending Json data to player.");
            }

            stream.SerializeString(ref json);
            if (stream.IsReading && !NetworkSceneManager.IsServer) {
                MelonLogger.Msg($"Received custom item json data from server!");

                if (string.IsNullOrWhiteSpace(json)) {
                    MelonLogger.Error($"An error occured while getting Json data from the server. Please try joining again.");

                    connection.FlushPacketManagers();

                    connection.Dispose();

                    MessageBox(GetActiveWindow(), $"An error occured while getting Json data from the server. Please try joining again.", "SyncLib - Server Error, (A Township Tale)", 0);

                    Application.Quit();
                }
                else {
                    NetworkPrefabRegistry.recievedSyncData = true;

                    NetworkPrefabRegistry.jsonData = json;

                    PrefabManagerPatch.TryRegisterClient();
                }
            }
        }
    }

    [HarmonyPatch(typeof(Socket), "CreateConnection", new Type[] { typeof(string), typeof(int) })]
    internal static class ISocketPatch {
        private static void Postfix(ref Connection __result) {
            MelonLogger.Msg("Connection created to server, waiting for Json data...");

            __result.SetHandler(SyncLib.JsonSync, SyncLib.JsonSerialize);
        }
    }

    [HarmonyPatch(typeof(PrefabManager), "PrepareSpawnSetups")]
    internal static class PrefabManagerPatch {
        private static bool prefabsPrepped;

        private static bool inProcess;

        private static bool called;

        private static void Postfix() {
            prefabsPrepped = true;

            TryRegisterClient();
        }

        internal static void TryRegisterClient() {
            if (NetworkSceneManager.IsServer && !NetworkPrefabRegistry.recievedSyncData && NetworkPrefabRegistry.hasRegistered)
                return;

            if (!inProcess && prefabsPrepped) {
                inProcess = true;

                try {
                    NetworkPrefabRegistry.RegisterIntoGame();
                }
                finally {
                    inProcess = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(NetworkScene), "FirstInitialize", new Type[] { typeof(bool) })]
    internal static class NetworkScenePatch {
        private static void Postfix(bool isServer) {
            if (isServer) {
                PrefabManager.PrepareSpawnSetups();
                NetworkPrefabRegistry.RegisterIntoGame();
            }
        }
    }

    [HarmonyPatch(typeof(ServerHandler), "ConnectionCreated", new Type[] { typeof(Connection) })]
    internal static class ServerHandlerPatch {
        private static void Postfix(Connection connection) {
            MelonLogger.Msg("Player is connecting, waiting for connection approval.");

            connection.SetHandler(SyncLib.JsonSync, SyncLib.JsonSerialize);

            connection.Approved += OnApproved;
        }

        private static void OnApproved(Connection connection) {
            MelonLogger.Msg("Player connection was approved, attempting to send Json data.");

            if (NetworkPrefabRegistry.hasRegistered && !string.IsNullOrWhiteSpace(NetworkPrefabRegistry.clientSerializableJsonData)) {
                SendRegistry(connection);

                return;
            }

            Action sendIfReady = null;

            sendIfReady = delegate {
                NetworkPrefabRegistry.OnItemsFinishedRegistering -= sendIfReady;
                SendRegistry(connection);
            };

            NetworkPrefabRegistry.OnItemsFinishedRegistering += sendIfReady;

            if (NetworkPrefabRegistry.hasRegistered) {
                sendIfReady();
            }
        }

        private static void SendRegistry(Connection connection) {
            if (!connection.Send(null, SyncLib.JsonSync, SyncLib.JsonSerialize)) {
                MelonLogger.Error("Failed to send json data to client.");
            }
        }
    }

    // Why on earth does "GetComponentInParent" not work SPECIFICALLY ON UNINSTANTIATED PREFABS!?

    // I spent FOUR HOURS trying to figure out why SOME components cannot have their fields set. Just to realize it's all because of Unity being weird.
    [HarmonyPatch(typeof(GetComponentInParentAttribute), nameof(GetComponentInParentAttribute.GetComponent))]
    public static class FixGetComponentInParentPrefabBugPatch {
        private static bool Prefix(Transform transform, Type type, ref Component __result) {
            Transform current = transform;
            while (current != null) {
                Component targetComponent = current.GetComponent(type);
                if (targetComponent != null) {
                    __result = targetComponent;

                    return false;
                }

                current = current.parent;
            }

            __result = null;
            return false;
        }
    }
}