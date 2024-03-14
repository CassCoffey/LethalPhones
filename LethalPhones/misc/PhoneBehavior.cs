using Dissonance;
using GameNetcodeStuff;
using Scoops.patch;
using Scoops.service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.Netcode;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Scoops.misc
{
    public class PhoneBehavior : NetworkBehaviour
    {
        public static float RECORDING_START_DIST = 15f;
        public static float BACKGROUND_VOICE_DIST = 20f;
        public static float EAVESDROP_DIST = 5f;

        public string phoneNumber;

        public bool spectatorClear = false;

        public ulong activeCaller = 0;
        public ulong incomingCaller = 0;

        protected AudioSource ringAudio;
        protected AudioSource thisAudio;
        protected AudioSource target;

        protected string incomingCall = null;
        protected string activeCall = null;
        protected string outgoingCall = null;

        protected List<AudioSource> untrackedAudioSources = new List<AudioSource>();
        protected List<AudioSourceStorage> audioSourcesInRange = new List<AudioSourceStorage>();

        protected HashSet<PhoneBehavior> modifiedVoices = new HashSet<PhoneBehavior>();

        protected Queue<int> dialedNumbers = new Queue<int>(4);

        protected float updateInterval;
        protected float connectionInterval = 0f;
        protected bool staticMode = false;
        protected bool hardStatic = false;
        protected float staticChance = 0f;

        public float targetConnectionQuality = 1f;
        public float currentConnectionQuality = 1f;
        public NetworkVariable<float> connectionQuality = new NetworkVariable<float>(1f);

        protected IEnumerator activePhoneRingCoroutine;

        public virtual void Start()
        {
            this.thisAudio = GetComponent<AudioSource>();
            this.target = transform.Find("Target").gameObject.GetComponent<AudioSource>();

            this.GetAllAudioSourcesToUpdate();
        }

        public virtual void Update()
        {
            if (this.activeCall == null || spectatorClear)
            {
                if (!spectatorClear) activeCaller = 0;

                staticChance = 0f;

                if (target.isPlaying)
                {
                    target.Stop();
                }

                if (audioSourcesInRange.Count > 0)
                {
                    foreach (AudioSourceStorage storage in this.audioSourcesInRange)
                    {
                        storage.Reset();
                    }

                    this.audioSourcesInRange.Clear();
                }
                if (modifiedVoices.Count > 0)
                {
                    foreach (PhoneBehavior modifiedPhone in modifiedVoices)
                    {
                        modifiedPhone.RemovePhoneVoiceEffect();
                    }

                    this.modifiedVoices.Clear();
                }

                spectatorClear = false;
            }

            this.UpdatePlayerVoices();
            this.UpdateAllAudioSources();
            this.GetAllAudioSourcesToUpdate();

            if (IsOwner)
            {
                if (this.connectionInterval >= 0.75f)
                {
                    this.connectionInterval = 0f;
                    ManageConnectionQuality();
                }
                else
                {
                    this.connectionInterval += Time.deltaTime;
                }

                if (this.updateInterval >= 0f)
                {
                    this.updateInterval -= Time.deltaTime;
                    return;
                }
                this.updateInterval = 1f;

                this.UpdateConnectionQualityServerRpc(currentConnectionQuality);
            }
        }

        public void StopLocalSound()
        {
            if (IsOwner)
            {
                thisAudio.Stop();
            }
        }

        public void PlayHangupSound()
        {
            if (IsOwner)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneAssetManager.phoneHangup);
            }
        }

        public void PlayPickupSound()
        {
            if (IsOwner)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneAssetManager.phonePickup);
            }
        }

        public void PlayBusySound()
        {
            if (IsOwner)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneAssetManager.phoneBusy);
            }
        }

        protected virtual void GetAllAudioSourcesToUpdate()
        {
            if (IsOwner || PhoneNetworkHandler.Instance.localPhone == null)
            {
                return;
            }
            if (activeCall != PhoneNetworkHandler.Instance.localPhone.phoneNumber && !IsBeingSpectated())
            {
                return;
            }

            untrackedAudioSources = StartOfRoundPhonePatch.GetAllAudioSourcesInRange(transform.position);
            foreach (AudioSource source in untrackedAudioSources)
            {
                if (source != null && source.spatialBlend != 0f)
                {
                    AudioSourceStorage storage = new AudioSourceStorage(source);
                    storage.InitAudio();
                    audioSourcesInRange.Add(storage);
                }
            }
        }

        protected virtual void UpdateAllAudioSources()
        {
            if (IsOwner || PhoneNetworkHandler.Instance.localPhone == null)
            {
                return;
            }
            if (activeCaller == 0 || activeCall != PhoneNetworkHandler.Instance.localPhone.phoneNumber && !IsBeingSpectated())
            {
                return;
            }

            PhoneBehavior callerPhone = PhoneNetworkHandler.Instance.localPhone;
            if (callerPhone == null)
            {
                return;
            }

            float worseConnection = callerPhone.connectionQuality.Value < this.connectionQuality.Value ? callerPhone.connectionQuality.Value : this.connectionQuality.Value;

            for (int j = audioSourcesInRange.Count - 1; j >= 0; j--)
            {
                AudioSourceStorage storage = audioSourcesInRange[j];
                AudioSource source = storage.audioSource;
                if (source != null)
                {
                    float callerDist = Vector3.Distance(source.transform.position, callerPhone.transform.position);
                    float ownerDist = (source.transform.position - this.transform.position).sqrMagnitude;
                    float ownerToCallerDist = (callerPhone.transform.position - this.transform.position).sqrMagnitude;

                    if (ownerToCallerDist <= (RECORDING_START_DIST * RECORDING_START_DIST) || (callerDist * callerDist) < ownerDist || ownerDist > (source.maxDistance * source.maxDistance))
                    {
                        storage.Reset();
                        audioSourcesInRange.RemoveAt(j);
                    }
                    else
                    {
                        storage.ApplyPhone(callerDist, worseConnection, staticMode && hardStatic);
                    }
                }
                else
                {
                    audioSourcesInRange.RemoveAt(j);
                }
            }
        }

        protected virtual bool IsBeingSpectated()
        {
            return false;
        }

        protected virtual void UpdatePlayerVoices()
        {
            if (GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null)
            {
                return;
            }

            if (activeCaller != 0)
            {
                if (activeCall != null)
                {
                    float listenDist = 0f;
                    float listenAngle = 0f;
                    if (!IsOwner)
                    {
                        if (!IsBeingSpectated())
                        {
                            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                            listenDist = Vector3.Distance(localPlayer.transform.position, transform.position);
                            if (listenDist > EAVESDROP_DIST)
                            {
                                return;
                            }
                            Vector3 directionTo = transform.position - localPlayer.transform.position;
                            directionTo = directionTo / listenDist;
                            listenAngle = Vector3.Dot(directionTo, localPlayer.transform.right);
                        }
                    }

                    PhoneBehavior callerPhone = GetNetworkObject(activeCaller).GetComponent<PhoneBehavior>();
                    if (callerPhone == PhoneNetworkHandler.Instance.localPhone)
                    {
                        return;
                    }

                    float worseConnection = callerPhone.connectionQuality.Value < this.connectionQuality.Value ? callerPhone.connectionQuality.Value : this.connectionQuality.Value;

                    if (IsOwner || listenDist > 0f)
                    {
                        UpdateStatic(worseConnection, listenDist);
                    }

                    float dist = Vector3.Distance(callerPhone.transform.position, transform.position);

                    if (dist > RECORDING_START_DIST)
                    {
                        modifiedVoices.Add(callerPhone);
                        callerPhone.ApplyPhoneVoiceEffect(0f, listenDist, listenAngle, worseConnection);
                    }
                    else
                    {
                        if (modifiedVoices.Contains(callerPhone))
                        {
                            modifiedVoices.Remove(callerPhone);
                            callerPhone.RemovePhoneVoiceEffect();
                        }
                    }
                }
            }
        }

        protected virtual void UpdateCallingUI()
        {
            // Nothing by default
        }

        public virtual bool PhoneInsideFactory()
        {
            return true;
        }

        public void InfluenceConnectionQuality(float change)
        {
            currentConnectionQuality = Mathf.Clamp01(currentConnectionQuality + change);
        }

        protected virtual void ManageConnectionQuality()
        {
            targetConnectionQuality = 1f;
            LevelWeatherType[] badWeathers = { LevelWeatherType.Flooded, LevelWeatherType.Rainy, LevelWeatherType.Foggy };
            LevelWeatherType[] worseWeathers = { LevelWeatherType.Stormy };
            if (badWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
            {
                targetConnectionQuality -= 0.25f;
            }
            if (worseWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
            {
                targetConnectionQuality -= 0.5f;
            }

            if (PhoneInsideFactory())
            {
                targetConnectionQuality -= 0.1f;
                float entranceDist = 300f;

                EntranceTeleport[] entranceArray = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>(false);
                for (int i = 0; i < entranceArray.Length; i++)
                {
                    if (!entranceArray[i].isEntranceToBuilding)
                    {
                        float newDist = Vector3.Distance(entranceArray[i].transform.position, transform.position);
                        if (newDist < entranceDist)
                        {
                            entranceDist = newDist;
                        }
                    }
                }

                targetConnectionQuality -= Mathf.Lerp(0f, 0.4f, Mathf.InverseLerp(0f, 300f, entranceDist));

                float apparatusDist = 300f;

                LungProp[] apparatusArray = UnityEngine.Object.FindObjectsOfType<LungProp>(false);
                for (int i = 0; i < apparatusArray.Length; i++)
                {
                    if (apparatusArray[i].isLungDocked)
                    {
                        float newDist = Vector3.Distance(apparatusArray[i].transform.position, transform.position);
                        if (newDist < apparatusDist)
                        {
                            apparatusDist = newDist;
                        }
                    }
                }

                if (apparatusDist <= 50f)
                {
                    targetConnectionQuality -= Mathf.Lerp(0.4f, 0f, Mathf.InverseLerp(0f, 50f, apparatusDist));
                }
            }

            targetConnectionQuality = Mathf.Clamp01(targetConnectionQuality);

            if (targetConnectionQuality < currentConnectionQuality)
            {
                currentConnectionQuality = targetConnectionQuality;
            }
            else if (targetConnectionQuality > currentConnectionQuality)
            {
                currentConnectionQuality += 0.005f;
            }

            if (staticChance > 0f)
            {
                // we are in the static zone
                float staticChanceMod = Mathf.Lerp(0.15f, 0.85f, staticChance);

                staticMode = UnityEngine.Random.Range(0f, 1f) < staticChanceMod;
                hardStatic = UnityEngine.Random.Range(0f, 1f) < staticChanceMod;
            }
            else
            {
                staticMode = false;
                hardStatic = false;
            }
        }

        protected void UpdateStatic(float quality, float dist = 0f)
        {
            if (quality <= 0.5f)
            {
                staticChance = Mathf.InverseLerp(0.5f, 0f, quality);

                if (staticMode)
                {
                    float listenerMod = 1f;
                    if (dist != 0f)
                    {
                        listenerMod = Mathf.InverseLerp(EAVESDROP_DIST, 0f, dist);
                        target.panStereo = 0f;
                    } 
                    else
                    {
                        target.panStereo = -0.4f;
                    }

                    if (hardStatic)
                    {
                        target.GetComponent<AudioLowPassFilter>().cutoffFrequency = 2899f;
                        target.volume = 1f * listenerMod;
                    }
                    else
                    {
                        target.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(1000f, 2800f, staticChance);
                        target.volume = Mathf.Clamp01(staticChance + 0.75f) * listenerMod;
                    }

                    if (!target.isPlaying)
                    {
                        switch (UnityEngine.Random.Range(1, 4))
                        {
                            case (1):
                                target.clip = PhoneAssetManager.phoneStaticOne;
                                break;

                            case (2):
                                target.clip = PhoneAssetManager.phoneStaticTwo;
                                break;

                            case (3):
                                target.clip = PhoneAssetManager.phoneStaticThree;
                                break;

                            default:
                                break;
                        }

                        target.Play();
                    }
                }
                else
                {
                    if (target.isPlaying) target.Stop();
                }
            } 
            else
            {
                staticChance = 0f;
                if (target.isPlaying) target.Stop();
            }
        }

        [ServerRpc]
        protected void UpdateConnectionQualityServerRpc(float currentConnectionQuality)
        {
            connectionQuality.Value = currentConnectionQuality;
        }

        [ClientRpc]
        public virtual void SetNewPhoneNumberClientRpc(string number)
        {
            this.phoneNumber = number;
        }

        [ClientRpc]
        public virtual void InvalidCallClientRpc(string reason)
        {
            outgoingCall = null;
        }

        [ClientRpc]
        public void RecieveCallClientRpc(ulong callerId, string callerNumber)
        {
            if (incomingCall == null)
            {
                StartRinging();

                incomingCall = callerNumber;
                incomingCaller = callerId;
                dialedNumbers.Clear();
                UpdateCallingUI();
            }
            else if (IsOwner)
            {
                PhoneNetworkHandler.Instance.LineBusyServerRpc(callerNumber);
            }
        }

        [ClientRpc]
        public void CallAcceptedClientRpc(ulong accepterId, string accepterNumber)
        {
            if (outgoingCall != accepterNumber)
            {
                // Whoops, how did we get this call? Send back a no.
                return;
            }

            StopRinging();
            PlayPickupSound();

            outgoingCall = null;
            activeCall = accepterNumber;
            activeCaller = accepterId;
            UpdateCallingUI();
        }

        [ClientRpc]
        public void HangupCallClientRpc(ulong cancellerId, string cancellerNumber)
        {
            if (activeCall == cancellerNumber)
            {
                PlayHangupSound();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
                UpdateCallingUI();
            }
            else if (outgoingCall == cancellerNumber)
            {
                // Line busy
                PlayHangupSound();
                outgoingCall = null;
                UpdateCallingUI();
            }
            else if (incomingCall == cancellerNumber)
            {
                // incoming call cancelled
                StopRinging();
                thisAudio.Stop();
                incomingCall = null;
                UpdateCallingUI();
            }
            else
            {
                // No you can't hang up a call you're not on.
            }
        }

        [ServerRpc]
        public void StopRingingServerRpc()
        {
            StopRingingClientRpc();
        }

        [ClientRpc]
        public void StopRingingClientRpc()
        {
            StopRinging();
        }

        protected virtual void StartRinging()
        {
            ringAudio.Stop();
            StartCoroutine(activePhoneRingCoroutine);
            ringAudio.clip = PhoneAssetManager.phoneRingReciever;
            ringAudio.Play();
        }

        protected void StopRinging()
        {
            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
            ringAudio.Stop();
        }

        public virtual void ApplyPhoneVoiceEffect(float distance = 0f, float listeningDistance = 0f, float listeningAngle = 0f, float connectionQuality = 1f)
        {
            // Does nothing
        }

        public virtual void RemovePhoneVoiceEffect(ulong phoneId)
        {
            PhoneBehavior otherPhone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
            if (otherPhone != null)
            {
                otherPhone.RemovePhoneVoiceEffect();
            }
        }

        public virtual void RemovePhoneVoiceEffect()
        {
            // Nothing by default
        }

        public override void OnDestroy()
        {
            if (target != null)
            {
                if (target.isPlaying)
                {
                    target.Stop();
                }
            }

            if (audioSourcesInRange != null)
            {
                if (audioSourcesInRange.Count > 0)
                {
                    foreach (AudioSourceStorage storage in this.audioSourcesInRange)
                    {
                        storage.Reset();
                    }

                    this.audioSourcesInRange.Clear();
                }
            }

            if (modifiedVoices != null)
            {
                if (modifiedVoices.Count > 0)
                {
                    foreach (PhoneBehavior modifiedPhone in modifiedVoices)
                    {
                        modifiedPhone.RemovePhoneVoiceEffect();
                    }

                    this.modifiedVoices.Clear();
                }
            }

            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
        }
    }
}