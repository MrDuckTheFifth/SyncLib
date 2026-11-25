using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Alta.Networking;
using Alta.Serialization;
using MelonLoader;
using Newtonsoft.Json;
using SyncLib.Items;

namespace SyncLib {
    // Done by VMan.

    internal static class RegistryTransfer {
        internal static Func<double> Clock = () => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        private const double IdleTimeoutSeconds = 60;
        private const double OverallTimeoutSeconds = 180;

        private sealed class Incoming {
            internal readonly RegistryChunkCodec.Receiver Receiver = new RegistryChunkCodec.Receiver();

            internal readonly double Started = Clock();
            internal double LastProgress;

            internal Incoming() { LastProgress = Started; }

            internal bool Complete;
        }

        private static readonly Dictionary<Connection, Incoming> incoming = new Dictionary<Connection, Incoming>();

        private static Connection activeClient;

        private static readonly HashSet<Connection> scheduled = new HashSet<Connection>();

        internal static void Tick() {
            Connection[] array = scheduled.Where((Connection c) => c.IsDisposed).ToArray();
            foreach (Connection item in array) {
                scheduled.Remove(item);
            }
            KeyValuePair<Connection, Incoming>[] array2 = incoming.ToArray();
            for (int num = 0; num < array2.Length; num++) {
                KeyValuePair<Connection, Incoming> keyValuePair = array2[num];
                if (keyValuePair.Key.IsDisposed) {
                    ResetClient(keyValuePair.Key);
                }
                else if (!keyValuePair.Value.Complete) {
                    Incoming transfer = keyValuePair.Value;
                    double now = Clock();
                    double elapsed = now - transfer.Started;
                    double idle = now - transfer.LastProgress;
                    if (elapsed < OverallTimeoutSeconds && idle < IdleTimeoutSeconds) continue;
                    bool noData = transfer.Receiver.ReceivedChunks == 0;
                    string state = noData ? "no_data" : elapsed >= OverallTimeoutSeconds ? "overall_limit" : "stalled";
                    string reason = noData
                        ? "SyncLib: No mod registry data received from the server within 60 seconds. The server or connection may be unresponsive. Try again once the server is responding."
                        : "SyncLib: Mod registry transfer " + (state == "overall_limit" ? "exceeded the 180-second limit" : "stalled for 60 seconds")
                            + " (" + transfer.Receiver.ReceivedChunks + "/" + transfer.Receiver.TotalChunks + " parts received). Please reconnect; if this repeats, ask the server admin to check the server.";
                    MelonLogger.Error("SYNC_REGISTRY_TIMEOUT state=" + state + " received=" + transfer.Receiver.ReceivedChunks
                        + " total=" + transfer.Receiver.TotalChunks + " elapsed_s=" + elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                        + " idle_s=" + idle.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                        + ": " + reason + " Partial registry will not be registered.");
                    keyValuePair.Key.Disconnect(reason);
                    ResetClient(keyValuePair.Key);
                }
            }
        }

        // Both hooks may run for one join. Reinstall the handler without restarting
        // a transfer that may already have completed before SetupConnectionToServer.
        internal static void AttachClient(Connection connection) {
            if (connection == null || connection.IsDisposed) return;
            connection.SetHandler(SyncLib.JsonSync, SyncLib.JsonSerialize);
            if (ReferenceEquals(activeClient, connection)) return;
            if (activeClient != null) ResetClient(activeClient);
            activeClient = connection;
            incoming.Clear();
            incoming.Add(connection, new Incoming());
            NetworkPrefabRegistry.recievedSyncData = false;
            NetworkPrefabRegistry.jsonData = null;
            ClientPrefabSupport.SetServerConnection(connection);
            connection.Disconnected += ResetClient;
            MelonLogger.Msg("SYNC_CLIENT_HANDLER_ATTACHED message=" + (int)SyncLib.JsonSync);
        }

        private static void ResetClient(Connection connection) {
            connection.Disconnected -= ResetClient;
            incoming.Remove(connection);
            if (!ReferenceEquals(activeClient, connection)) return;
            activeClient = null;
            NetworkPrefabRegistry.recievedSyncData = false;
            NetworkPrefabRegistry.jsonData = null;
            ClientPrefabSupport.SetServerConnection(null);
            MelonLogger.Msg("SYNC_CLIENT_CONNECTION_RESET");
        }

        internal static void Receive(Connection connection, Stream stream) {
            string value = null;
            stream.SerializeString(ref value);
            if (!stream.IsReading) {
                return;
            }
            if (NetworkSceneManager.IsServer) {
                ClientPrefabSupport.ReceiveReport(connection, value);
                return;
            }
            if (!ReferenceEquals(activeClient, connection) || connection.IsDisposed) return;
            try {
                if (!incoming.TryGetValue(connection, out var value2)) return;
                if (value2.Complete) {
                    return;
                }
                string json;
                if (value != null && value.StartsWith("SLCH1|", StringComparison.Ordinal)) {
                    int previousCount = value2.Receiver.ReceivedChunks;
                    bool complete = value2.Receiver.Accept(value, out json);
                    if (value2.Receiver.ReceivedChunks > previousCount) {
                        value2.LastProgress = Clock();
                        if (previousCount == 0) MelonLogger.Msg("SYNC_REGISTRY_STARTED total=" + value2.Receiver.TotalChunks);
                    }
                    if (!complete) {
                        return;
                    }
                }
                else {
                    if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) > 2097152) {
                        throw new InvalidOperationException("Invalid legacy registry");
                    }
                    json = value;
                }
                HashRegistryFile hashRegistryFile = JsonConvert.DeserializeObject<HashRegistryFile>(json);
                if (hashRegistryFile == null || hashRegistryFile.Mods == null || hashRegistryFile.Mods.Any((ModHashRegistry m) => m == null || string.IsNullOrEmpty(m.ModId) || m.ItemHashes == null)) {
                    throw new InvalidOperationException("Invalid registry JSON structure");
                }
                value2.Complete = true;
                NetworkPrefabRegistry.jsonData = json;
                NetworkPrefabRegistry.recievedSyncData = true;
                MelonLogger.Msg("SYNC_REGISTRY_RECEIVED chunks=" + value2.Receiver.TotalChunks + " bytes=" + Encoding.UTF8.GetByteCount(json) + " validated=true");
            }
            catch (Exception ex) {
                MelonLogger.Error("SYNC_REGISTRY_RECEIVE_FAILED: " + ex.Message);
                incoming.Remove(connection);
                connection.Dispose();
                return;
            }
            PrefabManagerPatch.TryRegisterClient();
            ClientPrefabSupport.ReportRegisteredPrefabs();
        }

        internal static void SendWhenReady(Connection connection) {
            if (scheduled.Add(connection)) {
                MelonCoroutines.Start(Send(connection));
            }
        }

        internal static ConnectionChannel ReliableChannel(Connection connection) {
            ConnectionChannel defaultChannel = connection.DefaultChannel;
            if (defaultChannel != null && defaultChannel.IsReliable) {
                return defaultChannel;
            }
            FieldInfo field = typeof(Connection).GetField("channels", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((field == null) ? null : (field.GetValue(connection) as ConnectionChannel[]))?.FirstOrDefault((ConnectionChannel c) => c != null && c.IsReliable);
        }

        private static IEnumerator Send(Connection connection) {
            DateTime deadline = DateTime.UtcNow.AddSeconds(60.0);
            while (!connection.IsDisposed && !NetworkPrefabRegistry.hasRegistered && DateTime.UtcNow < deadline) {
                yield return null;
            }
            if (connection.IsDisposed) {
                yield break;
            }
            if (!NetworkPrefabRegistry.hasRegistered) {
                MelonLogger.Error("SYNC_REGISTRY_SEND_FAILED: registry was not ready within 60 seconds.");
                connection.Dispose();
                yield break;
            }
            string[] array;
            ConnectionChannel channel;
            try {
                array = RegistryChunkCodec.Encode(NetworkPrefabRegistry.clientSerializableJsonData, Guid.NewGuid().ToString("N"));
                channel = ReliableChannel(connection);
                if (channel == null) {
                    throw new InvalidOperationException("No reliable connection channel is available");
                }
                Type type = typeof(Connection).Assembly.GetType("Alta.Networking.Internal.OutgoingPacketManager");
                PropertyInfo propertyInfo = ((type == null) ? null : type.GetProperty("PacketSize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
                int limit = ((!(propertyInfo == null)) ? ((int)propertyInfo.GetValue(null, null)) : 0);
                if (limit > 0 && array.Any((string c) => Encoding.ASCII.GetByteCount(c) + 32 > limit)) {
                    throw new InvalidOperationException("Configured packet size is too small for the chunk protocol");
                }
                MelonLogger.Msg("SYNC_REGISTRY_SEND chunks=" + array.Length + " channel=" + channel.Identifier + " reliable=true packetLimit=" + limit);
            }
            catch (Exception ex) {
                MelonLogger.Error("SYNC_REGISTRY_SEND_FAILED: " + ex.Message);
                connection.Dispose();
                yield break;
            }
            int queued = 0;
            string[] array2 = array;
            foreach (string chunk in array2) {
                int attempts = 0;
                bool sent = false;
                while (!connection.IsDisposed && !sent && attempts++ < 10) {
                    string body = chunk;
                    try {
                        sent = connection.Send(channel, SyncLib.JsonSync, delegate (Connection c, Stream s)
                        {
                            s.SerializeString(ref body);
                        });
                    }
                    catch (Exception ex2) {
                        MelonLogger.Error("SYNC_REGISTRY_SEND_FAILED: " + ex2.Message);
                        connection.Dispose();
                    }
                    if (!sent) {
                        yield return null;
                    }
                }
                if (connection.IsDisposed) {
                    yield break;
                }
                if (!sent) {
                    MelonLogger.Error("SYNC_REGISTRY_SEND_FAILED: reliable queue rejected chunk after retries.");
                    connection.Dispose();
                    yield break;
                }
                int num2 = queued + 1;
                queued = num2;
                if (num2 % 4 == 0) {
                    yield return null;
                }
            }
            connection.FlushPacketManagers();
            MelonLogger.Msg("SYNC_REGISTRY_QUEUED chunks=" + queued);
        }
    }
}