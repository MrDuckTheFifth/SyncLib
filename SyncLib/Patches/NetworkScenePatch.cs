using System;
using Alta.Networking;
using HarmonyLib;
using SyncLib.Items;

namespace SyncLib {
    [HarmonyPatch(typeof(NetworkScene), "FirstInitialize", new Type[] { typeof(bool) })]
    internal static class NetworkScenePatch {
        private static void Postfix(bool isServer) {
            if (isServer) {
                PrefabManager.PrepareSpawnSetups();
                NetworkPrefabRegistry.RegisterIntoGame();
            }
        }
    }
}