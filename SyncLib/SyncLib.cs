using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Alta.Networking;
using MelonLoader;
using SyncLib;

[assembly: MelonInfo(typeof(SyncLib.SyncLib), "SyncLib", "1.0.5", "MrDuckTheFifth")]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace SyncLib {
    public class SyncLib : MelonMod {
        internal static SyncLib instance;

        public static MessageType JsonSync = (MessageType)18;

        private static int[] _existingHashIDs;

        public static int[] ExistingHashIDs => _existingHashIDs;

        [DllImport("user32.dll")]
        internal static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public override void OnEarlyInitializeMelon() {
            using System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SyncLib.Dependencies.ATT Workshop Utilities.dll");

            if (stream != null) {
                byte[] array = new byte[stream.Length];
                stream.Read(array, 0, array.Length);
                Assembly.Load(array);

                MelonLogger.Msg("Successfully loaded workshop utilities!");
            }
        }

        public override void OnInitializeMelon() {
            base.OnInitializeMelon();
            instance = this;

            MelonLogger.Msg("SyncLib Initalized.");

            using System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SyncLib.Prefabs.ExistingPrefabIDs.txt");
            using System.IO.StreamReader streamReader = new System.IO.StreamReader(stream);

            string[] array = streamReader.ReadToEnd().Replace("\r", "").Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            _existingHashIDs = new int[array.Length];

            for (int i = 0; i < array.Length; i++) {
                if (int.TryParse(array[i], out var result)) {
                    _existingHashIDs[i] = result;
                }
                else {
                    base.LoggerInstance.Error("Failed to parse '" + array[i] + "' from ExistingPrefabIDs.txt.");
                }
            }

            ExampleItemRegistry.Awake();
        }

        public override void OnLateInitializeMelon() {
            base.OnLateInitializeMelon();
            if (_existingHashIDs == null) {
                base.LoggerInstance.Error("Something went wrong while reading ExistingPrefabIDs.");
            }
        }

        public override void OnUpdate() {
            RegistryTransfer.Tick();
        }

        internal static void JsonSerialize(Connection connection, Alta.Serialization.Stream stream) {
            RegistryTransfer.Receive(connection, stream);
        }
    }
}