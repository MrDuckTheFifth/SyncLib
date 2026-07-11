using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Serialization;
using MelonLoader;
using System;
using System.Reflection;

namespace SyncLib {
    // John sync
    public class PlayerJsonSync : NetworkEntityBehaviour {
        public MethodSync syncJson;

        internal bool recievedData;

        public IPlayer player;

        private void Awake() {
            NetworkEntity entity = GetComponent<NetworkEntity>();

            if (entity is null) {
                MelonLogger.Error("Attached PlayerJsonSync to player, however NetworkEntity was null.");
            }

            Player player = GetComponent<Player>();

            this.player = player;

            FieldInfo fieldInfoNP = typeof(NetworkEntityBehaviour).GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfoNP == null)
                throw new Exception("Couldn't find NetworkEntityBehaviour.entity");

            fieldInfoNP.SetValue(this, entity);

            Initialize();
        }

        public override void Initialize() {
            base.Initialize();

            syncJson = new MethodSync(base.Entity, SyncLib.SyncLibJson, Serialize);
        }

        internal void SendPlayerJsonData() {
            if (!NetworkSceneManager.IsServer)
                return;

            syncJson.SendToPlayer(player);
        }

        private void Serialize(IPlayer player, Stream stream) {
            string json = SL_NetworkPrefabRegistry.jsonData;
            stream.SerializeString(ref json);
            if (stream.IsReading) {
                SL_NetworkPrefabRegistry.jsonData = json;

                SL_NetworkPrefabRegistry.RegisterIntoGame();

                recievedData = true;
            }
        }
    }
}