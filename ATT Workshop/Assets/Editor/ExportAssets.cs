using System.IO;
using UnityEditor;

public class ExportAssets {
    [MenuItem("ATT Workshop/Build All Assets")]
    public static void BuildAssets() {
        if (!Directory.Exists("Assets/Exported Assets")) {
            Directory.CreateDirectory("Assets/Exported Assets");
        }

        BuildPipeline.BuildAssetBundles("Assets/Exported Assets", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}