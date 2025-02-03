using GameNetcodeStuff;
using Scoops.compatability;
using Scoops.customization;
using Scoops.gameobjects;
using Scoops.patch;
using Scoops.service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.Netcode;
using Unity.Profiling;
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

        public Transform recordPos;
        public Transform playPos;

        protected AudioSource ringAudio;
        protected AudioSource thisAudio;
        protected AudioSource staticAudio;
        public AudioSourceStorage staticStorage;

        protected string incomingCall = null;
        protected string activeCall = null;
        protected string outgoingCall = null;

        protected Queue<int> dialedNumbers = new Queue<int>(4);

        protected float updateInterval;
        protected float localInterference;
        protected float temporaryInterference;

        protected IEnumerator activePhoneRingCoroutine;

        public virtual void Start()
        {
            this.thisAudio = GetComponent<AudioSource>();
            this.staticAudio = transform.Find("Target").gameObject.GetComponent<AudioSource>();

            this.recordPos = transform;
            this.playPos = transform;

            AudioSourceManager.RegisterPhone(this);
        }

        public virtual void Update()
        {
            if (this.activeCall == null || spectatorClear)
            {
                if (!spectatorClear) activeCaller = 0;

                spectatorClear = false;
            }

            localInterference = ConnectionQualityManager.GetLocalInterference(this);

            if (temporaryInterference > 0f)
            {
                // Should take 30 seconds to repair a full interference bar
                temporaryInterference -= (1f / 30f) * Time.deltaTime;

                if (temporaryInterference < 0f)
                {
                    temporaryInterference = 0f;
                }
            }
        }

        public virtual string GetPhoneName()
        {
            return "???";
        }

        public bool IsBusy()
        {
            return outgoingCall != null || incomingCall != null || activeCall != null;
        }

        public bool IsActive()
        {
            return activeCall != null;
        }

        public float GetTotalInterference()
        {
            return localInterference + ConnectionQualityManager.AtmosphericInterference + temporaryInterference;
        }

        public AudioSource GetStaticAudioSource()
        {
            return staticAudio;
        }

        public PhoneBehavior GetCallerPhone()
        {
            return GetNetworkObject(activeCaller).GetComponent<PhoneBehavior>();
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

        public virtual void CallNumber(string number)
        {
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

        public virtual bool IsBeingSpectated()
        {
            return false;
        }

        public virtual void UpdateCallValues()
        {
            // Nothing by default
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
            return false;
        }

        public virtual bool PhoneInsideShip()
        {
            return false;
        }

        public void PropogateInformation()
        {
            PropogateInformationClientRpc(this.phoneNumber, this.phoneSkinId, this.phoneCharmId, this.phoneRingtoneId);
        }

        public void ApplyTemporaryInterference(float change)
        {
            temporaryInterference += change;
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
        public void TransferCallClientRpc(ulong callerId, string callerNumber, string transferNumber)
        {
            if (!IsOwner)
            {
                return;
            }

            if (activeCall == callerNumber)
            {
                PlayHangupSoundServerRpc();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
                UpdateCallingUI();

                if (transferNumber != phoneNumber)
                {
                    CallNumber(transferNumber);
                }

                UpdateCallValues();
            }
            else
            {
                // No you can't transfer a call you're not on.
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

        [ServerRpc(RequireOwnership = false)]
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

        [ServerRpc(RequireOwnership = false)]
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

        [ServerRpc(RequireOwnership = false)]
        public void StopRingingServerRpc()
        {
            StopRingingClientRpc();
        }

        [ClientRpc]
        public void StopRingingClientRpc()
        {
            StopRinging();
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartOutgoingRingingServerRpc()
        {
            StartOutgoingRingingClientRpc();
        }

        [ClientRpc]
        public void StartOutgoingRingingClientRpc()
        {
            StartOutgoingRinging();
        }

        [ServerRpc(RequireOwnership = false)]
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

        protected virtual void StopRinging()
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
            AudioSourceManager.DeregisterPhone(this);

            if (staticAudio != null)
            {
                if (staticAudio.isPlaying)
                {
                    staticAudio.Stop();
                }
            }

            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
        }
    }
}