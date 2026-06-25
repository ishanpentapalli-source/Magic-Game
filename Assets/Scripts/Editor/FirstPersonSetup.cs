using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FirstPersonSetup
{
    private const string CharacterFbxPath = "Assets/Character/character.fbx";
    private const string HeadlessMeshPath = "Assets/Character/Alpha_Surface_Headless.asset";
    private const string SetupDoneKey = "MagicAdventure.FirstPersonSetupDone";

    static FirstPersonSetup()
    {
        EditorApplication.delayCall += TryAutoSetup;
    }

    [MenuItem("Magic Adventure/Setup First Person (Remove Head)")]
    public static void SetupFromMenu()
    {
        EditorPrefs.DeleteKey(SetupDoneKey);

        if (RunSetup())
        {
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "First Person Setup",
                "Head removed successfully. Press Play to test first-person view.",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "First Person Setup",
            "Head removal failed. Check the Console for details.",
            "OK");
    }

    public static void Setup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");

        if (!RunSetup())
        {
            Debug.LogError("First person setup failed.");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("First person setup completed.");
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    private static void TryAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        if (EditorPrefs.GetBool(SetupDoneKey, false))
            return;

        if (!AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath))
            return;

        if (!RunSetup())
            return;

        EditorPrefs.SetBool(SetupDoneKey, true);
        AssetDatabase.SaveAssets();
        Debug.Log("First person head removal applied automatically.");
    }

    internal static void TryAutoSetupAfterReimport()
    {
        EditorApplication.delayCall += TryAutoSetup;
    }

    private static bool RunSetup()
    {
        EnsureMeshIsReadable();

        var characterRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath);
        if (characterRoot == null)
        {
            Debug.LogError($"Could not load character model at {CharacterFbxPath}.");
            return false;
        }

        var bodyRenderer = characterRoot
            .GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.name.Contains("Surface"));

        if (bodyRenderer == null)
        {
            Debug.LogError("Could not find Alpha_Surface SkinnedMeshRenderer on the character.");
            return false;
        }

        Mesh headlessMesh;
        try
        {
            headlessMesh = HeadlessMeshUtility.CreateHeadlessMesh(
                bodyRenderer.sharedMesh,
                bodyRenderer.bones);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"Failed to build headless mesh: {exception.Message}");
            return false;
        }

        var savedMesh = SaveHeadlessMesh(headlessMesh);
        if (savedMesh == null)
            return false;

        ConfigureOpenScenes(savedMesh);
        return true;
    }

    private static Mesh SaveHeadlessMesh(Mesh headlessMesh)
    {
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HeadlessMeshPath);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(headlessMesh, HeadlessMeshPath);
        }
        else
        {
            existingMesh.Clear(false);
            EditorUtility.CopySerialized(headlessMesh, existingMesh);
            Object.DestroyImmediate(headlessMesh);
            headlessMesh = existingMesh;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HeadlessMeshPath);
        if (savedMesh != null)
            return savedMesh;

        Debug.LogError($"Could not save headless mesh to {HeadlessMeshPath}.");
        return null;
    }

    private static void EnsureMeshIsReadable()
    {
        var importer = AssetImporter.GetAtPath(CharacterFbxPath) as ModelImporter;
        if (importer == null)
            return;

        if (importer.isReadable)
            return;

        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private static void ConfigureOpenScenes(Mesh headlessMesh)
    {
        var sampleScenePath = "Assets/Scenes/SampleScene.unity";
        if (!System.IO.File.Exists(sampleScenePath))
            return;

        var activeScenePath = SceneManager.GetActiveScene().path;
        var openedSampleScene = false;

        if (activeScenePath != sampleScenePath)
        {
            EditorSceneManager.OpenScene(sampleScenePath);
            openedSampleScene = true;
        }

        var changed = false;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!ConfigureCharacterInstance(root, headlessMesh))
                continue;

            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        if (openedSampleScene && !string.IsNullOrEmpty(activeScenePath) && activeScenePath != sampleScenePath)
            EditorSceneManager.OpenScene(activeScenePath);
    }

    private static bool ConfigureCharacterInstance(GameObject root, Mesh headlessMesh)
    {
        var hasCharacterName = root.name.Contains("character");
        var hasAnimator = root.GetComponentInChildren<Animator>() != null;
        if (!hasCharacterName && !hasAnimator)
            return false;

        var bodyRenderer = root
            .GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.name.Contains("Surface"));

        if (bodyRenderer == null)
            return false;

        var headHider = root.GetComponent<FirstPersonHeadHider>();
        if (headHider == null)
            headHider = root.AddComponent<FirstPersonHeadHider>();

        var serialized = new SerializedObject(headHider);
        serialized.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
        serialized.FindProperty("headlessBodyMesh").objectReferenceValue = headlessMesh;
        serialized.FindProperty("hideJointSpheres").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return true;
    }
}
