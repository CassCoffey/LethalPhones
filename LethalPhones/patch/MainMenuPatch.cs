using HarmonyLib;
using Scoops.compatability;
using Scoops.customization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scoops.patch
{
    internal class MainMenuPatch
    {
        // Borrowing many methods for menu injection from LethalConfig.

        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        public static void Start(ref MenuManager __instance)
        {
            if (__instance.isInitScene) return;

            if (CustomizationManager.skinCustomizations.Count == 1 && CustomizationManager.charmCustomizations.Count == 1 && CustomizationManager.ringtoneCustomizations.Count == 1) return; // Don't spawn the ui if no cosmetics are loaded

            __instance.StartCoroutine(DelayedMainMenuInjection());
        }

        private static IEnumerator DelayedMainMenuInjection()
        {
            yield return new WaitForSeconds(0);
            InjectToMainMenu();
        }

        private static void InjectToMainMenu()
        {
            Plugin.Log.LogInfo("Injecting phone customization menu into main menu...");

            var menuContainer = GameObject.Find("MenuContainer");
            if (!menuContainer) return;

            var mainButtonsTransform = menuContainer.transform.Find("MainButtons");
            if (!mainButtonsTransform) return;

            var quitButton = mainButtonsTransform.Find("QuitButton");
            if (!quitButton) return;

            CustomizationMenuUtils.InjectMenu(mainButtonsTransform, quitButton.gameObject);
        }
    }
}
