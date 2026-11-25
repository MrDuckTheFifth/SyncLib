using System.Collections.Generic;

namespace SyncLib.Items {
    public class HashRegistryFile {
        public int FormatVersion = 1;

        public int NextHashId = 1;

        public List<ModHashRegistry> Mods = new List<ModHashRegistry>();
    }
}