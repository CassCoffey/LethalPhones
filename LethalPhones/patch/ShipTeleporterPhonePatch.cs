using GameNetcodeStuff;
using HarmonyLib;
using Scoops.misc;
using Scoops.service;
using UnityEngine;

namespace Scoops.patch
{
    [HarmonyPatch(typeof(ShipTeleporter))]
    public class ShipTeleporterPhonePatch
    {
        [HarmonyPatch("TeleportPlayerOutWithInverseTeleporter")]
        [HarmonyPostfix]
        private static void InverseTeleported(ref ShipTeleporter __instance, int playerObj, Vector3 teleportPos)
        {
            PlayerControllerB playerControllerB = StartOfRound.Instance.allPlayerScripts[playerObj];
            if (playerControllerB == null || !playerControllerB.IsLocalPlayer)
            {
                return;
            }

            PlayerPhone phone = PhoneNetworkHandler.Instance.localPhone;
            if (phone != null)
            {
                phone.ApplyTemporaryInterference(1f);
            }
        }
    }
}
