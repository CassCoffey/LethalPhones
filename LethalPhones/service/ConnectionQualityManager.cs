using HarmonyLib;
using Scoops.compatability;
using Scoops.misc;
using Scoops.patch;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.service
{
    public class ConnectionModifier : MonoBehaviour
    {
        public float range = 50f;
        public float interferenceMod = 0.5f;

        private RadarBoosterItem radarBooster;

        public void Start()
        {
            ConnectionQualityManager.RegisterConnectionModifier(this);

            radarBooster = GetComponent<RadarBoosterItem>();
        }

        public void Update()
        {
            if (radarBooster)
            {
                if (radarBooster.radarEnabled)
                {
                    interferenceMod = -1f;
                }
                else
                {
                    interferenceMod = 0f;
                }
            }
        }

        public void OnDestroy()
        {
            ConnectionQualityManager.DeregisterConnectionModifier(this);
        }
    }

    [HarmonyPatch]
    public class ConnectionQualityManager : NetworkBehaviour
    {
        public static ConnectionQualityManager Instance { get; private set; }

        public static float AtmosphericInterference = 0f;

        public const float STATIC_START_INTERFERENCE = 0.3f;

        private const float MAX_ENTRANCE_DIST = 200f * 200f;

        private EntranceTeleport[] entranceArray = new EntranceTeleport[0];
        private List<ConnectionModifier> connectionModifiers = new List<ConnectionModifier>();

        private static LevelWeatherType[] badWeathers = { LevelWeatherType.Flooded, LevelWeatherType.Foggy, LevelWeatherType.DustClouds, LevelWeatherType.Eclipsed };
        private static LevelWeatherType[] worseWeathers = { LevelWeatherType.Rainy };
        private static LevelWeatherType[] worstWeathers = { LevelWeatherType.Stormy };

        private static string[] registryBadWeathers = { "flooded", "foggy", "dust clouds", "heatwave", "eclipsed" };
        private static string[] registryWorseWeathers = { "rainy", "snowfall", "toxic smog" };
        private static string[] registryWorstWeathers = { "stormy", "blizzard", "solar flare" };

        private Coroutine atmosphericInterferenceCoroutine;

        public void Start()
        {
            if (Instance == null) Instance = this;

            StartOfRound.Instance.StartNewRoundEvent.AddListener(NewRoundStart);

            // Add a connection modifier script to every Apparatus
            LungProp[] loadedApparatus = Resources.FindObjectsOfTypeAll<LungProp>();

            foreach (LungProp apparatus in loadedApparatus)
            {
                if (apparatus.GetComponent<ConnectionModifier>() == null)
                {
                    ConnectionModifier modifier = apparatus.gameObject.AddComponent<ConnectionModifier>();
                    modifier.interferenceMod = 0.5f;
                    modifier.range = Config.apparatusRange.Value;
                }
            }

            // Add a connection modifier script to every Radar Booster
            RadarBoosterItem[] loadedBooster = Resources.FindObjectsOfTypeAll<RadarBoosterItem>();

            foreach (RadarBoosterItem booster in loadedBooster)
            {
                if (booster.GetComponent<ConnectionModifier>() == null)
                {
                    ConnectionModifier modifier = booster.gameObject.AddComponent<ConnectionModifier>();
                    modifier.interferenceMod = -1f;
                    modifier.range = Config.radarBoosterRange.Value;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                atmosphericInterferenceCoroutine = StartCoroutine(ManageAtmosphericInterference());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                if (atmosphericInterferenceCoroutine != null)
                {
                    StopCoroutine(atmosphericInterferenceCoroutine);
                }
            }
        }

        public static void RegisterConnectionModifier(ConnectionModifier modifier)
        {
            if (Instance != null)
            {
                if (Instance.connectionModifiers == null) Instance.connectionModifiers = new List<ConnectionModifier>();

                Instance.connectionModifiers.Add(modifier);
            }
        }

        public static void DeregisterConnectionModifier(ConnectionModifier modifier)
        {
            if (Instance != null)
            {
                if (Instance.connectionModifiers == null) Instance.connectionModifiers = new List<ConnectionModifier>();

                Instance.connectionModifiers.Remove(modifier);
            }
        }

        public static float GetLocalInterference(PhoneBehavior phone)
        {
            float interference = 0f;

            if (Instance != null)
            {
                if (phone.PhoneInsideFactory())
                {
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

                float totalModifierInterference = 0f;

                foreach (ConnectionModifier modifier in Instance.connectionModifiers)
                {
                    float maxModifierDist = modifier.range * modifier.range;
                    float modifierDist = (modifier.transform.position - phone.recordPos.position).sqrMagnitude;

                    if (modifierDist <= maxModifierDist)
                    {
                        totalModifierInterference += modifier.interferenceMod * (1f - (modifierDist / maxModifierDist));
                    }
                }
                
                interference += totalModifierInterference;
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
                    string currWeather;
                    if (WeatherTweaksCompat.Enabled)
                    {
                        currWeather = WeatherTweaksCompat.CurrentWeatherName().ToLower();
                    } 
                    else
                    {
                        currWeather = WeatherRegistryCompat.CurrentWeatherName().ToLower();
                    }

                    foreach (string weather in registryWorstWeathers)
                    {
                        if (currWeather.Contains(weather))
                        {
                            interference = 0.5f;
                        }
                    }
                    if (interference == 0f)
                    {
                        foreach (string weather in registryWorseWeathers)
                        {
                            if (currWeather.Contains(weather))
                            {
                                interference = 0.35f;
                            }
                        }
                    }
                    if (interference == 0f)
                    {
                        foreach (string weather in registryBadWeathers)
                        {
                            if (currWeather.Contains(weather))
                            {
                                interference = 0.2f;
                            }
                        }
                    }
                }
                else if (TimeOfDay.Instance != null)
                {
                    if (badWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
                    {
                        interference = 0.2f;
                    }
                    if (worseWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
                    {
                        interference = 0.35f;
                    }
                    if (worstWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
                    {
                        interference = 0.5f;
                    }
                }

                // Variance increases based on interference
                float variance = UnityEngine.Random.Range(0f, interference) - (interference/2);

                UpdateAtmosphericInterferenceClientRpc(interference + variance);
                
                // Now we generate a delay until the next atmospheric change
                float delay = UnityEngine.Random.Range(1.5f, 6f);

                yield return new WaitForSeconds(delay);
            }
        }

        [ClientRpc]
        public void UpdateAtmosphericInterferenceClientRpc(float interference)
        {
            AtmosphericInterference = interference;
        }

        public static void NewRoundStart()
        {
            if (Instance != null)
            {
                Instance.entranceArray = UnityEngine.Object.FindObjectsByType<EntranceTeleport>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        static void SpawnConnectionQualityManager(ref StartOfRound __instance)
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                if (Instance == null)
                {
                    GameObject connectionQualityManagerObject = GameObject.Instantiate(NetworkObjectManager.connectionManagerPrefab, Vector3.zero, Quaternion.identity);
                    ConnectionQualityManager manager = connectionQualityManagerObject.GetComponent<ConnectionQualityManager>();
                    connectionQualityManagerObject.GetComponent<NetworkObject>().Spawn();

                    Instance = manager;
                }
            }
        }
    }
}
