using GameNetcodeStuff;
using Scoops.service;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.misc
{
    public class PlayerPhone : NetworkBehaviour
    {
        public PlayerControllerB player;
        public GameObject localPhoneModel;
        public string phoneNumber;
        public bool toggled = false;

        private bool isLocalPhone = false;

        private Queue<int> dialedNumbers = new Queue<int>(4);

        public int activeCaller = -1;
        public int incomingCaller = -1;

        private AudioSource ringAudio;
        private AudioSource thisAudio;
        private AudioSource target;

        private string incomingCall = null;
        private string activeCall = null;
        private string outgoingCall = null;

        private float recordingRange = 6f;
        private float maxVolume = 0.6f;

        private List<AudioSource> audioSourcesToReplay = new List<AudioSource>();
        private Dictionary<AudioSource, AudioSource> audioSourcesReceiving = new Dictionary<AudioSource, AudioSource>();

        public Collider[] collidersInRange = new Collider[30];

        private float cleanUpInterval;
        private float updateInterval;

        public void Start()
        {
            this.thisAudio = GetComponent<AudioSource>();
            this.target = transform.Find("Target").gameObject.GetComponent<AudioSource>();

            this.GetAllAudioSourcesToReplay();
            this.SetupAudiosourceClip();

            this.player = transform.parent.GetComponent<PlayerControllerB>();
            this.ringAudio = player.transform.Find("Audios").Find("PhoneAudioExternal(Clone)").GetComponent<AudioSource>();

            this.localPhoneModel = player.localArmsTransform.Find("RigArms").Find("LeftArm").Find("ArmsLeftArm_target").Find("LocalPhoneModel(Clone)").gameObject;
        }

        private void SetupAudiosourceClip()
        {
            this.target.Stop();
        }

        public void Update()
        {
            if (this.cleanUpInterval >= 0f)
            {
                this.cleanUpInterval -= Time.deltaTime;
            }
            else
            {
                this.cleanUpInterval = 15f;
                if (this.audioSourcesReceiving.Count > 10)
                {
                    foreach (KeyValuePair<AudioSource, AudioSource> keyValuePair in this.audioSourcesReceiving)
                    {
                        if (keyValuePair.Key == null)
                        {
                            this.audioSourcesReceiving.Remove(keyValuePair.Key);
                        }
                    }
                }
            }
            if (this.updateInterval >= 0f)
            {
                this.updateInterval -= Time.deltaTime;
                return;
            }
            this.updateInterval = 0.3f;
            this.GetAllAudioSourcesToReplay();
            this.TimeAllAudioSources();
            this.UpdatePlayerVoices();
        }

        public string GetFullDialNumber()
        {
            return String.Join("", dialedNumbers);
        }

        public void DialNumber(int number)
        {
            dialedNumbers.Enqueue(number);

            if (dialedNumbers.Count > 4)
            {
                dialedNumbers.Dequeue();
            }

            Plugin.Log.LogInfo("Current dialing number: " + GetFullDialNumber());
        }

        public void CallButtonPressed()
        {
            if (activeCall != null)
            {
                // We're on a call, hang up
                Plugin.Log.LogInfo("Hanging Up: " + activeCall);
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall);
                PlayHangupSound();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
            else if (outgoingCall != null)
            {
                // We're calling, cancel
                Plugin.Log.LogInfo("Canceling: " + outgoingCall);
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall);
                PlayHangupSound();
                outgoingCall = null;
            } 
            else if (incomingCall != null)
            {
                // We have an incoming call, pick up
                activeCall = incomingCall;
                activeCaller = incomingCaller;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall);
                StopRingingServerRpc();
                PlayPickupSound();
                Plugin.Log.LogInfo("Picking up: " + activeCall);
            }
            else
            {
                // No calls of any sort are happening, make a new one
                CallDialedNumber();
            }

            if (isLocalPhone)
            {
                UpdateCallValues();
            }
        }

        public void CallDialedNumber()
        {
            string number = GetFullDialNumber();
            if (dialedNumbers.Count != 4)
            {
                Plugin.Log.LogInfo("Not enough digits: " + number);
                return;
            }
            if (number == phoneNumber)
            {
                Plugin.Log.LogInfo("You cannot call yourself yet. Messages will be here later.");
                dialedNumbers.Clear();
                return;
            }

            thisAudio.Play();
            outgoingCall = number;
            dialedNumbers.Clear();

            Plugin.Log.LogInfo("Dialing: " + number);

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number);
        }

        public void PlayHangupSound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneSoundManager.phoneHangup);
            }
        }

        public void PlayPickupSound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneSoundManager.phonePickup);
            }
        }

        private void GetAllAudioSourcesToReplay()
        {
            if (activeCall == null)
            {
                return;
            }
            int num = Physics.OverlapSphereNonAlloc(base.transform.position, this.recordingRange, this.collidersInRange, Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < num; i++)
            {
                if (!this.collidersInRange[i].gameObject.GetComponent<WalkieTalkie>())
                {
                    AudioSource component = this.collidersInRange[i].GetComponent<AudioSource>();
                    if (component != null && component.isPlaying && component.clip != null && component.time > 0f && !this.audioSourcesToReplay.Contains(component))
                    {
                        this.audioSourcesToReplay.Add(component);
                    }
                }
            }
        }

        private void TimeAllAudioSources()
        {
            if (activeCaller == -1) return;

            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[activeCaller];
            PlayerPhone callerPhone = caller.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();

            if (activeCall != null)
            {
                for (int j = callerPhone.audioSourcesToReplay.Count - 1; j >= 0; j--)
                {
                    AudioSource audioSource = callerPhone.audioSourcesToReplay[j];
                    if (!(audioSource == null))
                    {
                        if (this.audioSourcesReceiving.TryAdd(audioSource, null))
                        {
                            this.audioSourcesReceiving[audioSource] = this.target.gameObject.AddComponent<AudioSource>();
                            this.audioSourcesReceiving[audioSource].clip = audioSource.clip;
                            try
                            {
                                if (audioSource.time >= audioSource.clip.length)
                                {
                                    Plugin.Log.LogInfo(string.Format("phone: {0}, {1}, {2}", audioSource.time, audioSource.clip.length, audioSource.clip.name));
                                    if (audioSource.time - 0.05f < audioSource.clip.length)
                                    {
                                        this.audioSourcesReceiving[audioSource].time = Mathf.Clamp(audioSource.time - 0.05f, 0f, 1000f);
                                    }
                                    else
                                    {
                                        this.audioSourcesReceiving[audioSource].time = audioSource.time / 5f;
                                    }
                                    Plugin.Log.LogInfo(string.Format("sourcetime: {0}", this.audioSourcesReceiving[audioSource].time));
                                }
                                else
                                {
                                    this.audioSourcesReceiving[audioSource].time = audioSource.time;
                                }
                                this.audioSourcesReceiving[audioSource].spatialBlend = 1f;
                                this.audioSourcesReceiving[audioSource].Play();
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.LogInfo(string.Format("Error while playing audio clip in phone. Clip name: {0} object: {1}; time: {2}; {3}", new object[]
                                {
                                        audioSource.clip.name,
                                        audioSource.gameObject.name,
                                        audioSource.time,
                                        ex
                                }));
                            }
                        }
                        float num = Vector3.Distance(audioSource.transform.position, callerPhone.transform.position);
                        Plugin.Log.LogInfo(string.Format("Receiving audiosource with name: {0}; recording distance: {1}", audioSource.gameObject.name, num));
                        if (num > this.recordingRange + 7f)
                        {
                            Plugin.Log.LogInfo("Recording distance out of range; removing audio with name: " + audioSource.gameObject.name);
                            AudioSource obj;
                            this.audioSourcesReceiving.Remove(audioSource, out obj);
                            UnityEngine.Object.Destroy(obj);
                            callerPhone.audioSourcesToReplay.RemoveAt(j);
                        }
                        else
                        {
                            this.audioSourcesReceiving[audioSource].volume = Mathf.Lerp(this.maxVolume, 0f, num / (this.recordingRange + 3f));
                            if ((audioSource.isPlaying && !this.audioSourcesReceiving[audioSource].isPlaying) || audioSource.clip != this.audioSourcesReceiving[audioSource].clip)
                            {
                                this.audioSourcesReceiving[audioSource].clip = audioSource.clip;
                                this.audioSourcesReceiving[audioSource].Play();
                            }
                            else if (!audioSource.isPlaying)
                            {
                                this.audioSourcesReceiving[audioSource].Stop();
                            }
                            this.audioSourcesReceiving[audioSource].time = audioSource.time;
                        }
                    }
                }
            }
            else if (activeCall == null)
            {
                activeCaller = -1;
                foreach (AudioSource key in callerPhone.audioSourcesToReplay)
                {
                    if (this.audioSourcesReceiving.ContainsKey(key))
                    {
                        AudioSource obj;
                        this.audioSourcesReceiving.Remove(key, out obj);
                        UnityEngine.Object.Destroy(obj);
                    }
                }
                callerPhone.audioSourcesToReplay.Clear();
            }
        }

        private void UpdatePlayerVoices()
        {
            if (player == null || GameNetworkManager.Instance == null || player != GameNetworkManager.Instance.localPlayerController || GameNetworkManager.Instance.localPlayerController == null || !isLocalPhone)
            {
                return;
            }

            if (activeCaller != -1)
            {
                PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[activeCaller];

                applyPhoneVoiceEffect(caller);

                // Later we'll hear others in the background
                for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                {

                }
            }
        }

        [ClientRpc]
        public void SetNewPhoneNumberClientRpc(string number)
        {
            if (player == null)
            {
                player = transform.parent.GetComponent<PlayerControllerB>();
            }

            Plugin.Log.LogInfo("New Phone Setup");

            this.phoneNumber = number;

            if (this.IsOwner)
            {
                Plugin.Log.LogInfo("This is our phone, setting local");
                PhoneNetworkHandler.Instance.localPhone = this;
                isLocalPhone = true;
            }

            if (isLocalPhone) Plugin.Log.LogInfo("New Phone for " + player.name + "! Your number is: " + phoneNumber);
        }

        [ClientRpc]
        public void InvalidCallClientRpc()
        {
            Plugin.Log.LogInfo("Invalid number.");

            PlayHangupSound();
            outgoingCall = null;
        }

        [ClientRpc]
        public void RecieveCallClientRpc(int callerId, string callerNumber)
        {
            Plugin.Log.LogInfo("Someone is calling with ID " + callerId + " with number " + callerNumber);
            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[callerId];

            RoundManager.Instance.PlayAudibleNoise(player.serverPlayerPosition, 16f, 0.9f, 0, player.isInElevator && StartOfRound.Instance.hangarDoorsClosed, 0);
            ringAudio.Play();

            if (isLocalPhone) Plugin.Log.LogInfo("You've got a call from " + caller.name + " with number " + callerNumber);

            if (incomingCall == null && activeCall == null)
            {
                Plugin.Log.LogInfo("Updating call values for " + player.name);
                incomingCall = callerNumber;
                incomingCaller = callerId;
            }
            else if (isLocalPhone)
            {
                // Line is busy
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(callerNumber);
            }
        }

        [ClientRpc]
        public void CallAcceptedClientRpc(int accepterId, string accepterNumber)
        {
            PlayerControllerB accepter = StartOfRound.Instance.allPlayerScripts[accepterId];

            if (isLocalPhone) Plugin.Log.LogInfo("Your call was accepted by " + accepter.name + " with number " + accepterNumber);

            if (outgoingCall != accepterNumber)
            {
                Plugin.Log.LogInfo("We got a call we never made? " + player.name);
                // Whoops, how did we get this call? Send back a no.
                return;
            }

            ringAudio.Stop();
            PlayPickupSound();

            outgoingCall = null;
            activeCall = accepterNumber;
            activeCaller = accepterId;
        }

        [ClientRpc]
        public void HangupCallClientRpc(int cancellerId, string cancellerNumber)
        {
            PlayerControllerB canceller = StartOfRound.Instance.allPlayerScripts[cancellerId];

            if (isLocalPhone) Plugin.Log.LogInfo("Your call was hung up by " + canceller.name + " with number " + cancellerNumber);

            if (activeCall == cancellerNumber)
            {
                PlayHangupSound();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
            else if (outgoingCall == cancellerNumber)
            {
                // outgoing call was invalid
                outgoingCall = null;
            }
            else if (incomingCall == cancellerNumber)
            {
                // incoming call cancelled
                ringAudio.Stop();
                thisAudio.Stop();
                incomingCall = null;
            }
            else
            {
                // No you can't hang up a call you're not on.
            }
        }

        public void UpdateCallValues()
        {
            UpdateCallValuesServerRpc(
                   outgoingCall == null ? -1 : int.Parse(outgoingCall),
                   incomingCall == null ? -1 : int.Parse(incomingCall),
                   activeCall == null ? -1 : int.Parse(activeCall),
                   incomingCaller,
                   activeCaller);
        }

        [ServerRpc]
        public void UpdateCallValuesServerRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, int incomingCallerUpdate, int activeCallerUpdate)
        {
            UpdateCallValuesClientRpc(outgoingCallUpdate, incomingCallUpdate, activeCallUpdate, incomingCallerUpdate, activeCallerUpdate);
        }

        [ClientRpc]
        public void UpdateCallValuesClientRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, int incomingCallerUpdate, int activeCallerUpdate)
        {
            // A little messy? I don't like this.
            outgoingCall = outgoingCallUpdate == -1 ? null : outgoingCallUpdate.ToString("D4");
            incomingCall = incomingCallUpdate == -1 ? null : incomingCallUpdate.ToString("D4");
            activeCall = activeCallUpdate == -1 ? null : activeCallUpdate.ToString("D4");
            incomingCaller = incomingCallerUpdate;
            activeCaller = activeCallerUpdate;
        }

        [ServerRpc]
        public void StopRingingServerRpc()
        {
            StopRingingClientRpc();
        }

        [ClientRpc]
        public void StopRingingClientRpc()
        {
            ringAudio.Stop();
        }

        private static void applyPhoneVoiceEffect(PlayerControllerB playerController)
        {
            AudioSource currentVoiceChatAudioSource = playerController.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = true;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = true;

            currentVoiceChatAudioSource.spatialBlend = 0f;
            playerController.currentVoiceChatIngameSettings.set2D = true;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[playerController.playerClientId];
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = GameNetworkManager.Instance.localPlayerController.isPlayerDead ? 0f : 0.4f;
            occludeAudio.lowPassOverride = 4000f;
            lowPass.lowpassResonanceQ = 3f;
        }
    }
}