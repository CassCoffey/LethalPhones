using Dissonance;
using GameNetcodeStuff;
using Scoops.patch;
using Scoops.service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;
using static Scoops.misc.PlayerPhone;

namespace Scoops.misc
{
    public class HoardingPhone : PhoneBehavior
    {
        public HoarderBugAI bug;
        public GameObject serverPhoneModel;

        private float chitterInterval = 0f;
        private float randomChitterTime = 3f;

        private bool preppingCall = false;
        private bool preppingPickup = false;

        private IEnumerator activePickupDelayCoroutine;
        private IEnumerator activeCallDelayCoroutine;
        private IEnumerator activeCallTimeoutCoroutine;

        public override void Start()
        {
            base.Start();

            this.bug = transform.parent.GetComponent<HoarderBugAI>();
            this.ringAudio = this.GetComponent<AudioSource>();

            GameObject serverPhoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("BugServerPhoneModel");
            serverPhoneModel = GameObject.Instantiate(serverPhoneModelPrefab, bug.animationContainer.Find("Armature").Find("Abdomen").Find("Chest").Find("Head").Find("Bone.03").Find("Bone.04").Find("Bone.04_end"), false);
        }

        public void Death()
        {
            HoardingBugPhonePatch.phoneBugs--;

            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
            if (activePickupDelayCoroutine != null) StopCoroutine(activePickupDelayCoroutine);
            if (activeCallDelayCoroutine != null) StopCoroutine(activeCallDelayCoroutine);
            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);

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
            if (IsOwner && !bug.isEnemyDead)
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

            if (activeCall != null)
            {
                if (this.chitterInterval >= randomChitterTime)
                {
                    this.chitterInterval = 0f;
                    randomChitterTime = UnityEngine.Random.Range(4f, 8f);
                    RoundManager.PlayRandomClip(bug.creatureVoice, bug.chitterSFX, true, 1f, 0);
                }
                else
                {
                    this.chitterInterval += Time.deltaTime;
                }
            }


            base.Update();
        }

        private IEnumerator PickupDelayCoroutine(float time)
        {
            preppingPickup = true;
            yield return new WaitForSeconds(time);

            if (incomingCall != null && outgoingCall == null && activeCall == null && !bug.isEnemyDead)
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

        private IEnumerator CallDelayCoroutine(float time)
        {
            preppingCall = true;
            yield return new WaitForSeconds(time);

            if (incomingCall == null && outgoingCall == null && activeCall == null && !bug.isEnemyDead)
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

        private IEnumerator CallTimeoutCoroutine(string number)
        {
            yield return new WaitForSeconds(14f);

            if (outgoingCall == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall, NetworkObjectId);
                outgoingCall = null;
                UpdateCallValues();
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