using Dissonance;
using GameNetcodeStuff;
using HarmonyLib;
using Scoops.gameobjects;
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
        [HarmonyPatch("OnPlayerDC")]
        [HarmonyPrefix]
        private static void CleanupPlayerPhone(ref StartOfRound __instance, int playerObjectNumber, ulong clientId)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                return;
            }
            PhoneNetworkHandler.Instance.DeletePlayerPhone(playerObjectNumber);
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

        [HarmonyPatch(typeof(HUDManager))]
        [HarmonyPatch("AddPlayerChatMessageClientRpc")]
        [HarmonyPostfix]
        private static void AddPlayerChatMessageClientRpc(ref HUDManager __instance, string chatMessage, int playerId)
        {
            PlayerControllerB otherPlayer = __instance.playersManager.allPlayerScripts[playerId];
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

            if (otherPlayer != localPlayer && (localPlayer.transform.position - otherPlayer.transform.position).sqrMagnitude > (25f * 25f))
            {
                PhoneBehavior otherPhone = otherPlayer.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
                PhoneBehavior localPhone = PhoneNetworkHandler.Instance.localPhone;
                SwitchboardPhone switchboard = otherPhone.GetCallerPhone() as SwitchboardPhone;
                if (switchboard == null) switchboard = localPhone.GetCallerPhone() as SwitchboardPhone;

                if (localPhone.GetCallerPhone() == otherPhone || 
                    (switchboard != null && ((switchboard.switchboardOperator == otherPlayer && localPhone.GetCallerPhone() == switchboard) ||
                    switchboard.switchboardOperator == localPlayer && otherPhone.GetCallerPhone() == switchboard)))
                {
                    __instance.AddChatMessage(chatMessage, otherPlayer.playerUsername);
                }
            }
        }
    }
}
