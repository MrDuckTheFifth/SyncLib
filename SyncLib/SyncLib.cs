using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MelonLoader;
using SyncLib.Prefabs;
using System;
using Assembly = System.Reflection.Assembly;

[assembly: MelonInfo(typeof(SyncLib.SyncLib), "SyncLib", "1.0.0", "MrDuckTheFifth")]
[assembly: MelonGame("Alta", "A Township Tale")]
namespace SyncLib {
    public class SyncLib : MelonMod {
        /* 
        Just a tiny note for anyone reading this code and trying to figure out MessageTypes, apparentally only MessageTypes up to 31 will actually work :)

        I had to learn this the hard way.

        - John Sync
        */
        public static MessageType JsonSync = (MessageType)18;

        private static int[] _existingHashIDs;

        public static int[] ExistingHashIDs => _existingHashIDs;

        public override void OnInitializeMelon() {
            base.OnInitializeMelon();

            using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SyncLib.ExistingPrefabIDs.txt")) {
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
        }

        internal static void JsonSerialize(Connection connection, Alta.Serialization.Stream stream) {
            string json = SL_NetworkPrefabRegistry.jsonData;
            stream.SerializeString(ref json);
            if (stream.IsReading && !NetworkSceneManager.IsServer) {
                MelonLogger.Msg($"Received custom item json data from server: {json}");
                SL_NetworkPrefabRegistry.jsonData = json;
            }
        }
    }

    // Writing these three patches and magically fixing all of the bugs caused the happiest day of my life

    [HarmonyPatch(typeof(Socket), "CreateConnection", new Type[] { typeof(string), typeof(int) })]
    internal static class ISocketPatch {
        private static void Postfix(ref Connection __result) {
            MelonLogger.Msg("Connection created to server, waiting for HashId json data...");

            __result.SetHandler(SyncLib.JsonSync, SyncLib.JsonSerialize);
        }
    }

    [HarmonyPatch(typeof(PrefabManager), "PrepareSpawnSetups")]
    internal static class PrefabManagerPatch {
        private static void Postfix() {
            SL_NetworkPrefabRegistry.RegisterIntoGame();
        }
    }

    [HarmonyPatch(typeof(ServerHandler), "ConnectionCreated", new Type[] { typeof(Connection) })]
    internal static class ServerHandlerPatch {
        private static void Postfix(Connection connection) {
            MelonLogger.Msg("Player is connecting, waiting for connection approval.");
            
            if(NetworkSceneManager.IsServer && SL_NetworkPrefabRegistry.jsonData is null)
                SL_NetworkPrefabRegistry.ReadJsonFile();

            connection.SetHandler(SyncLib.JsonSync, SyncLib.JsonSerialize);

            connection.Approved += OnApproved;
        }

        private static void OnApproved(Connection connection) {
            MelonLogger.Msg("Player connection was approved, attempting to send HashId json data.");

            bool result = connection.Send(null, SyncLib.JsonSync, SyncLib.JsonSerialize);

            if (!result) {
                MelonLogger.Error("Failed to send json data to client.");
            }

            connection.Approved -= OnApproved;
        }
    }
}