using Scoops.misc;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Scoops.service;
using Unity.Netcode;
using UnityEngine.UI;
using GameNetcodeStuff;
using System.Collections;
using Scoops.customization;
using static Scoops.misc.PlayerPhone;
using LethalLib.Modules;

namespace Scoops.gameobjects
{
    public class SwitchboardPhone : PhoneBehavior
    {
        private PlayerControllerB switchboardOperator;

        private List<PhoneBehavior> allPhones;
        private Transform[] phoneInfoArray;
        private Transform activeCallerInfo;
        private Transform operatorInfo;
        private Transform inboundInfo;

        private ulong outgoingCaller;

        private PhoneBehavior selectedPhone = null;
        private int selectedIndex = -1;

        private float uiUpdateCounter = 0f;
        private const float uiUpdateInterval = 1f;

        private bool silenced = false;

        protected IEnumerator activeCallTimeoutCoroutine;

        public override void Start()
        {
            base.Start();

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
            transform.Find("RedButtonCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                HangupButtonPressed();
            });

            transform.Find("RingerCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                VolumeSwitchPressed();
            });

            transform.Find("HeadphoneCube").GetComponent<InteractTrigger>().onInteract.AddListener((PlayerControllerB player) =>
            {
                OperatorSwitch(player);
            });

            if (IsOwner)
            {
                PhoneNetworkHandler.Instance.RegisterSwitchboard(this.NetworkObjectId);
            }
        }

        public override void Update()
        {
            if (IsOwner)
            {
                uiUpdateCounter += Time.deltaTime;
                if (uiUpdateCounter >= uiUpdateInterval)
                {
                    uiUpdateCounter = 0f;
                    UpdateCallingUI();
                }
            }
        }

        public override string GetPhoneName()
        {
            return "Switchboard";
        }

        private void ChangeSelection(int change)
        {
            // no input during inbound call
            if (incomingCall != null)
            {
                return;
            }

            if (allPhones.Count == 0)
            {
                selectedIndex = 0;
                selectedPhone = null;
            } 
            else
            {
                selectedIndex += change;
                while (selectedIndex < 0)
                {
                    selectedIndex = allPhones.Count - 1;
                }
                while (selectedIndex >= allPhones.Count)
                {
                    selectedIndex = 0;
                }

                selectedPhone = allPhones[selectedIndex];

                UpdateCallingUI();
            }
        }

        protected override void UpdateCallingUI()
        {
            UpdateInfoList();
            UpdateActiveCallerUI();
            UpdateOperatorUI();
            UpdateInboundUI();
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
            // The selected info box
            for (int i = 0; i < 5; i++)
            {
                int index = selectedIndex + i - 2;
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

        public void CallSelectedNumber()
        {
            string number = selectedPhone.phoneNumber;
            if (number == phoneNumber)
            {
                Plugin.Log.LogInfo("You cannot call yourself yet. Messages will be here later.");
                UpdateCallingUI();
                return;
            }

            StartOutgoingRingingServerRpc();
            outgoingCall = number;
            outgoingCaller = selectedPhone.NetworkObjectId;

            UpdateCallingUI();

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number, NetworkObjectId);

            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);
            activeCallTimeoutCoroutine = CallTimeoutCoroutine(number);
            StartCoroutine(activeCallTimeoutCoroutine);
        }

        public void OperatorSwitch(PlayerControllerB player)
        {
            if (switchboardOperator == null)
            {
                transform.Find("SwitchboardHeadphones").GetComponent<Renderer>().enabled = false;
                switchboardOperator = player;
                UpdateCallingUI();
            }
            else
            {
                if (switchboardOperator == player)
                {
                    transform.Find("SwitchboardHeadphones").GetComponent<Renderer>().enabled = true;
                    switchboardOperator = null;
                    UpdateCallingUI();
                }
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

            if (IsOwner)
            {
                UpdateCallValues();
            }
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
                    RemovePhoneVoiceEffect(activeCaller);
                }
                activeCall = incomingCall;
                activeCaller = incomingCaller;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall, NetworkObjectId);
                StopRingingServerRpc();
                PlayPickupSoundServerRpc();

                UpdateCallingUI();
            }
            else
            {
                // No calls of any sort are happening, make a new one
                CallSelectedNumber();
            }

            if (IsOwner)
            {
                UpdateCallValues();
            }
        }

        public void TransferButtonPressed()
        {
            // nothing atm
        }

        public void VolumeSwitchPressed()
        {
            silenced = !transform.Find("SwitchboardMesh").GetComponent<Animator>().GetBool("ringer");
        }

        public void UpdateCallValues()
        {
            UpdateCallValuesServerRpc(
                   outgoingCall == null ? -1 : int.Parse(outgoingCall),
                   incomingCall == null ? -1 : int.Parse(incomingCall),
                   activeCall == null ? -1 : int.Parse(activeCall),
                   incomingCaller,
                   outgoingCaller,
                   activeCaller,
                   silenced);
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
            if (!silenced)
            {
                activePhoneRingCoroutine = PhoneRingCoroutine(4);
                StartCoroutine(activePhoneRingCoroutine);
                ringAudio.clip = CustomizationManager.ringtoneCustomizations[CustomizationManager.DEFAULT_RINGTONE];
                ringAudio.Play();
            }
        }

        [ServerRpc]
        public void UpdateCallValuesServerRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong outgoingCallerUpdate, ulong activeCallerUpdate, bool volumeUpdate)
        {
            UpdateCallValuesClientRpc(outgoingCallUpdate, incomingCallUpdate, activeCallUpdate, incomingCallerUpdate, outgoingCallerUpdate, activeCallerUpdate, volumeUpdate);
        }

        [ClientRpc]
        public void UpdateCallValuesClientRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong outgoingCallerUpdate, ulong activeCallerUpdate, bool volumeUpdate)
        {
            // A little messy? I don't like this.
            outgoingCall = outgoingCallUpdate == -1 ? null : outgoingCallUpdate.ToString("D4");
            incomingCall = incomingCallUpdate == -1 ? null : incomingCallUpdate.ToString("D4");
            activeCall = activeCallUpdate == -1 ? null : activeCallUpdate.ToString("D4");
            incomingCaller = incomingCallerUpdate;
            outgoingCaller = outgoingCallerUpdate;
            activeCaller = activeCallerUpdate;
            silenced = volumeUpdate;
        }

        [ClientRpc]
        public void UpdatePhoneListClientRpc(ulong[] phoneIds)
        {
            allPhones.Clear();

            foreach (ulong phoneId in phoneIds)
            {
                PhoneBehavior phone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
                allPhones.Add(phone);
            }

            if (allPhones.Count > 0)
            {
                if (!allPhones.Contains(selectedPhone))
                {
                    selectedPhone = allPhones[0];
                    selectedIndex = 0;
                }

                UpdateInfoList();
            }
        }

        public override void ApplyPhoneVoiceEffect(float distance = 0f, float listeningDistance = 0f, float listeningAngle = 0f, float connectionQuality = 1f)
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
                Plugin.Log.LogInfo("Player " + switchboardOperator.name + " Voice Chat Audio Source still null after refresh? Something has gone wrong.");
                return;
            }

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

        public override void RemovePhoneVoiceEffect()
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
