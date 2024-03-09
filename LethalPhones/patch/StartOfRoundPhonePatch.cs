using GameNetcodeStuff;
using HarmonyLib;
using Scoops.misc;
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
            PlayerPhone[] allPhones = GameObject.FindObjectsByType<PlayerPhone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < allPhones.Length; i++)
            {
                allPhones[i].SetPhoneLocalModelActive(false);
                allPhones[i].SetPhoneServerModelActive(false);
            }

            PhoneNetworkHandler.Instance.localPhone.ToggleActive(false);
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

            AudioSource[] allAudioSources = GameObject.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sortedSources = new List<AudioSource>();

            for (int i = 0; i < allAudioSources.Length; i++)
            {
                if (allAudioSources[i].spatialBlend != 0f)
                {
                    sortedSources.Add(allAudioSources[i]);
                }
            }
        }

        public static List<AudioSource> GetAllAudioSourcesInRange(Vector3 position)
        {
            List<AudioSource> closeSources = new List<AudioSource>();
            PlayerControllerB localPlayer = StartOfRound.Instance.localPlayerController;

            if (localPlayer)
            {
                for (int i = 0; i < sortedSources.Count; i++)
                {
                    float dist = Vector3.Distance(position, sortedSources[i].transform.position);
                    float localDist = Vector3.Distance(localPlayer.transform.position, sortedSources[i].transform.position);
                    float localToOtherDist = Vector3.Distance(localPlayer.transform.position, position);
                    if (localToOtherDist > PlayerPhone.RECORDING_START_DIST && dist < sortedSources[i].maxDistance && dist < localDist)
                    {
                        closeSources.Add(sortedSources[i]);
                    }
                }
            }

            return closeSources;
        }
    }
}
