using GLTFast.Export;
using GLTFast.Materials;
using HarmonyLib;
using UnityEngine;

namespace Marioalexsan.AtlyssGLTF.HarmonyPatches;

[HarmonyPatch(typeof(ImageExport), nameof(ImageExport.LoadBlitMaterial))]
static class ImageExport_LoadBlitMaterial
{
    static bool Prefix(ref string shaderName, ref Material __result)
    {
        var mappedShader = shaderName switch
        {
            "glTFExportColor" => AtlyssGLTF.ExportColor,
            "glTFExportNormal" => AtlyssGLTF.ExportNormal,
            "glTFExportMetalGloss" => AtlyssGLTF.ExportMetalGloss,
            "glTFExportOcclusion" => AtlyssGLTF.ExportOcclusion,
            "glTFExportSmoothness" => AtlyssGLTF.ExportSmoothness,
            _ => null,
        };

        if (mappedShader == null)
        {
            return true;
        }

        __result = new Material(mappedShader);
        return false;
    }
}

[HarmonyPatch(typeof(BuiltInMaterialGenerator), nameof(BuiltInMaterialGenerator.FinderShaderMetallicRoughness))]
static class FinderShaderMetallicRoughnessPatch
{
    static bool Prefix(BuiltInMaterialGenerator __instance, ref Shader __result)
    {
        __result = AtlyssGLTF.PBRMetallicRoughness;
        return false;
    }
}

[HarmonyPatch(typeof(BuiltInMaterialGenerator), nameof(BuiltInMaterialGenerator.FinderShaderSpecularGlossiness))]
static class FinderShaderSpecularGlossinessPatch
{
    static bool Prefix(BuiltInMaterialGenerator __instance, ref Shader __result)
    {
        __result = AtlyssGLTF.PBRSpecularGlossiness;
        return false;
    }
}

[HarmonyPatch(typeof(BuiltInMaterialGenerator), nameof(BuiltInMaterialGenerator.FinderShaderUnlit))]
static class FinderShaderUnlitPatch
{
    static bool Prefix(BuiltInMaterialGenerator __instance, ref Shader __result)
    {
        __result = AtlyssGLTF.Unlit;
        return false;
    }
}