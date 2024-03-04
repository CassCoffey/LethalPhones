using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Scoops.service;
using System;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

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
        GameObject.Instantiate(phoneModelPrefab, __instance.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L"), false);

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

        Keyboard.current.onTextInput += KeyboardType;
    }

    [HarmonyPatch("Update")]
    [HarmonyPostfix]
    private static void ReadInput(ref PlayerControllerB __instance)
    {
        if (((!((NetworkBehaviour)__instance).IsOwner || !__instance.isPlayerControlled || (((NetworkBehaviour)__instance).IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer) || __instance.inTerminalMenu || __instance.isTypingChat || !Application.isFocused)
        {
            return;
        }

        if (Plugin.InputActionInstance.TogglePhoneKey.triggered)
        {
            PhoneManager.localPhone.ToggleActive(!PhoneManager.localPhone.toggled);
            if (PhoneManager.localPhone.toggled)
            {
                Plugin.Log.LogInfo("Phone opened! Your number is: " + PhoneManager.localPhone.phoneNumber + ", your name is: " + __instance.gameObject.name);
            } else
            {
                Plugin.Log.LogInfo("Phone closed!");
            }
        }

        if (PhoneManager.localPhone.toggled)
        {
            if (Plugin.InputActionInstance.PickupHangupPhoneKey.triggered)
            {
                PhoneManager.localPhone.CallButtonPressed();
            }
        }
    }

    private static void KeyboardType(char ch)
    {
        if (PhoneManager.localPhone.toggled && Char.IsNumber(ch))
        {
            PhoneManager.localPhone.DialNumber(int.Parse(ch.ToString()));
        }
    }
}