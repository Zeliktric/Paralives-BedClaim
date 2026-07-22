using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using UnityEngine.AI;

using Setting;
using TMPro;

using Zeliktric;
using Zeliktric.BedClaim;

/// <summary>
/// Initialises the claimed bed icon for the player's recently "selected" character
/// </summary>
[HarmonyPatch]
internal static class OnCharacterSelect
{
    const string interactionClass = "UICharacterItem",
        interactionMethod = "OnListItemClicked";

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

		Plugin.Logger.LogInfo((object)("[OnCharacterSelect] Patching " + GeneralExtensions.FullDescription((MethodBase)methodInfo)));
		return methodInfo;
	}

    /// <summary>
    /// Called after the original function has been executed.
    /// </summary>
	private static void Postfix(UICharacterItem __instance)
	{
        if (Plugin.WorkshopModNotFound()) return;

        // Check whether the icon should be hidden
        BedClaimSettings settings = SettingsManager.Load<BedClaimSettings>(MyPluginInfo.PLUGIN_NAME);
        if (!settings.showClaimedBedIcons) return;

        ThoughtBubbles thoughtBubbles = Settings.Get<ThoughtBubbles>();
        ThoughtBubble thoughtBubble = thoughtBubbles.AllThoughtBubbles.FirstOrDefault(x => x.DisplayName == "Claimed Bed");
        UIThoughtBubbles uIThoughtBubbles = GameObject.Find("Hybrid").gameObject.GetComponentInChildren<UIThoughtBubbles>();
        List<ThoughtBubbleData> thoughtBubbleDatas = (List<ThoughtBubbleData>)AccessTools.Field(typeof(UIThoughtBubbles), "_thoughtBubbles").GetValue(uIThoughtBubbles);

        // Reset the existing icons
        thoughtBubbleDatas.Clear();

        BedData charBed = DataManager.Load(__instance.Character.GUID);
        ItemObjectRoot objectRoot = ItemManager.Instance.GetItemByInstanceID(charBed.bedID);
        
        bool lockIcon = thoughtBubbleDatas.Any(x => x.Item.Equals(objectRoot));

        if (!lockIcon)
        {
            // Add the lock icon if it doesn't already exist
            thoughtBubbleDatas.Add(new ThoughtBubbleData
            {
                CharacterGUID = 0uL,
                Item = objectRoot,
                BubbleGUID = thoughtBubble.GUID,
                FrameAddedAt = int.MaxValue - 2
            });
        }
	}
}
