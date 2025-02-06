using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
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
        public bool staticAudio = false;

        public PlayerControllerB player = null;

        public Transform recordPos;
        public Transform playPos;
        public Transform listenerPos;
        public Transform sourcePos;

        public float recordInterference;

        public AudioSource audioSource;

        private float recordDist;

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

            this.recordInterference = 0f;

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
            if (AudioSourceManager.Instance != null && audioSource != null)
            {
                if (voice && player == null) return;
                if (recordPos != null && playPos != null && listenerPos != null)
                {
                    if (!modified) InitPhone();
                    ApplyPhoneVolume();
                    ApplyPhoneEffect();
                    ApplyPhoneLocation();
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

            if (staticAudio && !audioSource.isPlaying)
            {
                audioSource.Play();
            }

            // Remove spatialization so that the audio can be heard from anywhere
            audioSource.spatialBlend = 0f;

            if (audioSource.GetComponent<AudioLowPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().enabled = true;
            }
            if (audioSource.GetComponent<OccludeAudio>())
            {
                audioSource.GetComponent<OccludeAudio>().enabled = false;
            }

            if (!staticAudio)
            {
                if (audioSource.GetComponent<AudioHighPassFilter>())
                {
                    audioSource.GetComponent<AudioHighPassFilter>().enabled = true;
                }
            }
        }

        public void ApplyPhoneVolume()
        {
            float mod = GetAudioVolumeAtPos(recordPos.position);
            
            audioSource.volume = (origVolume * mod);

            // If this is a voice apply the voiceSound config, otherwise apply the backgroundSound config
            if (!staticAudio)
            {
                audioSource.volume *= voice ? Config.voiceSoundAdjust.Value : Config.backgroundSoundAdjust.Value;

                float interferenceVolumeMod = (1f - recordInterference);
                interferenceVolumeMod *= interferenceVolumeMod;
                audioSource.volume *= interferenceVolumeMod;
            }
        }

        public void ApplyPhoneEffect()
        {
            // Static bypasses audio effects
            if (staticAudio)
            {
                if (audioSource.GetComponent<AudioLowPassFilter>())
                {
                    audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = 3000f;
                    audioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 3f;
                }
                return;
            }
            // Apply Voice Audio Source specific changes
            if (voice)
            {
                player.currentVoiceChatIngameSettings.set2D = true;
            }

            if (audioSource.GetComponent<AudioLowPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = 3000f;
                audioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = Mathf.Lerp(3f, 10f, recordInterference);
            }
            if (audioSource.GetComponent<AudioHighPassFilter>())
            {
                audioSource.GetComponent<AudioHighPassFilter>().cutoffFrequency = Mathf.Lerp(2000f, 2500f, recordInterference);
                audioSource.GetComponent<AudioHighPassFilter>().highpassResonanceQ = Mathf.Lerp(2f, 3f, recordInterference);
            }

            if (voice && player.voiceMuffledByEnemy && audioSource.GetComponent<AudioLowPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = 500;
            }
        }

        public void ApplyPhoneLocation()
        {
            Vector3 directionTo = playPos.position - listenerPos.position;
            float listenDist = directionTo.sqrMagnitude;
            float listenAngle = Vector3.Dot(directionTo.normalized, listenerPos.right);

            float maxListenDistSqr = Config.listeningDist.Value;
            maxListenDistSqr *= maxListenDistSqr;

            float recordMod = Mathf.Clamp01(AudioSourceManager.Instance.recorderCurve.Evaluate(Mathf.Clamp01(recordDist / Config.recordingDist.Value)));
            audioSource.volume *= recordMod;
            if (audioSource.GetComponent<AudioLowPassFilter>())
            {
                audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(1500f, 3000f, recordMod);
            }

            if (listenDist > 0f)
            {
                float listenMod = Mathf.Clamp01(AudioSourceManager.Instance.listenerCurve.Evaluate(Mathf.Clamp01(listenDist / maxListenDistSqr)));
                audioSource.volume *= listenMod;
                if (audioSource.GetComponent<AudioLowPassFilter>())
                {
                    audioSource.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(1000f, 750f, Mathf.Clamp01(listenDist / maxListenDistSqr));
                }
                audioSource.panStereo = listenAngle;
            }
        }

        public float GetAudioVolumeAtPos(Vector3 recordPos)
        {
            float mod = 0f;

            // Short circuit if max interference
            if (!staticAudio && recordInterference == 1f)
            {
                return 0f;
            }

            recordDist = staticAudio ? 0f : Vector3.Distance(recordPos, sourcePos.position);

            // Recalculate volume from distance information
            if (staticAudio)
            {
                float staticStart = ConnectionQualityManager.STATIC_START_INTERFERENCE;

                // Static is the inverse of other volumes
                if (recordInterference > staticStart)
                {
                    float volumeInterference = Mathf.InverseLerp(1f - staticStart, 0f, recordInterference - staticStart);
                    volumeInterference = 1f - (volumeInterference * volumeInterference);
                    mod = volumeInterference * Config.staticSoundAdjust.Value;
                }
            }
            else if (audioSource.rolloffMode == AudioRolloffMode.Linear)
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

            return mod;
        }

        public void Reset()
        {
            GameObject audioSourceHolder = audioSource.gameObject;

            if (voice)
            {
                player.currentVoiceChatIngameSettings.set2D = false;
            }

            if (staticAudio && audioSource.isPlaying)
            {
                audioSource.Stop();
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

        private bool staticAudio = false;

        public void Start()
        {
            if (AudioSourceManager.Instance == null)
            {
                Destroy(this);
                return;
            }
            storage = AudioSourceManager.RegisterAudioSource(GetComponent<AudioSource>());
            storage.staticAudio = staticAudio;
        }

        public void SetVoice(PlayerControllerB player)
        {
            storage.voice = true;
            storage.player = player;
        }

        public void SetStaticAudio()
        {
            staticAudio = true;

            if (storage != null)
            {
                storage.staticAudio = true;
            }
        }

        public void Update()
        {
            if (AudioSourceManager.Instance == null)
            {
                Destroy(this);
                return;
            }
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
        public AnimationCurve recorderCurve;

        private List<AudioSourceStorage> trackedAudioSources = new List<AudioSourceStorage>();

        private List<PhoneBehavior> allPhones = new List<PhoneBehavior>();

        private PlayerControllerB localPlayer;
        private Transform listenerPos;
        private PhoneBehavior listenerPhone;

        private float listenDistSqr;
        private float recordDistSqr;

        public void Init()
        {
            // Add a hook script to every audio source in the game
            AudioSource[] loadedAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>();

            foreach (AudioSource source in loadedAudioSources)
            {
                if (source.GetComponent<AudioSourceHook>() == null)
                {
                    source.gameObject.AddComponent<AudioSourceHook>();
                }
            }

            // pre-square the config values
            listenDistSqr = Config.listeningDist.Value * Config.listeningDist.Value;
            recordDistSqr = Config.recordingDist.Value * Config.recordingDist.Value;

            // You like magic numbers?
            Keyframe[] listenKeys = new Keyframe[3];

            listenKeys[0] = new Keyframe(0f, 0.5f, -4, -4, 0.3333f, 0.3333f);
            listenKeys[0].tangentMode = 34;
            listenKeys[0].weightedMode = WeightedMode.None;

            listenKeys[1] = new Keyframe(0.4f, 0.1f, -0.0889f, -0.0889f, 0.3333f, 0.3333f);
            listenKeys[1].tangentMode = 136;
            listenKeys[1].weightedMode = WeightedMode.None;

            listenKeys[2] = new Keyframe(1f, 0f, 0, 0, 0.3333f, 0.3333f);
            listenKeys[2].tangentMode = 0;
            listenKeys[2].weightedMode = WeightedMode.None;

            listenerCurve = new AnimationCurve(listenKeys);

            Keyframe[] recordKeys = new Keyframe[3];

            recordKeys[0] = new Keyframe(0.1f, 1f, -4, -4, 0.3333f, 0.3333f);
            recordKeys[0].tangentMode = 34;
            recordKeys[0].weightedMode = WeightedMode.None;

            recordKeys[1] = new Keyframe(0.5f, 0.4f, -0.0889f, -0.0889f, 0.3333f, 0.3333f);
            recordKeys[1].tangentMode = 136;
            recordKeys[1].weightedMode = WeightedMode.None;

            recordKeys[2] = new Keyframe(1f, 0f, 0, 0, 0.3333f, 0.3333f);
            recordKeys[2].tangentMode = 0;
            recordKeys[2].weightedMode = WeightedMode.None;

            recorderCurve = new AnimationCurve(recordKeys);
        }

        public void OnDestroy()
        {
            Instance = null;
        }

        public void Update()
        {
            if (localPlayer == null)
            {
                localPlayer = GameNetworkManager.Instance.localPlayerController;
            }

            if (localPlayer != null)
            {
                if (localPlayer.isPlayerDead && localPlayer.spectatedPlayerScript != null)
                {
                    listenerPos = localPlayer.spectatedPlayerScript.playerGlobalHead;
                }
                else
                {
                    listenerPos = localPlayer.playerGlobalHead;
                }

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

                storage.recordInterference = 0f;

                PhoneBehavior phone = ClosestActivePhone(storage);
                if (phone != null)
                {
                    PhoneBehavior callerPhone = phone.GetCallerPhone();

                    float realVol = storage.GetAudioVolumeAtPos(listenerPos.position);
                    
                    if (realVol < 0.1f)
                    {
                        storage.recordPos = callerPhone.recordPos;
                        storage.playPos = phone.playPos;
                        storage.listenerPos = listenerPos;

                        // If we're spectating, put our ears inside the phone for clarity
                        if (localPlayer.isPlayerDead && localPlayer.spectatedPlayerScript != null)
                        {
                            storage.listenerPos = phone.playPos;
                        }

                        float phoneInterference = phone.GetTotalInterference();
                        float callerPhoneInterference = callerPhone.GetTotalInterference();

                        float worseInterference = phoneInterference < callerPhoneInterference ? callerPhoneInterference : phoneInterference;

                        storage.recordInterference = worseInterference;
                    }
                }
            }
        }

        private PhoneBehavior ClosestActivePhone(AudioSourceStorage storage)
        {
            PhoneBehavior closestPhone = null;

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

                    if (callerPhone != null)
                    {
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
                                closestPhone = phone;
                            }
                        }
                    }
                }
            }

            return closestPhone;
        }

        public static AudioSourceStorage RegisterAudioSource(AudioSource source)
        {
            if (Instance == null) return null;

            AudioSourceStorage newStorage = new AudioSourceStorage(source);

            if (source.spatialBlend != 0f)
            {
                Instance.trackedAudioSources.Add(newStorage);
            }

            return newStorage;
        }

        public static void DeregisterAudioSource(AudioSourceStorage source)
        {
            if (Instance == null || source == null) return;

            Instance.trackedAudioSources.Remove(source);
        }

        public static void RegisterPhone(PhoneBehavior phone)
        {
            if (Instance == null) return;

            Instance.allPhones.Add(phone);

            // Set static audio storage for new phone
            if (phone.GetStaticAudioSource() != null)
            {
                AudioSourceHook hook = phone.GetStaticAudioSource().GetComponent<AudioSourceHook>();
                hook.SetStaticAudio();
            }
        }

        public static void DeregisterPhone(PhoneBehavior phone)
        {
            if (Instance == null) return;

            Instance.allPhones.Remove(phone);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.RefreshPlayerVoicePlaybackObjects))]
        static void PlayerVoiceRefresh(ref PlayerVoiceIngameSettings __instance)
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
                manager.Init();

                Instance = manager;
            }
        }
    }
}
