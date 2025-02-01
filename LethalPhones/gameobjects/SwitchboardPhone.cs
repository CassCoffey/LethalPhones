﻿using Scoops.misc;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Scoops.service;
using Unity.Netcode;
using UnityEngine.UI;
using GameNetcodeStuff;
using System.Collections;
using Scoops.customization;
using Scoops.patch;
using static UnityEngine.CullingGroup;
using LethalLib.Modules;

namespace Scoops.gameobjects
{
    public class SwitchboardPhone : PhoneBehavior
    {
        public NetworkVariable<ulong> switchboardOperatorId = new NetworkVariable<ulong>();
        public NetworkVariable<bool> silenced = new NetworkVariable<bool>();
        public NetworkVariable<int> selectedIndex = new NetworkVariable<int>();

        public GameObject headsetMic = null;

        public PlayerControllerB switchboardOperator;

        private List<PhoneBehavior> allPhones;
        private Transform[] phoneInfoArray;
        private Transform activeCallerInfo;
        private Transform operatorInfo;
        private Transform inboundInfo;

        private Transform headphonePos;

        private ulong outgoingCaller;

        private PhoneBehavior selectedPhone = null;

        private float uiUpdateCounter = 0f;
        private const float uiUpdateInterval = 1f;

        private int localSelectedIndex = -1;

        protected IEnumerator activeCallTimeoutCoroutine;

        private bool started = false;

        public override void Start()
        {
            base.Start();

            this.recordPos = transform.Find("HeadphoneCube");
            this.playPos = transform.Find("HeadphoneCube");

            this.ringAudio = transform.Find("RingerAudio").GetComponent<AudioSource>();
            ringAudio.volume = Config.ringtoneVolume.Value;

            phoneInfoArray = new Transform[5];

            Transform NumbersList = transform.Find("SwitchboardScreen/SwitchboardPanel/NumbersPanel/NumbersList");

            phoneInfoArray[0] = NumbersList.Find("PhoneInfoPanel0");
            phoneInfoArray[1] = NumbersList.Find("PhoneInfoPanel1");
            phoneInfoArray[2] = NumbersList.Find("PhoneInfoPanel2");
            phoneInfoArray[3] = NumbersList.Find("PhoneInfoPanel3");
            phoneInfoArray[4] = NumbersList.Find("PhoneInfoPanel4");

            activeCallerInfo = transform.Find("SwitchboardScreen/SwitchboardPanel/CallerPanel");
            operatorInfo = transform.Find("SwitchboardScreen/SwitchboardPanel/OperatorPanel");
            inboundInfo = transform.Find("SwitchboardScreen/SwitchboardPanel/NumbersPanel/InboundPanel");

            headphonePos = transform.Find("HeadphoneCube");

            allPhones = new List<PhoneBehavior>();

            transform.Find("UpCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                ChangeSelection(-1);
            });
            transform.Find("DownCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                ChangeSelection(1);
            });

            transform.Find("GreenButtonCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                CallButtonPressed();
            });
            transform.Find("YellowButtonCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                TransferButtonPressed();
            });
            transform.Find("RedButtonCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                HangupButtonPressed();
            });

            transform.Find("RingerCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                VolumeSwitch();
            });

            transform.Find("HeadphoneCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                OperatorSwitch(player);
            });

            transform.Find("SwitchboardMesh/SwitchboardTape/TapeCanvas/Text (TMP)").GetComponent<TextMeshProUGUI>().text = Config.switchboardNumber.Value;

            if (IsOwner)
            {
                PhoneNetworkHandler.Instance.RegisterSwitchboard(this.NetworkObjectId);
            } 
            else
            {
                PhoneNetworkHandler.Instance.RequestSwitchboardUpdates();
            }

            UpdateCallingUI();

            started = true;
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                switchboardOperatorId.Value = this.NetworkObjectId;
                silenced.Value = false;
                selectedIndex.Value = -1;
            } 
            else
            {
                if (switchboardOperatorId.Value != this.NetworkObjectId)
                {
                    OnOperatorChanged(this.NetworkObjectId, switchboardOperatorId.Value);
                }
                switchboardOperatorId.OnValueChanged += OnOperatorChanged;

                if (silenced.Value != false)
                {
                    transform.Find("RingerCube").GetComponent<AnimatedObjectTrigger>().boolValue = false;
                    transform.Find("SwitchboardMesh").GetComponent<Animator>().SetBool("ringer", false);
                    OnVolumeSwitched(false, true);
                }
                silenced.OnValueChanged += OnVolumeSwitched;

                if (selectedIndex.Value != -1)
                {
                    OnSelectedIndexChanged(-1, selectedIndex.Value);
                }
                selectedIndex.OnValueChanged += OnSelectedIndexChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            switchboardOperatorId.OnValueChanged -= OnOperatorChanged;
            selectedIndex.OnValueChanged -= OnSelectedIndexChanged;
            silenced.OnValueChanged -= OnVolumeSwitched;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
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
            }

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                PhoneNetworkHandler.Instance.DeleteSwitchboard();
            }
        }

        public override void Update()
        {
            base.Update();

            uiUpdateCounter += Time.deltaTime;
            if (uiUpdateCounter >= uiUpdateInterval)
            {
                uiUpdateCounter = 0f;
                UpdateCallingUI();
            }

            if (IsOwner)
            {
                if (switchboardOperator != null)
                {
                    if (!switchboardOperator.isInHangarShipRoom || switchboardOperator.isPlayerDead)
                    {
                        switchboardOperatorId.Value = this.NetworkObjectId;
                        switchboardOperator = null;
                        ToggleOperator(false);
                    }
                }
            }
        }

        public override string GetPhoneName()
        {
            return "Switchboard";
        }

        private void ChangeSelection(int change)
        {
            // do a local selection change to avoid lag
            ChangeSelectionLocal(change);

            ChangeSelectionServerRpc(change);
        }

        private void ChangeSelectionLocal(int change)
        {
            if (incomingCall != null)
            {
                return;
            }

            if (allPhones.Count == 0)
            {
                localSelectedIndex = 0;
                selectedPhone = null;
            }
            else
            {
                int newValue = localSelectedIndex + change;
                while (newValue < 0)
                {
                    newValue = allPhones.Count - 1;
                }
                while (newValue >= allPhones.Count)
                {
                    newValue = 0;
                }

                selectedPhone = allPhones[newValue];
                localSelectedIndex = newValue;

                UpdateCallingUI();
            }
        }

        protected override void UpdateCallingUI()
        {
            UpdateInfoList();
            UpdateActiveCallerUI();
            UpdateOperatorUI();
            UpdateInboundUI();

            if (headsetMic != null)
            {
                Material[] mats = headsetMic.GetComponent<Renderer>().materials;

                if ((activeCall != null || incomingCall != null || outgoingCall != null) && mats[1] != PhoneAssetManager.greenLight)
                {
                    mats[1] = PhoneAssetManager.greenLight;
                    headsetMic.GetComponent<Renderer>().SetMaterialArray(mats);
                }
                else if (!(activeCall != null || incomingCall != null || outgoingCall != null) &&  mats[1] != PhoneAssetManager.offLight)
                {
                    mats[1] = PhoneAssetManager.offLight;
                    headsetMic.GetComponent<Renderer>().SetMaterialArray(mats);
                }
            }
        }

        private void UpdateActiveCallerUI()
        {
            if (activeCall != null)
            {
                PhoneBehavior phone = GetNetworkObject(activeCaller).GetComponent<PhoneBehavior>();

                activeCallerInfo.Find("CallerText").GetComponent<TextMeshProUGUI>().text = "CONNECTED: " + phone.GetPhoneName();
                activeCallerInfo.Find("NumberText").GetComponent<TextMeshProUGUI>().text = activeCall;
            }
            else if (outgoingCall != null)
            {
                PhoneBehavior phone = GetNetworkObject(outgoingCaller).GetComponent<PhoneBehavior>();

                activeCallerInfo.Find("CallerText").GetComponent<TextMeshProUGUI>().text = "DIALING: " + phone.GetPhoneName();
                activeCallerInfo.Find("NumberText").GetComponent<TextMeshProUGUI>().text = outgoingCall;
            }
            else
            {
                activeCallerInfo.Find("CallerText").GetComponent<TextMeshProUGUI>().text = "";
                activeCallerInfo.Find("NumberText").GetComponent<TextMeshProUGUI>().text = "";
            }
        }

        private void UpdateOperatorUI()
        {
            if (switchboardOperator != null)
            {
                operatorInfo.Find("OperatorText").GetComponent<TextMeshProUGUI>().text = "OPERATOR: " + switchboardOperator.playerUsername;
            } 
            else
            {
                operatorInfo.Find("OperatorText").GetComponent<TextMeshProUGUI>().text = "OPERATOR: INACTIVE";
            }
        }

        private void UpdateInboundUI()
        {
            if (incomingCall != null)
            {
                PhoneBehavior phone = GetNetworkObject(incomingCaller).GetComponent<PhoneBehavior>();

                inboundInfo.Find("InboundPanelBack/InboundCallNumber").GetComponent<TextMeshProUGUI>().text = phone.phoneNumber;
                inboundInfo.Find("InboundPanelBack/InboundCallName").GetComponent<TextMeshProUGUI>().text = phone.GetPhoneName();

                inboundInfo.gameObject.SetActive(true);
            } 
            else
            {
                inboundInfo.gameObject.SetActive(false);
            }
        }

        private void UpdateInfoList()
        {
            if (allPhones.Count == 0) return;

            // The selected info box
            for (int i = 0; i < 5; i++)
            {
                int index = localSelectedIndex + i - 2;
                while (index < 0 || index >= allPhones.Count)
                {
                    if (index < 0)
                    {
                        index = allPhones.Count + index;
                    } 
                    else if (index >= allPhones.Count)
                    {
                        index = index - allPhones.Count;
                    }
                }

                phoneInfoArray[i].Find("NameText").GetComponent<TextMeshProUGUI>().text = allPhones[index].GetPhoneName();
                phoneInfoArray[i].Find("NumberText").GetComponent<TextMeshProUGUI>().text = allPhones[index].phoneNumber;
                UpdateInfoStatusIndicator(phoneInfoArray[i].Find("StatusIndicator").GetComponent<Image>(), allPhones[index]);
            }
        }

        private void UpdateInfoStatusIndicator(Image statusIndicator, PhoneBehavior phone)
        {
            if (phone.IsBusy())
            {
                if (activeCall == phone.phoneNumber)
                {
                    statusIndicator.color = Color.green;
                }
                else
                {
                    statusIndicator.color = Color.yellow;
                }
            }
            else
            {
                statusIndicator.color = Color.grey;
            }
        }

        public override void CallNumber(string number)
        {
            StartOutgoingRingingServerRpc();
            outgoingCall = number;
            outgoingCaller = selectedPhone.NetworkObjectId;

            UpdateCallingUI();

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number, NetworkObjectId);

            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);
            activeCallTimeoutCoroutine = CallTimeoutCoroutine(number);
            StartCoroutine(activeCallTimeoutCoroutine);
        }

        public void CallSelectedNumber()
        {
            string number = selectedPhone.phoneNumber;
            if (number == phoneNumber)
            {
                Plugin.Log.LogInfo("You cannot call yourself yet. Messages will be here later.");
                UpdateCallingUI();
                return;
            }

            CallNumber(number);
        }

        public void OperatorSwitch(PlayerControllerB player)
        {
            OperatorSwitchServerRpc(player.NetworkObjectId);
        }

        private void ToggleOperator(bool active)
        {
            transform.Find("SwitchboardHeadphones").GetComponent<Renderer>().enabled = !active;
            headphonePos = active ? switchboardOperator.playerGlobalHead : transform.Find("HeadphoneCube");
            recordPos = headphonePos;
            playPos = headphonePos;

            if (active && switchboardOperator == GameNetworkManager.Instance.localPlayerController)
            {
                headsetMic = GameObject.Instantiate(PhoneAssetManager.headphoneDisplayPrefab, switchboardOperator.localVisorTargetPoint);
            }
            else if (headsetMic != null)
            {
                GameObject.Destroy(headsetMic);
            }

            if (started)
            {
                UpdateCallingUI();
            }
        }

        private void OnSelectedIndexChanged(int prev, int current)
        {
            localSelectedIndex = selectedIndex.Value;

            if (started && allPhones.Count > 0)
            {
                if (localSelectedIndex < 0 || localSelectedIndex >= allPhones.Count)
                {
                    localSelectedIndex = 0;
                }

                selectedPhone = allPhones[selectedIndex.Value];
                UpdateCallingUI();
            }
        }

        private void OnOperatorChanged(ulong prev, ulong current)
        {
            PlayerControllerB newOperator = GetNetworkObject(current).GetComponent<PlayerControllerB>();

            switchboardOperator = newOperator;
            ToggleOperator(newOperator != null);
        }

        public void OnVolumeSwitched(bool prev, bool current)
        {
            SkinnedMeshRenderer renderer = transform.Find("SwitchboardMesh").GetComponent<SkinnedMeshRenderer>();

            if (silenced.Value)
            {
                Material[] mats = renderer.materials;

                mats[2] = PhoneAssetManager.redLight;
                mats[3] = PhoneAssetManager.offLight;

                renderer.SetMaterialArray(mats);
            }
            else
            {
                Material[] mats = renderer.materials;

                mats[2] = PhoneAssetManager.offLight;
                mats[3] = PhoneAssetManager.greenLight;

                renderer.SetMaterialArray(mats);
            }
        }

        public void HangupButtonPressed()
        {
            if (activeCall != null)
            {
                // We're on a call, hang up
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall, NetworkObjectId);
                PlayHangupSoundServerRpc();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
                UpdateCallingUI();
            }
            else if (outgoingCall != null)
            {
                // We're calling, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall, NetworkObjectId);
                PlayHangupSoundServerRpc();
                outgoingCall = null;
                UpdateCallingUI();
            }
            else if (incomingCall != null)
            {
                // We're being called, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(incomingCall, NetworkObjectId);
                StopRingingServerRpc();
                PlayHangupSound();
                incomingCall = null;
                UpdateCallingUI();
            }

            UpdateCallValues();
        }

        public void CallButtonPressed()
        {
            if (incomingCall != null)
            {
                // We have an incoming call, pick up
                if (activeCall != null)
                {
                    //hang up our active first!
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall, NetworkObjectId);
                }
                activeCall = incomingCall;
                activeCaller = incomingCaller;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall, NetworkObjectId);
                StopRingingServerRpc();
                PlayPickupSoundServerRpc();

                UpdateCallingUI();
            }
            else if (outgoingCall == null)
            {
                // No calls of any sort are happening, make a new one
                CallSelectedNumber();
            }

            UpdateCallValues();
        }

        public void TransferButtonPressed()
        {
            if (activeCall != null)
            {
                // hang up our call and have them auto-call the transfer
                PhoneNetworkHandler.Instance.TransferCallServerRpc(activeCall, selectedPhone.phoneNumber, NetworkObjectId);
                PlayHangupSoundServerRpc();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
                UpdateCallingUI();
            } 
            else
            {
                // Do nothing
            }

            UpdateCallValues();
        }

        public void VolumeSwitch()
        {
            VolumeSwitchServerRpc();
        }

        public override void UpdateCallValues()
        {
            UpdateCallValuesServerRpc(
                   outgoingCall == null ? -1 : int.Parse(outgoingCall),
                   incomingCall == null ? -1 : int.Parse(incomingCall),
                   activeCall == null ? -1 : int.Parse(activeCall),
                   incomingCaller,
                   outgoingCaller,
                   activeCaller);
        }

        private IEnumerator CallTimeoutCoroutine(string number)
        {
            yield return new WaitForSeconds(15f);

            if (outgoingCall == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall, NetworkObjectId);
                StopOutgoingRingingServerRpc();
                outgoingCall = null;
                //StartCoroutine(TemporaryStatusCoroutine("No Answer"));
                //StartCoroutine(BusyHangupCoroutine());
            }
        }

        protected override void StartRinging()
        {
            ringAudio.Stop();
            transform.Find("SwitchboardMesh").GetComponent<Animator>().Play("RingerRest");

            if (!silenced.Value)
            {
                activePhoneRingCoroutine = PhoneRingCoroutine(4);
                StartCoroutine(activePhoneRingCoroutine);
                ringAudio.clip = PhoneAssetManager.switchboardRing;
                ringAudio.Play();
                transform.Find("SwitchboardMesh").GetComponent<Animator>().Play("RingerRing");
            }
        }

        protected override void StopRinging()
        {
            base.StopRinging();
            transform.Find("SwitchboardMesh").GetComponent<Animator>().Play("RingerRest");
        }

        [ServerRpc(RequireOwnership = false)]
        public void ChangeSelectionServerRpc(int change)
        {
            // no input during inbound call
            if (incomingCall != null)
            {
                return;
            }

            if (allPhones.Count == 0)
            {
                selectedIndex.Value = 0;
                selectedPhone = null;
            }
            else
            {
                int newValue = selectedIndex.Value + change;
                while (newValue < 0)
                {
                    newValue = allPhones.Count - 1;
                }
                while (newValue >= allPhones.Count)
                {
                    newValue = 0;
                }

                selectedPhone = allPhones[newValue];
                selectedIndex.Value = newValue;
                localSelectedIndex = newValue;

                UpdateCallingUI();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void VolumeSwitchServerRpc()
        {
            silenced.Value = !transform.Find("SwitchboardMesh").GetComponent<Animator>().GetBool("ringer");
            OnVolumeSwitched(!silenced.Value, silenced.Value);
        }

        [ServerRpc(RequireOwnership = false)]
        public void OperatorSwitchServerRpc(ulong playerId)
        {
            PlayerControllerB player = GetNetworkObject(playerId).GetComponent<PlayerControllerB>();

            if (switchboardOperator == null)
            {
                switchboardOperator = player;
                switchboardOperatorId.Value = player.NetworkObjectId;
                ToggleOperator(true);
            }
            else
            {
                if (switchboardOperator == player)
                {
                    switchboardOperator = null;
                    switchboardOperatorId.Value = this.NetworkObjectId;
                    ToggleOperator(false);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdateCallValuesServerRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong outgoingCallerUpdate, ulong activeCallerUpdate)
        {
            UpdateCallValuesClientRpc(outgoingCallUpdate, incomingCallUpdate, activeCallUpdate, incomingCallerUpdate, outgoingCallerUpdate, activeCallerUpdate);
        }

        [ClientRpc]
        public void UpdateCallValuesClientRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong outgoingCallerUpdate, ulong activeCallerUpdate)
        {
            // A little messy? I don't like this.
            outgoingCall = outgoingCallUpdate == -1 ? null : outgoingCallUpdate.ToString("D4");
            incomingCall = incomingCallUpdate == -1 ? null : incomingCallUpdate.ToString("D4");
            activeCall = activeCallUpdate == -1 ? null : activeCallUpdate.ToString("D4");
            incomingCaller = incomingCallerUpdate;
            outgoingCaller = outgoingCallerUpdate;
            activeCaller = activeCallerUpdate;
        }

        [ClientRpc]
        public void UpdatePhoneListClientRpc(ulong[] phoneIds)
        {
            if (allPhones == null)
            {
                allPhones = new List<PhoneBehavior>();
            }

            allPhones.Clear();

            foreach (ulong phoneId in phoneIds)
            {
                PhoneBehavior phone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
                allPhones.Add(phone);
            }

            if (IsOwner && allPhones.Count > 0)
            {
                if (!allPhones.Contains(selectedPhone))
                {
                    selectedPhone = allPhones[0];
                    selectedIndex.Value = 0;
                    localSelectedIndex = 0;
                } 
                else
                {
                    localSelectedIndex = allPhones.IndexOf(selectedPhone);
                    selectedIndex.Value = localSelectedIndex;
                }

                UpdateInfoList();
            }
        }

        public override bool IsBeingSpectated()
        {
            if (switchboardOperator != null)
            {
                return (GameNetworkManager.Instance.localPlayerController.isPlayerDead && GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript == switchboardOperator);
            }
            else
            {
                return false;
            }
        }

        protected override void UpdatePlayerVoices()
        {
            if (GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null)
            {
                return;
            }
            if (activeCaller == 0 || activeCall == null)
            {
                return;
            }

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

            float listenDist = 0f;
            float listenAngle = 0f;
            if (switchboardOperator != localPlayer)
            {
                if (!IsBeingSpectated())
                {
                    listenDist = Vector3.Distance(localPlayer.transform.position, headphonePos.position);
                    if (listenDist > Config.eavesdropDist.Value)
                    {
                        return;
                    }

                    Vector3 directionTo = headphonePos.position - localPlayer.transform.position;
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

            if (switchboardOperator == localPlayer || listenDist > 0f)
            {
                UpdateStatic(worseConnection, listenDist);
            }
        }

        public void ApplyPhoneVoiceEffect(float distance = 0f, float listeningDistance = 0f, float listeningAngle = 0f, float connectionQuality = 1f)
        {
            if (switchboardOperator == null)
            {
                return;
            }
            if (switchboardOperator.voiceMuffledByEnemy)
            {
                connectionQuality = 0f;
            }
            if (switchboardOperator.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }
            if (switchboardOperator.currentVoiceChatAudioSource == null)
            {
                Plugin.Log.LogWarning("Player " + switchboardOperator.name + " Voice Chat Audio Source still null after refresh? Something has gone wrong.");
                return;
            }

            Debug.Log("Switchboard Operator voice at dist - " + distance);
            Debug.Log("and listening dist - " + listeningDistance);

            AudioSource currentVoiceChatAudioSource = switchboardOperator.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = true;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = true;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 0f;
            switchboardOperator.currentVoiceChatIngameSettings.set2D = true;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[switchboardOperator.playerClientId];
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = GameNetworkManager.Instance.localPlayerController.isPlayerDead ? 0f : -0.4f;
            occludeAudio.lowPassOverride = Mathf.Lerp(6000f, 3000f, connectionQuality);
            lowPass.lowpassResonanceQ = Mathf.Lerp(6f, 3f, connectionQuality);
            highPass.highpassResonanceQ = Mathf.Lerp(3f, 1f, connectionQuality);
            
            if (distance != 0f)
            {
                float mod = Mathf.InverseLerp(Config.backgroundVoiceDist.Value, 0f, distance);
                currentVoiceChatAudioSource.volume = currentVoiceChatAudioSource.volume * mod;
                occludeAudio.lowPassOverride = 1500f;
            }
            
            if (listeningDistance != 0f)
            {
                float mod = Mathf.InverseLerp(Config.eavesdropDist.Value, 0f, listeningDistance);
                currentVoiceChatAudioSource.volume = currentVoiceChatAudioSource.volume * mod;
                occludeAudio.lowPassOverride = 750f;
                currentVoiceChatAudioSource.panStereo = listeningAngle;
            }

            if (switchboardOperator.voiceMuffledByEnemy)
            {
                occludeAudio.lowPassOverride = 500f;
            }

            currentVoiceChatAudioSource.volume += Config.voiceSoundMod.Value;

            if ((staticMode && hardStatic) || switchboardOperator.isPlayerDead)
            {
                currentVoiceChatAudioSource.volume = 0f;
            }
        }

        public void RemovePhoneVoiceEffect()
        {
            if (switchboardOperator == null)
            {
                return;
            }
            if (switchboardOperator.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }

            AudioSource currentVoiceChatAudioSource = switchboardOperator.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = false;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = switchboardOperator.voiceMuffledByEnemy;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 1f;
            switchboardOperator.currentVoiceChatIngameSettings.set2D = false;
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = 0f;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[switchboardOperator.playerClientId];
            lowPass.lowpassResonanceQ = 1f;
            highPass.highpassResonanceQ = 1f;
        }
    }
}
