using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using BepInEx.Logging;

namespace Scoops.patch;

/// <summary>
/// Patch to modify the behavior of a player.
/// </summary>
[HarmonyPatch(typeof(PlayerControllerB))]
public class PlayerPhonePatch
{
    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void InitPhone(ref PlayerControllerB __instance)
    {
        Plugin.Instance.PhoneManager.CreateNewPhone(__instance);
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
            Plugin.Log.LogInfo("Phone opened!");
        }
    }
}
