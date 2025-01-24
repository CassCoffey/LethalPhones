using LethalLib.Modules;
using Scoops.compatability;
using Scoops.customization;
using Scoops.patch;
using Scoops.service;
using System.Collections;
using System.Numerics;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Scoops.misc
{
    public class MaskedPhone : PhoneBehavior
    {
        public MaskedPlayerEnemy masked;
        public GameObject serverPhoneModel;

        public TextMeshProUGUI serverPersonalPhoneNumberUI;

        private bool preppingCall = false;
        private bool preppingPickup = false;

        private IEnumerator activePickupDelayCoroutine;
        private IEnumerator activeCallDelayCoroutine;
        private IEnumerator activeCallTimeoutCoroutine;

        private Transform upperSpine;

        private bool armsActive = false;

        public override void Start()
        {
            base.Start();

            this.masked = transform.parent.GetComponent<MaskedPlayerEnemy>();
            this.ringAudio = this.GetComponent<AudioSource>();

            upperSpine = masked.transform.Find("ScavengerModel").Find("metarig").Find("spine").Find("spine.001").Find("spine.002").Find("spine.003");

            GameObject serverPhoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerPhoneModel");
            serverPhoneModel = GameObject.Instantiate(serverPhoneModelPrefab, upperSpine.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L"), false);

            Transform ServerArmsRig = masked.transform.Find("ScavengerModel").Find("metarig").Find("Rig 1");
            GameObject leftArmServerPhoneRigPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerLeftArmPhone");
            GameObject leftArmServerPhoneTargetPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerPhoneTargetHolder");

            GameObject serverLeftArmPhoneRig = GameObject.Instantiate(leftArmServerPhoneRigPrefab, ServerArmsRig, false);

            GameObject serverLeftArmPhoneTarget = GameObject.Instantiate(leftArmServerPhoneTargetPrefab, upperSpine, false);

            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.root = upperSpine.Find("shoulder.L").Find("arm.L_upper");
            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.tip = upperSpine.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L");
            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.target = serverLeftArmPhoneTarget.transform.Find("ServerPhoneTarget");

            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().MarkDirty();

            masked.transform.Find("ScavengerModel").Find("metarig").GetComponent<RigBuilder>().Build();

            SetPhoneServerModelActive(false);

            ChainIKConstraint LeftArmRig = ServerArmsRig.Find("ServerLeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();
            LeftArmRig.weight = 0f;

            Transform serverPhoneCanvas = serverPhoneModel.transform.Find("ServerPhoneModel").Find("PhoneTop").Find("PhoneCanvas");
            this.serverPersonalPhoneNumberUI = serverPhoneCanvas.Find("PersonalNumber").GetComponent<TextMeshProUGUI>();

            PhoneNetworkHandler.Instance.RequestClientUpdates();
        }

        public void SetPhoneServerModelActive(bool enabled = false)
        {
            if (serverPhoneModel != null)
            {
                SkinnedMeshRenderer mainRenderer = serverPhoneModel.transform.Find("ServerPhoneModel").GetComponent<SkinnedMeshRenderer>();
                if (mainRenderer != null)
                {
                    mainRenderer.enabled = enabled;
                }
                SkinnedMeshRenderer antennaRenderer = serverPhoneModel.transform.Find("ServerPhoneModel").Find("PhoneAntenna").GetComponent<SkinnedMeshRenderer>();
                if (antennaRenderer != null)
                {
                    antennaRenderer.enabled = enabled;
                }
                MeshRenderer topRenderer = serverPhoneModel.transform.Find("ServerPhoneModel").Find("PhoneTop").GetComponent<MeshRenderer>();
                if (topRenderer != null)
                {
                    topRenderer.enabled = enabled;
                }
                MeshRenderer dialRenderer = serverPhoneModel.transform.Find("ServerPhoneModel").Find("PhoneDial").GetComponent<MeshRenderer>();
                if (dialRenderer != null)
                {
                    dialRenderer.enabled = enabled;
                }

                Canvas canvasRenderer = serverPhoneModel.transform.Find("ServerPhoneModel").Find("PhoneTop").Find("PhoneCanvas").GetComponent<Canvas>();
                if (canvasRenderer != null)
                {
                    canvasRenderer.enabled = enabled;
                }

                GameObject charmPoint = serverPhoneModel.transform.Find("ServerPhoneModel").Find("CharmAttach").gameObject;
                if (charmPoint != null)
                {
                    charmPoint.SetActive(enabled);
                }

                if (serverPersonalPhoneNumberUI != null)
                {
                    serverPersonalPhoneNumberUI.text = phoneNumber;
                }
            }
        }

        public void Death()
        {
            MaskedPhonePatch.phoneMasks--;

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
            if (IsOwner && !masked.isEnemyDead)
            {
                if (outgoingCall == null && activeCall == null)
                {
                    // put the phone away
                    if (armsActive)
                    {
                        ToggleServerPhoneModelServerRpc(false);
                    }

                    if (incomingCall == null)
                    {
                        // we NEED to be on a call or we'll DIE
                        if (!preppingCall)
                        {
                            activeCallDelayCoroutine = CallDelayCoroutine(UnityEngine.Random.Range(Config.minPhoneMaskedInterval.Value, Config.maxPhoneMaskedInterval.Value));
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
                MirageCompat.UnmuteEnemy(masked);
            }
        }

        private IEnumerator PickupDelayCoroutine(float time)
        {
            preppingPickup = true;
            yield return new WaitForSeconds(time);

            if (incomingCall != null && outgoingCall == null && activeCall == null && !masked.isEnemyDead)
            {
                activeCall = incomingCall;
                activeCaller = incomingCaller;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall, NetworkObjectId);
                StopRingingServerRpc();
                PlayPickupSoundServerRpc();
                UpdateCallValues();

                ToggleServerPhoneModelServerRpc(true);
            }

            preppingPickup = false;
        }

        private IEnumerator CallDelayCoroutine(float time)
        {
            preppingCall = true;
            yield return new WaitForSeconds(time);

            if (incomingCall == null && outgoingCall == null && activeCall == null && !masked.isEnemyDead)
            {
                CallRandomNumber();
                if (outgoingCall != null)
                {
                    activeCallTimeoutCoroutine = CallTimeoutCoroutine(outgoingCall);
                    StartCoroutine(activeCallTimeoutCoroutine);
                    UpdateCallValues();
                    ToggleServerPhoneModelServerRpc(true);
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
                ToggleServerPhoneModelServerRpc(false);
            }
        }

        protected override void ApplySkin(string skinId)
        {
            GameObject skinObject = CustomizationManager.skinCustomizations[skinId];
            if (skinObject == null) return;

            if (serverPhoneModel == null)
            {
                serverPhoneModel = upperSpine.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("ServerPhoneModel(Clone)").gameObject;
            }
            Transform serverPhoneDisplay = serverPhoneModel.transform.Find("ServerPhoneModel");

            // Main Mat
            serverPhoneDisplay.GetComponent<Renderer>().materials = skinObject.GetComponent<Renderer>().materials;
            // Antenna Mat
            serverPhoneDisplay.Find("PhoneAntenna").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneAntenna").GetComponent<Renderer>().materials;
            // Dial Mat
            serverPhoneDisplay.Find("PhoneDial").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneDial").GetComponent<Renderer>().materials;
            // Top Mat
            serverPhoneDisplay.Find("PhoneTop").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneTop").GetComponent<Renderer>().materials;
        }

        protected override void ApplyCharm(string charmId)
        {
            GameObject charmPrefab = CustomizationManager.charmCustomizations[charmId];
            if (charmPrefab == null) return;

            if (serverPhoneModel == null)
            {
                serverPhoneModel = upperSpine.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("ServerPhoneModel(Clone)").gameObject;
            }
            Transform serverPhoneDisplay = serverPhoneModel.transform.Find("ServerPhoneModel");

            if (serverPhoneDisplay.Find("CharmAttach").childCount == 0)
            {
                GameObject.Instantiate(charmPrefab, serverPhoneDisplay.Find("CharmAttach"));
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

        [ServerRpc]
        public void ToggleServerPhoneModelServerRpc(bool active)
        {
            ToggleServerPhoneModelClientRpc(active);
        }

        [ClientRpc]
        public void ToggleServerPhoneModelClientRpc(bool active)
        {
            SetPhoneServerModelActive(active);

            Transform ServerArmsRig = masked.transform.Find("ScavengerModel").Find("metarig").Find("Rig 1");
            ChainIKConstraint LeftArmRig = ServerArmsRig.Find("ServerLeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();

            armsActive = active;

            if (active)
            {
                LeftArmRig.weight = 1f;
            }
            else
            {
                LeftArmRig.weight = 0f;
            }
        }

        public override void ApplyPhoneVoiceEffect(float distance = 0f, float listeningDistance = 0f, float listeningAngle = 0f, float connectionQuality = 1f)
        {
            if (masked == null)
            {
                return;
            }
            if (masked.creatureVoice == null)
            {
                return;
            }

            AudioSource currentVoiceChatAudioSource = masked.creatureVoice;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            occludeAudio.overridingLowPass = true;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 0f;
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = GameNetworkManager.Instance.localPlayerController.isPlayerDead ? 0f : -0.4f;
            occludeAudio.lowPassOverride = Mathf.Lerp(6000f, 3000f, connectionQuality);
            if (lowPass != null)
            {
                lowPass.enabled = true;
                lowPass.lowpassResonanceQ = Mathf.Lerp(6f, 3f, connectionQuality);
            }
            if (highPass != null)
            {
                highPass.enabled = true;
                highPass.highpassResonanceQ = Mathf.Lerp(3f, 1f, connectionQuality);
            }


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

            currentVoiceChatAudioSource.volume += Config.voiceSoundMod.Value;

            if ((staticMode && hardStatic) || masked.isEnemyDead)
            {
                currentVoiceChatAudioSource.volume = 0f;
            }
        }

        public override void RemovePhoneVoiceEffect()
        {
            if (masked == null)
            {
                return;
            }
            if (masked.creatureVoice == null)
            {
                return;
            }

            AudioSource currentVoiceChatAudioSource = masked.creatureVoice;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            occludeAudio.overridingLowPass = false;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 1f;
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = 0f;

            if (lowPass != null)
            {
                lowPass.enabled = true;
                lowPass.lowpassResonanceQ = 1f;
            }
            if (highPass != null)
            {
                highPass.enabled = false;
                highPass.highpassResonanceQ = 1f;
            }
        }
    }
}