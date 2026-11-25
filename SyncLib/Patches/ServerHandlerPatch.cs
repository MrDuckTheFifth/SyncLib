using System;
using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MelonLoader;

namespace SyncLib {
    [HarmonyPatch(typeof(ServerHandler), "ConnectionCreated", new Type[] { typeof(Connection) })]
    internal static class ServerHandlerPatch {
        private static void Postfix(Connection connection) {
            MelonLogger.Msg("Player is connecting, waiting for connection approval.");
            connection.SetHandler(SyncLib.JsonSync, SyncLib.JsonSerialize);
            connection.Approved += OnApproved;
        }

        private static void OnApproved(Connection connection) {
            MelonLogger.Msg("Player connection was approved, waiting for chunked registry transfer.");
            RegistryTransfer.SendWhenReady(connection);
        }
    }

}