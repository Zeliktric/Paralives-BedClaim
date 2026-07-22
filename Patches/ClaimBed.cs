using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

using Setting;
using TMPro;

using Zeliktric;
using Zeliktric.BedClaim;

/// <summary>
/// Allows the player to claim / unclaim a bed.
/// </summary>
[HarmonyPatch]
internal static class ClaimBed
{
    const string interactionClass = "UIInteractionsListItem",
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

		Plugin.Logger.LogInfo((object)("[ClaimBed] Patching " + GeneralExtensions.FullDescription((MethodBase)methodInfo)));
		return methodInfo;
	}

    /// <summary>
    /// Claims a bed for the player's character.
    /// </summary>
    /// <param name="bedData">The data for this bed claim.</param>
    /// <param name="charID">The ID of the character who has claimed this bed.</param>
    /// <param name="bedID">The ID of the bed being claimed.</param>
    /// <param name="objectRoot">The bed's object root (used to show the 'bed claim' icon).</param>
    private static void BedClaim(BedData bedData, ulong charID, int bedID, ItemObjectRoot objectRoot)
    {
        bedData.bedID = bedID;
        bedData.slotIDs = ItemSlotManager.Instance.GetActiveItemObjectSlots(bedID).Select(x => x.GUID).ToList();
        DataManager.Save(charID, bedData);
        UI.Get<UIPrompt>().ShowOk("Success!", $"{Plugin.GetCharacterName(charID)} has claimed this bed!");

        // Check whether the icon should be hidden
        BedClaimSettings settings = SettingsManager.Load<BedClaimSettings>(MyPluginInfo.PLUGIN_NAME);
        if (!settings.showClaimedBedIcons) return;

        // For the "bed" icon
        ThoughtBubbles thoughtBubbles = Settings.Get<ThoughtBubbles>();
        ThoughtBubble thoughtBubble = thoughtBubbles.AllThoughtBubbles.FirstOrDefault(x => x.DisplayName == "Claimed Bed");
        UIThoughtBubbles uIThoughtBubbles = GameObject.Find("Hybrid").gameObject.GetComponentInChildren<UIThoughtBubbles>();
        List<ThoughtBubbleData> thoughtBubbleDatas = (List<ThoughtBubbleData>)AccessTools.Field(typeof(UIThoughtBubbles), "_thoughtBubbles").GetValue(uIThoughtBubbles);

        // Reset the existing icons
        thoughtBubbleDatas.Clear();

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

    /// <summary>
    /// Called after the original function has been executed.
    /// </summary>
	private static void Postfix(UIInteractionsListItem __instance)
	{
        if (Plugin.WorkshopModNotFound()) return;

		string interaction = __instance.InteractionGroupItem.DisplayNameOfNestedInteraction;
        if (interaction == "Bed_Claim")
        {
            TMP_Text claimItemText = __instance.transform.Find("LabelInteractionName").GetComponent<TMP_Text>();

            // Find the bed's ID
            ItemObjectRoot objectRoot = (ItemObjectRoot)AccessTools.Field(typeof(UIInteractionsListItem), "_rootObject").GetValue(__instance);
            int bedID = objectRoot.InstanceID;

            UIInteractionsList interactionsList = (UIInteractionsList)AccessTools.Field(typeof(UIInteractionsListItem), "_uiInteractionsList").GetValue(__instance);
            ulong[] selectedCharacters = (ulong[])AccessTools.Field(typeof(UIInteractionsList), "_selectedCharactersGUID").GetValue(interactionsList);
            ulong charID = selectedCharacters[0];

            if (selectedCharacters.Length > 1)
            {
                // Can only claim a bed when 1 character is selected
                UI.Get<UIPrompt>().ShowOk("Oops!", $"Please only select 1 character to claim a bed!", null, 0uL);
                return;
            }

            BedData bedData = DataManager.Load(charID);
            if (bedData.bedID == -1)
            {
                Plugin.Log($"Character: {Plugin.GetCharacterName(charID)}\nCharacter ID: {charID}\nBed ID: {bedID}\nClaim Status: Claimed");
                // This character has no claimed bed
                BedClaim(bedData, charID, bedID, objectRoot);
            }
            else
            {
                // Allow the player to unclaim this bed for the character
                string unclaimText = TranslationManager.TranslationSettings.Items.FirstOrDefault(x => x.GUID == Init.unclaimTranslationGUID).Value;
                if (claimItemText.text == unclaimText)
                {
                    bedData.bedID = -1;
                    bedData.slotIDs.Clear();
                    DataManager.Save(charID, bedData);
                    
                    Plugin.Log($"Character: {Plugin.GetCharacterName(charID)}\nCharacter ID: {charID}\nBed ID: {bedID}\nClaim Status: Unclaimed");
                    UI.Get<UIPrompt>().ShowOk("Success!", $"{Plugin.GetCharacterName(charID)} has unclaimed this bed!");
                    return;
                }

                Plugin.Log($"Character: {Plugin.GetCharacterName(charID)}\nCharacter ID: {charID}\nBed ID: {bedID}\nClaim Status: Overriding Claim?");
                // This character already has a claimed bed, ask the player if they want to reassign
                UI.Get<UIPrompt>().ShowOkCancel(
                    "Override claimed bed?",
                    $"{Plugin.GetCharacterName(charID)} already has a claimed bed.\nDo you wish to override it with this bed?",
                    () =>
                    {
                        // Claim the bed if the player wishes to reassign
                        Plugin.Log($"Character: {Plugin.GetCharacterName(charID)}\nCharacter ID: {charID}\nBed ID: {bedID}\nClaim Status: Claimed");
                        BedClaim(bedData, charID, bedID, objectRoot);
                    },
                    defaultIsOK: false
                );
            }
        }
        
	}
}
