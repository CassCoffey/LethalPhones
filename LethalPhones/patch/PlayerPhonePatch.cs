using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Scoops.service;
using System;
using UnityEngine.InputSystem;

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

        Transform leftHand = __instance.localArmsTransform.Find("RigArms").Find("LeftArm").Find("ArmsLeftArm_target");
        GameObject phoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("LocalPhoneModel");
        GameObject.Instantiate(phoneModelPrefab, leftHand, false);

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
            PhoneManager.localPhone.toggled = !PhoneManager.localPhone.toggled;
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