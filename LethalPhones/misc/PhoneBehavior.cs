using Dissonance;
using GameNetcodeStuff;
using Scoops.patch;
using Scoops.service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;

namespace Scoops.misc
{
    public class PhoneBehavior : NetworkBehaviour
    {
        public static float RECORDING_START_DIST = 15f;
        public static float BACKGROUND_VOICE_DIST = 20f;
        public static float EAVESDROP_DIST = 5f;

        public string phoneNumber;

        public bool spectatorClear = false;

        public ushort activeCaller = 0;
        public ushort incomingCaller = 0;

        protected AudioSource ringAudio;
        protected AudioSource thisAudio;
        protected AudioSource target;

        protected string incomingCall = null;
        protected string activeCall = null;
        protected string outgoingCall = null;

        protected List<AudioSource> untrackedAudioSources = new List<AudioSource>();
        protected List<AudioSourceStorage> audioSourcesInRange = new List<AudioSourceStorage>();

        protected HashSet<PlayerControllerB> modifiedPlayerVoices = new HashSet<PlayerControllerB>();

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
                if (modifiedPlayerVoices.Count > 0)
                {
                    foreach (PlayerControllerB player in modifiedPlayerVoices)
                    {
                        RemovePhoneVoiceEffect(player);
                    }

                    this.modifiedPlayerVoices.Clear();
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

        }

        protected virtual void UpdateAllAudioSources()
        {

        }

        protected virtual void UpdatePlayerVoices()
        {

        }

        protected virtual void UpdateCallingUI()
        {
            
        }

        public void InfluenceConnectionQuality(float change)
        {
            currentConnectionQuality = Mathf.Clamp01(currentConnectionQuality + change);
        }

        protected virtual void ManageConnectionQuality()
        {
            
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
            
        }

        [ClientRpc]
        public void RecieveCallClientRpc(ushort callerId, string callerNumber)
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
        public void CallAcceptedClientRpc(ushort accepterId, string accepterNumber)
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
        public void HangupCallClientRpc(ushort cancellerId, string cancellerNumber)
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

        protected void ApplyPhoneVoiceEffect(PlayerControllerB playerController, float distance = 0f, float listeningDistance = 0f, float listeningAngle = 0f, float connectionQuality = 1f)
        {
            if (playerController == null)
            {
                return;
            }
            if (playerController.voiceMuffledByEnemy)
            {
                connectionQuality = 0f;
            }
            if (playerController.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }
            if (playerController.currentVoiceChatAudioSource == null)
            {
                Plugin.Log.LogInfo("Player " + playerController.name + " Voice Chat Audio Source still null after refresh? Something has gone wrong.");
                return;
            }

            AudioSource currentVoiceChatAudioSource = playerController.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = true;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = true;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 0f;
            playerController.currentVoiceChatIngameSettings.set2D = true;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[playerController.playerClientId];
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = GameNetworkManager.Instance.localPlayerController.isPlayerDead ? 0f : -0.4f;
            occludeAudio.lowPassOverride = Mathf.Lerp(6000f, 3000f, connectionQuality);
            lowPass.lowpassResonanceQ = Mathf.Lerp(6f, 3f, connectionQuality);
            highPass.highpassResonanceQ = Mathf.Lerp(3f, 1f, connectionQuality);

            if (distance != 0f)
            {
                float mod = Mathf.InverseLerp(BACKGROUND_VOICE_DIST, 0f, distance);
                currentVoiceChatAudioSource.volume = currentVoiceChatAudioSource.volume * mod;
                occludeAudio.lowPassOverride = 1500f;
            }

            if (listeningDistance != 0f)
            {
                float mod = Mathf.InverseLerp(EAVESDROP_DIST, 0f, listeningDistance);
                currentVoiceChatAudioSource.volume = currentVoiceChatAudioSource.volume * mod;
                occludeAudio.lowPassOverride = 750f;
                currentVoiceChatAudioSource.panStereo = listeningAngle;
            }

            if (playerController.voiceMuffledByEnemy)
            {
                occludeAudio.lowPassOverride = 500f;
            }

            if (staticMode && hardStatic)
            {
                currentVoiceChatAudioSource.volume = 0f;
            }
        }

        protected void RemovePhoneVoiceEffect(int playerId)
        {
            PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
            RemovePhoneVoiceEffect(playerController);
        }

        protected void RemovePhoneVoiceEffect(PlayerControllerB playerController)
        {
            if (playerController == null)
            {
                return;
            }
            if (playerController.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }

            AudioSource currentVoiceChatAudioSource = playerController.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = false;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = playerController.voiceMuffledByEnemy;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 1f;
            playerController.currentVoiceChatIngameSettings.set2D = false;
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = 0f;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[playerController.playerClientId];
            lowPass.lowpassResonanceQ = 1f;
            highPass.highpassResonanceQ = 1f;
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

            if (modifiedPlayerVoices != null)
            {
                if (modifiedPlayerVoices.Count > 0)
                {
                    foreach (PlayerControllerB player in modifiedPlayerVoices)
                    {
                        RemovePhoneVoiceEffect(player);
                    }

                    this.modifiedPlayerVoices.Clear();
                }
            }

            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
        }
    }
}