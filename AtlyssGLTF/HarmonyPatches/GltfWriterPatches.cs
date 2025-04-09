using HarmonyLib;
using GLTFast.Export;
using UnityEngine;
using UnityEngine.Rendering;

namespace Marioalexsan.AtlyssGLTF.HarmonyPatches;

[HarmonyPatch(typeof(GltfWriter), nameof(GltfWriter.AddMesh))]
static class GltfWriter_AddMesh
{
    //We make the mesh readable during GltfWriter
    static void Prefix(ref Mesh uMesh, ref VertexAttributeUsage attributeUsage)
    {
        if (!uMesh.isReadable)
            uMesh = MakeReadableMeshCopy(uMesh);
    }

    //Slightly Modified Version of Pohype's Code Snippet :
    //https://discussions.unity.com/t/reading-meshes-at-runtime-that-are-not-enabled-for-read-write/804189/8
    public static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
    {
        Mesh meshCopy = new Mesh();
        meshCopy.name = nonReadableMesh.name + "_copy";
        meshCopy.indexFormat = nonReadableMesh.indexFormat;

        // Handle vertices
        CopyVertexBuffers(meshCopy, nonReadableMesh);

        // Handle triangles
        meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
        GraphicsBuffer indexesBuffer = nonReadableMesh.GetIndexBuffer();
        int tot = indexesBuffer.stride * indexesBuffer.count;
        byte[] indexesData = new byte[tot];
        indexesBuffer.GetData(indexesData);
        meshCopy.SetIndexBufferParams(indexesBuffer.count, nonReadableMesh.indexFormat);
        meshCopy.SetIndexBufferData(indexesData, 0, 0, tot);
        indexesBuffer.Release();

        // Restore submesh structure
        uint currentIndexOffset = 0;
        for (int i = 0; i < meshCopy.subMeshCount; i++)
        {
            uint subMeshIndexCount = nonReadableMesh.GetIndexCount(i);
            meshCopy.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
            currentIndexOffset += subMeshIndexCount;
        }

        // Copy blendshapes
        CopyBlendShapes(meshCopy, nonReadableMesh);

        // Recalculate normals and bounds
        meshCopy.RecalculateNormals();
        meshCopy.RecalculateBounds();

        //Copy bones
        meshCopy.boneWeights = nonReadableMesh.boneWeights;
        meshCopy.bindposes = nonReadableMesh.bindposes;

        return meshCopy;
    }
    //We copy the blendshapes over from our target Mesh, Unsure if this is reliable but it works.
    private static void CopyBlendShapes(Mesh targetMesh, Mesh sourceMesh)
    {
        try
        {
            for (int i = 0; i < sourceMesh.blendShapeCount; i++)
            {
                string shapeName = sourceMesh.GetBlendShapeName(i);
                int frameCount = sourceMesh.GetBlendShapeFrameCount(i);

                for (int frame = 0; frame < frameCount; frame++)
                {
                    float weight = sourceMesh.GetBlendShapeFrameWeight(i, frame);
                    Vector3[] deltaVertices = new Vector3[sourceMesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[sourceMesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[sourceMesh.vertexCount];

                    sourceMesh.GetBlendShapeFrameVertices(i, frame, deltaVertices, deltaNormals, deltaTangents);
                    targetMesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                }
            }
        }
        catch (System.Exception e) { Debug.LogException(e); };
    }
    //Copy multiple VertexBuffers directly. Before we did each Buffer manually, going through each iteratively works well enough.
    private static void CopyVertexBuffers(Mesh targetMesh, Mesh sourceMesh)
    {
        for (int i = 0; i < sourceMesh.vertexBufferCount; i++)
        {
            GraphicsBuffer verticesBuffer = sourceMesh.GetVertexBuffer(i);
            int totalSize = verticesBuffer.stride * verticesBuffer.count;
            byte[] data = new byte[totalSize];
            verticesBuffer.GetData(data);
            targetMesh.SetVertexBufferParams(sourceMesh.vertexCount, sourceMesh.GetVertexAttributes());
            targetMesh.SetVertexBufferData(data, 0, 0, totalSize, i);
            verticesBuffer.Release();
        }
    }
}
