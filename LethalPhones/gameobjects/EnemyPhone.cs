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
                if (activeCall.Value != -1)
                {
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall.Value, NetworkObjectId);
                    activeCall.Value = -1;
                }
                if (outgoingCall.Value != -1)
                {
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall.Value, NetworkObjectId);
                    outgoingCall.Value = -1;
                }
                if (incomingCall.Value != -1)
                {
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(incomingCall.Value, NetworkObjectId);
                    incomingCall.Value = -1;
                }

                PhoneNetworkHandler.Instance.RemoveNumber(phoneNumber);
            }
        }

        public override void Update()
        {
            if (IsOwner && !enemy.isEnemyDead)
            {
                if (outgoingCall.Value == -1 && activeCall.Value == -1)
                {
                    if (incomingCall.Value == -1)
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
            if (MirageCompat.Enabled && activeCall.Value != -1)
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

            if (!IsBusy() && !enemy.isEnemyDead)
            {
                activeCall.Value = incomingCall.Value;
                activeCaller.Value = incomingCaller.Value;
                incomingCall.Value = -1;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall.Value, NetworkObjectId);
                StopRingingServerRpc();
                PlayPickupSoundServerRpc();
            }

            preppingPickup = false;
        }

        protected virtual IEnumerator CallDelayCoroutine(float time)
        {
            preppingCall = true;
            yield return new WaitForSeconds(time);

            if (!IsBusy() && !enemy.isEnemyDead)
            {
                short number = GetRandomExistingPhoneNumber();

                if (number != -1)
                {
                    outgoingCall.Value = number;
                    PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(outgoingCall.Value, NetworkObjectId);
                    activeCallTimeoutCoroutine = CallTimeoutCoroutine(outgoingCall.Value);
                    StartCoroutine(activeCallTimeoutCoroutine);
                }
            }
            preppingCall = false;
        }

        protected virtual IEnumerator CallTimeoutCoroutine(short number)
        {
            yield return new WaitForSeconds(14f);

            if (outgoingCall.Value == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall.Value, NetworkObjectId);
                outgoingCall.Value = -1;
            }
        }

        protected virtual IEnumerator CallHangupCoroutine(short number, float time)
        {
            yield return new WaitForSeconds(time);

            if (activeCall.Value == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall.Value, NetworkObjectId);
                activeCall.Value = -1;
            }
            preppingHangup = false;
        }
    }
}
