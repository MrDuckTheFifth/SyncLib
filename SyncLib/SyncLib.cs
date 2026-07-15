using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MelonLoader;
using SyncLib.Prefabs;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

[assembly: MelonInfo(typeof(SyncLib.SyncLib), "SyncLib", "1.0.0", "MrDuckTheFifth")]
[assembly: MelonGame("Alta", "A Township Tale")]
namespace SyncLib {
    public class SyncLib : MelonMod {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int MessageBox(
            IntPtr hWnd,
            string text,
            string caption,
            uint type
        );

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
        }

        public override void OnLateInitializeMelon() {
            base.OnLateInitializeMelon();

            if (_existingHashIDs is null) {
                LoggerInstance.Error("Something went wrong while reading ExistingPrefabIDs.");

                return;
            }

            //Player.LocalPlayerSet += LocalPlayerSet;
        }

        //internal static List<Type> RPCs = new List<Type>();

        //private void LocalPlayerSet(IPlayer p) {
        //    Player player = p as Player;

        //    GameObject playerObject = player.gameObject;

        //    foreach (Type Type in RPCs) {
        //        playerObject.AddComponent(Type);
        //    }
        //}

        internal static void JsonSerialize(Connection connection, Alta.Serialization.Stream stream) {
            string json = NetworkPrefabRegistry.jsonData;

            if (NetworkSceneManager.IsServer) {
                MelonLogger.Msg($"Sending Json data to player.");
            }

            stream.SerializeString(ref json);
            if (stream.IsReading && !NetworkSceneManager.IsServer) {
                MelonLogger.Msg($"Received custom item json data from server!");

                if (string.IsNullOrWhiteSpace(json)) {
                    MelonLogger.Error($"An error occured while getting Json data from the server. Please try joining again.");

                    MessageBox(GetActiveWindow(), $"An error occured while getting Json data from the server. Please try joining again.", "SyncLib - Server Error, (A Township Tale)", 0);

                    Application.Quit();
                }

                NetworkPrefabRegistry.recievedSyncData = true;

                NetworkPrefabRegistry.jsonData = json;
            }
        }
    }

    // Writing these patches and magically fixing all of the bugs caused the happiest day of my life

    // Remember to remove this since TavernLib is adding it next update.
    // This is just for development purposes because I have no wifi right now lmao
    [HarmonyPatch(typeof(ApiAccess), "IsConnectedToInternetInternal")]
    internal static class ApiAccessPatch {
        private static void Postfix(ref bool __result) {
            __result = true;
        }
    }

    [HarmonyPatch(typeof(Socket), "CreateConnection", new Type[] { typeof(string), typeof(int) })]
    internal static class ISocketPatch {
        private static void Postfix(ref Connection __result) {
            MelonLogger.Msg("Connection created to server, waiting for HashId json data...");

            __result.SetHandler(SyncLib.JsonSync, SyncLib.JsonSerialize);
        }
    }

    [HarmonyPatch(typeof(PrefabManager), "PrepareSpawnSetups")]
    internal static class OrefabManagerPatch {
        private static void Postfix() {
            if (!NetworkSceneManager.IsServer) {
                NetworkPrefabRegistry.RegisterIntoGame();
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
            MelonLogger.Msg("Player connection was approved, attempting to send HashId json data.");

            bool result = connection.Send(null, SyncLib.JsonSync, SyncLib.JsonSerialize);

            if (!result) {
                MelonLogger.Error("Failed to send json data to client.");
            }

            connection.Approved -= OnApproved;
        }
    }

    // I keep looking over my shoulder because I feel like someone is watching me write code through my window and secretly laughing at me.










    // Actually I think I'm just losing my mind
}