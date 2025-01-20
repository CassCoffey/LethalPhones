using HarmonyLib;
using Scoops.customization;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Scoops.patch
{
    internal class MainMenuPatch
    {
        [HarmonyPatch(typeof(MenuManager), "Awake")]
        [HarmonyPostfix]
        public static void AttachPhoneCustomizationUI(MenuManager __instance)
        {
            if (__instance.isInitScene) return;

            CustomizationManager.SpawnCustomizationGUI();
        }
    }
}
