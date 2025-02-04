using HarmonyLib;
using Scoops.compatability;
using Scoops.misc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Scoops.service
{
    [HarmonyPatch]
    public class ConnectionQualityManager : MonoBehaviour
    {
        public static ConnectionQualityManager Instance { get; private set; }

        public static float AtmosphericInterference = 0f;

        public const float STATIC_START_INTERFERENCE = 0.4f;

        private const float MAX_ENTRANCE_DIST = 300f * 300f;
        private const float MAX_APPARATUS_DIST = 50f * 50f;

        private EntranceTeleport[] entranceArray = new EntranceTeleport[0];
        private LungProp[] apparatusArray = new LungProp[0];

        private static LevelWeatherType[] badWeathers = { LevelWeatherType.Flooded, LevelWeatherType.Rainy, LevelWeatherType.Foggy, LevelWeatherType.DustClouds };
        private static LevelWeatherType[] worseWeathers = { LevelWeatherType.Stormy };

        private static string[] registryBadWeathers = { "flooded", "rainy", "foggy", "dust clouds", "heatwave", "snowfall" };
        private static string[] registryWorseWeathers = { "stormy", "blizzard", "toxic smog", "solar flare" };

        private Coroutine atmosphericInterferenceCoroutine;

        public void Start()
        {
            atmosphericInterferenceCoroutine = StartCoroutine(ManageAtmosphericInterference());
        }

        public void OnDestroy()
        {
            if (atmosphericInterferenceCoroutine != null)
            {
                StopCoroutine(atmosphericInterferenceCoroutine);
            }
        }

        public static float GetLocalInterference(PhoneBehavior phone)
        {
            float interference = 0f;

            if (Instance != null)
            {
                if (phone.PhoneInsideFactory())
                {
                    interference += 0.1f;

                    if (Instance.entranceArray.Length > 0)
                    {
                        float entranceDist = MAX_ENTRANCE_DIST;

                        foreach (EntranceTeleport entrance in Instance.entranceArray)
                        {
                            if (entrance != null)
                            {
                                if (!entrance.isEntranceToBuilding)
                                {
                                    float newDist = (entrance.transform.position - phone.recordPos.position).sqrMagnitude;
                                    if (newDist < entranceDist)
                                    {
                                        entranceDist = newDist;
                                    }
                                }
                            }
                        }

                        interference += Mathf.Lerp(0f, 0.4f, Mathf.InverseLerp(0f, MAX_ENTRANCE_DIST, entranceDist));
                    }
                }

                if (Instance.apparatusArray.Length > 0)
                {
                    float apparatusDist = MAX_APPARATUS_DIST;
                    bool apparatusFound = false;

                    foreach (LungProp apparatus in Instance.apparatusArray)
                    {
                        if (apparatus != null)
                        {
                            float newDist = (apparatus.transform.position - phone.recordPos.position).sqrMagnitude;
                            if (apparatus.isLungDocked)
                            {
                                newDist += 10f * 10f;
                            }
                            if (newDist < apparatusDist)
                            {
                                apparatusDist = newDist;
                                apparatusFound = true;
                            }
                        }
                    }

                    if (apparatusFound)
                    {
                        interference += Mathf.Lerp(0.5f, 0f, Mathf.InverseLerp(0f, MAX_APPARATUS_DIST, apparatusDist));
                    }
                }
            }

            return interference;
        }

        public IEnumerator ManageAtmosphericInterference()
        {
            while (true)
            {
                float interference = 0f;

                if (WeatherRegistryCompat.Enabled)
                {
                    string currWeather = WeatherRegistryCompat.CurrentWeatherName().ToLower();
                    if (registryBadWeathers.Contains(currWeather))
                    {
                        interference += 0.25f;
                    }
                    if (registryWorseWeathers.Contains(currWeather))
                    {
                        interference += 0.5f;
                    }
                }
                else
                {
                    if (badWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
                    {
                        interference += 0.25f;
                    }
                    if (worseWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
                    {
                        interference += 0.5f;
                    }
                }

                // Variance increases based on interference
                float variance = UnityEngine.Random.Range(0f, interference) - (interference/2);

                AtmosphericInterference = Mathf.Clamp01(interference + variance);
                
                // Now we generate a delay until the next atmospheric change
                float delay = UnityEngine.Random.Range(0.75f, 3f);

                yield return new WaitForSeconds(delay);
            }
        }

        public static void NewRoundStart()
        {
            if (Instance != null)
            {
                Instance.entranceArray = UnityEngine.Object.FindObjectsByType<EntranceTeleport>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                Instance.apparatusArray = UnityEngine.Object.FindObjectsByType<LungProp>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        static void SpawnConnectionQualityManager(StartOfRound __instance)
        {
            if (Instance == null)
            {
                GameObject connectionQualityManagerObject = new GameObject("PhoneConnectionQualityManager");
                ConnectionQualityManager manager = connectionQualityManagerObject.AddComponent<ConnectionQualityManager>();

                Instance = manager;

                __instance.StartNewRoundEvent.AddListener(NewRoundStart);
            }
        }
    }
}
