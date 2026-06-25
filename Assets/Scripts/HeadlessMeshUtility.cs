using System;
using System.Collections.Generic;
using UnityEngine;

public static class HeadlessMeshUtility
{
    public static readonly string[] DefaultHeadBoneFragments = { "Head", "HeadTop" };

    public static Mesh CreateHeadlessMesh(Mesh source, Transform[] bones, float weightThreshold = 0.35f)
    {
        return CreateHeadlessMesh(source, bones, DefaultHeadBoneFragments, weightThreshold);
    }

    public static Mesh CreateHeadlessMesh(
        Mesh source,
        Transform[] bones,
        string[] headBoneNameFragments,
        float weightThreshold)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (bones == null || bones.Length == 0)
            throw new ArgumentException("Bones are required to identify head vertices.", nameof(bones));

        var headBoneIndices = new HashSet<int>();
        for (var i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
                continue;

            var boneName = bones[i].name;
            foreach (var fragment in headBoneNameFragments)
            {
                if (boneName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    headBoneIndices.Add(i);
                    break;
                }
            }
        }

        if (headBoneIndices.Count == 0)
            throw new InvalidOperationException("No head bones were found on the skinned mesh.");

        var boneWeights = source.boneWeights;
        var isHeadVertex = new bool[source.vertexCount];
        for (var v = 0; v < source.vertexCount; v++)
        {
            var bw = boneWeights[v];
            var headWeight = 0f;
            if (headBoneIndices.Contains(bw.boneIndex0)) headWeight += bw.weight0;
            if (headBoneIndices.Contains(bw.boneIndex1)) headWeight += bw.weight1;
            if (headBoneIndices.Contains(bw.boneIndex2)) headWeight += bw.weight2;
            if (headBoneIndices.Contains(bw.boneIndex3)) headWeight += bw.weight3;
            isHeadVertex[v] = headWeight >= weightThreshold;
        }

        var sourceTriangles = source.triangles;
        var keptTriangles = new List<int>(sourceTriangles.Length);
        for (var i = 0; i < sourceTriangles.Length; i += 3)
        {
            var a = sourceTriangles[i];
            var b = sourceTriangles[i + 1];
            var c = sourceTriangles[i + 2];
            if (isHeadVertex[a] || isHeadVertex[b] || isHeadVertex[c])
                continue;

            keptTriangles.Add(a);
            keptTriangles.Add(b);
            keptTriangles.Add(c);
        }

        var usedVertices = new HashSet<int>();
        foreach (var index in keptTriangles)
            usedVertices.Add(index);

        var oldToNew = new int[source.vertexCount];
        Array.Fill(oldToNew, -1);

        var newVertices = new List<Vector3>(usedVertices.Count);
        var newNormals = source.normals is { Length: > 0 } ? new List<Vector3>(usedVertices.Count) : null;
        var newTangents = source.tangents is { Length: > 0 } ? new List<Vector4>(usedVertices.Count) : null;
        var newUv = source.uv is { Length: > 0 } ? new List<Vector2>(usedVertices.Count) : null;
        var newUv2 = source.uv2 is { Length: > 0 } ? new List<Vector2>(usedVertices.Count) : null;
        var newUv3 = source.uv3 is { Length: > 0 } ? new List<Vector2>(usedVertices.Count) : null;
        var newUv4 = source.uv4 is { Length: > 0 } ? new List<Vector2>(usedVertices.Count) : null;
        var newColors = source.colors is { Length: > 0 } ? new List<Color>(usedVertices.Count) : null;
        var newBoneWeights = new List<BoneWeight>(usedVertices.Count);

        foreach (var oldIndex in usedVertices)
        {
            oldToNew[oldIndex] = newVertices.Count;
            newVertices.Add(source.vertices[oldIndex]);
            newBoneWeights.Add(boneWeights[oldIndex]);

            if (newNormals != null) newNormals.Add(source.normals[oldIndex]);
            if (newTangents != null) newTangents.Add(source.tangents[oldIndex]);
            if (newUv != null) newUv.Add(source.uv[oldIndex]);
            if (newUv2 != null) newUv2.Add(source.uv2[oldIndex]);
            if (newUv3 != null) newUv3.Add(source.uv3[oldIndex]);
            if (newUv4 != null) newUv4.Add(source.uv4[oldIndex]);
            if (newColors != null) newColors.Add(source.colors[oldIndex]);
        }

        var remappedTriangles = new int[keptTriangles.Count];
        for (var i = 0; i < keptTriangles.Count; i++)
            remappedTriangles[i] = oldToNew[keptTriangles[i]];

        var headlessMesh = new Mesh
        {
            name = source.name + "_Headless",
            indexFormat = newVertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
        };

        headlessMesh.SetVertices(newVertices);
        headlessMesh.SetTriangles(remappedTriangles, 0, true);
        headlessMesh.boneWeights = newBoneWeights.ToArray();
        headlessMesh.bindposes = source.bindposes;

        if (newNormals != null) headlessMesh.SetNormals(newNormals);
        else headlessMesh.RecalculateNormals();

        if (newTangents != null) headlessMesh.SetTangents(newTangents);
        else headlessMesh.RecalculateTangents();

        if (newUv != null) headlessMesh.SetUVs(0, newUv);
        if (newUv2 != null) headlessMesh.SetUVs(1, newUv2);
        if (newUv3 != null) headlessMesh.SetUVs(2, newUv3);
        if (newUv4 != null) headlessMesh.SetUVs(3, newUv4);
        if (newColors != null) headlessMesh.SetColors(newColors);

        headlessMesh.RecalculateBounds();
        return headlessMesh;
    }
}
