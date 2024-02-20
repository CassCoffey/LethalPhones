using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using BepInEx.Logging;
using Scoops.misc;
using Scoops.service;

namespace Scoops.patch;

/// <summary>
/// Patch to modify the behavior of a player.
/// </summary>
[HarmonyPatch(typeof(PlayerControllerB))]
public class PlayerPhonePatch
{
    public static PhoneNetworkHandler PhoneManager;

    [HarmonyPatch("ConnectClientToPlayerObject")]
    [HarmonyPostfix]
    private static void InitPhone(ref PlayerControllerB __instance)
    {
        PhoneManager = PhoneNetworkHandler.Instance;
        PhoneManager.CreateNewPhone(__instance);
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
                Plugin.Log.LogInfo("Phone opened!");
            } else
            {
                Plugin.Log.LogInfo("Phone closed!");
            }
        }
    }
}
