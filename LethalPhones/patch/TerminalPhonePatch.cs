using GameNetcodeStuff;
using HarmonyLib;
using Scoops.misc;
using Scoops.service;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.patch
{
    [HarmonyPatch(typeof(Terminal))]
    public class TerminalPhonePatch
    {
        [HarmonyPatch("TextPostProcess")]
        [HarmonyPostfix]
        private static void TextPostProcess(ref Terminal __instance, string modifiedDisplayText, TerminalNode node)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                return;
            }
            Debug.Log("Locked - " + PhoneNetworkHandler.Locked.Value);
            Debug.Log("hasBeenUnlockedByPlayer - " + PhoneAssetManager.PersonalPhones.hasBeenUnlockedByPlayer);
            if (PhoneNetworkHandler.Locked.Value && PhoneAssetManager.PersonalPhones.hasBeenUnlockedByPlayer)
            {
                PhoneNetworkHandler.Locked.Value = false;
            }
        }
    }
}
