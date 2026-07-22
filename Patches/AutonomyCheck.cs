using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

using Setting;
using TMPro;

using Zeliktric.BedClaim;

/// <summary>
/// Intercepts the "sleep/nap in bed" autonomy interaction to reassign the bed if needed.
/// </summary>
[HarmonyPatch]
internal static class AutonomyCheck
{
    const string interactionClass = "InteractionManager",
        interactionMethod = "InjectInteraction";

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

		Plugin.Logger.LogInfo((object)("[AutonomyCheck] Patching " + GeneralExtensions.FullDescription((MethodBase)methodInfo)));
		return methodInfo;
	}

    /// <summary>
    /// Called before the original function has been executed.
    /// </summary>
    /// <returns>True if the original function should be executed instead, False if it should not be executed at all.</returns>
	private static bool Prefix(
        InteractionManager __instance,
        AssetCharacter character, InteractionToInject interactionToInject, AssetCharacter targetOtherCharacter = null,
        bool isIdleAutonomous = false, bool isForcedAutonomous = false, ulong skinGUID = 0uL, ulong lotGUID = 0uL
    )
	{
        if (Plugin.WorkshopModNotFound()) return true;

		AssetCharacterData data = character.Data;
        Interactions interactions = Settings.Get<Interactions>();
        InteractionUnit interactionInSettings = interactions.GetInteractionByGUID(interactionToInject.InjectedInteraction);
        ActionUnit actionByGUID = Settings.Get<Actions>().GetActionByGUID(interactionInSettings.ActionGUID);

        // Taken from Paralives.dll
        ItemFinderRuleManager.ItemFinderFunctionParam itemFinderFunctionParam = ItemFinderRuleManager.ItemFinderFunctionParam.None;
		if (isForcedAutonomous || isIdleAutonomous)
		{
			itemFinderFunctionParam = ItemFinderRuleManager.ItemFinderFunctionParam.ReturnOnFirstItemSlotFound | ItemFinderRuleManager.ItemFinderFunctionParam.ShuffleItems;
		}
        var (navigateToFirstActionWithItemFinderResult, itemIDAndSlot) = ItemFinderRuleManager.Instance.FindNearestPathableAndFreeItemSlotOfFirstItemFinderActionPlease(actionByGUID, character, skinGUID, itemFinderFunctionParam);

        if (interactionInSettings.DisplayName != "ClaimBed" && interactionInSettings.DisplayName.Contains("Bed"))
        {
            // Character is attempting to do a bed action
            int bedID = itemIDAndSlot.ItemInstanceID;
            ulong charID = character.GUID;

            // Attempt to find the character's claimed bed
            BedData bedData = DataManager.Load(charID);
            int claimedBedID = bedData.bedID;

            // This character hasn't claimed a bed, so let the game decide
            if (claimedBedID == -1) return true;

            // Check if the claimed bed actually has available slots
            bool freeSlot = false;
            for (int i = 0; i < bedData.slotIDs.Count; i += 2)
            {
                if (ItemSlotManager.Instance.GetCharacterAtSlot(bedData.slotIDs[i], bedID) == 0)
                {
                    freeSlot = true;
                    break;
                }
            }

            Plugin.Log($"Character: {Plugin.GetCharacterName(charID)}\nCharacter ID: {charID}\nFree Slot: {freeSlot}\nBed ID: {bedID}\nClaimed Bed ID: {claimedBedID}");

            if (freeSlot)
            {
                // Make the character target their claimed bed as it has a free slot.
                Plugin.Log($"Forcing {Plugin.GetCharacterName(charID)} to target Bed: {claimedBedID}");

                // Basically the paralives' function but making the target the character's claimed bed instead of the nearest bed
                ParalivesInjection(
                    __instance,
                    claimedBedID,
                    bedData.slotIDs,
                    character,
                    interactionToInject,
                    targetOtherCharacter,
                    isIdleAutonomous,
                    isForcedAutonomous,
                    skinGUID,
                    lotGUID
                );

                return false;
            }
        }

        // No free slot in claimed bed, let game decide which bed to put the character in
        return true;
	}

    private static void ParalivesInjection(
        InteractionManager __instance, int bedID, List<ulong> bedSlotIDs,
        AssetCharacter character, InteractionToInject interactionToInject, AssetCharacter targetOtherCharacter = null,
        bool isIdleAutonomous = false, bool isForcedAutonomous = false, ulong skinGUID = 0uL, ulong lotGUID = 0uL
    )
    {
        var GetRunningInteractionsToCancel = AccessTools.MethodDelegate<Func<AssetCharacter, (bool, List<AssetCharacterDataInteraction>)>>(
            AccessTools.Method(typeof(InteractionManager), "GetRunningInteractionsToCancel"), __instance
        );

        // Code taken from Paralives.dll
        _ = ZoneManager.Instance;
        Interactions interactions = Settings.Get<Interactions>();
        InteractionUnit interactionInSettings = interactions.GetInteractionByGUID(interactionToInject.InjectedInteraction);
        if (interactionInSettings == null)
        {
            Debug.LogError($"Interaction {interactionToInject} not found in settings");
            return;
        }
        Logger<InteractionManager>.LogCharacter(character, $"Injecting interaction {interactionInSettings.DisplayName} with skinGUID {skinGUID} to character", "InjectInteraction", 72, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
        ActionUnit actionByGUID = Settings.Get<Actions>().GetActionByGUID(interactionInSettings.ActionGUID);
        if (interactionToInject.TargetOtherCharacter != TargetOtherCharacterOfInjectedInteraction.None && targetOtherCharacter == null)
        {
            Debug.Log("trying to check closest character in interaction manager?");
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Get possible characters for interaction to inject", "InjectInteraction", 79, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            AutonomyManager.Instance.GetPossibleCharactersForInteractionToInject(character, interactionToInject, CharacterManager.Instance.GetLotForCharacter(character, canCheckLastFrequentedLot: false));
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
        }
        Vector3 vector = Vector3.zero;
        if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.None)
        {
            vector = character.Data.Position;
        }
        else if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.OwnedLot)
        {
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Get position for OwnedLot target position of injected interaction", "InjectInteraction", 92, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            List<ulong> ownedLotsOfCharacterHousehold = HouseholdManager.Instance.GetOwnedLotsOfCharacterHousehold(character);
            if (ownedLotsOfCharacterHousehold != null && ownedLotsOfCharacterHousehold.Count > 0)
            {
                ulong lotGUID2 = ownedLotsOfCharacterHousehold[0];
                vector = AutonomyManager.Instance.GetPointToPathfindToForLot(character, lotGUID2, targetLotFrontSegment: false);
            }
            else
            {
                vector = character.Data.Position;
            }
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
        }
        else if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.OtherCharacter && targetOtherCharacter != null)
        {
            vector = targetOtherCharacter.Data.Position;
        }
        else if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.CurrentLot)
        {
            ulong lotForCharacter = CharacterManager.Instance.GetLotForCharacter(character, canCheckLastFrequentedLot: false);
            ZoneSegment lotNearestSegmentAt = HouseholdManager.Instance.GetLotNearestSegmentAt(lotForCharacter, character.Data.Position);
            if (lotNearestSegmentAt != null)
            {
                vector = lotNearestSegmentAt.Segment.GetCenter();
            }
            else
            {
                vector = character.Data.Position;
                Logger<InteractionManager>.LogCharacter(character, $"Asked to go to current lot {lotForCharacter}, but no segment was found", "InjectInteraction", 120, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            }
        }
        else if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.AwayFromFire)
        {
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Get position for AwayFromFire target position of injected interaction", "InjectInteraction", 125, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            ulong lotForCharacter2 = CharacterManager.Instance.GetLotForCharacter(character, canCheckLastFrequentedLot: false);
            vector = AutonomyManager.Instance.GetPointToPathfindToForLot(character, lotForCharacter2, targetLotFrontSegment: true);
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
        }
        else if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.OccupationToGoTo)
        {
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Get position for OccupationToGoTo target position of injected interaction", "InjectInteraction", 133, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            Occupation occupationByIndex = OccupationsManager.Instance.GetOccupationByIndex(character, character.Data.OccupationIndexToGoTo);
            if (occupationByIndex != null)
            {
                AssetLot lotOfOccupation = OccupationsManager.Instance.GetLotOfOccupation(occupationByIndex);
                if (lotOfOccupation != null)
                {
                    vector = AutonomyManager.Instance.GetPointToPathfindToForLot(character, lotOfOccupation.GUID, targetLotFrontSegment: false);
                    Logger<InteractionManager>.LogCharacter(character, $"Going to occupation {occupationByIndex.DisplayName}, at position: {vector}", "InjectInteraction", 142, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                }
                else
                {
                    vector = character.Data.Position;
                    Logger<InteractionManager>.LogCharacter(character, "Going to occupation " + occupationByIndex.DisplayName + ", but no company lot found, staying at current position.", "InjectInteraction", 148, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                }
            }
            else
            {
                vector = character.Data.Position;
                Logger<InteractionManager>.LogCharacter(character, "No occupation found for character", "InjectInteraction", 154, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            }
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
        }
        else if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.LotOfInteraction && lotGUID != 0L)
        {
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Get position for LotOfInteraction target position of injected interaction", "InjectInteraction", 160, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            AssetLot lotByGUID = LotManager.Instance.GetLotByGUID(lotGUID);
            vector = AutonomyManager.Instance.GetPointToPathfindToForLot(character, lotByGUID.GUID, targetLotFrontSegment: false);
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
            Logger<InteractionManager>.LogCharacter(character, $"Going to lot of interaction, at position: {vector}", "InjectInteraction", 164, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
        }
        else if (interactionToInject.TargetPosition == TargetPositionOfInjectedInteraction.RandomPositionInLot)
        {
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Get position for RandomPositionInLot target position of injected interaction", "InjectInteraction", 168, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
            ulong lotForCharacter3 = CharacterManager.Instance.GetLotForCharacter(character, canCheckLastFrequentedLot: false);
            LotPerimeterZoneObject lotPerimeterZoneObjectByGUID = ZoneManager.Instance.GetLotPerimeterZoneObjectByGUID(lotForCharacter3);
            if (lotPerimeterZoneObjectByGUID != null)
            {
                UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("FindRandomPositionInLot", "InjectInteraction", 173, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                (PathfindingManager.FindRandomPositionInLotResult, Vector3) tuple = PathfindingManager.Instance.FindRandomPositionInLot(character, LotManager.Instance.GetLotByGUID(lotForCharacter3), PathfindingManager.FindRandomPositionValidPositions.All);
                UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
                if (tuple.Item1 == PathfindingManager.FindRandomPositionInLotResult.Success)
                {
                    vector = tuple.Item2;
                }
                else
                {
                    Vector3 position = lotPerimeterZoneObjectByGUID.RandomPositionInZone();
                    UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("GetClosestPositionOnNavMesh for random position in lot", "InjectInteraction", 184, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                    (Vector3, bool) closestPositionOnNavMesh = PathfindingManager.Instance.GetClosestPositionOnNavMesh(position);
                    UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
                    if (closestPositionOnNavMesh.Item2)
                    {
                        (vector, _) = closestPositionOnNavMesh;
                    }
                    else
                    {
                        Logger<InteractionManager>.LogCharacter(character, $"Asked to go to a random position in their lot {lotForCharacter3}, but closest position is not found", "InjectInteraction", 193, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                        vector = character.Data.Position;
                    }
                }
            }
            else
            {
                Logger<InteractionManager>.LogCharacter(character, "Asked to go to a random position in their lot, but not in any lot", "InjectInteraction", 200, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                vector = character.Data.Position;
            }
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
        }
        if (Vector3.Distance(vector, Vector3.zero) < 0.01f)
        {
            Debug.LogError($"Clicked position for injected interaction {interactionInSettings.DisplayName} is Vector3.zero for character {character.Data.FullName}. target was {interactionToInject.TargetPosition}.");
        }
        List<ulong> characters = ((targetOtherCharacter == null) ? new List<ulong> { character.GUID } : new List<ulong> { character.GUID, targetOtherCharacter.GUID });
        UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Check item finder rules for interaction to inject", "InjectInteraction", 212, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
        ItemFinderRuleManager.ItemFinderFunctionParam itemFinderFunctionParam = ItemFinderRuleManager.ItemFinderFunctionParam.None;
        if (isForcedAutonomous || isIdleAutonomous)
        {
            itemFinderFunctionParam = ItemFinderRuleManager.ItemFinderFunctionParam.ReturnOnFirstItemSlotFound | ItemFinderRuleManager.ItemFinderFunctionParam.ShuffleItems;
        }
        var (navigateToFirstActionWithItemFinderResult, itemIDAndSlot) = ItemFinderRuleManager.Instance.FindNearestPathableAndFreeItemSlotOfFirstItemFinderActionPlease(actionByGUID, character, skinGUID, itemFinderFunctionParam);
        if (navigateToFirstActionWithItemFinderResult != ItemFinderRuleManager.NavigateToFirstActionWithItemFinderResult.Found && navigateToFirstActionWithItemFinderResult != ItemFinderRuleManager.NavigateToFirstActionWithItemFinderResult.ActionRequiresNoItemFinder)
        {
            UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
            return;
        }
        UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
        InteractionManager.NewInteractionValues newInteractionValues = new InteractionManager.NewInteractionValues
        {
            Characters = characters,
            CurrentInteractionGUID = AssetManager.GetNewGUID(),
            InteractionSettingGUID = interactionToInject.InjectedInteraction,
            Target = new InteractionTarget
            {
                WorldPosition = vector,
                CharacterGUID = ((interactionToInject.InjectOtherCharacterAsTargetCharacter && targetOtherCharacter != null) ? targetOtherCharacter.GUID : 0),
                ItemInstanceID = bedID, // Modification
                ItemSlotsGUID = (interactionToInject.InjectItemSlotAsTargetItem ? bedSlotIDs : null) // Modification
            },
            IsIdleAutonomous = isIdleAutonomous,
            IsForcedAutonomous = isForcedAutonomous,
            ItemSkinGUID = skinGUID,
            LotGUID = lotGUID
        };
        bool flag = true;
        Action action = null;
        if (character.Data.CarryingCharacterGUID != 0 && flag)
        {
            AssetCharacter carryingCharacter = AssetManager.Instance.GetCharacter(character.Data.CarryingCharacterGUID);
            CharacterCarrying characterCarrying = Settings.Get<CharacterCarrying>();
            List<ulong> charactersToInjectDrop = new List<ulong> { character.GUID, carryingCharacter.GUID };
            InteractionManager.NewInteractionValues dropInteractionValues = new InteractionManager.NewInteractionValues
            {
                CurrentInteractionGUID = AssetManager.GetNewGUID(),
                InteractionSettingGUID = characterCarrying.DropCharacterInteraction,
                Characters = charactersToInjectDrop,
                Target = new InteractionTarget
                {
                    WorldPosition = character.Data.Position
                },
                IsIdleAutonomous = isIdleAutonomous,
                IsForcedAutonomous = isForcedAutonomous
            };
            action = delegate
            {
                Logger<InteractionManager>.LogCharacter(character, "Dropping carried character " + carryingCharacter.Data.FullName + " before injecting interaction " + interactionInSettings.DisplayName, "InjectInteraction", 268, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                __instance.AddToInteractionQueueOfCharacters(charactersToInjectDrop, dropInteractionValues);
            };
        }
        UpdateCharacterAutonomy.AutonomyUpdateProfiler?.Start("Inject interaction to character", "InjectInteraction", 274, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
        switch (interactionToInject.Priority)
        {
        case InjectionPriority.CancelAllActions:
            foreach (AssetCharacterDataInteraction item2 in character.Data.CurrentInteractionsInQueue)
            {
                string text = Settings.Get<Interactions>().GetInteractionByGUID(item2.InteractionSettingGUID)?.DisplayName ?? "Unknown Interaction";
                Logger<InteractionManager>.LogCharacter(character, "Canceling interaction " + text + " to inject interaction " + interactionInSettings.DisplayName, "InjectInteraction", 281, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                item2.State = AssetCharacterDataInteractionState.ToBeCanceled;
            }
            action?.Invoke();
            __instance.AddToInteractionQueueOfCharacters(characters, newInteractionValues);
            break;
        case InjectionPriority.CancelCurrentAction:
            if (character.Data.CurrentInteractionsInQueue.Count > 0)
            {
                foreach (AssetCharacterDataInteraction item3 in GetRunningInteractionsToCancel(character).Item2) // Modification
                {
                    Logger<InteractionManager>.LogCharacter(character, "Canceling interaction " + (Settings.Get<Interactions>().GetInteractionByGUID(item3.InteractionSettingGUID)?.DisplayName ?? item3.InteractionSettingGUID.ToString()) + " to inject interaction " + interactionInSettings.DisplayName + " because the injection priority is CancelCurrentAction", "InjectInteraction", 295, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                    item3.State = AssetCharacterDataInteractionState.ToBeCanceled;
                }
            }
            action?.Invoke();
            __instance.AddToInteractionQueueOfCharacters(characters, newInteractionValues);
            break;
        case InjectionPriority.AtTheEndOfInteractionQueue:
            action?.Invoke();
            __instance.AddToInteractionQueueOfCharacters(characters, newInteractionValues);
            break;
        case InjectionPriority.AfterCurrentAction:
            if (character.Data.CurrentInteractionsInQueue.Count > 0)
            {
                List<AssetCharacterDataInteraction> item = GetRunningInteractionsToCancel(character).Item2; // Modification
                action?.Invoke();
                __instance.AddToInteractionQueueOfCharacters(characters, newInteractionValues, item.Count);
            }
            else
            {
                action?.Invoke();
                __instance.AddToInteractionQueueOfCharacters(characters, newInteractionValues);
            }
            break;
        case InjectionPriority.AfterCurrentActionOrCancelCurrentActionIfCancellable:
            if (character.Data.CurrentInteractionsInQueue.Count > 0)
            {
                var (flag2, list) = GetRunningInteractionsToCancel(character);
                if (flag2)
                {
                    foreach (AssetCharacterDataInteraction item4 in list)
                    {
                        Logger<InteractionManager>.LogCharacter(character, "Canceling interaction " + (Settings.Get<Interactions>().GetInteractionByGUID(item4.InteractionSettingGUID)?.DisplayName ?? item4.InteractionSettingGUID.ToString()) + " to inject interaction " + interactionInSettings.DisplayName + " because it is cancellable and the injection priority is AfterCurrentActionOrCancelCurrentActionIfCancellable", "InjectInteraction", 334, "C:\\Users\\poik0\\Documents\\paralives\\paralives\\Assets\\Scripts\\Characters\\Managers\\InteractionManager.cs");
                        item4.State = AssetCharacterDataInteractionState.ToBeCanceled;
                    }
                }
                action?.Invoke();
                __instance.AddToInteractionQueueOfCharacters(characters, newInteractionValues);
            }
            else
            {
                action?.Invoke();
                __instance.AddToInteractionQueueOfCharacters(characters, newInteractionValues);
            }
            break;
        }
        UpdateCharacterAutonomy.AutonomyUpdateProfiler?.StopOneLevel();
    }
}
