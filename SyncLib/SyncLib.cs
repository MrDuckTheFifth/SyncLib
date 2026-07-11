using Alta.Networking;
using Alta.Networking.Internal;
using Alta.NetworkingTransport;
using Alta.Utilities;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

[assembly: MelonInfo(typeof(SyncLib.SyncLib), "SyncLib", "1.0.0", "MrDuckTheFifth")]
[assembly: MelonGame("Alta", "A Township Tale")]
namespace SyncLib {
    public class SyncLib : MelonMod {
        internal const MessageType SyncLibJson = (MessageType)500;

        internal static GameObject registryObject;

        private static int[] _existingHashIDs;

        public static int[] ExistingHashIDs => _existingHashIDs;

        public override void OnInitializeMelon() {
            base.OnInitializeMelon();

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SyncLib.ExistingPrefabIDs.txt")) {
                using (StreamReader reader = new StreamReader(stream)) {
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
                            LoggerInstance.Error(
                                $"Failed to parse prefab Id: '{list[i]}'"
                            );
                        }
                    }
                }
            }
        }

        public override void OnLateInitializeMelon() {
            base.OnLateInitializeMelon();

            if (_existingHashIDs is null) {
                LoggerInstance.Error("Something went wrong while reading ExistingPrefabIDs.");

                return;
            }

            string modPrefabFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "modHashIDs.json"));

            registryObject = new GameObject("SyncLib - NetworkPrefabRegistry");
            registryObject.AddComponent<SL_NetworkPrefabRegistry>();

            if (File.Exists(modPrefabFilePath)) {
                string jsonData = File.ReadAllText(modPrefabFilePath);

                SL_NetworkPrefabRegistry.jsonData = jsonData;
            }

            DontDestroyOnLoad.DontDestroyOnLoad(registryObject);
        }
    }

    [HarmonyPatch(typeof(Player), "InitializePlayerOnServer", new Type[] { typeof(Connection), typeof(PlayerMode), typeof(PlatformTarget), typeof(IAltaFile) })]
    internal static class PlayerPatch {
        private static void Prefix(Connection connection) {
            if (NetworkSceneManager.IsServer) {
                connection.Send(null, SyncLib.SyncLibJson, Serialize);
            }
        }

        public static void Serialize(Connection connection, Alta.Serialization.Stream stream) {
            string jsonData = SL_NetworkPrefabRegistry.jsonData;
            string finalizedJsonData = "SYNCLIB-JSONSYNC:";
            finalizedJsonData += jsonData;
            stream.SerializeString(ref finalizedJsonData);
        }
    }

    public class JsonConnectionReciever : ConnectionReceiver {
        public JsonConnectionReciever(Connection connection, ConnectionChannel channel) : base(connection, channel) { }

        public override void ProcessExistingQueues() { }

        public override void ReceivePacket(ITransportSocket socket, ArraySegment<byte> data) {
            if (SL_NetworkPrefabRegistry.hasRegistered)
                return;

            while (data.Count > 0) {
                MessageType messageType;
                int num = MessageProcessor.ProcessSingleMessageFromData(connection, data, channel, out messageType);
                int offset = data.Offset + num;
                int count = data.Count - num;
                data = new ArraySegment<byte>(data.Array, offset, count);

                if (messageType == SyncLib.SyncLibJson) {
                    using MemoryStream stream = new MemoryStream(data.Array, data.Offset, data.Count);

                    using (StreamReader reader = new StreamReader(stream)) {
                        string text = reader.ReadToEnd();

                        if (!text.StartsWith("SYNCLIB-JSONSYNC:"))
                            continue;

                        text = text.Replace("SYNCLIB-JSONSYNC:", "");

                        SL_NetworkPrefabRegistry.jsonData = text;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PrefabManager), "PrepareSpawnSetups")]
    internal static class PrefabManagerPatch {
        private static void Postfix() {
            if(NetworkSceneManager.IsServer || SL_NetworkPrefabRegistry.recievedSyncData)
                SL_NetworkPrefabRegistry.RegisterIntoGame();
        }
    }
}