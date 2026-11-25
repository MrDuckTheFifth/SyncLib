using System;
using Alta.Networking;
using HarmonyLib;
using MelonLoader;

namespace SyncLib {
    [HarmonyPatch(typeof(Socket), "CreateConnection", new Type[] { typeof(string), typeof(int)})]
    internal static class ISocketPatch {
        private static void Postfix(ref Connection __result) {
            RegistryTransfer.AttachClient(__result);
        }
    }

    [HarmonyPatch(typeof(Alta.Networking.Servers.ConnectToServerUtility), "SetupConnectionToServer")]
    internal static class ClientConnectionSetupPatch {
        private static void Postfix(Connection connection) {
            RegistryTransfer.AttachClient(connection);
        }
    }
}