using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

using Setting;
using TMPro;

using Zeliktric.BedClaim;

/// <summary>
/// Checks the ownership of the bed that was interacted with.
/// </summary>
[HarmonyPatch]
internal static class BedOwnership
{
    const string interactionClass = "UIInteractions",
        interactionMethod = "Show";

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

		Plugin.Logger.LogInfo((object)("[BedOwnership] Patching " + GeneralExtensions.FullDescription((MethodBase)methodInfo)));
		return methodInfo;
	}

    /// <summary>
    /// Called after the original function has been executed.
    /// </summary>
	private static void Postfix(UIInteractions __instance, InteractionGroup interactionGroup)
	{
        if (Plugin.WorkshopModNotFound() || interactionGroup == null) return;

        if (interactionGroup.DisplayName == "GROUP_Bed")
        {
            // Find the UIInteractionListItem of the Claim/Unclaim action
            TMP_Text claimItemText = GameObject.Find("Hybrid").GetComponentsInChildren<UIInteractionsListItem>()
                .Select(item => item.transform.Find("LabelInteractionName")?.GetComponent<TMP_Text>())
                .FirstOrDefault(label => label?.text == "Claim" || label?.text == "Unclaim");

            // We already forced the player to select one bed in ClaimBed.cs
            ulong[] selectedCharacters = (ulong[])AccessTools.Field(typeof(UIInteractions), "_charactersSelectedOnOpen").GetValue(__instance);
            ulong charID = selectedCharacters[0];

            // Player hasn't selected any characters (lol?), so just ignore
            if (selectedCharacters.Length == 0) return;

            // Get the bed ID
            int bedID = __instance.ClickedItemInstanceID;
            BedData bedData = DataManager.Load(charID);

            Plugin.Log($"Character: {Plugin.GetCharacterName(charID)}\nCharacter ID: {charID}\nBed ID: {bedID}\nClaimed Bed?: {bedData.bedID == bedID}");

            if (bedData.bedID == bedID)
            {
                // Allow this character to "unclaim" their claimed bed
                claimItemText.text = TranslationManager.TranslationSettings.Items.FirstOrDefault(x => x.GUID == Init.unclaimTranslationGUID).Value;
            }
        }
	}
}
