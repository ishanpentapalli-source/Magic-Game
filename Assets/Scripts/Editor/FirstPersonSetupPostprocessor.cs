using System.Linq;
using UnityEditor;

public class FirstPersonSetupPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!importedAssets.Any(path => path == "Assets/Character/character.fbx"))
            return;

        FirstPersonSetup.TryAutoSetupAfterReimport();
    }
}
