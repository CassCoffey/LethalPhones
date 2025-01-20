using Scoops.service;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Scoops.customization
{
    public class CustomizationManager
    {
        public const string DEFAULT_SKIN = "lethalphones.customizations.default";

        public static string SelectedSkin = "lethalphones.customizations.default";

        public static Dictionary<string, GameObject> skinCustomizations = new Dictionary<string, GameObject>();
        public static List<string> skinIds = new List<string>();

        public static Dictionary<string, GameObject> charmCustomizations = new Dictionary<string, GameObject>();
        public static List<string> charmIds = new List<string>();

        private static Transform customizationPanel;
        private static GameObject displayPhone;

        private static int index = 0; 

        public static void LoadSkinCustomizations(AssetBundle bundle, string bundleName = null)
        {
            foreach (var potentialPrefab in bundle.GetAllAssetNames())
            {
                if (!potentialPrefab.ToLower().EndsWith("_skin.prefab"))
                {
                    continue;
                }
                GameObject customization = bundle.LoadAsset<GameObject>(potentialPrefab);

                string customizationId = (bundleName ?? "") + "." + customization.name.ToLower().Replace("_skin", "");

                if (skinCustomizations.ContainsKey(customizationId))
                {
                    Debug.Log("Skipped skin customization: " + customizationId + ", reason: duplicate id");
                    continue;
                }

                Debug.Log("Loaded skin customization: " + customizationId);
                skinCustomizations.Add(customizationId, customization);
                skinIds.Add(customizationId);
            }
        }

        public static void SpawnCustomizationGUI()
        {
            if (skinCustomizations.Count == 0 && charmCustomizations.Count == 0) return; // Don't spawn the ui if no cosmetics are loaded

            GameObject customizationGUI = GameObject.Instantiate(PhoneAssetManager.customizationGUI);

            customizationPanel = customizationGUI.transform.Find("CustomizationCanvas").Find("CustomizationPanel");

            displayPhone = customizationPanel.Find("Panel").Find("PhoneDisplayModel").gameObject;

            GameObject nextCustomizationButton = customizationPanel.Find("Panel").Find("NextButton").gameObject;
            GameObject prevCustomizationButton = customizationPanel.Find("Panel").Find("PrevButton").gameObject;
            nextCustomizationButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                NextSkin();
            });
            prevCustomizationButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                PrevSkin();
            });

            GameObject closeButton = customizationPanel.Find("Panel").Find("ResponseButton").gameObject;
            closeButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                SaveSkin();
            });

            ApplySkinToDisplay(SelectedSkin);
            index = skinIds.IndexOf(SelectedSkin);
        }

        public static void SaveSkin()
        {
            Plugin.WriteCustomizationToFile();
        }

        public static void NextSkin()
        {
            index++;
            if (index >= skinIds.Count) { index = 0; }

            SelectedSkin = skinIds[index];
            ApplySkinToDisplay(skinIds[index]);
        }

        public static void PrevSkin()
        {
            index--;
            if (index < 0) { index = skinIds.Count - 1; }

            SelectedSkin = skinIds[index];
            ApplySkinToDisplay(skinIds[index]);
        }

        public static void ApplySkinToDisplay(string skinId)
        {
            GameObject skinObject = skinCustomizations[skinId];
            if (skinObject == null) return;

            // Main Mat
            displayPhone.GetComponent<Renderer>().materials = skinObject.GetComponent<Renderer>().materials;
            // Antenna Mat
            displayPhone.transform.Find("PhoneAntenna").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneAntenna").GetComponent<Renderer>().materials;
            // Dial Mat
            displayPhone.transform.Find("PhoneDial").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneDial").GetComponent<Renderer>().materials;
            // Top Mat
            displayPhone.transform.Find("PhoneTop").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneTop").GetComponent<Renderer>().materials;
        }
    }
}
