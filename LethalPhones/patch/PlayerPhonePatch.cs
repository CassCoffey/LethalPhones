using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Scoops.service;
using System;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using System.ComponentModel;

namespace Scoops.patch;

/// <summary>
/// Patch to modify the behavior of a player.
/// </summary>
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

        GameObject phoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("LocalPhoneModel");
        GameObject phoneModel = GameObject.Instantiate(phoneModelPrefab, __instance.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L"), false);

        Transform ArmsRig = __instance.localArmsTransform.Find("RigArms");
        GameObject rightArmPhoneRigPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("RightArmPhone");
        GameObject leftArmPhoneRigPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("LeftArmPhone");

        GameObject rightArmPhoneRig = GameObject.Instantiate(rightArmPhoneRigPrefab, ArmsRig, false);
        GameObject leftArmPhoneRig = GameObject.Instantiate(leftArmPhoneRigPrefab, ArmsRig, false);

        rightArmPhoneRig.GetComponent<ChainIKConstraint>().data.root = __instance.localArmsTransform.Find("shoulder.R").Find("arm.R_upper");
        rightArmPhoneRig.GetComponent<ChainIKConstraint>().data.tip = __instance.localArmsTransform.Find("shoulder.R").Find("arm.R_upper").Find("arm.R_lower").Find("hand.R");

        leftArmPhoneRig.GetComponent<ChainIKConstraint>().data.root = __instance.localArmsTransform.Find("shoulder.L").Find("arm.L_upper");
        leftArmPhoneRig.GetComponent<ChainIKConstraint>().data.tip = __instance.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L");

        rightArmPhoneRig.GetComponent<ChainIKConstraint>().MarkDirty();
        leftArmPhoneRig.GetComponent<ChainIKConstraint>().MarkDirty();

        __instance.playerBodyAnimator.GetComponent<RigBuilder>().Build();

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            GameObject playerPhone = GameObject.Instantiate(NetworkObjectManager.phonePrefab, Vector3.zero, Quaternion.identity);
            playerPhone.GetComponent<NetworkObject>().Spawn();
            playerPhone.GetComponent<NetworkObject>().TrySetParent(__instance.transform, false);
        }
    }

    [HarmonyPatch("ConnectClientToPlayerObject")]
    [HarmonyPostfix]
    private static void InitPhone(ref PlayerControllerB __instance)
    {
        PhoneManager = PhoneNetworkHandler.Instance;
        PhoneManager.CreateNewPhone();

        Plugin.InputActionInstance.TogglePhoneKey.performed += OnTogglePhoneKeyPressed;
        Plugin.InputActionInstance.PickupPhoneKey.performed += OnPickupPhoneKeyPressed;
        Plugin.InputActionInstance.HangupPhoneKey.performed += OnHangupPhoneKeyPressed;
        Plugin.InputActionInstance.VolumePhoneKey.performed += OnVolumePhoneKeyPressed;
    }

    private static void OnTogglePhoneKeyPressed(InputAction.CallbackContext context)
    {
        PlayerControllerB localPlayer = PhoneManager.localPhone.player;
        if (localPlayer == null)
        {
            return;
        }
        if (localPlayer.isGrabbingObjectAnimation || localPlayer.isTypingChat || localPlayer.inTerminalMenu || localPlayer.throwingObject || localPlayer.IsInspectingItem)
        {
            return;
        }

        PhoneManager.localPhone.ToggleActive(!PhoneManager.localPhone.toggled);
    }

    private static void OnPickupPhoneKeyPressed(InputAction.CallbackContext context)
    {
        PhoneManager.localPhone.CallButtonPressed();
    }

    private static void OnHangupPhoneKeyPressed(InputAction.CallbackContext context)
    {
        PhoneManager.localPhone.HangupButtonPressed();
    }

    private static void OnVolumePhoneKeyPressed(InputAction.CallbackContext context)
    {
        PhoneManager.localPhone.VolumeButtonPressed();
    }

    [HarmonyPatch("KillPlayerClientRpc")]
    [HarmonyPostfix]
    private static void PlayerDeath(ref PlayerControllerB __instance, int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation)
    {
        if (__instance.IsOwner)
        {
            Plugin.Log.LogInfo("We died!");
            PhoneManager.localPhone.Death(causeOfDeath);
        }
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
        if (grabValidated && PhoneManager.localPhone.toggled)
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