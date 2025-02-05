using System.Runtime.CompilerServices;
using GameNetcodeStuff;
using HarmonyLib;
using OPJosMod.ReviveCompany;
using Scoops.misc;
using Scoops.service;
using UnityEngine;

namespace Scoops.compatability
{
    internal static class ReviveCompanyCompat
    {
        public static bool Enabled =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("OpJosMod.ReviveCompany");

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [HarmonyPatch(typeof(GeneralUtil))]
        [HarmonyPatch("RevivePlayer")]
        [HarmonyPostfix]
        public static void RevivePlayerPatch(int playerId)
        {
            if (playerId >= RoundManager.Instance.playersManager.allPlayerScripts.Length) return;

            PlayerControllerB player = RoundManager.Instance.playersManager.allPlayerScripts[playerId];

            if (player == null) return;

            PlayerPhone phone = player.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();

            if (phone == null) return;

            phone.Revive();
        }
    }
}
