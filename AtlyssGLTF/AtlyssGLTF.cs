using BepInEx;
using BepInEx.Logging;
using GLTFast;
using GLTFast.Export;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Marioalexsan.AtlyssGLTF;

[HarmonyPatch(typeof(ChatBehaviour), nameof(ChatBehaviour.Send_ChatMessage))]
static class ChatCommands
{
    static bool Prefix(string _message, ChatBehaviour __instance)
    {
        if (string.IsNullOrEmpty(_message))
            return true;

        var parts = _message.Split(" ", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || parts[0] != "/gltf")
            return true;

        if (parts.Length < 2)
        {
            HostConsole._current.Init_NetworkStatusMessage("Usage: /gltf <command> <params...>");
            return false;
        }

        switch (parts[1])
        {
            case "force_readable_meshes":
                {
                    if (parts.Length != 2)
                    {
                        HostConsole._current.Init_NetworkStatusMessage("Usage: /gltf force_readable_meshes");
                        return false;
                    }

                    AtlyssGLTF.ForceReadableMeshes = !AtlyssGLTF.ForceReadableMeshes;
                    HostConsole._current.Init_NetworkStatusMessage($"Forced readable meshes is now {(AtlyssGLTF.ForceReadableMeshes ? "on" : "off")}.");
                }
                return false;
            case "import":
                {
                    if (parts.Length != 4)
                    {
                        HostConsole._current.Init_NetworkStatusMessage("Usage: /gltf import <obj_name> <path>");
                        return false;
                    }

                    AtlyssGLTF.GLTFImportQueue.Enqueue((parts[2], Path.Combine(Path.GetDirectoryName(AtlyssGLTF.Plugin.Info.Location), parts[3])));
                }
                return false;
            case "export":
                {
                    if (parts.Length != 4)
                    {
                        HostConsole._current.Init_NetworkStatusMessage("Usage: /gltf export <obj_name> <path>");
                        return false;
                    }

                    AtlyssGLTF.GLTFExportQueue.Enqueue((parts[2], Path.Combine(Path.GetDirectoryName(AtlyssGLTF.Plugin.Info.Location), parts[3])));
                }
                return false;
            default:
                {
                    HostConsole._current.Init_NetworkStatusMessage("That command does not exist.");
                }
                return false;
        }
    }
}

[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
public class AtlyssGLTF : BaseUnityPlugin
{
    public static AtlyssGLTF Plugin { get; private set; }

    public static AssetBundle Bundle { get; private set; }

    public static Shader PBRMetallicRoughness { get; private set; }
    public static Shader PBRSpecularGlossiness { get; private set; }
    public static Shader Unlit { get; private set; }
    public static Shader ExportColor { get; private set; }
    public static Shader ExportMaskmap { get; private set; }
    public static Shader ExportMetalGloss { get; private set; }
    public static Shader ExportNormal { get; private set; }
    public static Shader ExportOcclusion { get; private set; }
    public static Shader ExportSmoothness { get; private set; }

    public static bool ForceReadableMeshes { get; internal set; } = false;

    internal static new ManualLogSource Logger { get; private set; }

    private Harmony _harmony;

    internal static Queue<(string ObjectName, string Path)> GLTFImportQueue { get; } = new();
    internal static Queue<(string ObjectName, string Path)> GLTFExportQueue { get; } = new();

    private void Awake()
    {
        Plugin = this;
        Logger = base.Logger;

        _harmony = new Harmony("AtlyssGLTF");
        _harmony.PatchAll();

        var gltfPath = Path.Combine(Path.GetDirectoryName(Info.Location), "gltf");
        Bundle = AssetBundle.LoadFromFile(gltfPath);

        if (Bundle)
        {
            static Shader LoadShader(string path)
            {
                var asset = Bundle.LoadAsset<Shader>(path);

                if (!asset)
                    Logger.LogWarning($"glTF shader load failed: {path}.");

                return asset;
            }

            const string BuiltinPath = "assets/atlyss/shader/gltf/built-in/";
            const string ExportPath = "assets/atlyss/shader/gltf/export/";

            PBRMetallicRoughness = LoadShader($"{BuiltinPath}gltfpbrmetallicroughness.shader");
            PBRSpecularGlossiness = LoadShader($"{BuiltinPath}gltfpbrspecularglossiness.shader");
            Unlit = LoadShader($"{BuiltinPath}gltfunlit.shader");
            ExportColor = LoadShader($"{ExportPath}gltfexportcolor.shader");
            ExportMaskmap = LoadShader($"{ExportPath}gltfexportmaskmap.shader");
            ExportMetalGloss = LoadShader($"{ExportPath}gltfexportmetalgloss.shader");
            ExportNormal = LoadShader($"{ExportPath}gltfexportnormal.shader");
            ExportOcclusion = LoadShader($"{ExportPath}gltfexportocclusion.shader");
            ExportSmoothness = LoadShader($"{ExportPath}gltfexportsmoothness.shader");

            Logger.LogInfo("Asset bundle items:");

            foreach (var name in Bundle.GetAllAssetNames())
            {
                Logger.LogInfo(" - " + name);
            }
        }
        else
        {
            Logger.LogWarning($"Unable to load glTF shader bundle from {gltfPath}.");
        }

        Logger.LogInfo("Initialized!");
    }

    public async Task Update()
    {
        if (GLTFImportQueue.Count > 0)
        {
            var data = GLTFImportQueue.Dequeue();

            await ImportGLTFScene(data.ObjectName, data.Path);
        }

        if (GLTFExportQueue.Count > 0)
        {
            var data = GLTFExportQueue.Dequeue();

            await ExportGLTFScene(data.ObjectName, data.Path);
        }
    }

    private async Task<GameObject> ImportGLTFScene(string objectName, string path)
    {
        try
        {
            var gltf = new GltfImport();
            var success = await gltf.Load(new Uri(path), new ImportSettings()
            {
                AnimationMethod = AnimationMethod.Mecanim
            });

            var gameObject = new GameObject(objectName);

            await gltf.InstantiateMainSceneAsync(gameObject.transform);

            HostConsole._current.Init_NetworkStatusMessage("Imported glTF.");

            return gameObject;
        }
        catch (Exception e)
        {
            HostConsole._current.Init_NetworkStatusMessage("Import failed! Check the console.");
            Logger.LogError(e);

            return null;
        }
    }

    private async Task ExportGLTFScene(string objectName, string path)
    {
        try
        {
            var gameObject = GameObject.Find(objectName);

            if (!gameObject)
            {
                HostConsole._current.Init_NetworkStatusMessage("Object not found.");
                return;
            }

            // GameObjectExport lets you create glTFs from GameObject hierarchies
            var export = new GameObjectExport(new ExportSettings()
            {
                Deterministic = true,
                Format = GltfFormat.Binary
            });

            // Add a scene
            export.AddScene([gameObject]);

            // Async glTF export
            bool success = await export.SaveToFileAndDispose(path);

            if (!success)
            {
                HostConsole._current.Init_NetworkStatusMessage("Something went wrong while exporting the glTF!");
            }

            HostConsole._current.Init_NetworkStatusMessage("Exported glTF!");
        }
        catch (Exception e)
        {
            HostConsole._current.Init_NetworkStatusMessage("Export failed! Check the console.");
            Logger.LogError(e);
        }
    }
}
