using GameNetcodeStuff;
using HarmonyLib;
using Scoops.service;

namespace Scoops.patch
{
    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPhonePatch
    {
        [HarmonyPatch("ReviveDeadPlayers")]
        [HarmonyPostfix]
        private static void ResetPhones(ref StartOfRound __instance)
        {
            PhoneNetworkHandler.Instance.localPhone.ToggleActive(false);
            PhoneNetworkHandler.Instance.localPhone.SetPhoneModelActive(false);
        }
    }
}
