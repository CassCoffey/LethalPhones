using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.service
{
    public class AudioSourceStorage
    {
        public AudioSource audioSource;
        private GameObject audioSourceHolder;
        private float origVolume;
        private float origPan;
        private bool hadLowPass;
        private bool hadHighPass;
        private bool hadOcclude;
        private float origLowPass;
        private float origLowPassResQ;
        private float origHighPass;
        private float origHighPassResQ;

        public AudioSourceStorage(AudioSource audioSource)
        {
            this.audioSource = audioSource;
            this.audioSourceHolder = audioSource.gameObject;
            this.origVolume = audioSource.volume;
            this.origPan = audioSource.panStereo;
            this.hadLowPass = audioSourceHolder.GetComponent<AudioLowPassFilter>() != null;
            this.hadHighPass = audioSourceHolder.GetComponent<AudioHighPassFilter>() != null;

            this.hadOcclude = audioSourceHolder.GetComponent<OccludeAudio>() != null;

            if (hadLowPass)
            {
                origLowPass = audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency;
                origLowPassResQ = audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ;
            }
            if (hadHighPass)
            {
                origHighPass = audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency;
                origHighPassResQ = audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ;
            }
        }

        public void InitAudio()
        {
            if (audioSource != null)
            {
                audioSource.spatialBlend = 0f;
                audioSource.panStereo = -0.4f;

                if (audioSourceHolder != null)
                {
                    if (!hadLowPass)
                    {
                        audioSourceHolder.AddComponent<AudioLowPassFilter>();
                    }
                    if (!hadHighPass)
                    {
                        audioSourceHolder.AddComponent<AudioHighPassFilter>();
                    }

                    if (hadOcclude)
                    {
                        audioSourceHolder.GetComponent<OccludeAudio>().enabled = false;
                    }

                    audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = 2899f;
                    audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 3f;
                    audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency = 1613f;
                    audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ = 1f;
                }
            }
        }

        public void ApplyPhone(float dist, float callQuality = 1f, float listenDist = 0f, float listenAngle = 0f, bool staticMode = false)
        {
            if (audioSourceHolder != null && audioSource != null)
            {
                float mod = 0f;

                if (audioSource.rolloffMode == AudioRolloffMode.Linear)
                {
                    mod = Mathf.Clamp01(Mathf.InverseLerp(audioSource.maxDistance, audioSource.minDistance, dist) + Config.backgroundSoundMod.Value);
                }
                else if (audioSource.rolloffMode == AudioRolloffMode.Custom)
                {
                    AnimationCurve audioRolloffCurve = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                    if (audioRolloffCurve != null)
                    {
                        mod = Mathf.Clamp01(audioRolloffCurve.Evaluate(dist / audioSource.maxDistance) + Config.backgroundSoundMod.Value);
                    }
                }
                else
                {
                    mod = Mathf.Clamp01((audioSource.minDistance * (1 / (1 + (dist - 1)))) + Config.backgroundSoundMod.Value);
                }

                audioSource.volume = origVolume * mod;

                if (staticMode)
                {
                    audioSource.volume = 0f;
                }

                if (audioSourceHolder.GetComponent<AudioLowPassFilter>())
                {
                    audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(2000f, 2899f, callQuality);
                    audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = Mathf.Lerp(5f, 3f, callQuality);
                }
                if (audioSourceHolder.GetComponent<AudioHighPassFilter>())
                {
                    audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ = Mathf.Lerp(2f, 1f, callQuality);
                }

                if (listenDist != 0f)
                {
                    float listenMod = Mathf.InverseLerp(Config.eavesdropDist.Value, 0f, listenDist);
                    audioSource.volume = audioSource.volume * listenMod;
                    if (audioSourceHolder.GetComponent<AudioLowPassFilter>()) audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = 750f;
                    audioSource.panStereo = listenAngle;
                }
            }
        }

        public void Reset()
        {
            if (audioSourceHolder != null)
            {
                if (audioSource != null)
                {
                    audioSource.spatialBlend = 1f;
                    audioSource.panStereo = origPan;
                    audioSource.volume = origVolume;
                }

                if (hadOcclude && audioSourceHolder.GetComponent<OccludeAudio>())
                {
                    audioSourceHolder.GetComponent<OccludeAudio>().enabled = true;
                }

                if (audioSourceHolder.GetComponent<AudioLowPassFilter>())
                {
                    if (hadLowPass)
                    {
                        audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = origLowPass;
                        audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = origLowPassResQ;
                    }
                    else
                    {
                        GameObject.Destroy(audioSourceHolder.GetComponent<AudioLowPassFilter>());
                    }

                }

                if (audioSourceHolder.GetComponent<AudioHighPassFilter>())
                {
                    if (hadHighPass)
                    {
                        audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency = origHighPass;
                        audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ = origHighPassResQ;
                    }
                    else
                    {
                        GameObject.Destroy(audioSourceHolder.GetComponent<AudioHighPassFilter>());
                    }
                }
            }
        }
    }

    // This script handles updating the manager on what audio sources exist
    // We want it to be the last thing that runs on Start, ideally
    [DefaultExecutionOrder(int.MaxValue)]
    public class AudioSourceHook : MonoBehaviour
    {
        AudioSourceStorage storage;

        public void Start()
        {
            storage = AudioSourceManager.RegisterAudioSource(GetComponent<AudioSource>());
        }

        public void OnDestroy()
        {
            AudioSourceManager.DeregisterAudioSource(storage);
        }
    }

    [HarmonyPatch]
    public class AudioSourceManager : MonoBehaviour
    {
        public static AudioSourceManager Instance { get; private set; }

        private List<AudioSourceStorage> allAudioSources = new List<AudioSourceStorage>();
        private List<AudioSourceStorage> spatialAudioSources = new List<AudioSourceStorage>();

        public void Start()
        {
            // Add a hook script to every audio source in the game
            AudioSource[] loadedAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>();

            foreach (AudioSource source in loadedAudioSources)
            {
                source.gameObject.AddComponent<AudioSourceHook>();
            }
        }

        public static AudioSourceStorage RegisterAudioSource(AudioSource source)
        {
            if (Instance == null) return null;

            AudioSourceStorage newStorage = new AudioSourceStorage(source);

            Instance.allAudioSources.Add(newStorage);
            if (source.spatialBlend != 0f)
            {
                Instance.spatialAudioSources.Add(newStorage);
            }

            return newStorage;
        }

        public static void DeregisterAudioSource(AudioSourceStorage source)
        {
            if (Instance == null || source == null) return;

            Instance.allAudioSources.Remove(source);
            Instance.spatialAudioSources.Remove(source);
        }

        public List<AudioSource> GetAllAudioSourcesInRange(Vector3 position)
        {
            List<AudioSource> closeSources = new List<AudioSource>();
            PlayerControllerB localPlayer = StartOfRound.Instance.localPlayerController;
            if (localPlayer.isPlayerDead)
            {
                localPlayer = localPlayer.spectatedPlayerScript;
            }

            if (localPlayer != null && spatialAudioSources.Count > 0)
            {
                for (int i = 0; i < spatialAudioSources.Count; i++)
                {
                    if (spatialAudioSources[i] != null)
                    {
                        AudioSource source = spatialAudioSources[i].audioSource;
                        float dist = (position - source.transform.position).sqrMagnitude;
                        float localDist = (localPlayer.transform.position - source.transform.position).sqrMagnitude;
                        float localToOtherDist = (localPlayer.transform.position - position).sqrMagnitude;
                        if (localToOtherDist > (Config.recordingStartDist.Value * Config.recordingStartDist.Value) && dist < (source.maxDistance * source.maxDistance) && dist < localDist)
                        {
                            closeSources.Add(source);
                        }
                    }
                }
            }

            return closeSources;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        static void SpawnAudioSourceManager()
        {
            if (Instance == null)
            {
                GameObject audioSourceManagerObject = new GameObject("PhoneAudioSourceManager");
                AudioSourceManager manager = audioSourceManagerObject.AddComponent<AudioSourceManager>();

                Instance = manager;
            }
        }
    }
}
