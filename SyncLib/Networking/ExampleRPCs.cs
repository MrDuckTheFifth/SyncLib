using Alta.Networking.Scripts.Player;
using MelonLoader;

namespace SyncLib.Networking {
    public class ExampleRPCs : SimpleRPC {
        public override void RPC_Awake() {
            SendToServer();
        }

        public override void ReceievedAsServer(IPlayer player) {
            base.ReceievedAsServer(player);
            MelonLogger.Msg($"Received SimpleRPC from {player.UserInfo.Username}!");
        }
    }
}