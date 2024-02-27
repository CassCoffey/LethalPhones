using GameNetcodeStuff;
using Scoops.service;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.misc
{
    public class PlayerPhone : NetworkBehaviour
    {
        public PlayerControllerB player;
        public AudioSource localPhoneAudio;
        public string phoneNumber;
        public bool toggled = false;

        public Queue<int> dialedNumbers;

        public int activeCaller;
        public int incomingCaller;

        public string incomingCall;
        public string activeCall;
        public string outgoingCall;

        private float recordingRange = 6f;
        private float maxVolume = 0.6f;

        private List<AudioSource> audioSourcesToReplay = new List<AudioSource>();
        private Dictionary<AudioSource, AudioSource> audioSourcesReceiving = new Dictionary<AudioSource, AudioSource>();
        private int audioSourcesToReplayLastFrameCount;

        public Collider listenCollider;
        public Collider[] collidersInRange = new Collider[30];

        private float cleanUpInterval;
        private float updateInterval;

        public void Init(PlayerControllerB player, string phoneNumber)
        {
            this.player = player;
            this.phoneNumber = phoneNumber;

            dialedNumbers = new Queue<int>(4);
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
                PlayPickupSound();
                Plugin.Log.LogInfo("Picking up: " + activeCall);
            }
            else
            {
                // No calls of any sort are happening, make a new one
                CallDialedNumber();
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

            localPhoneAudio.Play();
            PhoneNetworkHandler.Instance.MakeOutgoingCall(number);
            outgoingCall = number;

            dialedNumbers.Clear();
        }

        public void RecieveCall(string number, int playerId)
        {
            if (incomingCall == null && activeCall == null)
            {
                incomingCall = number;
                incomingCaller = playerId;
            }
            else
            {
                // Line is busy
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(number);
            }
        }

        public void OutgoingCallAccepted(string number, int playerId)
        {
            if (outgoingCall != number)
            {
                // Whoops, how did we get this call? Send back a no.
                return;
            }

            PlayPickupSound();

            outgoingCall = null;
            activeCall = number;
            activeCaller = playerId;
        }

        public void HangUpCall(string number)
        {
            if (activeCall == number)
            {
                PlayHangupSound();
                activeCall = null;
            }
            else if (outgoingCall == number)
            {
                // outgoing call was invalid
                outgoingCall = null;
            }
            else if (incomingCall == number)
            {
                // incoming call cancelled
                localPhoneAudio.Stop();
                incomingCall = null;
            }
            else
            {
                // No you can't hang up a call you're not on.
            }
        }

        public void InvalidNumber()
        {
            localPhoneAudio.Stop();
            localPhoneAudio.PlayOneShot(PhoneSoundManager.phoneHangup);
            outgoingCall = null;
        }

        public void PlayHangupSound()
        {
            localPhoneAudio.Stop();
            localPhoneAudio.PlayOneShot(PhoneSoundManager.phoneHangup);
        }

        public void PlayPickupSound()
        {
            localPhoneAudio.Stop();
            localPhoneAudio.PlayOneShot(PhoneSoundManager.phonePickup);
        }

        private void GetAllAudioSourcesToReplay()
        {
            if (activeCall == null)
            {
                return;
            }
            int num = Physics.OverlapSphereNonAlloc(base.transform.position, this.recordingRange, this.collidersInRange, 11010632, QueryTriggerInteraction.Collide);
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
            PlayerPhone callerPhone = caller.transform.Find("CellPhonePrefab(Clone)").GetComponent<PlayerPhone>();

            if (activeCall != null)
            {
                for (int j = callerPhone.audioSourcesToReplay.Count - 1; j >= 0; j--)
                {
                    AudioSource audioSource = callerPhone.audioSourcesToReplay[j];
                    if (!(audioSource == null))
                    {
                        if (this.audioSourcesReceiving.TryAdd(audioSource, null))
                        {
                            this.audioSourcesReceiving[audioSource] = this.localPhoneAudio.gameObject.AddComponent<AudioSource>();
                            this.audioSourcesReceiving[audioSource].clip = audioSource.clip;
                            try
                            {
                                if (audioSource.time >= audioSource.clip.length)
                                {
                                    Debug.Log(string.Format("phone: {0}, {1}, {2}", audioSource.time, audioSource.clip.length, audioSource.clip.name));
                                    if (audioSource.time - 0.05f < audioSource.clip.length)
                                    {
                                        this.audioSourcesReceiving[audioSource].time = Mathf.Clamp(audioSource.time - 0.05f, 0f, 1000f);
                                    }
                                    else
                                    {
                                        this.audioSourcesReceiving[audioSource].time = audioSource.time / 5f;
                                    }
                                    Debug.Log(string.Format("sourcetime: {0}", this.audioSourcesReceiving[audioSource].time));
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
                                Debug.LogError(string.Format("Error while playing audio clip in phone. Clip name: {0} object: {1}; time: {2}; {3}", new object[]
                                {
                                        audioSource.clip.name,
                                        audioSource.gameObject.name,
                                        audioSource.time,
                                        ex
                                }));
                            }
                        }
                        float num = Vector3.Distance(audioSource.transform.position, callerPhone.transform.position);
                        Debug.Log(string.Format("Receiving audiosource with name: {0}; recording distance: {1}", audioSource.gameObject.name, num));
                        if (num > this.recordingRange + 7f)
                        {
                            Debug.Log("Recording distance out of range; removing audio with name: " + audioSource.gameObject.name);
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
    }
}