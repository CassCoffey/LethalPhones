using HarmonyLib;
using Scoops.service;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Scoops.patch
{
    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPhonePatch
    {
        private static float updateInterval;

        private static List<AudioSource> sortedSources = new List<AudioSource>();

        [HarmonyPatch("ReviveDeadPlayers")]
        [HarmonyPostfix]
        private static void ResetPhones(ref StartOfRound __instance)
        {
            PhoneNetworkHandler.Instance.localPhone.ToggleActive(false);
            PhoneNetworkHandler.Instance.localPhone.SetPhoneModelActive(false);
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        private static void Update(ref StartOfRound __instance)
        {
            if (updateInterval >= 0f)
            {
                updateInterval -= Time.deltaTime;
                return;
            }
            updateInterval = 1f;

            AudioSource[] allAudioSources = GameObject.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sortedSources = new List<AudioSource>();

            for (int i = 0; i < allAudioSources.Length; i++)
            {
                if (allAudioSources[i].spatialBlend != 0f)
                {
                    if (allAudioSources[i].outputAudioMixerGroup && allAudioSources[i].outputAudioMixerGroup.audioMixer.name != "NonDiagetic")
                    {
                        sortedSources.Add(allAudioSources[i]);
                    }
                }
            }
        }

        public static List<AudioSource> GetAllAudioSourcesInRange(Vector3 position)
        {
            List<AudioSource> closeSources = new List<AudioSource>();

            for (int i = 0; i < sortedSources.Count; i++)
            {
                float dist = Vector3.Distance(position, sortedSources[i].transform.position);
                if (dist < sortedSources[i].maxDistance)
                {
                    closeSources.Add(sortedSources[i]);
                }
            }

            return closeSources;
        }
    }
}
