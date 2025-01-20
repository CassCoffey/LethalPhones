using Scoops.service;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Scoops.customization
{
    public class CustomizationManager
    {
        public static Dictionary<string, PhoneCustomization> skinCustomizations = new Dictionary<string, PhoneCustomization>();
        public static Dictionary<string, PhoneCustomization> charmCustomizations = new Dictionary<string, PhoneCustomization>();

        private static GameObject customizationGUI;
        private static GameObject displayPhone;

        public static void LoadCustomizations(AssetBundle bundle, string bundleName = null)
        {
            foreach (var potentialPrefab in bundle.GetAllAssetNames())
            {
                if (!potentialPrefab.EndsWith(".prefab"))
                {
                    continue;
                }
                GameObject customization = bundle.LoadAsset<GameObject>(potentialPrefab);
                PhoneCustomization customizationBehavior = customization.GetComponent<PhoneCustomization>();
                if (customizationBehavior == null)
                {
                    continue;
                }

                if (customizationBehavior.skin)
                {
                    if (skinCustomizations.ContainsKey(customizationBehavior.customizationId))
                    {
                        Debug.Log("Skipped skin customization: " + customizationBehavior.customizationId + ", bundle: " + bundleName + ", reason: duplicate id");
                        continue;
                    }

                    Debug.Log("Loaded skin customization: " + customizationBehavior.customizationId + " from bundle: " + bundleName);
                    skinCustomizations.Add(customizationBehavior.customizationId, customizationBehavior);
                }
                else if (customizationBehavior.charm)
                {
                    if (charmCustomizations.ContainsKey(customizationBehavior.customizationId))
                    {
                        Debug.Log("Skipped charm customization: " + customizationBehavior.customizationId + ", bundle: " + bundleName + ", reason: duplicate id");
                        continue;
                    }

                    Debug.Log("Loaded charm customization: " + customizationBehavior.customizationId + " from bundle: " + bundleName);
                    charmCustomizations.Add(customizationBehavior.customizationId, customizationBehavior);
                }
            }
        }

        public static void SpawnCustomizationGUI()
        {
            //if (skinCustomizations.Count == 0 && charmCustomizations.Count == 0) return; // Don't spawn the ui if no cosmetics are loaded

            customizationGUI = GameObject.Instantiate(PhoneAssetManager.customizationGUI);
        }
    }
}
