using Alta.Networking;
using HarmonyLib;
using SyncLib.Items;

namespace SyncLib {
    [HarmonyPatch(typeof(PrefabManager), "PrepareSpawnSetups")]
    internal static class PrefabManagerPatch {
        private static bool prefabsPrepped;

        private static bool inProcess;

        private static void Postfix() {
            prefabsPrepped = true;
            TryRegisterClient();
        }

        internal static void TryRegisterClient() {
            if (!NetworkPrefabRegistry.hasRegistered && (NetworkSceneManager.IsServer || NetworkPrefabRegistry.recievedSyncData) && !inProcess && prefabsPrepped) {
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
}