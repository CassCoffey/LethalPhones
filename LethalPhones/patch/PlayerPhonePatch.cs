using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using BepInEx.Logging;
using Scoops.misc;
using Scoops.service;
using System;
using UnityEngine.InputSystem;
using System.Numerics;
using System.Collections.Generic;

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

        GameObject phonePrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhonePrefab");
        GameObject playerPhone = GameObject.Instantiate(phonePrefab, __instance.transform);
        playerPhone.AddComponent<PlayerPhone>();
        playerPhone.GetComponent<NetworkObject>().Spawn();
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
                Plugin.Log.LogInfo("Phone opened! Your number is: " + PhoneManager.localPhone.phoneNumber);
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