using Alta.Networking;
using Alta.Networking.Internal;
using Alta.Networking.Scripts.Player;
using Alta.Networking.Servers;
using Alta.Utilities;
using HarmonyLib;
using MelonLoader;
using SyncLib.Prefabs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static AltaMenuItemBase.Assets.Create.Township.Features;
using Assembly = System.Reflection.Assembly;
using Connection = Alta.Networking.Connection;

[assembly: MelonInfo(typeof(SyncLib.SyncLib), "SyncLib", "1.0.0", "MrDuckTheFifth")]
[assembly: MelonGame("Alta", "A Township Tale")]
namespace SyncLib {
    public class SyncLib : MelonMod {
        internal const EntityMessageType SyncLibJson = (EntityMessageType)255;
        internal const EntityMessageType SyncLibJsonReturn = (EntityMessageType)256;

        internal static PlayerJsonSync localJsonSync;

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

            Player.LocalPlayerSet += LocalPlayerSet;
        }

        private void LocalPlayerSet(IPlayer player) {
            localJsonSync = player.Prefab.gameObject.AddComponent<PlayerJsonSync>();
        }
    }

    [HarmonyPatch(typeof(SceneSerializer), "InitialSyncEntities", new Type[] { typeof(Player) })]
    internal static class SceneSerializerPatch {
        private static Player lastPendingPlayer;

        private static bool Prefix(Player target) {
            if (NetworkSceneManager.IsServer) {
                PlayerJsonSync jsonSync = target.GetComponent<PlayerJsonSync>();

                if (jsonSync != null && !jsonSync.receivedData) {
                    lastPendingPlayer = target;

                    Task.Run(WaitForPlayer);

                    return false;
                }

                return true;
            }
            else
                return true;
        }

        private static async Task WaitForPlayer() {
            Player player = lastPendingPlayer;

            PlayerJsonSync jsonSync = player.GetComponent<PlayerJsonSync>();

            while (!jsonSync.receivedData) {
                await Task.Delay(1);
            }

            MelonCoroutines.Start(DoInitialSync(player));
        }

        private static IEnumerator DoInitialSync(Player player) {
            yield return null;

            INetworkSceneInternal scene = (INetworkSceneInternal)NetworkSceneManager.Current;

            scene.SceneSerializer.InitialSyncEntities(player);
        }
    }

    [HarmonyPatch(typeof(Player), "InitializePlayerOnServer", new Type[] { typeof(Connection), typeof(PlayerMode), typeof(PlatformTarget), typeof(IAltaFile) })]
    internal static class PlayerPatch {
        private static void Postfix(Player __instance) {
            if (NetworkSceneManager.IsServer) {
                if (!SL_NetworkPrefabRegistry.hasRegistered) {
                    string modPrefabFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "modHashIDs.json"));

                    if (File.Exists(modPrefabFilePath)) {
                        string jsonData = File.ReadAllText(modPrefabFilePath);

                        SL_NetworkPrefabRegistry.jsonData = jsonData;
                    }

                    SL_NetworkPrefabRegistry.RegisterIntoGame();
                }

                PlayerJsonSync sync = __instance.GetComponent<PlayerJsonSync>();

                if (sync == null) {
                    sync = __instance.gameObject.AddComponent<PlayerJsonSync>();
                }

                sync.SendPlayerJsonData();
            }
        }
    }
}