using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alta.Networking;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using SyncLib.Items;

namespace SyncLib {
    // Done by VMan.

    internal static class ClientPrefabSupport {
        private const string Prefix = "SLPF1|";
        private sealed class State {
            public State() { }
            internal readonly RegistryChunkCodec.Receiver Receiver = new RegistryChunkCodec.Receiver();
            internal HashSet<int> Hashes;
            internal readonly Dictionary<Chunker, Player> Pending = new Dictionary<Chunker, Player>();
        }
        private static readonly ConditionalWeakTable<Connection, State> states = new ConditionalWeakTable<Connection, State>();
        private static Connection server;
        private static bool reportScheduled;

        internal static void SetServerConnection(Connection connection) {
            server = connection;
            reportScheduled = false;
        }

        internal static void ReportRegisteredPrefabs() {
            if (NetworkSceneManager.IsServer || !NetworkPrefabRegistry.hasRegistered || !NetworkPrefabRegistry.recievedSyncData || server == null || server.IsDisposed || reportScheduled)
                return;
            reportScheduled = true;
            MelonCoroutines.Start(SendReport(server));
        }

        private static IEnumerator SendReport(Connection connection) {
            var channel = RegistryTransfer.ReliableChannel(connection);
            if (!ReferenceEquals(server, connection)) yield break;
            if (channel == null) { MelonLogger.Warning("SYNC_PREFABS_REPORT_FAILED: no reliable channel"); yield break; }
            var hashes = NetworkPrefabRegistry.RegisteredCustomPrefabs.Where(p => p.Value != null).Select(p => p.Key).ToArray();
            var chunks = RegistryChunkCodec.Encode(JsonConvert.SerializeObject(hashes), Guid.NewGuid().ToString("N"));
            foreach (var chunk in chunks) {
                if (!ReferenceEquals(server, connection)) yield break;
                bool sent = false;
                for (int attempt = 0; attempt < 10 && !connection.IsDisposed && !sent; attempt++) {
                    string body = Prefix + chunk.Substring(6);
                    try { sent = connection.Send(channel, SyncLib.JsonSync, (c, s) => s.SerializeString(ref body)); }
                    catch (Exception ex) { MelonLogger.Warning("SYNC_PREFABS_REPORT_FAILED: " + ex.Message); }
                    if (!sent) yield return null;
                }
                if (!sent) { MelonLogger.Warning("SYNC_PREFABS_REPORT_FAILED: queue rejected report"); yield break; }
                yield return null;
            }
            connection.FlushPacketManagers();
            MelonLogger.Msg("SYNC_PREFABS_REPORTED count=" + hashes.Length);
        }

        internal static void ReceiveReport(Connection connection, string value) {
            if (!NetworkSceneManager.IsServer || connection == null || value == null || !value.StartsWith(Prefix, StringComparison.Ordinal)) return;
            var state = states.GetOrCreateValue(connection);
            if (state.Hashes != null) return;
            try {
                if (!state.Receiver.Accept("SLCH1|" + value.Substring(6), out var json)) return;
                var hashes = JsonConvert.DeserializeObject<int[]>(json);
                if (hashes == null) throw new InvalidOperationException("Missing prefab list");
                state.Hashes = new HashSet<int>(hashes);
            }
            catch (Exception ex) { MelonLogger.Warning("SYNC_PREFABS_REPORT_INVALID: " + ex.Message); return; }
            MelonLogger.Msg("SYNC_PREFABS_RECEIVED count=" + state.Hashes.Count);
            var pending = state.Pending.OrderBy(p => Depth(p.Key)).ToArray();
            state.Pending.Clear();
            var started = new List<KeyValuePair<Chunker, Player>>();
            foreach (var pair in pending) {
                var entity = pair.Key.NetworkEntity;
                var chunk = pair.Key.Chunk;
                if (connection.IsDisposed || pair.Value == null || entity == null || entity.IsBeingDestroyed || chunk == null ||
                    (!chunk.IsGlobal && !pair.Value.LoadedChunks.Contains(chunk)) || !CanSpawn(pair.Key, pair.Value)) continue;
                if (((IChunker)pair.Key).StartSpawn(pair.Value, false, false)) started.Add(pair);
            }
            for (int i = started.Count - 1; i >= 0; i--)
                ((IChunker)started[i].Key).EndSpawn(started[i].Value, false);
        }

        private static int Depth(Chunker chunker) {
            int depth = 0;
            for (var parent = chunker.Parent; parent != null; parent = parent.Parent) depth++;
            return depth;
        }

        internal static bool CanSpawn(Chunker chunker, Player player) {
            if (!NetworkSceneManager.IsServer) return true;
            var connection = player?.ConnectionToRemotePlayer;
            for (var current = chunker; current != null; current = current.Parent) {
                var prefab = current.NetworkEntity?.Prefab;
                if (prefab == null || !NetworkPrefabRegistry.RegisteredCustomPrefabs.ContainsKey((int)prefab.Hash)) continue;
                if (connection == null) return false;
                var state = states.GetOrCreateValue(connection);
                if (state.Hashes == null) {
                    state.Pending[chunker] = player;
                    return false;
                }
                if (!state.Hashes.Contains((int)prefab.Hash)) return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class OptionalPrefabSpawnPatch {
        private static MethodBase TargetMethod() {
            var map = typeof(Chunker).GetInterfaceMap(typeof(IChunker));
            for (int i = 0; i < map.InterfaceMethods.Length; i++)
                if (map.InterfaceMethods[i].Name == nameof(IChunker.StartSpawn)) return map.TargetMethods[i];
            throw new MissingMethodException("Chunker.StartSpawn");
        }
        private static bool Prefix(Chunker __instance, Player target, ref bool __result) {
            if (ClientPrefabSupport.CanSpawn(__instance, target)) return true;
            __result = false;
            return false;
        }
    }
}