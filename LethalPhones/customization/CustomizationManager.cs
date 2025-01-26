using Scoops.service;
using System;
using System.Collections.Generic;
using System.IO;
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
        public const string DEFAULT_CHARM = "lethalphones.customizations.default";
        public const string DEFAULT_RINGTONE = "lethalphones.customizations.default";

        public static string SelectedSkin = "lethalphones.customizations.default";
        public static string SelectedCharm = "lethalphones.customizations.default";
        public static string SelectedRingtone = "lethalphones.customizations.default";

        public static Dictionary<string, GameObject> skinCustomizations = new Dictionary<string, GameObject>();
        public static List<string> skinIds = new List<string>();

        public static Dictionary<string, GameObject> charmCustomizations = new Dictionary<string, GameObject>();
        public static List<string> charmIds = new List<string>();

        public static Dictionary<string, AudioClip> ringtoneCustomizations = new Dictionary<string, AudioClip>();
        public static List<string> ringtoneIds = new List<string>();

        public static List<string> customizationBlacklist = new List<string>();

        private static Transform customizationPanel;

        private static GameObject displayPhone;
        private static GameObject displayRingtone;
        private static GameObject displayRingtoneName;
        private static Transform displayCharmPoint;
        private static GameObject displayNoCharm;

        private static int skinIndex = 0;
        private static int charmIndex = 0;
        private static int ringtoneIndex = 0;

        public static void RecursiveCustomizationLoad(string directory)
        {
            foreach (var subDirectory in Directory.GetDirectories(directory))
            {
                RecursiveCustomizationLoad(subDirectory);
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                if (file.EndsWith(".phoneCustom"))
                {
                    AssetBundle bundle = AssetBundle.LoadFromFile(file);
                    LoadSkinCustomizations(bundle, bundle.name);
                    LoadCharmCustomizations(bundle, bundle.name);
                    LoadRingtoneCustomizations(bundle, bundle.name);
                }
            }
        }

        public static void LoadSkinCustomizations(AssetBundle bundle, string bundleName = null)
        {
            foreach (var potentialPrefab in bundle.GetAllAssetNames())
            {
                if (!potentialPrefab.ToLower().EndsWith("_skin.prefab"))
                {
                    continue;
                }
                if (Config.removeBaseSkins.Value && bundleName == "lethalphones.customizations" && !potentialPrefab.ToLower().EndsWith("default_skin.prefab"))
                {
                    continue;
                }

                GameObject customization = bundle.LoadAsset<GameObject>(potentialPrefab);

                string customizationId = (bundleName ?? "") + "." + customization.name.ToLower().Replace("_skin", "");

                if (customizationBlacklist.Contains(customizationId))
                {
                    Plugin.Log.LogInfo("Skipped skin customization: " + customizationId + ", reason: blacklisted");
                    continue;
                }

                if (skinCustomizations.ContainsKey(customizationId))
                {
                    Plugin.Log.LogWarning("Skipped skin customization: " + customizationId + ", reason: duplicate id");
                    continue;
                }

                Plugin.Log.LogInfo("Loaded skin customization: " + customizationId);
                skinCustomizations.Add(customizationId, customization);
                skinIds.Add(customizationId);
            }
        }

        public static void LoadCharmCustomizations(AssetBundle bundle, string bundleName = null)
        {
            foreach (var potentialPrefab in bundle.GetAllAssetNames())
            {
                if (!potentialPrefab.ToLower().EndsWith("_charm.prefab"))
                {
                    continue;
                }
                if (Config.removeBaseCharms.Value && bundleName == "lethalphones.customizations" && !potentialPrefab.ToLower().EndsWith("default_charm.prefab"))
                {
                    continue;
                }

                GameObject customization = bundle.LoadAsset<GameObject>(potentialPrefab);

                string customizationId = (bundleName ?? "") + "." + customization.name.ToLower().Replace("_charm", "");

                if (customizationBlacklist.Contains(customizationId))
                {
                    Plugin.Log.LogInfo("Skipped charm customization: " + customizationId + ", reason: blacklisted");
                    continue;
                }

                if (charmCustomizations.ContainsKey(customizationId))
                {
                    Plugin.Log.LogWarning("Skipped charm customization: " + customizationId + ", reason: duplicate id");
                    continue;
                }

                Plugin.Log.LogInfo("Loaded charm customization: " + customizationId);
                charmCustomizations.Add(customizationId, customization);
                charmIds.Add(customizationId);
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
                if (Config.removeBaseRingtones.Value && bundleName == "lethalphones.customizations" && !potentialRingtone.ToLower().EndsWith("default_ringtone.wav"))
                {
                    continue;
                }

                AudioClip customization = bundle.LoadAsset<AudioClip>(potentialRingtone);

                string customizationId = (bundleName ?? "") + "." + customization.name.ToLower().Replace("_ringtone", "");

                if (customizationBlacklist.Contains(customizationId))
                {
                    Plugin.Log.LogInfo("Skipped ringtone customization: " + customizationId + ", reason: blacklisted");
                    continue;
                }

                if (ringtoneCustomizations.ContainsKey(customizationId))
                {
                    Plugin.Log.LogWarning("Skipped ringtone customization: " + customizationId + ", reason: duplicate id");
                    continue;
                }

                Plugin.Log.LogInfo("Loaded ringtone customization: " + customizationId);
                ringtoneCustomizations.Add(customizationId, customization);
                ringtoneIds.Add(customizationId);
            }
        }

        public static void ActivateCustomizationPanel()
        {
            customizationPanel.gameObject.SetActive(true);
        }

        public static void SpawnCustomizationGUI()
        {
            if (skinCustomizations.Count == 0 && charmCustomizations.Count == 0) return; // Don't spawn the ui if no cosmetics are loaded

            GameObject customizationGUI = GameObject.Instantiate(PhoneAssetManager.customizationGUI);

            customizationPanel = customizationGUI.transform.Find("CustomizationCanvas").Find("CustomizationPanel");

            displayPhone = customizationPanel.Find("Panel").Find("PhoneDisplayModel").gameObject;
            displayRingtone = customizationPanel.Find("Panel").Find("AudioDisplay").Find("AudioDisplayIcon").gameObject;
            displayRingtoneName = customizationPanel.Find("Panel").Find("AudioDisplay").Find("RingtoneName").gameObject;
            displayCharmPoint = customizationPanel.Find("Panel").Find("CharmDisplay").Find("CharmRotatePoint").Find("CharmDisplayModel");
            displayNoCharm = customizationPanel.Find("Panel").Find("CharmDisplay").Find("NoCharm").gameObject;

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

            ApplyCharmToDisplay(SelectedCharm);
            charmIndex = charmIds.IndexOf(SelectedCharm);

            ApplyRingtoneToDisplay(SelectedRingtone);
            ringtoneIndex = ringtoneIds.IndexOf(SelectedRingtone);
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
            else if (displayCharmPoint.gameObject.activeInHierarchy)
            {
                NextCharm();
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
            else if (displayCharmPoint.gameObject.activeInHierarchy)
            {
                PrevCharm();
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

        public static void NextCharm()
        {
            charmIndex++;
            if (charmIndex >= charmIds.Count) { charmIndex = 0; }

            SelectedCharm = charmIds[charmIndex];
            ApplyCharmToDisplay(charmIds[charmIndex]);
        }

        public static void PrevCharm()
        {
            charmIndex--;
            if (charmIndex < 0) { charmIndex = charmIds.Count - 1; }

            SelectedCharm = charmIds[charmIndex];
            ApplyCharmToDisplay(charmIds[charmIndex]);
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

        public static void ApplyCharmToDisplay(string charmId)
        {
            GameObject charmPrefab = charmCustomizations[charmId];
            if (charmPrefab == null) return;

            if (displayCharmPoint.childCount > 0)
            {
                GameObject.Destroy(displayCharmPoint.GetChild(0).gameObject);
            }

            GameObject charm = GameObject.Instantiate(charmPrefab, displayCharmPoint);

            charm.layer = 31;

            var children = charm.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var child in children)
            {
                child.gameObject.layer = 31;
            }

            displayNoCharm.SetActive(charmId == DEFAULT_CHARM);
        }

        public static void ApplyRingtoneToDisplay(string ringtoneId)
        {
            AudioClip ringtoneObject = ringtoneCustomizations[ringtoneId];
            if (ringtoneObject == null) return;

            displayRingtone.GetComponent<AudioSource>().clip = ringtoneObject;
            displayRingtoneName.GetComponent<TextMeshProUGUI>().text = "[ " + ringtoneObject.name.Replace("_Ringtone", "").Replace("_ringtone", "") + " ]";
        }
    }
}
