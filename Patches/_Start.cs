using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;

using UnityEngine;
using Setting;

using Zeliktric.BedClaim;

/// <summary>
/// Sets the GUID and name of the save being loaded.
/// </summary>
[HarmonyPatch]
internal static class Start
{
    const string interactionClass = "SavedGameManager",
        interactionMethod = "LoadGame";

    public static ulong SavedGameGUID;
    public static string SavedGameName;

    /// <summary>
    /// Creates a patch for the specified class and function.
    /// </summary>
    private static MethodBase TargetMethod()
	{
		Type type = AccessTools.TypeByName(interactionClass);
		if (type == null)
		{
			throw new Exception($"Could not find type: {interactionClass}");
		}

		MethodInfo methodInfo = AccessTools.DeclaredMethod(type, interactionMethod, (Type[])null, (Type[])null);
		if (methodInfo == null)
		{
			throw new Exception($"Could not find method: {interactionMethod} on " + type.FullName);
		}
        
		Plugin.Logger.LogInfo((object)("[Start] Patching " + GeneralExtensions.FullDescription((MethodBase)methodInfo)));
		return methodInfo;
	}

    /// <summary>
    /// Called after the original function has been executed.
    /// </summary>
	private static void Postfix(ulong savedGameGUID)
	{
        AssetSavedGame currentSavedGame = (AssetSavedGame)AssetManager.Instance.GetAsset(savedGameGUID);

        SavedGameGUID = savedGameGUID;
        SavedGameName = currentSavedGame.FileNameNoExtension;

        Plugin.Log($"Loaded save {SavedGameName} ({savedGameGUID})");
	}
}
