using Dissonance;
using GameNetcodeStuff;
using Scoops.compatability;
using Scoops.customization;
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
        public string phoneNumber;
        public string phoneSkinId;
        public string phoneCharmId;
        public string phoneRingtoneId;

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

        protected static LevelWeatherType[] badWeathers = { LevelWeatherType.Flooded, LevelWeatherType.Rainy, LevelWeatherType.Foggy, LevelWeatherType.DustClouds };
        protected static LevelWeatherType[] worseWeathers = { LevelWeatherType.Stormy };

        protected static string[] registryBadWeathers = { "flooded", "rainy", "foggy", "dust clouds", "heatwave", "snowfall" };
        protected static string[] registryWorseWeathers = { "stormy", "blizzard", "toxic smog", "solar flare" };

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

            this.GetAllAudioSourcesToUpdate();
            this.UpdateAllAudioSources();
            this.UpdatePlayerVoices();

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

        public void CallRandomNumber()
        {
            string number = GetRandomExistingPhoneNumber();
            if (number == null || number == "")
            {
                return;
            }

            outgoingCall = number;

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number, NetworkObjectId);
        }

        public string GetRandomExistingPhoneNumber()
        {
            PhoneBehavior[] allPhones = GameObject.FindObjectsByType<PhoneBehavior>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            List<PhoneBehavior> allValidPhones = new List<PhoneBehavior>();

            for (int i = 0; i < allPhones.Length; i++)
            {
                if (allPhones[i] != this && allPhones[i].phoneNumber != null && allPhones[i].phoneNumber != "")
                {
                    allValidPhones.Add(allPhones[i]);
                }
            }

            if (allValidPhones.Count > 0)
            {
                PhoneBehavior randPhone = allValidPhones[UnityEngine.Random.Range(0, allValidPhones.Count)];

                return randPhone.phoneNumber;
            }

            return null;
        }

        protected virtual void GetAllAudioSourcesToUpdate()
        {
            if (PhoneNetworkHandler.Instance.localPhone == null)
            {
                return;
            }
            if (activeCall == null || activeCaller == 0)
            {
                return;
            }

            PhoneBehavior callerPhone = GetNetworkObject(activeCaller).GetComponent<PhoneBehavior>();
            if (callerPhone == null)
            {
                return;
            }

            if (callerPhone != PhoneNetworkHandler.Instance.localPhone && !callerPhone.IsBeingSpectated())
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                float listenDist = (localPlayer.transform.position - callerPhone.transform.position).sqrMagnitude;
                if (listenDist > (Config.eavesdropDist.Value * Config.eavesdropDist.Value))
                {
                    return;
                }
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
            if (PhoneNetworkHandler.Instance.localPhone == null || GameNetworkManager.Instance.localPlayerController == null)
            {
                return;
            }
            if (activeCall == null || activeCaller == 0)
            {
                return;
            }

            PhoneBehavior callerPhone = GetNetworkObject(activeCaller).GetComponent<PhoneBehavior>();
            if (callerPhone == null)
            {
                return;
            }

            float listenDist = 0f;
            float listenAngle = 0f;
            if (callerPhone != PhoneNetworkHandler.Instance.localPhone && !callerPhone.IsBeingSpectated())
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                listenDist = Vector3.Distance(localPlayer.transform.position, callerPhone.transform.position);
                if (listenDist > Config.eavesdropDist.Value)
                {
                    return;
                }
                Vector3 directionTo = callerPhone.transform.position - localPlayer.transform.position;
                directionTo = directionTo / listenDist;
                listenAngle = Vector3.Dot(directionTo, localPlayer.transform.right);
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

                    if (ownerToCallerDist <= (Config.recordingStartDist.Value * Config.recordingStartDist.Value) || (callerDist * callerDist) < ownerDist || ownerDist > (source.maxDistance * source.maxDistance))
                    {
                        storage.Reset();
                        audioSourcesInRange.RemoveAt(j);
                    }
                    else
                    {
                        storage.ApplyPhone(ownerDist, worseConnection, listenDist, listenAngle, staticMode && hardStatic);
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
            if (activeCaller == 0 || activeCall == null)
            {
                return;
            }
            if (IsOwner && this != PhoneNetworkHandler.Instance.localPhone)
            {
                // Some server owned garbage
                return;
            }

            float listenDist = 0f;
            float listenAngle = 0f;
            if (!IsOwner)
            {
                if (!IsBeingSpectated())
                {
                    PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                    listenDist = Vector3.Distance(localPlayer.transform.position, transform.position);
                    if (listenDist > Config.eavesdropDist.Value)
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

            if (dist > Config.recordingStartDist.Value)
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

            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                PlayerControllerB background = StartOfRound.Instance.allPlayerScripts[i];
                PlayerPhone backgroundPhone = background.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
                if (background != null && backgroundPhone != null && background.isPlayerControlled && !background.isPlayerDead && !background.IsLocalPlayer)
                {
                    if (background != GameNetworkManager.Instance.localPlayerController && backgroundPhone != this && backgroundPhone != callerPhone)
                    {
                        float callDist = Vector3.Distance(backgroundPhone.transform.position, callerPhone.transform.position);
                        float localDist = (backgroundPhone.transform.position - GameNetworkManager.Instance.localPlayerController.transform.position).sqrMagnitude;
                        if (localDist > (Config.recordingStartDist.Value * Config.recordingStartDist.Value) && callDist < Config.backgroundVoiceDist.Value)
                        {
                            modifiedVoices.Add(backgroundPhone);
                            backgroundPhone.ApplyPhoneVoiceEffect(callDist, listenDist, listenAngle, worseConnection);
                        }
                        else
                        {
                            if (modifiedVoices.Contains(backgroundPhone))
                            {
                                modifiedVoices.Remove(backgroundPhone);
                                backgroundPhone.RemovePhoneVoiceEffect();
                            }
                        }
                    }
                }
            }
        }

        protected virtual void ApplySkin(string skinId)
        {
            // Nothing by default
        }

        protected virtual void ApplyCharm(string charmId)
        {
            // Nothing by default
        }

        protected virtual void UpdateCallingUI()
        {
            // Nothing by default
        }

        public virtual bool PhoneInsideFactory()
        {
            return true;
        }

        public virtual bool PhoneInsideShip()
        {
            return false;
        }

        public void InfluenceConnectionQuality(float change)
        {
            currentConnectionQuality = Mathf.Clamp01(currentConnectionQuality + change);
        }

        protected virtual void ManageConnectionQuality()
        {
            targetConnectionQuality = 1f;
            if (WeatherRegistryCompat.Enabled)
            {
                string currWeather = WeatherRegistryCompat.CurrentWeatherName().ToLower();
                if (registryBadWeathers.Contains(currWeather))
                {
                    targetConnectionQuality -= 0.25f;
                }
                if (registryWorseWeathers.Contains(currWeather))
                {
                    targetConnectionQuality -= 0.5f;
                }
            } 
            else
            {
                if (badWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
                {
                    targetConnectionQuality -= 0.25f;
                }
                if (worseWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
                {
                    targetConnectionQuality -= 0.5f;
                }
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
                        listenerMod = Mathf.InverseLerp(Config.eavesdropDist.Value, 0f, dist);
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

        public void PropogateInformation()
        {
            PropogateInformationClientRpc(this.phoneNumber, this.phoneSkinId, this.phoneCharmId, this.phoneRingtoneId);
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

            StopOutgoingRinging();
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
        public void PlayHangupSoundServerRpc()
        {
            PlayHangupSoundClientRpc();
        }

        [ClientRpc]
        public void PlayHangupSoundClientRpc()
        {
            PlayHangupSound();
        }

        public void PlayHangupSound()
        {
            thisAudio.Stop();
            thisAudio.PlayOneShot(PhoneAssetManager.phoneHangup);
        }

        [ServerRpc]
        public void PlayPickupSoundServerRpc()
        {
            PlayPickupSoundClientRpc();
        }

        [ClientRpc]
        public void PlayPickupSoundClientRpc()
        {
            PlayPickupSound();
        }

        public void PlayPickupSound()
        {
            thisAudio.Stop();
            thisAudio.PlayOneShot(PhoneAssetManager.phonePickup);
        }

        public void PlayBusySound()
        {
            thisAudio.Stop();
            thisAudio.PlayOneShot(PhoneAssetManager.phoneBusy);
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

        [ServerRpc]
        public void StartOutgoingRingingServerRpc()
        {
            StartOutgoingRingingClientRpc();
        }

        [ClientRpc]
        public void StartOutgoingRingingClientRpc()
        {
            StartOutgoingRinging();
        }

        [ServerRpc]
        public void StopOutgoingRingingServerRpc()
        {
            StopOutgoingRingingClientRpc();
        }

        [ClientRpc]
        public void StopOutgoingRingingClientRpc()
        {
            StopOutgoingRinging();
        }

        [ClientRpc]
        public void PropogateInformationClientRpc(string number, string skinId, string charmId, string ringtoneId)
        {
            this.phoneNumber = number;

            this.phoneSkinId = skinId;
            ApplySkin(skinId);

            this.phoneCharmId = charmId;
            ApplyCharm(charmId);

            this.phoneRingtoneId = ringtoneId;
        }

        protected virtual void StartRinging()
        {
            ringAudio.Stop();
            activePhoneRingCoroutine = PhoneRingCoroutine(4);
            StartCoroutine(activePhoneRingCoroutine);
            if (Config.disableRingtones.Value)
            {
                ringAudio.clip = CustomizationManager.ringtoneCustomizations[CustomizationManager.DEFAULT_RINGTONE];
            } 
            else
            {
                ringAudio.clip = CustomizationManager.ringtoneCustomizations[phoneRingtoneId];
            }
            
            ringAudio.Play();
        }

        protected void StopRinging()
        {
            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
            ringAudio.Stop();
        }

        protected virtual void StartOutgoingRinging()
        {
            thisAudio.Stop();
            thisAudio.clip = PhoneAssetManager.phoneRingCaller;
            thisAudio.Play();
        }

        protected void StopOutgoingRinging()
        {
            thisAudio.Stop();
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

        protected IEnumerator PhoneRingCoroutine(int repeats)
        {
            for (int i = 0; i < repeats; i++)
            {
                RoundManager.Instance.PlayAudibleNoise(ringAudio.transform.position, 50f, 0.95f, i, PhoneInsideShip(), 0);
                yield return new WaitForSeconds(4f);
            }
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