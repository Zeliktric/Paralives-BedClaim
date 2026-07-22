using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;

using Setting;

using Zeliktric;
using Zeliktric.BedClaim;

/// <summary>
/// Creates the in-game mod.
/// </summary>
[HarmonyPatch]
internal static class Init
{
    const string interactionClass = "UIMainMenu",
        interactionMethod = "OnShow",
        initMessage = "Mod Initialisation Successful!\n\nThank you for downloading my plugin! :)\nPlease contact @zeliktric on Discord for any support!";

    // Has the mod already created the in-game mod?
    private static bool init = false;
    public static ulong unclaimTranslationGUID = 4501746336882473388;

    private static ThoughtBubblesSetter ThoughtBubblesSetterAPI => new ThoughtBubblesSetter();

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

		Plugin.Logger.LogInfo((object)("[Init] Patching " + GeneralExtensions.FullDescription((MethodBase)methodInfo)));
		return methodInfo;
	}

    /// <summary>
    /// Called after the original function has been executed.
    /// </summary>
	private static void Postfix()
	{
        if (init) return;
        
        // Check whether the in-game mod already exists in the user's files
        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (ulong mod in ModManager.Instance.Mods)
		{
			AssetData asset = AssetManager.Instance.GetAsset(mod);
			if (asset.FileName.StartsWith(string.Join("", MyPluginInfo.PLUGIN_NAME.Split(invalidFileNameChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.').Replace(" ", ""))) return;
		}

        AssetMod assetMod = ModManager.Instance.CreateNewEmptyMod(MyPluginInfo.PLUGIN_NAME, false, userCreatedMod: true);
        assetMod.CreatorId = "Zeliktric (NexusMods)";

        ulong actionGUID = CreateNewAction("ClaimBed", assetMod.GUID);
        ulong interactionGUID = CreateNewInteraction("ClaimBed", actionGUID, assetMod.GUID);

        ulong groupGUID = 7297545825684127626;
        string nestedNameTranslationKey = "Bed_Claim";
        string translation = "Claim";

        AddInteractionToGroup(groupGUID, interactionGUID, nestedNameTranslationKey, assetMod.GUID);
        SetTranslation(nestedNameTranslationKey, translation, assetMod.GUID);
        SetTranslation("Bed_Unclaim", "Unclaim", assetMod.GUID);

        ulong spriteGUID = 3484653011502679369;
        CreateThoughtBubble("Claimed Bed", spriteGUID, assetMod.GUID);

        UI.Get<UIPrompt>().Hide();
        UI.Get<UIPrompt>().ShowOk(MyPluginInfo.PLUGIN_NAME, initMessage);

        init = true;
	}

    /// <summary>
    /// Creates a new action.
    /// </summary>
    /// <param name="name">The name of the action to be created.</param>
    /// <param name="modGUID">The GUID of this in-game mod.</param>
    /// <returns>The GUID of the created action.</returns>
    private static ulong CreateNewAction(string name, ulong modGUID)
    {
        int num = Settings.Get<Actions>().AllActions.Length;
	    Settings.ActionsSetterAPI.InMod(modGUID).AllActions_SetArraySize(num + 1);

        ulong newGUID = AssetManager.GetNewGUID();
        Settings.ActionsSetterAPI.InMod(modGUID).AllActions_GetArrayElement(num, newGUID).SetGUID(newGUID);
        Settings.ActionsSetterAPI.InMod(modGUID).AllActions_GetArrayElement(num, newGUID).SetDisplayName(name);
        Settings.ActionsSetterAPI.InMod(modGUID).AllActions_GetArrayElement(num, newGUID).SetEndCondition(ActionEndCondition.InstantNoQueue);

        Plugin.Log($"Created '{name}' Action ({newGUID})");

        return newGUID;
    }

    /// <summary>
    /// Creates a new interaction.
    /// </summary>
    /// <param name="name">The name of the interaction to be created.</param>
    /// <param name="actionGUID">The GUID of the corresponding action for this interaction.</param>
    /// <param name="modGUID">The GUID of this in-game mod.</param>
    /// <returns>The GUID of the created interaction.</returns>
    private static ulong CreateNewInteraction(string name, ulong actionGUID, ulong modGUID)
    {
        int num = Settings.Get<Interactions>().AllInteractions.Length;
		Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_SetArraySize(num + 1);

		ulong newGUID = AssetManager.GetNewGUID();

        Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_GetArrayElement(num, newGUID).SetGUID(newGUID);
        Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_GetArrayElement(num, newGUID).SetDisplayName(name);
        Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_GetArrayElement(num, newGUID).SetInteractionQueueIcon(3484653011502679369);
        Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_GetArrayElement(num, newGUID).SetActionGUID(actionGUID);
        Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_GetArrayElement(num, newGUID).SetIsInstant(true);
        Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_GetArrayElement(num, newGUID).SetInstantIsDoneByOnlyOneCharacter(true);
        Settings.InteractionsSetterAPI.InMod(modGUID).AllInteractions_GetArrayElement(num, newGUID).SetCanPerformIfNeedsAreCritical(true);

        Plugin.Log($"Created '{name}' Interaction ({newGUID})");

        return newGUID;
    }

    /// <summary>
    /// Adds the specified interaction to an interaction group.
    /// </summary>
    /// <param name="groupGUID">The GUID of the interaction group.</param>
    /// <param name="interactionGUID">The GUID of the interaction to be added.</param>
    /// <param name="nestedNameTranslationKey">The name of the treanslation key for the interaction in the interaction group.</param>
    /// <param name="modGUID">The GUID of this in-game mod.</param>
    private static void AddInteractionToGroup(ulong groupGUID, ulong interactionGUID, string nestedNameTranslationKey, ulong modGUID)
    {
        int index = Array.IndexOf(Settings.Get<Interactions>().InteractionGroups, Settings.Get<Interactions>().InteractionGroups.FirstOrDefault(x => x.GUID == groupGUID));
        InteractionGroup groupInteraction = Settings.Get<Interactions>().GetInteractionGroupByGUID(groupGUID);

        ulong newGUID = AssetManager.GetNewGUID();

        int len = groupInteraction.ChildrenInteractionAndGroups.Length;
        Settings.InteractionsSetterAPI.InMod(modGUID).InteractionGroups_GetArrayElement(index, groupGUID).ChildrenInteractionAndGroups_SetArraySize(len + 1);

        Settings.InteractionsSetterAPI.InMod(modGUID).InteractionGroups_GetArrayElement(index, groupGUID).ChildrenInteractionAndGroups_GetArrayElement(len, newGUID).SetGUID(newGUID);
        Settings.InteractionsSetterAPI.InMod(modGUID).InteractionGroups_GetArrayElement(index, groupGUID).ChildrenInteractionAndGroups_GetArrayElement(len, newGUID).SetGroup(groupGUID);
        Settings.InteractionsSetterAPI.InMod(modGUID).InteractionGroups_GetArrayElement(index, groupGUID).ChildrenInteractionAndGroups_GetArrayElement(len, newGUID).SetInteraction(interactionGUID);
        Settings.InteractionsSetterAPI.InMod(modGUID).InteractionGroups_GetArrayElement(index, groupGUID).ChildrenInteractionAndGroups_GetArrayElement(len, newGUID).SetIsNestedNameDifferentThanInteractionName(true);
        Settings.InteractionsSetterAPI.InMod(modGUID).InteractionGroups_GetArrayElement(index, groupGUID).ChildrenInteractionAndGroups_GetArrayElement(len, newGUID).SetDisplayNameOfNestedInteraction(nestedNameTranslationKey);
    
        Plugin.Log($"Added interaction to {groupInteraction.DisplayName}");
    }

    /// <summary>
    /// Creates a new translation entry.
    /// </summary>
    /// <param name="nestedNameTranslationKey">The name of the translation key.</param>
    /// <param name="translation">The value of the translation key.</param>
    /// <param name="modGUID">The GUID of this in-game mod.</param>
    /// <returns>The GUID of the created translation entry.</returns>
    private static ulong SetTranslation(string nestedNameTranslationKey, string translation, ulong modGUID)
    {
        // Hardcoded value for "Bed_Unclaim" to ensure compatibility with older version of the in-game mod
        ulong newGUID = nestedNameTranslationKey == "Bed_Unclaim" ? 4501746336882473388 : AssetManager.GetNewGUID();

        Translations translationSettings = TranslationManager.TranslationSettings;
        int index2 = translationSettings.Items.Length;

        TranslationItem item = TranslationManager.GetTranslationItem(nestedNameTranslationKey);
        if (item != null)
        {
            index2 = Array.IndexOf(translationSettings.Items, item);
            newGUID = item.GUID;
        }

        Settings.TranslationsSetterAPI.InMod(modGUID).Items_SetArraySize(translationSettings.Items.Length + 1);
        Settings.TranslationsSetterAPI.InMod(modGUID).Items_GetArrayElement(index2, newGUID).SetGUID(newGUID);
        Settings.TranslationsSetterAPI.InMod(modGUID).Items_GetArrayElement(index2, newGUID).SetKey($"NestedInteraction_{nestedNameTranslationKey}");        
        Settings.TranslationsSetterAPI.InMod(modGUID).Items_GetArrayElement(index2, newGUID).SetValue(translation);
        
        TranslationManager.TranslationSettings.RebuildDictionnary();
        TranslationItem translationItem = TranslationManager.GetTranslationItem(newGUID);
        
        Plugin.Log($"Set interaction translation ({translationItem.GUID})");

        return newGUID;
    }

    /// <summary>
    /// Creates a new thought bubble.
    /// </summary>
    /// <param name="name">The display name of the thought bubble.</param>
    /// <param name="spriteGUID">The GUID of the sprite to be used.</param>
    /// <param name="modGUID">The GUID of this in-game mod.</param>
    private static void CreateThoughtBubble(string name, ulong spriteGUID, ulong modGUID)
    {
        int num = Settings.Get<ThoughtBubbles>().AllThoughtBubbles.Length;
        ThoughtBubblesSetterAPI.InMod(modGUID).States_SetArraySize(num + 1);

        ulong newGUID = AssetManager.GetNewGUID();
        ThoughtBubblesSetterAPI.InMod(modGUID).States_GetArrayElement(num, newGUID).SetGUID(newGUID);
        ThoughtBubblesSetterAPI.InMod(modGUID).States_GetArrayElement(num, newGUID).SetDisplayName(name);
        ThoughtBubblesSetterAPI.InMod(modGUID).States_GetArrayElement(num, newGUID).SetSprite(spriteGUID);

        Plugin.Log($"Created '{name}' ThoughtBubble ({newGUID})");
    }
}