using HarmonyLib;
using UnityEngine;

namespace Marioalexsan.AtlyssGLTF.HarmonyPatches;

[HarmonyPatch(typeof(Mesh), nameof(Mesh.canAccess), MethodType.Getter)]
static class ReadableMesh
{
    static bool Prefix(ref bool __result)
    {
        if (!AtlyssGLTF.ForceReadableMeshes)
            return true;

        __result = true;
        return false;
    }
}