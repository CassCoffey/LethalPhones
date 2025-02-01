using Dissonance;
using GameNetcodeStuff;
using HarmonyLib;
using Scoops.misc;
using Scoops.service;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

namespace Scoops.patch
{
    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPhonePatch
    {
        [HarmonyPatch("ReviveDeadPlayers")]
        [HarmonyPostfix]
        private static void ResetPhones(ref StartOfRound __instance)
        {
            PlayerPhone[] allPhones = GameObject.FindObjectsByType<PlayerPhone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < allPhones.Length; i++)
            {
                allPhones[i].Revive();
            }

            PhoneNetworkHandler.Instance.localPhone.ToggleActive(false);
        }

        [HarmonyPatch("OnPlayerDC")]
        [HarmonyPrefix]
        private static void CleanupPlayerPhone(ref StartOfRound __instance, int playerObjectNumber, ulong clientId)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                return;
            }
            PhoneNetworkHandler.Instance.DeletePlayerPhone(playerObjectNumber);

            PhoneNetworkHandler.Instance.UpdateClipboardText();
        }

        [HarmonyPatch("PassTimeToNextDay")]
        [HarmonyPostfix]
        private static void PassTimeToNextDay(ref StartOfRound __instance)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                return;
            }
            if (Config.respawnClipboard.Value)
            {
                PhoneNetworkHandler.Instance.CheckClipboardRespawn();
            }
        }

        [HarmonyPatch("UnlockShipObject")]
        [HarmonyPostfix]
        private static void UnlockShipObject(ref StartOfRound __instance, int unlockableID)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                return;
            }
            PhoneNetworkHandler.CheckPhoneUnlock();
        }

        [HarmonyPatch("LoadUnlockables")]
        [HarmonyPostfix]
        private static void LoadUnlockables(ref StartOfRound __instance)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                return;
            }
            PhoneNetworkHandler.CheckPhoneUnlock();
        }

        [HarmonyPatch("ResetShip")]
        [HarmonyPostfix]
        private static void ResetShip(ref StartOfRound __instance)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                return;
            }
            PhoneNetworkHandler.CheckPhoneUnlock();
        }
    }
}
