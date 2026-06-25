using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class FirstPersonHeadHider : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;
    [SerializeField] private Mesh headlessBodyMesh;
    [SerializeField] private bool hideJointSpheres = true;
    [SerializeField] [Range(0.1f, 1f)] private float headWeightThreshold = 0.35f;

    private Mesh originalMesh;
    private Mesh runtimeGeneratedMesh;

    private void Reset()
    {
        bodyRenderer = FindBodyRenderer();
    }

    private void Awake()
    {
        ApplyHeadlessMesh();
    }

    public void ApplyHeadlessMesh()
    {
        if (bodyRenderer == null)
            bodyRenderer = FindBodyRenderer();

        if (bodyRenderer == null)
        {
            Debug.LogWarning("FirstPersonHeadHider could not find a body SkinnedMeshRenderer.", this);
            return;
        }

        var meshToUse = headlessBodyMesh;
        if (meshToUse == null)
        {
            try
            {
                runtimeGeneratedMesh = HeadlessMeshUtility.CreateHeadlessMesh(
                    bodyRenderer.sharedMesh,
                    bodyRenderer.bones,
                    headWeightThreshold);
                meshToUse = runtimeGeneratedMesh;
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"FirstPersonHeadHider failed to remove the head mesh. Enable Read/Write on character.fbx and reimport. {exception.Message}",
                    this);
                return;
            }
        }

        if (originalMesh == null)
            originalMesh = bodyRenderer.sharedMesh;

        bodyRenderer.sharedMesh = meshToUse;

        if (!hideJointSpheres)
            return;

        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer != bodyRenderer && renderer.name.Contains("Joint"))
                renderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (bodyRenderer != null && originalMesh != null)
        {
            var activeMesh = headlessBodyMesh != null ? headlessBodyMesh : runtimeGeneratedMesh;
            if (bodyRenderer.sharedMesh == activeMesh)
                bodyRenderer.sharedMesh = originalMesh;
        }

        if (runtimeGeneratedMesh != null)
            Destroy(runtimeGeneratedMesh);
    }

    private SkinnedMeshRenderer FindBodyRenderer()
    {
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.name.Contains("Surface"))
                return renderer;
        }

        return GetComponentInChildren<SkinnedMeshRenderer>(true);
    }
}
