using Scoops.compatability;
using Scoops.misc;
using Scoops.patch;
using Scoops.service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static Unity.Collections.LowLevel.Unsafe.BurstLike;
using Unity.Netcode;
using UnityEngine;
using LethalLib.Modules;

namespace Scoops.gameobjects
{
    public class EnemyPhone : PhoneBehavior
    {
        public EnemyAI enemy;

        protected bool preppingCall = false;
        protected bool preppingPickup = false;
        protected bool preppingHangup = false;

        protected IEnumerator activePickupDelayCoroutine;
        protected IEnumerator activeCallDelayCoroutine;
        protected IEnumerator activeCallTimeoutCoroutine;
        protected IEnumerator activeCallHangupCoroutine;

        public override void Start()
        {
            base.Start();

            this.enemy = transform.parent.GetComponent<EnemyAI>();
            this.ringAudio = this.GetComponent<AudioSource>();
            ringAudio.volume = Config.ringtoneVolume.Value;
        }

        public virtual void Death()
        {
            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
            if (activePickupDelayCoroutine != null) StopCoroutine(activePickupDelayCoroutine);
            if (activeCallDelayCoroutine != null) StopCoroutine(activeCallDelayCoroutine);
            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);
            if (activeCallHangupCoroutine != null) StopCoroutine(activeCallHangupCoroutine);

            if (IsOwner)
            {
                if (activeCall != null)
                {
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall, NetworkObjectId);
                    activeCall = null;
                    StartOfRound.Instance.UpdatePlayerVoiceEffects();
                }
                if (outgoingCall != null)
                {
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall, NetworkObjectId);
                    outgoingCall = null;
                }
                if (incomingCall != null)
                {
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(incomingCall, NetworkObjectId);
                    incomingCall = null;
                }

                PhoneNetworkHandler.Instance.RemoveNumber(phoneNumber);
            }
        }

        public override void Update()
        {
            if (IsOwner && !enemy.isEnemyDead)
            {
                if (outgoingCall == null && activeCall == null)
                {
                    if (incomingCall == null)
                    {
                        // we NEED to be on a call or we'll DIE
                        if (!preppingCall)
                        {
                            activeCallDelayCoroutine = CallDelayCoroutine(UnityEngine.Random.Range(Config.minPhoneBugInterval.Value, Config.maxPhoneBugInterval.Value));
                            StartCoroutine(activeCallDelayCoroutine);
                        }
                    }
                    else if (!preppingPickup)
                    {
                        activePickupDelayCoroutine = PickupDelayCoroutine(2f);
                        StartCoroutine(activePickupDelayCoroutine);
                    }
                }
            }

            base.Update();
        }

        public void LateUpdate()
        {
            if (MirageCompat.Enabled && activeCall != null)
            {
                MirageCompat.UnmuteEnemy(enemy);
            }
        }

        public override bool PhoneInsideFactory()
        {
            return true;
        }

        protected virtual IEnumerator PickupDelayCoroutine(float time)
        {
            preppingPickup = true;
            yield return new WaitForSeconds(time);

            if (incomingCall != null && outgoingCall == null && activeCall == null && !enemy.isEnemyDead)
            {
                activeCall = incomingCall;
                activeCaller = incomingCaller;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall, NetworkObjectId);
                StopRingingServerRpc();
                PlayPickupSoundServerRpc();
                UpdateCallValues();
            }

            preppingPickup = false;
        }

        protected virtual IEnumerator CallDelayCoroutine(float time)
        {
            preppingCall = true;
            yield return new WaitForSeconds(time);

            if (incomingCall == null && outgoingCall == null && activeCall == null && !enemy.isEnemyDead)
            {
                CallRandomNumber();
                if (outgoingCall != null)
                {
                    activeCallTimeoutCoroutine = CallTimeoutCoroutine(outgoingCall);
                    StartCoroutine(activeCallTimeoutCoroutine);
                    UpdateCallValues();
                }
            }
            preppingCall = false;
        }

        protected virtual IEnumerator CallTimeoutCoroutine(string number)
        {
            yield return new WaitForSeconds(14f);

            if (outgoingCall == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall, NetworkObjectId);
                outgoingCall = null;
                UpdateCallValues();
            }
        }

        protected virtual IEnumerator CallHangupCoroutine(string number, float time)
        {
            yield return new WaitForSeconds(time);

            if (activeCall == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall, NetworkObjectId);
                activeCall = null;
                UpdateCallValues();
            }
            preppingHangup = false;
        }

        public override void UpdateCallValues()
        {
            UpdateCallValuesServerRpc(
                   outgoingCall == null ? -1 : int.Parse(outgoingCall),
                   incomingCall == null ? -1 : int.Parse(incomingCall),
                   activeCall == null ? -1 : int.Parse(activeCall),
                   incomingCaller,
                   activeCaller);
        }

        [ServerRpc]
        public void UpdateCallValuesServerRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong activeCallerUpdate)
        {
            UpdateCallValuesClientRpc(outgoingCallUpdate, incomingCallUpdate, activeCallUpdate, incomingCallerUpdate, activeCallerUpdate);
        }

        [ClientRpc]
        public void UpdateCallValuesClientRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong activeCallerUpdate)
        {
            outgoingCall = outgoingCallUpdate == -1 ? null : outgoingCallUpdate.ToString("D4");
            incomingCall = incomingCallUpdate == -1 ? null : incomingCallUpdate.ToString("D4");
            activeCall = activeCallUpdate == -1 ? null : activeCallUpdate.ToString("D4");
            incomingCaller = incomingCallerUpdate;
            activeCaller = activeCallerUpdate;
        }
    }
}
