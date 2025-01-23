using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Scoops.service;
using System;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using System.ComponentModel;
using System.Collections.Generic;
using Scoops.misc;
using System.Reflection;
using System.Reflection.Emit;
using Scoops.customization;
using LethalLib.Modules;

namespace Scoops.patch;

[HarmonyPatch(typeof(PlayerControllerB))]
[HarmonyPatch("SetPlayerSanityLevel")]
public static class PlayerControllerB_SetPlayerSanityLevel_Patch
{
    static FieldInfo f_isPlayerAlone = AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.isPlayerAlone));
    static MethodInfo m_UpdatePhoneSanity = AccessTools.Method(typeof(PlayerPhone), nameof(PlayerPhone.UpdatePhoneSanity), new Type[] { typeof(PlayerControllerB) });

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);

        int insertionIndex = -1;
        for (int i = 0; i < code.Count - 3; i++)
        {
            if (code[i].opcode == OpCodes.Ldarg_0 && code[i + 1].opcode == OpCodes.Ldfld && code[i + 2].opcode == OpCodes.Ldc_R4 && code[i + 3].opcode == OpCodes.Bge_Un)
            {
                insertionIndex = i + 1;
                break;
            }
        }

        var instructionsToInsert = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Call, m_UpdatePhoneSanity),
            new CodeInstruction(OpCodes.Ldarg_0)
        };

        if (insertionIndex != -1)
        {
            code.InsertRange(insertionIndex, instructionsToInsert);
        }

        return code;
    }
}

[HarmonyPatch(typeof(PlayerControllerB))]
public class PlayerPhonePatch
{
    public static PhoneNetworkHandler PhoneManager;

    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void CreatePhoneAssets(ref PlayerControllerB __instance)
    {
        GameObject phoneAudioPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhoneAudioExternal");
        GameObject.Instantiate(phoneAudioPrefab, __instance.transform.Find("Audios"));

        GameObject localPhoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("LocalPhoneModel");
        GameObject localPhoneModel = GameObject.Instantiate(localPhoneModelPrefab, __instance.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L"), false);

        GameObject serverPhoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerPhoneModel");
        GameObject serverPhoneModel = GameObject.Instantiate(serverPhoneModelPrefab, __instance.lowerSpine.Find("spine.002").Find("spine.003").Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L"), false);

        Transform ArmsRig = __instance.localArmsTransform.Find("RigArms");
        Transform ServerArmsRig = __instance.meshContainer.Find("metarig").Find("Rig 1");
        GameObject rightArmPhoneRigPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("RightArmPhone");
        GameObject leftArmPhoneRigPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("LeftArmPhone");
        GameObject leftArmServerPhoneRigPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerLeftArmPhone");
        GameObject leftArmServerPhoneTargetPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerPhoneTargetHolder");

        GameObject rightArmPhoneRig = GameObject.Instantiate(rightArmPhoneRigPrefab, ArmsRig, false);
        GameObject leftArmPhoneRig = GameObject.Instantiate(leftArmPhoneRigPrefab, ArmsRig, false);
        GameObject serverLeftArmPhoneRig = GameObject.Instantiate(leftArmServerPhoneRigPrefab, ServerArmsRig, false);

        GameObject serverLeftArmPhoneTarget = GameObject.Instantiate(leftArmServerPhoneTargetPrefab, __instance.lowerSpine.Find("spine.002").Find("spine.003"), false);

        rightArmPhoneRig.GetComponent<ChainIKConstraint>().data.root = __instance.localArmsTransform.Find("shoulder.R").Find("arm.R_upper");
        rightArmPhoneRig.GetComponent<ChainIKConstraint>().data.tip = __instance.localArmsTransform.Find("shoulder.R").Find("arm.R_upper").Find("arm.R_lower").Find("hand.R");

        leftArmPhoneRig.GetComponent<ChainIKConstraint>().data.root = __instance.localArmsTransform.Find("shoulder.L").Find("arm.L_upper");
        leftArmPhoneRig.GetComponent<ChainIKConstraint>().data.tip = __instance.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L");

        serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.root = __instance.lowerSpine.Find("spine.002").Find("spine.003").Find("shoulder.L").Find("arm.L_upper");
        serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.tip = __instance.lowerSpine.Find("spine.002").Find("spine.003").Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L");
        serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.target = serverLeftArmPhoneTarget.transform.Find("ServerPhoneTarget");

        rightArmPhoneRig.GetComponent<ChainIKConstraint>().MarkDirty();
        leftArmPhoneRig.GetComponent<ChainIKConstraint>().MarkDirty();
        serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().MarkDirty();

        __instance.playerBodyAnimator.GetComponent<RigBuilder>().Build();

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            GameObject playerPhone = GameObject.Instantiate(NetworkObjectManager.phonePrefab, Vector3.zero, Quaternion.identity);
            playerPhone.GetComponent<NetworkObject>().Spawn();
            playerPhone.GetComponent<NetworkObject>().TrySetParent(__instance.transform, false);
        }
    }

    [HarmonyPatch("SpectateNextPlayer")]
    [HarmonyPostfix]
    private static void SpectatedNextPlayer(ref PlayerControllerB __instance)
    {
        PlayerPhone[] allPhones = GameObject.FindObjectsByType<PlayerPhone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < allPhones.Length; i++)
        {
            allPhones[i].spectatorClear = true;
        }
    }

    [HarmonyPatch("ConnectClientToPlayerObject")]
    [HarmonyPostfix]
    private static void InitPhone(ref PlayerControllerB __instance)
    {
        PhoneManager = PhoneNetworkHandler.Instance;
        NetworkObject phone = __instance.transform.Find("PhonePrefab(Clone)").GetComponent<NetworkObject>();
        PhoneManager.CreateNewPhone(phone.NetworkObjectId, CustomizationManager.SelectedSkin, CustomizationManager.SelectedCharm, CustomizationManager.SelectedRingtone);

        PhoneManager.RequestClientUpdates();

        Plugin.InputActionInstance.TogglePhoneKey.performed += OnTogglePhoneKeyPressed;
        Plugin.InputActionInstance.PickupPhoneKey.performed += OnPickupPhoneKeyPressed;
        Plugin.InputActionInstance.HangupPhoneKey.performed += OnHangupPhoneKeyPressed;
        Plugin.InputActionInstance.VolumePhoneKey.performed += OnVolumePhoneKeyPressed;
    }

    [HarmonyPatch("OnDestroy")]
    [HarmonyPrefix]
    private static void CleanupPhone(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return;
        }
        Plugin.InputActionInstance.TogglePhoneKey.performed -= OnTogglePhoneKeyPressed;
        Plugin.InputActionInstance.PickupPhoneKey.performed -= OnPickupPhoneKeyPressed;
        Plugin.InputActionInstance.HangupPhoneKey.performed -= OnHangupPhoneKeyPressed;
        Plugin.InputActionInstance.VolumePhoneKey.performed -= OnVolumePhoneKeyPressed;
    }

    [HarmonyPatch("DamagePlayer")]
    [HarmonyPostfix]
    private static void PlayerDamaged(ref PlayerControllerB __instance, int damageNumber, bool hasDamageSFX = true, bool callRPC = true, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, int deathAnimation = 0, bool fallDamage = false, Vector3 force = default(Vector3))
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return;
        }

        float changeAmount = 0f - Mathf.Clamp01(damageNumber / 50f);
        PhoneManager.localPhone.InfluenceConnectionQuality(changeAmount);
    }

    [HarmonyPatch("DisablePlayerModel")]
    [HarmonyPostfix]
    private static void PlayerModelDisabled(ref PlayerControllerB __instance, GameObject playerObject, bool enable = false, bool disableLocalArms = false)
    {
        PlayerPhone phone = __instance.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
        phone.SetPhoneLocalModelActive(false);
        phone.SetPhoneServerModelActive(false);
    }

    [HarmonyPatch("KillPlayerClientRpc")]
    [HarmonyPostfix]
    private static void PlayerDeath(ref PlayerControllerB __instance, int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation)
    {
        PlayerPhone phone = __instance.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
        phone.Death(causeOfDeath);
    }

    [HarmonyPatch("SpawnDeadBody")]
    [HarmonyPostfix]
    private static void PlayerSpawnBody(ref PlayerControllerB __instance, int playerId, Vector3 bodyVelocity, int causeOfDeath, PlayerControllerB deadPlayerController, int deathAnimation = 0, Transform overridePosition = null)
    {
        PlayerPhone phone = __instance.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
        phone.ApplyCorpse();
    }

    private static void OnTogglePhoneKeyPressed(InputAction.CallbackContext context)
    {
        if (PhoneManager == null || PhoneManager.localPhone == null)
        {
            return;
        }
        if (PhoneNetworkHandler.Locked.Value)
        {
            return;
        }
        PlayerControllerB localPlayer = PhoneManager.localPhone.player;
        if (localPlayer == null)
        {
            return;
        }
        if (localPlayer.quickMenuManager.isMenuOpen || localPlayer.isGrabbingObjectAnimation || localPlayer.isTypingChat || localPlayer.inTerminalMenu || localPlayer.throwingObject || localPlayer.IsInspectingItem)
        {
            return;
        }

        PhoneManager.localPhone.ToggleActive(!PhoneManager.localPhone.toggled);
    }

    private static void OnPickupPhoneKeyPressed(InputAction.CallbackContext context)
    {
        if (PhoneManager == null || PhoneManager.localPhone == null)
        {
            return;
        }
        PlayerControllerB localPlayer = PhoneManager.localPhone.player;
        if (localPlayer == null)
        {
            return;
        }
        if (localPlayer.quickMenuManager.isMenuOpen || localPlayer.isGrabbingObjectAnimation || localPlayer.isTypingChat || localPlayer.inTerminalMenu || localPlayer.throwingObject || localPlayer.IsInspectingItem)
        {
            return;
        }

        PhoneManager.localPhone.CallButtonPressed();
    }

    private static void OnHangupPhoneKeyPressed(InputAction.CallbackContext context)
    {
        if (PhoneManager == null || PhoneManager.localPhone == null)
        {
            return;
        }
        PlayerControllerB localPlayer = PhoneManager.localPhone.player;
        if (localPlayer == null)
        {
            return;
        }
        if (localPlayer.quickMenuManager.isMenuOpen || localPlayer.isGrabbingObjectAnimation || localPlayer.isTypingChat || localPlayer.inTerminalMenu || localPlayer.throwingObject || localPlayer.IsInspectingItem)
        {
            return;
        }

        PhoneManager.localPhone.HangupButtonPressed();
    }

    private static void OnVolumePhoneKeyPressed(InputAction.CallbackContext context)
    {
        PlayerControllerB localPlayer = PhoneManager.localPhone.player;
        if (localPlayer == null)
        {
            return;
        }
        if (localPlayer.quickMenuManager.isMenuOpen || localPlayer.isGrabbingObjectAnimation || localPlayer.isTypingChat || localPlayer.inTerminalMenu || localPlayer.throwingObject || localPlayer.IsInspectingItem)
        {
            return;
        }

        PhoneManager.localPhone.VolumeButtonPressed();
    }

    [HarmonyPatch("ActivateItem_performed")]
    [HarmonyPrefix]
    private static bool ActivateItem_performed(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return true;
        }
        if (PhoneManager.localPhone.toggled)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch("InspectItem_performed")]
    [HarmonyPrefix]
    private static bool InspectItem_performed(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return true;
        }
        if (PhoneManager.localPhone.toggled)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch("QEItemInteract_performed")]
    [HarmonyPrefix]
    private static bool QEItemInteract_performed(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return true;
        }
        if (PhoneManager.localPhone.toggled)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch("ItemSecondaryUse_performed")]
    [HarmonyPrefix]
    private static bool ItemSecondaryUse_performed(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return true;
        }
        if (PhoneManager.localPhone.toggled)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch("ItemTertiaryUse_performed")]
    [HarmonyPrefix]
    private static bool ItemTertiaryUse_performed(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return true;
        }
        if (PhoneManager.localPhone.toggled)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch("Interact_performed")]
    [HarmonyPostfix]
    private static void Interact_performed(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return;
        }
        if (PhoneManager.localPhone.toggled && __instance.inTerminalMenu)
        {
            PhoneManager.localPhone.ToggleActive(false);
        }
    }

    [HarmonyPatch("GrabObjectClientRpc")]
    [HarmonyPostfix]
    private static void GrabObjectClientRpc(ref PlayerControllerB __instance, bool grabValidated, NetworkObjectReference grabbedObject)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return;
        }
        if (PhoneManager.localPhone.toggled)
        {
            PhoneManager.localPhone.ToggleActive(false);
        }
    }

    [HarmonyPatch("ScrollMouse_performed")]
    [HarmonyPostfix]
    private static void ScrollMouse_performed(ref PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return;
        }
        if (PhoneManager.localPhone.toggled && __instance.isHoldingObject)
        {
            PhoneManager.localPhone.ToggleActive(false);
        }
    }
}