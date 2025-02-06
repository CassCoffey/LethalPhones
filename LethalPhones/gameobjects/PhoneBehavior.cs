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

namespace Scoops.misc
{
    public class PhoneBehavior : NetworkBehaviour
    {
        protected NetworkVariable<short> incomingCall = new NetworkVariable<short>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        protected NetworkVariable<short> outgoingCall = new NetworkVariable<short>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        protected NetworkVariable<short> activeCall = new NetworkVariable<short>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        protected NetworkVariable<ulong> incomingCaller = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        protected NetworkVariable<ulong> outgoingCaller = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        protected NetworkVariable<ulong> activeCaller = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public short phoneNumber;
        public string phoneSkinId;
        public string phoneCharmId;
        public string phoneRingtoneId;

        public Transform recordPos;
        public Transform playPos;

        protected AudioSource ringAudio;
        protected AudioSource thisAudio;
        protected AudioSource staticAudio;
        public AudioSourceStorage staticStorage;

        protected Queue<int> dialedNumbers = new Queue<int>(4);

        protected float updateInterval;
        protected float localInterference;
        protected float temporaryInterference;

        protected IEnumerator activePhoneRingCoroutine;

        public virtual void Start()
        {
            this.thisAudio = GetComponent<AudioSource>();
            this.staticAudio = transform.Find("Target").gameObject.GetComponent<AudioSource>();

            if (this.recordPos == null || this.playPos == null)
            {
                this.recordPos = transform;
                this.playPos = transform;
            }

            AudioSourceManager.RegisterPhone(this);
        }

        public virtual void Update()
        {
            localInterference = ConnectionQualityManager.GetLocalInterference(this);

            if (temporaryInterference > 0f)
            {
                // Should take 45 seconds to repair a full interference bar
                temporaryInterference -= (1f / Config.connectionHealTime.Value) * Time.deltaTime;

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
            return outgoingCall.Value != -1 || incomingCall.Value != -1 || activeCall.Value != -1;
        }

        public bool IsActive()
        {
            return activeCall.Value != -1;
        }

        public float GetTotalInterference()
        {
            return Mathf.Clamp01(localInterference + ConnectionQualityManager.AtmosphericInterference + temporaryInterference);
        }

        public AudioSource GetStaticAudioSource()
        {
            return staticAudio;
        }

        public PhoneBehavior GetCallerPhone()
        {
            NetworkObject otherPhone = GetNetworkObject(activeCaller.Value);
            if (otherPhone.gameObject != null)
            {
                return otherPhone.GetComponent<PhoneBehavior>();
            }
            return null;
        }

        public short GetRandomExistingPhoneNumber()
        {
            if (PhoneNetworkHandler.allPhoneBehaviors.Count > 0)
            {
                PhoneBehavior randPhone = PhoneNetworkHandler.allPhoneBehaviors[UnityEngine.Random.Range(0, PhoneNetworkHandler.allPhoneBehaviors.Count)];

                return randPhone.phoneNumber;
            }

            return -1;
        }

        public virtual bool IsBeingSpectated()
        {
            return false;
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
            ApplyTemporaryInterferenceServerRpc(change);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ApplyTemporaryInterferenceServerRpc(float change)
        {
            ApplyTemporaryInterferenceClientRpc(change);
        }

        [ClientRpc]
        public void ApplyTemporaryInterferenceClientRpc(float change)
        {
            temporaryInterference += change;
        }

        public virtual void CallNumber(short number)
        {
            outgoingCall.Value = number;
            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number, NetworkObjectId);
        }

        [ClientRpc]
        public virtual void SetNewPhoneNumberClientRpc(short number)
        {
            this.phoneNumber = number;
        }

        [ClientRpc]
        public virtual void InvalidCallClientRpc(string reason)
        {
            if (!IsOwner) return;

            outgoingCall.Value = -1;
        }

        [ClientRpc]
        public void RecieveCallClientRpc(ulong callerId, short callerNumber)
        {
            if (incomingCall.Value == -1)
            {
                StartRinging();

                if (IsOwner)
                {
                    incomingCall.Value = callerNumber;
                    incomingCaller.Value = callerId;
                }
                dialedNumbers.Clear();
                UpdateCallingUI();
            }
            else if (IsOwner)
            {
                PhoneNetworkHandler.Instance.LineBusyServerRpc(callerNumber);
            }
        }

        [ClientRpc]
        public void TransferCallClientRpc(ulong callerId, short callerNumber, short transferNumber)
        {
            if (activeCall.Value == callerNumber)
            {
                PlayHangupSound();

                if (IsOwner)
                {
                    activeCall.Value = -1;

                    if (transferNumber != phoneNumber)
                    {
                        CallNumber(transferNumber);
                    }
                }

                UpdateCallingUI();
            }
        }

        [ClientRpc]
        public void CallAcceptedClientRpc(ulong accepterId, short accepterNumber)
        {
            if (outgoingCall.Value != accepterNumber)
            {
                // Whoops, how did we get this call? Send back a no.
                return;
            }

            StopOutgoingRinging();
            PlayPickupSound();

            if (IsOwner)
            {
                outgoingCall.Value = -1;
                activeCall.Value = accepterNumber;
                activeCaller.Value = accepterId;
            }
            
            UpdateCallingUI();
        }

        [ClientRpc]
        public void HangupCallClientRpc(ulong cancellerId, short cancellerNumber)
        {
            if (activeCall.Value == cancellerNumber)
            {
                PlayHangupSound();
                if (IsOwner)
                {
                    activeCall.Value = -1;
                }
                UpdateCallingUI();
            }
            else if (outgoingCall.Value == cancellerNumber)
            {
                // Line busy
                PlayHangupSound();
                if (IsOwner)
                {
                    outgoingCall.Value = -1;
                }
                UpdateCallingUI();
            }
            else if (incomingCall.Value == cancellerNumber)
            {
                // incoming call cancelled
                StopRinging();
                thisAudio.Stop();
                if (IsOwner)
                {
                    incomingCall.Value = -1;
                }
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
        public void PropogateInformationClientRpc(short number, string skinId, string charmId, string ringtoneId)
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

            base.OnDestroy();
        }
    }
}