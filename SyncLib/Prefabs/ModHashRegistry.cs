using System.Collections.Generic;

namespace SyncLib.Items {
    public class ModHashRegistry {
        public string ModId { get; set; }

        public string LastKnownVersion { get; set; }

        public bool Installed { get; set; }

        public Dictionary<string, int> ItemHashes { get; set; } = new Dictionary<string, int>();

        public ModHashRegistry(string modId, string version) {
            ModId = modId;
            LastKnownVersion = version;
            Installed = true;
        }
    }
}