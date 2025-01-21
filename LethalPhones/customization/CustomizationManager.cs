﻿using Scoops.service;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scoops.customization
{
    public class CustomizationManager
    {
        public const string DEFAULT_SKIN = "lethalphones.customizations.default";
        public const string DEFAULT_RINGTONE = "lethalphones.customizations.default";

        public static string SelectedSkin = "lethalphones.customizations.default";
        public static string SelectedRingtone = "lethalphones.customizations.default";

        public static Dictionary<string, GameObject> skinCustomizations = new Dictionary<string, GameObject>();
        public static List<string> skinIds = new List<string>();

        public static Dictionary<string, GameObject> charmCustomizations = new Dictionary<string, GameObject>();
        public static List<string> charmIds = new List<string>();

        public static Dictionary<string, AudioClip> ringtoneCustomizations = new Dictionary<string, AudioClip>();
        public static List<string> ringtoneIds = new List<string>();

        private static Transform customizationPanel;

        private static GameObject displayPhone;
        private static GameObject displayRingtone;
        private static GameObject displayRingtoneName;

        private static int skinIndex = 0;
        private static int ringtoneIndex = 0;

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

        public static void LoadRingtoneCustomizations(AssetBundle bundle, string bundleName = null)
        {
            foreach (var potentialRingtone in bundle.GetAllAssetNames())
            {
                if (!potentialRingtone.ToLower().EndsWith("_ringtone.wav") && !potentialRingtone.ToLower().EndsWith("_ringtone.mp3"))
                {
                    continue;
                }
                AudioClip customization = bundle.LoadAsset<AudioClip>(potentialRingtone);

                string customizationId = (bundleName ?? "") + "." + customization.name.ToLower().Replace("_ringtone", "");

                if (ringtoneCustomizations.ContainsKey(customizationId))
                {
                    Debug.Log("Skipped ringtone customization: " + customizationId + ", reason: duplicate id");
                    continue;
                }

                Debug.Log("Loaded ringtone customization: " + customizationId);
                ringtoneCustomizations.Add(customizationId, customization);
                ringtoneIds.Add(customizationId);
            }
        }

        public static void SpawnCustomizationGUI()
        {
            if (skinCustomizations.Count == 0 && charmCustomizations.Count == 0) return; // Don't spawn the ui if no cosmetics are loaded

            GameObject customizationGUI = GameObject.Instantiate(PhoneAssetManager.customizationGUI);

            customizationPanel = customizationGUI.transform.Find("CustomizationCanvas").Find("CustomizationPanel");

            displayPhone = customizationPanel.Find("Panel").Find("PhoneDisplayModel").gameObject;
            displayRingtone = customizationPanel.Find("Panel").Find("AudioDisplay").Find("AudioDisplayIcon").gameObject;
            displayRingtoneName = customizationPanel.Find("Panel").Find("AudioDisplay").Find("RingtoneName").gameObject;

            GameObject nextCustomizationButton = customizationPanel.Find("Panel").Find("NextButton").gameObject;
            GameObject prevCustomizationButton = customizationPanel.Find("Panel").Find("PrevButton").gameObject;
            nextCustomizationButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                NextOption();
            });
            prevCustomizationButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                PrevOption();
            });

            GameObject closeButton = customizationPanel.Find("Panel").Find("ResponseButton").gameObject;
            closeButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                SaveCustomization();
            });

            ApplySkinToDisplay(SelectedSkin);
            skinIndex = skinIds.IndexOf(SelectedSkin);

            ApplyRingtoneToDisplay(SelectedRingtone);
            skinIndex = skinIds.IndexOf(SelectedSkin);
        }

        public static void SaveCustomization()
        {
            Plugin.WriteCustomizationToFile();
        }

        public static void NextOption()
        {
            if (displayPhone.activeInHierarchy)
            {
                NextSkin();
            }
            else if (displayRingtone.activeInHierarchy)
            {
                NextRingtone();
            }
        }

        public static void PrevOption()
        {
            if (displayPhone.activeInHierarchy)
            {
                PrevSkin();
            }
            else if (displayRingtone.activeInHierarchy)
            {
                PrevRingtone();
            }
        }

        public static void NextSkin()
        {
            skinIndex++;
            if (skinIndex >= skinIds.Count) { skinIndex = 0; }

            SelectedSkin = skinIds[skinIndex];
            ApplySkinToDisplay(skinIds[skinIndex]);
        }

        public static void PrevSkin()
        {
            skinIndex--;
            if (skinIndex < 0) { skinIndex = skinIds.Count - 1; }

            SelectedSkin = skinIds[skinIndex];
            ApplySkinToDisplay(skinIds[skinIndex]);
        }

        public static void NextRingtone()
        {
            ringtoneIndex++;
            if (ringtoneIndex >= ringtoneIds.Count) { ringtoneIndex = 0; }

            SelectedRingtone = ringtoneIds[ringtoneIndex];
            ApplyRingtoneToDisplay(ringtoneIds[ringtoneIndex]);
        }

        public static void PrevRingtone()
        {
            ringtoneIndex--;
            if (ringtoneIndex < 0) { ringtoneIndex = ringtoneIds.Count - 1; }

            SelectedRingtone = ringtoneIds[ringtoneIndex];
            ApplyRingtoneToDisplay(ringtoneIds[ringtoneIndex]);
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

        public static void ApplyRingtoneToDisplay(string ringtoneId)
        {
            AudioClip ringtoneObject = ringtoneCustomizations[ringtoneId];
            if (ringtoneObject == null) return;

            displayRingtone.GetComponent<AudioSource>().clip = ringtoneObject;
            displayRingtoneName.GetComponent<TextMeshProUGUI>().text = "[ " + ringtoneObject.name + " ]";
        }
    }
}
