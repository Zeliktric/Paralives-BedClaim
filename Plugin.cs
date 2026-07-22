using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

using Setting;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace Zeliktric.BedClaim;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Paralives.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    internal static string LogPath => Path.Combine(Paths.BepInExRootPath, "data", "Zeliktric", MyPluginInfo.PLUGIN_NAME, "Logs.txt");
        
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        SettingsManager.Init(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME);

        if (!File.Exists(LogPath)) Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        File.WriteAllText(LogPath, $"{MyPluginInfo.PLUGIN_GUID} | {MyPluginInfo.PLUGIN_NAME} | {MyPluginInfo.PLUGIN_VERSION}\n\n");
        
		new Harmony($"{MyPluginInfo.PLUGIN_GUID}").PatchAll(typeof(Plugin).Assembly);
    }

    public static void Log(object text)
    {
        var frame = new StackFrame(1);
        var callerClass = frame.GetMethod().DeclaringType.Name;

        File.AppendAllText(LogPath, $"[{callerClass}]\n{text}\n\n");
    }

    public static void Print(object text)
    {
        Logger.LogInfo($"{text}");
    }

    public static bool WorkshopModNotFound()
    {
        return Settings.Get<Interactions>().AllInteractions.FirstOrDefault(x => x.DisplayName == "ClaimBed") == null;
    }

    public static string GetCharacterName(ulong guid)
    {
        CharacterManager characterManager = GameObject.Find("Hybrid/CharacterManager").GetComponent<CharacterManager>();
        AssetCharacter character = characterManager.GetCharacterByGUID(guid);
        AssetCharacterData characterData = (AssetCharacterData)AccessTools.Field(typeof(AssetCharacter), "_data").GetValue(character);

        return $"{characterData.FirstName} {characterData.LastName}";
    }
}
