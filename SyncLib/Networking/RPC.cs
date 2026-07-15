using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Serialization;
using HarmonyLib;
using MelonLoader;
using System;

namespace SyncLib.Networking {
    public class RPC : NetworkEntityBehaviour {
        public static bool canRegisterRPC = true;

        public static void RegisterRPCs(params RPC[] SimpleRPCs) {
            if (!canRegisterRPC) {
                MelonLogger.Error("Cannot register RPC, please register RPCs before OnLateInitializeMelon.");

                return;
            }

            

            //SyncLib.RPCs.AddRange(SimpleRPCs);
        }

        public virtual EntityMessageType messageType { get; protected set; } = EntityMessageType.NetworkedEventA;

        public Player player { get; private set; }

        private void Awake() {
            player = GetComponent<Player>();
            if (player is null) {
                MelonLogger.Warning("Simple RPCs cannot be attached to non Player objects!");
                Destroy(this);
            }

            Traverse.Create(this).Field("entity").SetValue(GetComponent<NetworkEntity>());
            RPC_Awake();
        }

        public void SendToServer() {
            if (player != Player.Current as Player) return;
            InternalMessage.SendToServer(Entity, messageType, Serialize, NetworkEntity.SyncChannel);
        }

        public virtual void Serialize(IPlayer player, Stream stream) { }

        public virtual void RPC_Awake() { }
    }

    /// <summary>
    /// A simple way for the player to send a network event directly to the server.
    /// </summary>
    public class SimpleRPC : RPC {
        public override void Serialize(IPlayer player, Stream stream) {
            if (stream.IsReading && NetworkSceneManager.IsServer)
                ReceievedAsServer(player);
        }

        public virtual void ReceievedAsServer(IPlayer player) { }
    }

    /// <summary>
    /// A simple way for the player to send a string directly to the server.
    /// </summary>
    public class SimpleStringRPC : RPC {
        public string value { get; protected set; }

        public override void Serialize(IPlayer player, Stream stream) {
            string value = this.value;
            stream.SerializeString(ref value);
            if (stream.IsReading && NetworkSceneManager.IsServer)
                ReceievedAsServer(player, value);
        }

        public virtual void ReceievedAsServer(IPlayer player, string data) { value = data; }
    }
}