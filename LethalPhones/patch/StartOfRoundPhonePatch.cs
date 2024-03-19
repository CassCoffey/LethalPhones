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

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(ShipBuildModeManager.CreateGhostObjectAndHighlight))]
        [HarmonyPostfix]
        private static void CreateGhostObjectAndHighlight(ref ShipBuildModeManager __instance)
        {
            Plugin.Log.LogInfo("Ghost Rendering: " + __instance.ghostObjectRenderer.enabled);
            Plugin.Log.LogInfo("Ghost Object mesh: " + __instance.ghostObjectMesh.mesh);
            Plugin.Log.LogInfo("Selection Outline mesh: " + __instance.selectionOutlineMesh.mesh);
            Plugin.Log.LogInfo("mainMesh Euler: " + __instance.placingObject.mainMesh.transform.eulerAngles);
            Plugin.Log.LogInfo("mainMesh Scale: " + __instance.placingObject.mainMesh.transform.localScale);
            Plugin.Log.LogInfo("mainMesh Position: " + __instance.placingObject.mainMesh.transform.position);
        }

        [HarmonyPatch("ReviveDeadPlayers")]
        [HarmonyPostfix]
        private static void ResetPhones(ref StartOfRound __instance)
        {
            PlayerPhone[] allPhones = GameObject.FindObjectsByType<PlayerPhone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < allPhones.Length; i++)
            {
                allPhones[i].Revive();
            }

            PhoneNetworkHandler.Instance.localPhone.ToggleActive(false);
        }

        [HarmonyPatch("OnPlayerDC")]
        [HarmonyPrefix]
        private static void CleanupPlayerPhone(ref StartOfRound __instance, int playerObjectNumber, ulong clientId)
        {
            PhoneNetworkHandler.Instance.DeletePlayerPhone(playerObjectNumber);
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
            if (localPlayer.isPlayerDead)
            {
                localPlayer = localPlayer.spectatedPlayerScript;
            }

            if (localPlayer != null && sortedSources.Count > 0)
            {
                for (int i = 0; i < sortedSources.Count; i++)
                {
                    if (sortedSources[i])
                    {
                        float dist = (position - sortedSources[i].transform.position).sqrMagnitude;
                        float localDist = (localPlayer.transform.position - sortedSources[i].transform.position).sqrMagnitude;
                        float localToOtherDist = (localPlayer.transform.position - position).sqrMagnitude;
                        if (localToOtherDist > (Config.recordingStartDist.Value * Config.recordingStartDist.Value) && dist < (sortedSources[i].maxDistance * sortedSources[i].maxDistance) && dist < localDist)
                        {
                            closeSources.Add(sortedSources[i]);
                        }
                    }
                }
            }

            return closeSources;
        }
    }
}
