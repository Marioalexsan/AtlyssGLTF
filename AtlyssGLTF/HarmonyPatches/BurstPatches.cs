//#define USE_BURST // Uncomment + have the generated DLL to use Burst

using HarmonyLib;
using Unity.Burst;
using System.Runtime.InteropServices;

namespace Marioalexsan.AtlyssGLTF.HarmonyPatches;

static class BurstManager
{
    public static void Initialize()
    {
#if USE_BURST
        BurstCompiler_Compile.InitializeFromBundle();
#endif
    }
}

#if !USE_BURST

[HarmonyPatch(typeof(BurstCompiler), "Compile", typeof(object), typeof(bool))]
static class BurstRemove
{
    unsafe static bool Prefix(object delegateObj, out void* __result)
    {
        // Let's skip any Burst stuff for GLTF for now
        if (delegateObj.GetType().Assembly == typeof(GLTFast.GltfAsset).Assembly)
        {
            __result = (void*)Marshal.GetFunctionPointerForDelegate(delegateObj);
            return false;
        }

        __result = null;
        return true;
    }
}

#else

[HarmonyPatch(typeof(BurstCompiler), "Compile", typeof(object), typeof(bool))]
static class BurstCompiler_Compile
{
    // Questionable if this is neccesary, And how much of a impact this has on burst performance?
    // We only really need to use this for Shape-keys, and Exporting.
    unsafe static bool Prefix(object delegateObj, out void* __result)
    {
        if (BurstAssemblyLoaded && delegateObj.GetType().Assembly == typeof(GltfImport).Assembly)
        {
            __result = (void*)Marshal.GetFunctionPointerForDelegate(delegateObj);
            return false;
        }

        __result = null;
        return true;
    }

    public static bool BurstAssemblyLoaded { get; private set; } = false;

    private static string BurstGeneratedPath => Path.Combine(Path.GetDirectoryName(typeof(GltfImport).Assembly.Location), "gltfast_burst_generated.dll");

    /// <summary>
    /// LoadShader Burst Assembly from an entry point.
    /// </summary>
    public static bool InitializeFromBundle()
    {
        if (BurstAssemblyLoaded)
        {
            Debug.Log("GLTFast Burst Assembly is already loaded.");
            return true;
        }

        if (!BurstCompiler.IsLoadAdditionalLibrarySupported())
        {
            Debug.LogWarning("Runtime Additional Burst Library Not Supported");
            Debug.LogWarning("Only Supported from 2021.1 and later.");
            return false;
        }

        Debug.Log($"Loading GLTFast Burst Assembly :\n{BurstGeneratedPath}");

        if (!BurstRuntime.LoadAdditionalLibrary(BurstGeneratedPath))
        {
            Debug.LogWarning("Failed to load GLTFast Burst Assembly.");
            return false;
        }

        Debug.Log("Successfully loaded GLTFast Burst Assembly.");
        BurstAssemblyLoaded = true;
        return true;
    }
}

#endif