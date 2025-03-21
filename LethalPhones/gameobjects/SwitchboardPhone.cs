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

        private PhoneBehavior selectedPhone = null;

        private float uiUpdateCounter = 0f;
        private const float uiUpdateInterval = 1f;

        private int localSelectedIndex = -1;

        protected IEnumerator activeCallTimeoutCoroutine;

        private bool started = false;
        private bool sentInfoRequest = false;

        public void Awake()
        {
            PhoneNetworkHandler.phoneListUpdateEvent.AddListener(UpdatePhoneList);
        }

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

            recordPos = transform.Find("HeadphoneCube");
            playPos = recordPos;

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
                CallButtonPressedServerRpc();
            });
            transform.Find("YellowButtonCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                TransferButtonPressedServerRpc();
            });
            transform.Find("RedButtonCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                HangupButtonPressedServerRpc();
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

            PhoneNetworkHandler.Instance.RequestPhoneListUpdates();

            // Switchboard also acts as a booster
            ConnectionModifier modifier = gameObject.AddComponent<ConnectionModifier>();
            modifier.interferenceMod = -1f;
            modifier.range = Config.switchboardBoosterRange.Value;

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

            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);

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
            if (incomingCall.Value != -1)
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

                if (IsBusy() && mats[1] != PhoneAssetManager.greenLight)
                {
                    mats[1] = PhoneAssetManager.greenLight;
                    headsetMic.GetComponent<Renderer>().SetMaterialArray(mats);
                }
                else if (!IsBusy() &&  mats[1] != PhoneAssetManager.offLight)
                {
                    mats[1] = PhoneAssetManager.offLight;
                    headsetMic.GetComponent<Renderer>().SetMaterialArray(mats);
                }
            }
        }

        private void UpdateActiveCallerUI()
        {
            if (activeCall.Value != -1)
            {
                PhoneBehavior phone = GetNetworkObject(activeCaller.Value).GetComponent<PhoneBehavior>();

                activeCallerInfo.Find("CallerText").GetComponent<TextMeshProUGUI>().text = "CONNECTED: " + phone.GetPhoneName();
                activeCallerInfo.Find("NumberText").GetComponent<TextMeshProUGUI>().text = activeCall.Value.ToString("D4");
            }
            else if (outgoingCall.Value != -1)
            {
                PhoneBehavior phone = GetNetworkObject(outgoingCaller.Value).GetComponent<PhoneBehavior>();

                activeCallerInfo.Find("CallerText").GetComponent<TextMeshProUGUI>().text = "DIALING: " + phone.GetPhoneName();
                activeCallerInfo.Find("NumberText").GetComponent<TextMeshProUGUI>().text = outgoingCall.Value.ToString("D4");
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
            if (incomingCall.Value != -1)
            {
                PhoneBehavior phone = GetNetworkObject(incomingCaller.Value).GetComponent<PhoneBehavior>();

                inboundInfo.Find("InboundPanelBack/InboundCallNumber").GetComponent<TextMeshProUGUI>().text = phone.phoneNumber.ToString("D4");
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
            if (allPhones == null || allPhones.Count == 0)
            {
                // A catch for if a client's switchboard ends up null somehow.
                if (!sentInfoRequest)
                {
                    PhoneNetworkHandler.Instance.RequestPhoneListUpdates();
                    sentInfoRequest = true;
                }
                return;
            }

            if (phoneInfoArray == null) return;

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

                if (allPhones[index] != null && phoneInfoArray[0] != null)
                {
                    phoneInfoArray[i].Find("NameText").GetComponent<TextMeshProUGUI>().text = allPhones[index].GetPhoneName();
                    phoneInfoArray[i].Find("NumberText").GetComponent<TextMeshProUGUI>().text = allPhones[index].phoneNumber.ToString("D4");
                    UpdateInfoStatusIndicator(phoneInfoArray[i].Find("StatusIndicator").GetComponent<Image>(), allPhones[index]);
                }
            }
        }

        private void UpdateInfoStatusIndicator(Image statusIndicator, PhoneBehavior phone)
        {
            if (phone.IsBusy())
            {
                if (activeCall.Value == phone.phoneNumber)
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

        public override void CallNumber(short number)
        {
            StartOutgoingRingingServerRpc();
            outgoingCall.Value = number;
            outgoingCaller.Value = selectedPhone.NetworkObjectId;

            UpdateCallingUI();

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number, NetworkObjectId);

            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);
            activeCallTimeoutCoroutine = CallTimeoutCoroutine(number);
            StartCoroutine(activeCallTimeoutCoroutine);
        }

        public void CallSelectedNumber()
        {
            short number = selectedPhone.phoneNumber;
            if (number == phoneNumber)
            {
                Plugin.Log.LogInfo("You cannot call yourself yet.");
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
            recordPos = active ? switchboardOperator.playerGlobalHead : transform.Find("HeadphoneCube");
            playPos = recordPos;

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

        [ServerRpc(RequireOwnership = false)]
        public void HangupButtonPressedServerRpc()
        {
            if (incomingCall.Value != -1)
            {
                // We're being called, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(incomingCall.Value, NetworkObjectId);
                StopRingingServerRpc();
                PlayHangupSoundServerRpc();
                incomingCall.Value = -1;
                UpdateCallingUI();
            } 
            else if (activeCall.Value != -1)
            {
                // We're on a call, hang up
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall.Value, NetworkObjectId);
                PlayHangupSoundServerRpc();
                activeCall.Value = -1;
                UpdateCallingUI();
            }
            else if (outgoingCall.Value != -1)
            {
                // We're calling, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall.Value, NetworkObjectId);
                PlayHangupSoundServerRpc();
                outgoingCall.Value = -1;
                UpdateCallingUI();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void CallButtonPressedServerRpc()
        {
            if (incomingCall.Value != -1)
            {
                // We have an incoming call, pick up
                if (activeCall.Value != -1)
                {
                    //hang up our active first!
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall.Value, NetworkObjectId);
                }
                activeCall.Value = incomingCall.Value;
                activeCaller.Value = incomingCaller.Value;
                incomingCall.Value = -1;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall.Value, NetworkObjectId);
                StopRingingServerRpc();
                PlayPickupSoundServerRpc();

                UpdateCallingUI();
            }
            else if (outgoingCall.Value == -1 && activeCall.Value == -1)
            {
                // No calls of any sort are happening, make a new one
                CallSelectedNumber();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferButtonPressedServerRpc()
        {
            if (activeCall.Value != -1)
            {
                // hang up our call and have them auto-call the transfer
                PhoneNetworkHandler.Instance.TransferCallServerRpc(activeCall.Value, selectedPhone.phoneNumber, NetworkObjectId);
                PlayHangupSoundServerRpc();
                activeCall.Value = -1;
                UpdateCallingUI();
            }
            else
            {
                // Do nothing
            }
        }

        public void VolumeSwitch()
        {
            VolumeSwitchServerRpc();
        }

        private IEnumerator CallTimeoutCoroutine(short number)
        {
            yield return new WaitForSeconds(15f);

            if (outgoingCall.Value == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall.Value, NetworkObjectId);
                StopOutgoingRingingServerRpc();
                outgoingCall.Value = -1;
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

        private void UpdatePhoneList()
        {
            sentInfoRequest = false;
            if (allPhones == null)
            {
                allPhones = new List<PhoneBehavior>();
            }

            allPhones.Clear();

            foreach (PhoneBehavior phone in PhoneNetworkHandler.allPhoneBehaviors)
            {
                if (!(phone is MaskedPhone || phone is SwitchboardPhone))
                {
                    allPhones.Add(phone);
                }
            }

            if (allPhones.Count > 1)
            {
                allPhones.Sort((e, f) => { return e.phoneNumber.CompareTo(f.phoneNumber); });
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

        [ServerRpc(RequireOwnership = false)]
        public void ChangeSelectionServerRpc(int change)
        {
            // no input during inbound call
            if (incomingCall.Value != -1)
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
    }
}
