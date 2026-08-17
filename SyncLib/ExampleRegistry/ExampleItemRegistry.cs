using SyncLib.Items;
using System.Reflection;
using UnityEngine;
using Alta.Inventory;
using Alta.Networking;

internal static class ExampleItemRegistry {
    // Call this method from wherever
    // Preferably, you would subscribe to this from MelonLoader's OnMelonInitialized method.
    public static void Awake() {
        NetworkPrefabRegistry.RegisterPrefabs += RegisterItem;
    }

    private static GameObject GetGameObjectFromAssetBundle(string embeddedPath, string objectName) {
        using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedPath)) {
            AssetBundle assetBundle = AssetBundle.LoadFromStream(stream);

            GameObject obj = (GameObject)assetBundle.LoadAsset(objectName);

            return obj;
        }
    }

    private static void RegisterItem() {
        // Remember to set your Asset Bundle as an "Embedded resource".
        GameObject obj = GetGameObjectFromAssetBundle(
            embeddedPath: "SyncLib.ExampleRegistry.Resources.example sphere",
            objectName: "Example Sphere"
        );

        // This only registers it as a Network Prefab, giving it a unique HashId per server that stays consistant even when uninstalled and reinstalled.
        // For some occasions, you will only need to register it as a NetworkPrefab for structures that require network behaviours.
        NetworkPrefab? prefab = SyncLib.SyncLib.instance.RegisterCustomPrefab(
            prefab: obj,
            CustomPrefabId: "ExampleSphere"
        );

        if (prefab is null) {
            // Failed to register this prefab.

            // This can mean two things:
            // - An issue occured while registering (Which will be logged)
            // - The client had this mod, while the server didn't, so it was skipped.

            return;
        }

        // If you are using ATT Workshop, it will find the Item component, and setup accordingly.
        Item? item = prefab.RegisterCustomItem();
    }
}