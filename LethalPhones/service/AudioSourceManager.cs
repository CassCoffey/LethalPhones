using GameNetcodeStuff;
using HarmonyLib;
using Scoops.misc;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Scoops.service
{
    public class AudioSourceStorage
    {
        public bool modified = false;
        public bool voice = false;

        public PlayerControllerB player = null;

        public Transform recordPos;
        public Transform playPos;
        public Transform listenerPos;
        public Transform sourcePos;

        public float recordConnectionQuality;
        public bool recordStaticMode;

        public AudioSource audioSource;

        private float origVolume;
        private float origSpatial;
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

            this.recordPos = null;
            this.playPos = null;
            this.listenerPos = null;
            this.sourcePos = audioSource.transform;

            this.recordConnectionQuality = 1f;
            this.recordStaticMode = false;

            this.origVolume = audioSource.volume;
            this.origSpatial = audioSource.spatialBlend;
            this.origPan = audioSource.panStereo;

            this.hadLowPass = audioSource.GetComponent<AudioLowPassFilter>() != null;
            this.hadHighPass = audioSource.GetComponent<AudioHighPassFilter>() != null;
            this.hadOcclude = audioSource.GetComponent<OccludeAudio>() != null;

            if (hadLowPass)
            {
                origLowPass = audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency;
                origLowPassResQ = audioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ;
            } 
            else
            {
                audioSource.gameObject.AddComponent<AudioLowPassFilter>().enabled = false;
            }

            if (hadHighPass)
            {
                origHighPass = audioSource.GetComponent<AudioHighPassFilter>().cutoffFrequency;
                origHighPassResQ = audioSource.GetComponent<AudioHighPassFilter>().highpassResonanceQ;
            } 
            else
            {
                audioSource.gameObject.AddComponent<AudioHighPassFilter>().enabled = false;
            }
        }

        public void Update()
        {
            if (audioSource != null)
            {
                if (recordPos != null && playPos != null && listenerPos != null)
                {
                    if (!modified) InitPhone();
                    ApplyPhone();
                }
                else if (modified)
                {
                    Reset();
                }
            }
        }

        public void InitPhone()
        {
            modified = true;

            // Remove spatialization so that the audio can be heard from anywhere
            audioSource.spatialBlend = 0f;

            if (audioSource.GetComponent<AudioLowPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().enabled = true;
            }
            if (audioSource.GetComponent<AudioHighPassFilter>())
            {
                audioSource.GetComponent<AudioHighPassFilter>().enabled = true;
            }
            if (audioSource.GetComponent<OccludeAudio>())
            {
                audioSource.GetComponent<OccludeAudio>().enabled = false;
            }
        }

        public void ApplyPhone()
        {
            if (recordStaticMode)
            {
                // No audio if playing static
                audioSource.volume = 0f;
                return;
            }

            // Apply Voice Audio Source specific changes
            if (voice)
            {
                player.currentVoiceChatIngameSettings.set2D = true;
            }

            float mod = 0f;

            float recordDist = Vector3.Distance(recordPos.position, sourcePos.position);

            Vector3 directionTo = playPos.position - listenerPos.position;
            float listenDist = directionTo.sqrMagnitude;
            float listenAngle = Vector3.Dot(directionTo.normalized, listenerPos.right);

            float maxListenDistSqr = Config.listeningDist.Value;
            maxListenDistSqr *= maxListenDistSqr;

            // Recalculate volume from distance information
            if (audioSource.rolloffMode == AudioRolloffMode.Linear)
            {
                mod = Mathf.Clamp01(Mathf.InverseLerp(audioSource.maxDistance, audioSource.minDistance, recordDist));
            }
            else if (audioSource.rolloffMode == AudioRolloffMode.Custom)
            {
                AnimationCurve audioRolloffCurve = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                if (audioRolloffCurve != null)
                {
                    mod = Mathf.Clamp01(audioRolloffCurve.Evaluate(recordDist / audioSource.maxDistance));
                }
            }
            else
            {
                mod = Mathf.Clamp01((audioSource.minDistance * (1 / (1 + (recordDist - 1)))));
            }

            audioSource.volume = (origVolume * mod);
            // If this is a voice apply the voiceSound config, otherwise apply the backgroundSound config
            audioSource.volume += voice ? Config.voiceSoundAdjust.Value : Config.backgroundSoundAdjust.Value;

            float recordMod = AudioSourceManager.Instance.listenerCurve.Evaluate(recordDist / audioSource.maxDistance);

            if (audioSource.GetComponent<AudioLowPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(3000f, 750f, recordMod);
                audioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = Mathf.Lerp(10f, 3f, recordConnectionQuality);
            }
            if (audioSource.GetComponent<AudioHighPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(2500f, 2000f, recordConnectionQuality);
                audioSource.GetComponent<AudioHighPassFilter>().highpassResonanceQ = Mathf.Lerp(3f, 2f, recordConnectionQuality);
            }

            if (listenDist > 1f)
            {
                float listenMod = AudioSourceManager.Instance.listenerCurve.Evaluate(Mathf.Clamp01((listenDist / maxListenDistSqr) + 0.1f));
                audioSource.volume *= listenMod;
                if (audioSource.GetComponent<AudioLowPassFilter>())
                {
                    audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = 750f;
                }
                audioSource.panStereo = listenAngle;
            }

            if (voice && player.voiceMuffledByEnemy && audioSource.GetComponent<AudioLowPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = 500;
            }
        }

        public void Reset()
        {
            GameObject audioSourceHolder = audioSource.gameObject;

            if (voice)
            {
                player.currentVoiceChatIngameSettings.set2D = false;
            }

            audioSource.panStereo = origPan;
            audioSource.spatialBlend = origSpatial;
            audioSource.volume = origVolume;

            if (hadOcclude && audioSourceHolder.GetComponent<OccludeAudio>())
            {
                audioSourceHolder.GetComponent<OccludeAudio>().enabled = true;

                if (voice)
                {
                    audioSourceHolder.GetComponent<OccludeAudio>().overridingLowPass = player.voiceMuffledByEnemy;
                }
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
                    audioSourceHolder.GetComponent<AudioLowPassFilter>().enabled = false;
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
                    audioSourceHolder.GetComponent<AudioHighPassFilter>().enabled = false;
                }
            }

            modified = false;
        }
    }

    // This script handles updating the manager on what audio sources exist
    // We want it to be the last thing that runs on Start, ideally
    [DefaultExecutionOrder(int.MaxValue)]
    public class AudioSourceHook : MonoBehaviour
    {
        private AudioSourceStorage storage;

        public void Start()
        {
            storage = AudioSourceManager.RegisterAudioSource(GetComponent<AudioSource>());
        }

        public void SetVoice(PlayerControllerB player)
        {
            storage.voice = true;
            storage.player = player;
        }

        public void Update()
        {
            storage.Update();
        }

        public void OnDestroy()
        {
            if (storage != null)
            {
                storage.Reset();
                AudioSourceManager.DeregisterAudioSource(storage);
            }
        }
    }

    [HarmonyPatch]
    public class AudioSourceManager : MonoBehaviour
    {
        public static AudioSourceManager Instance { get; private set; }

        public AnimationCurve listenerCurve;

        private List<AudioSourceStorage> allAudioSources = new List<AudioSourceStorage>();
        private List<AudioSourceStorage> trackedAudioSources = new List<AudioSourceStorage>();

        private List<PhoneBehavior> allPhones = new List<PhoneBehavior>();

        private PlayerControllerB localPlayer;
        private Transform listenerPos;

        private float listenDistSqr;
        private float recordDistSqr;

        public void Start()
        {
            // Add a hook script to every audio source in the game
            AudioSource[] loadedAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>();

            foreach (AudioSource source in loadedAudioSources)
            {
                source.gameObject.AddComponent<AudioSourceHook>();
            }

            // pre-square the config values
            listenDistSqr = Config.listeningDist.Value * Config.listeningDist.Value;
            recordDistSqr = Config.recordingDist.Value * Config.recordingDist.Value;

            listenerCurve = AnimationCurve.Linear(0, 1, 1, 0);
            listenerCurve.ClearKeys();
            listenerCurve.AddKey(.1f, 1f);
            listenerCurve.AddKey(.3f, .2f);
            listenerCurve.AddKey(1f, 0f);

            listenerCurve.keys[0].inTangent = -3.5f;
            listenerCurve.keys[0].outTangent = -3.5f;
            listenerCurve.keys[0].tangentModeInternal = 0;
            listenerCurve.keys[0].weightedMode = WeightedMode.None;
            listenerCurve.keys[0].inWeight = 0f;
            listenerCurve.keys[0].outWeight = 0f;

            listenerCurve.keys[0].inTangent = -1.25f;
            listenerCurve.keys[0].outTangent = -1.25f;
            listenerCurve.keys[0].tangentModeInternal = 136;
            listenerCurve.keys[0].weightedMode = WeightedMode.None;
            listenerCurve.keys[0].inWeight = 0.3333f;
            listenerCurve.keys[0].outWeight = 0.3333f;

            listenerCurve.keys[0].inTangent = -0.3f;
            listenerCurve.keys[0].outTangent = -0.3f;
            listenerCurve.keys[0].tangentModeInternal = 0;
            listenerCurve.keys[0].weightedMode = WeightedMode.None;
            listenerCurve.keys[0].inWeight = 0f;
            listenerCurve.keys[0].outWeight = 0f;
        }

        public void Update()
        {
            if (localPlayer == null)
            {
                localPlayer = GameNetworkManager.Instance.localPlayerController;
            }

            if (localPlayer != null)
            {
                listenerPos = localPlayer.isPlayerDead && localPlayer.spectatedPlayerScript != null ? localPlayer.spectatedPlayerScript.playerGlobalHead.transform : localPlayer.playerGlobalHead.transform;

                // Update the audio redirect info for all audio source storages
                UpdateAudioSourceStorages();
            }
        }

        private void UpdateAudioSourceStorages()
        {
            foreach (AudioSourceStorage storage in trackedAudioSources)
            {
                storage.recordPos = null;
                storage.playPos = null;
                storage.listenerPos = null;

                storage.recordConnectionQuality = 1f;
                storage.recordStaticMode = false;

                // Max distance is set in the config, if the audio source has a shorter max dist, use that
                float closestDistSqr = recordDistSqr;
                float maxDistSqr = storage.audioSource.maxDistance * storage.audioSource.maxDistance;
                if (maxDistSqr < closestDistSqr)
                {
                    closestDistSqr = maxDistSqr;
                }

                // Only continue if there are actually calls happening within range
                foreach (PhoneBehavior phone in allPhones)
                {
                    if (phone.IsActive())
                    {
                        PhoneBehavior callerPhone = phone.GetCallerPhone();

                        float listenerDistToPhone = (phone.playPos.position - listenerPos.position).sqrMagnitude;
                        float listenerDistToCaller = (callerPhone.playPos.position - listenerPos.position).sqrMagnitude;

                        // Need the player to be in range of a phone, but not also in range of the phone it's calling
                        if (listenerDistToPhone <= listenDistSqr && listenerDistToCaller > listenDistSqr)
                        {
                            float audioDistToCaller = (callerPhone.recordPos.position - storage.sourcePos.position).sqrMagnitude;
                            float audioDistToListener = (listenerPos.position - storage.sourcePos.position).sqrMagnitude;

                            // Need to be closer than the existing closest phone and the max recording dist
                            // Also need to be closer to the phone than the local player
                            if (audioDistToCaller <= closestDistSqr && audioDistToCaller < audioDistToListener)
                            {
                                storage.recordPos = callerPhone.recordPos;
                                storage.playPos = phone.playPos;
                                storage.listenerPos = listenerPos;

                                float worseConnection = callerPhone.GetCallQuality() < phone.GetCallQuality() ? callerPhone.GetCallQuality() : phone.GetCallQuality();

                                storage.recordConnectionQuality = worseConnection;
                                storage.recordStaticMode = phone.GetStaticMod();
                            }
                        }
                    }
                }
            }
        }

        public static AudioSourceStorage RegisterAudioSource(AudioSource source)
        {
            if (Instance == null) return null;

            AudioSourceStorage newStorage = new AudioSourceStorage(source);

            Instance.allAudioSources.Add(newStorage);
            if (source.spatialBlend != 0f)
            {
                Instance.trackedAudioSources.Add(newStorage);
            }

            return newStorage;
        }

        public static void DeregisterAudioSource(AudioSourceStorage source)
        {
            if (Instance == null || source == null) return;

            Instance.allAudioSources.Remove(source);
            Instance.trackedAudioSources.Remove(source);
        }

        public static void RegisterPhone(PhoneBehavior phone)
        {
            if (Instance == null) return;

            Instance.allPhones.Add(phone);
        }

        public static void DeregisterPhone(PhoneBehavior phone)
        {
            if (Instance == null) return;

            Instance.allPhones.Remove(phone);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.RefreshPlayerVoicePlaybackObjects))]
        static void PlayerVoiceRefresh(PlayerVoiceIngameSettings __instance)
        {
            // Apply voice params to all player voice audio sources
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player.currentVoiceChatAudioSource != null)
                {
                    AudioSourceHook hook = player.currentVoiceChatAudioSource.GetComponent<AudioSourceHook>();
                    hook.SetVoice(player);
                }
            }
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
