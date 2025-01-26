using Dissonance;
using GameNetcodeStuff;
using Scoops.customization;
using Scoops.patch;
using Scoops.service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;

namespace Scoops.misc
{
    public class PlayerPhone : PhoneBehavior
    {
        public PlayerControllerB player;
        public GameObject localPhoneModel;
        public Transform localPhoneInteractionNode;
        public Vector3 localPhoneInteractionBase;
        public Transform localPhoneStopperNode;
        public Transform localPhoneDial;
        public List<GameObject> localPhoneDialNumbers;
        public GameObject serverPhoneModel;

        public bool toggled = false;

        public TextMeshProUGUI dialingNumberUI;
        public TextMeshProUGUI phoneStatusUI;
        public TextMeshProUGUI personalPhoneNumberUI;
        public TextMeshProUGUI serverPersonalPhoneNumberUI;

        public Image incomingCallUI;
        public Image volumeRingUI;
        public Image volumeSilentUI;
        public Image volumeVibrateUI;
        public Image connectionQualityNoUI;
        public Image connectionQualityLowUI;
        public Image connectionQualityMedUI;
        public Image connectionQualityHighUI;

        private AudioSource rotaryAudio;

        private AudioSource nonCorpseRingAudio;

        private Transform currentDialingNumber;

        private float timeSinceRotaryMoved = 0f;
        private bool reversingRotary = false;
        private bool previousToggled = false;
        private bool stoppered = false;

        private float phoneEquipAnimSpeed = 0.2f;
        private float phoneEquipAnimProgress = 1f;

        private Transform serverArmsRig;
        private ChainIKConstraint serverLeftArmRig;

        public enum phoneVolume { Ring = 1, Silent = 2, Vibrate = 3 };
        private phoneVolume currentVolume = phoneVolume.Ring;

        protected IEnumerator activeCallTimeoutCoroutine;

        protected IEnumerator closeDelayCoroutine;

        public override void Start()
        {
            base.Start();

            this.player = transform.parent.GetComponent<PlayerControllerB>();
            this.ringAudio = player.transform.Find("Audios").Find("PhoneAudioExternal(Clone)").GetComponent<AudioSource>();
            ringAudio.volume = Config.ringtoneVolume.Value;
            this.nonCorpseRingAudio = ringAudio;

            this.localPhoneModel = player.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("LocalPhoneModel(Clone)").gameObject;
            SetPhoneLocalModelActive(false);

            this.serverPhoneModel = player.lowerSpine.Find("spine.002").Find("spine.003").Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("ServerPhoneModel(Clone)").gameObject;
            SetPhoneServerModelActive(false);
            serverArmsRig = player.meshContainer.Find("metarig").Find("Rig 1");
            serverLeftArmRig = serverArmsRig.Find("ServerLeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();
            serverLeftArmRig.weight = 0f;

            rotaryAudio = localPhoneModel.GetComponent<AudioSource>();

            this.localPhoneInteractionNode = localPhoneModel.transform.Find("LocalPhoneModel").Find("InteractionNode");
            localPhoneInteractionBase = new Vector3(localPhoneInteractionNode.localPosition.x, localPhoneInteractionNode.localPosition.y, localPhoneInteractionNode.localPosition.z);
            this.localPhoneStopperNode = localPhoneModel.transform.Find("LocalPhoneModel").Find("StopperNode");

            Transform phoneCanvas = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneTop").Find("PhoneCanvas");
            this.dialingNumberUI = phoneCanvas.Find("DialingNumber").GetComponent<TextMeshProUGUI>();
            dialingNumberUI.text = "----";
            this.phoneStatusUI = phoneCanvas.Find("PhoneState").GetComponent<TextMeshProUGUI>();
            phoneStatusUI.text = "";
            this.personalPhoneNumberUI = phoneCanvas.Find("PersonalNumber").GetComponent<TextMeshProUGUI>();

            Transform serverPhoneCanvas = serverPhoneModel.transform.Find("ServerPhoneModel").Find("PhoneTop").Find("PhoneCanvas");
            this.serverPersonalPhoneNumberUI = serverPhoneCanvas.Find("PersonalNumber").GetComponent<TextMeshProUGUI>();

            this.incomingCallUI = phoneCanvas.Find("IncomingCall").GetComponent<Image>();
            incomingCallUI.enabled = false;
            this.connectionQualityNoUI = phoneCanvas.Find("ConnectionNo").GetComponent<Image>();
            this.connectionQualityLowUI = phoneCanvas.Find("ConnectionLow").GetComponent<Image>();
            this.connectionQualityMedUI = phoneCanvas.Find("ConnectionMed").GetComponent<Image>();
            this.connectionQualityHighUI = phoneCanvas.Find("ConnectionHigh").GetComponent<Image>();
            this.volumeRingUI = phoneCanvas.Find("Ring").GetComponent<Image>();
            this.volumeSilentUI = phoneCanvas.Find("Silent").GetComponent<Image>();
            this.volumeVibrateUI = phoneCanvas.Find("Vibrate").GetComponent<Image>();

            localPhoneDial = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneDial");
            this.localPhoneDialNumbers = new List<GameObject>(10);
            foreach (Transform child in localPhoneDial)
            {
                this.localPhoneDialNumbers.Add(child.gameObject);
            }
        }

        public void ToggleActive(bool active)
        {
            if (!active && Config.hangupOnPutaway.Value)
            {
                HangupButtonPressed();
            }
            
            if (active)
            {
                if (closeDelayCoroutine != null) StopCoroutine(closeDelayCoroutine);

                toggled = active;
                phoneEquipAnimProgress = 0f;

                SetPhoneLocalModelActive(active);
                personalPhoneNumberUI.text = phoneNumber;
                
                if (player.twoHanded || player.isHoldingObject)
                {
                    int emptySlot = player.FirstEmptyItemSlot();
                    if (!player.twoHanded && emptySlot != -1)
                    {
                        player.SwitchToItemSlot(emptySlot, null);
                        ChangeItemSlotServerRpc(emptySlot);
                    } 
                    else
                    {
                        player.DiscardHeldObject();
                    }
                }

                HUDManager.Instance.ChangeControlTip(0, "Call Phone : [" + Plugin.InputActionInstance.PickupPhoneKey.bindings[0].ToDisplayString() + "]", true);
                HUDManager.Instance.ChangeControlTip(1, "Hangup Phone : [" + Plugin.InputActionInstance.HangupPhoneKey.bindings[0].ToDisplayString() + "]", false);
                HUDManager.Instance.ChangeControlTip(2, "Dial Phone : [" + Plugin.InputActionInstance.DialPhoneKey.bindings[0].ToDisplayString() + "]", false);
                HUDManager.Instance.ChangeControlTip(3, "Toggle Phone Volume : [" + Plugin.InputActionInstance.VolumePhoneKey.bindings[0].ToDisplayString() + "]", false);

                localPhoneModel.GetComponent<Animator>().Play("PhoneFlipOpen");

                rotaryAudio.PlayOneShot(PhoneAssetManager.phoneFlipOpen);
            } 
            else
            {
                HUDManager.Instance.ClearControlTips();

                localPhoneModel.GetComponent<Animator>().Play("PhoneFlipClosed");

                rotaryAudio.PlayOneShot(PhoneAssetManager.phoneFlipClosed);

                if (closeDelayCoroutine != null) StopCoroutine(closeDelayCoroutine);
                closeDelayCoroutine = CloseDelayCoroutine();
                StartCoroutine(closeDelayCoroutine);
            }

            ToggleServerPhoneModelServerRpc(active);
        }

        public IEnumerator CloseDelayCoroutine()
        {
            yield return new WaitForSeconds(0.15f);

            toggled = false;
            phoneEquipAnimProgress = 0f;
        }

        // Here's where we break some bones
        public void LateUpdate()
        {
            if (IsOwner && toggled && !Config.hideHands.Value)
            {
                Transform handL = player.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L");
                Transform fingerL1 = handL.Find("finger1.L");
                fingerL1.localEulerAngles = new Vector3(36.1390076f, 275.679993f, 76.3550034f);
                fingerL1.Find("finger1.L.001").localEulerAngles = new Vector3(284.725983f, 350.876007f, 10.5490074f);
                Transform fingerL2 = handL.Find("finger2.L");
                fingerL2.localEulerAngles = new Vector3(5.05699778f, 270f, 98.189003f);
                fingerL2.Find("finger2.L.001").localEulerAngles = new Vector3(2.75100255f, 356.675018f, 83.1589966f);
                Transform fingerL3 = handL.Find("finger3.L");
                fingerL3.localEulerAngles = new Vector3(2.48299932f, 270.306f, 88.9009857f);
                fingerL3.Find("finger3.L.001").localEulerAngles = new Vector3(1.4079988f, 355.457977f, 97.9869995f);
                Transform fingerL4 = handL.Find("finger4.L");
                fingerL4.localEulerAngles = new Vector3(354.190002f, 270f, 85.822998f);
                fingerL4.Find("finger4.L.001").localEulerAngles = new Vector3(5.63999939f, 357.084991f, 121.238007f);
                Transform fingerL5 = handL.Find("finger5.L");
                fingerL5.localEulerAngles = new Vector3(356.811005f, 260.109009f, 85.6919861f);
                fingerL5.Find("finger5.L.001").localEulerAngles = new Vector3(342.890991f, 8.13000202f, 109.431984f);

                if (Plugin.InputActionInstance.DialPhoneKey.IsPressed())
                {
                    Transform handR = player.localArmsTransform.Find("shoulder.R").Find("arm.R_upper").Find("arm.R_lower").Find("hand.R");
                    Transform fingerR1 = handR.Find("finger1.R");
                    fingerR1.localEulerAngles = new Vector3(32.4089966f, 57.2649841f, 319.315002f);
                    fingerR1.Find("finger1.R.001").localEulerAngles = new Vector3(288.475983f, 341.432007f, 4.1400032f);
                    Transform fingerR2 = handR.Find("finger2.R");
                    fingerR2.localEulerAngles = new Vector3(13.0880003f, 90f, 335.346008f);
                    fingerR2.Find("finger2.R.001").localEulerAngles = new Vector3(351.102997f, 359.130005f, 345.136993f);
                    Transform fingerR3 = handR.Find("finger3.R");
                    fingerR3.localEulerAngles = new Vector3(5.7479949f, 89.9990005f, 262.154968f);
                    fingerR3.Find("finger3.R.001").localEulerAngles = new Vector3(353.714996f, 0.0349977836f, 248.507996f);
                    Transform fingerR4 = handR.Find("finger4.R");
                    fingerR4.localEulerAngles = new Vector3(359.694f, 90f, 256.720001f);
                    fingerR4.Find("finger4.R.001").localEulerAngles = new Vector3(351.419006f, 0.498000294f, 248.522003f);
                    Transform fingerR5 = handR.Find("finger5.R");
                    fingerR5.localEulerAngles = new Vector3(358.115021f, 90f, 241.14502f);
                    fingerR5.Find("finger5.R.001").localEulerAngles = new Vector3(327.817017f, 329.139008f, 267.781006f);
                }
            }
        }

        public override void Update()
        {
            if (IsOwner)
            {
                this.ManageInputs();
            }

            base.Update();

            previousToggled = toggled;
        }

        private void ManageInputs()
        {
            Transform ArmsRig = player.localArmsTransform.Find("RigArms");
            ChainIKConstraint RightArmRig = ArmsRig.Find("RightArmPhone(Clone)").GetComponent<ChainIKConstraint>();
            ChainIKConstraint LeftArmRig = ArmsRig.Find("LeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();

            if (phoneEquipAnimProgress != 1f)
            {
                phoneEquipAnimProgress = Mathf.Clamp01(phoneEquipAnimProgress + (Time.deltaTime / phoneEquipAnimSpeed));

                if (toggled)
                {
                    LeftArmRig.weight = Mathf.Lerp(0f, 1f, phoneEquipAnimProgress);
                }
                else if (!toggled)
                {
                    LeftArmRig.weight = Mathf.Lerp(1f, 0f, phoneEquipAnimProgress);

                    if (phoneEquipAnimProgress == 1f)
                    {
                        SetPhoneLocalModelActive(false);
                    }
                }
            }

            if (toggled && Plugin.InputActionInstance.DialPhoneKey.IsPressed())
            {
                if (Config.hideHands.Value)
                {
                    localPhoneInteractionNode.Find("SphereHelper").gameObject.SetActive(true);
                }

                HUDManager.Instance.SetNearDepthOfFieldEnabled(!Plugin.InputActionInstance.DialPhoneKey.IsPressed());

                LeftArmRig.transform.Find("ArmsLeftArm_target").position = Vector3.Lerp(LeftArmRig.transform.Find("ArmsLeftArm_target").position, LeftArmRig.transform.Find("PhoneDialPos").position, 25f * Time.deltaTime);
                LeftArmRig.transform.Find("ArmsLeftArm_target").rotation = Quaternion.Lerp(LeftArmRig.transform.Find("ArmsLeftArm_target").rotation, LeftArmRig.transform.Find("PhoneDialPos").rotation, 25f * Time.deltaTime);

                if (RightArmRig.weight < 0.9f)
                {
                    RightArmRig.weight = Mathf.Lerp(RightArmRig.weight, 1f, 25f * Time.deltaTime);

                    if (RightArmRig.weight >= 0.9f)
                    {
                        RightArmRig.weight = 1f;
                    }
                }

                player.disableLookInput = true;

                Vector2 vector = player.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * (float)IngamePlayerSettings.Instance.settings.lookSensitivity;
                if (!IngamePlayerSettings.Instance.settings.invertYAxis)
                {
                    vector.y *= -1f;
                }
                vector *= 0.0005f;

                if (!Plugin.InputActionInstance.PickupPhoneKey.IsPressed())
                {
                    Vector3 localPosition = localPhoneInteractionNode.localPosition;
                    localPosition.x = Mathf.Clamp(localPosition.x + vector.x, localPhoneInteractionBase.x - 0.0075f, localPhoneInteractionBase.x + 0.0075f);
                    localPosition.y = Mathf.Clamp(localPosition.y + vector.y, localPhoneInteractionBase.y - 0.0075f, localPhoneInteractionBase.y + 0.0075f);
                    localPhoneInteractionNode.localPosition = new Vector3(localPosition.x, localPosition.y, localPhoneInteractionNode.localPosition.z);
                }
                else if (!reversingRotary && Plugin.InputActionInstance.PickupPhoneKey.WasPressedThisFrame())
                {
                    stoppered = false;
                    float closestDist = 100f;
                    GameObject closestNum = null;

                    foreach (GameObject number in localPhoneDialNumbers)
                    {
                        float dist = Vector3.Distance(number.transform.position, localPhoneInteractionNode.transform.position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestNum = number;
                        }
                    }

                    if (closestDist <= 0.04f)
                    {
                        currentDialingNumber = closestNum.transform;

                        rotaryAudio.Stop();
                        rotaryAudio.clip = PhoneAssetManager.phoneRotaryForward;
                        rotaryAudio.Play();

                        timeSinceRotaryMoved = 0f;
                    }
                    else
                    {
                        currentDialingNumber = null;
                    }
                }
                else if (currentDialingNumber != null)
                {
                    float dist = Vector3.Distance(currentDialingNumber.transform.position, localPhoneStopperNode.transform.position);

                    Vector3 localNumberLocation = localPhoneInteractionNode.parent.InverseTransformPoint(currentDialingNumber.position);
                    localPhoneInteractionNode.localPosition = new Vector3(localNumberLocation.x, localNumberLocation.y, localPhoneInteractionNode.localPosition.z);

                    if (dist > 0.03f)
                    {
                        timeSinceRotaryMoved += Time.deltaTime;

                        Vector2 mouseVect = vector.normalized;
                        Vector3 radialVect3 = localPhoneInteractionNode.localPosition - localPhoneInteractionBase;
                        Vector2 radialVect2 = new Vector2(radialVect3.x, radialVect3.y).normalized;
                        Vector2 perpVect2 = Vector2.Perpendicular(radialVect2);

                        float rotationPower = Mathf.Clamp01(Vector2.Dot(mouseVect, perpVect2));
                        rotationPower *= vector.magnitude;
                        rotationPower *= 7500f;

                        localPhoneDial.localEulerAngles = new Vector3(0, 0, localPhoneDial.localEulerAngles.z + rotationPower);

                        if (rotationPower != 0f)
                        {
                            timeSinceRotaryMoved = 0f;
                        }

                        if (timeSinceRotaryMoved > 0.25f)
                        {
                            rotaryAudio.Pause();
                        }
                        else if (!rotaryAudio.isPlaying)
                        {
                            rotaryAudio.Play();
                        }
                    } 
                    else
                    {
                        if (timeSinceRotaryMoved == 0f)
                        {
                            rotaryAudio.Stop();
                            rotaryAudio.PlayOneShot(PhoneAssetManager.phoneRotaryStopper);
                            stoppered = true;
                        }

                        timeSinceRotaryMoved += Time.deltaTime;
                    }
                }

                if (!Config.hideHands.Value)
                {
                    RightArmRig.transform.Find("ArmsRightArm_target").position = localPhoneInteractionNode.Find("HandLoc").position;
                    RightArmRig.transform.Find("ArmsRightArm_target").rotation = localPhoneInteractionNode.Find("HandLoc").rotation;
                }
            } 
            else if (Plugin.InputActionInstance.DialPhoneKey.WasReleasedThisFrame() || (previousToggled && !toggled))
            {
                HUDManager.Instance.SetNearDepthOfFieldEnabled(!Plugin.InputActionInstance.DialPhoneKey.IsPressed());
                player.disableLookInput = false;
                localPhoneInteractionNode.Find("SphereHelper").gameObject.SetActive(false);
            } 
            else
            {
                LeftArmRig.transform.Find("ArmsLeftArm_target").position = Vector3.Lerp(LeftArmRig.transform.Find("ArmsLeftArm_target").position, LeftArmRig.transform.Find("PhoneRestPos").position, 25f * Time.deltaTime);
                LeftArmRig.transform.Find("ArmsLeftArm_target").rotation = Quaternion.Lerp(LeftArmRig.transform.Find("ArmsLeftArm_target").rotation, LeftArmRig.transform.Find("PhoneRestPos").rotation, 25f * Time.deltaTime);

                RightArmRig.weight = Mathf.Lerp(RightArmRig.weight, 0f, 25f * Time.deltaTime);

                if (RightArmRig.weight <= 0.1f)
                {
                    RightArmRig.weight = 0f;
                }
            }

            if (!reversingRotary && (Plugin.InputActionInstance.DialPhoneKey.WasReleasedThisFrame() || Plugin.InputActionInstance.PickupPhoneKey.WasReleasedThisFrame()))
            {
                rotaryAudio.Stop();

                if (localPhoneDial.localEulerAngles.z != 0f)
                {
                    if (stoppered)
                    {
                        DialNumber(int.Parse(currentDialingNumber.gameObject.name));
                    }
                    else
                    {
                        float closestDist = 100f;
                        GameObject closestNum = null;

                        foreach (GameObject number in localPhoneDialNumbers)
                        {
                            float dist = Vector3.Distance(number.transform.position, localPhoneStopperNode.transform.position);
                            Vector3 localNumPos = localPhoneStopperNode.parent.InverseTransformPoint(number.transform.position);
                            if (dist < closestDist && localNumPos.y <= localPhoneStopperNode.localPosition.y)
                            {
                                closestDist = dist;
                                closestNum = number;
                            }
                        }

                        if (closestDist <= 0.05f)
                        {
                            DialNumber(int.Parse(closestNum.name));
                        }
                    }

                    reversingRotary = true;

                    rotaryAudio.clip = PhoneAssetManager.phoneRotaryBackward;
                    rotaryAudio.Play();
                }

                currentDialingNumber = null;
                stoppered = false;
            }

            if (reversingRotary)
            {
                if (localPhoneDial.localEulerAngles.z >= 10f)
                {
                    localPhoneDial.localEulerAngles = new Vector3(0, 0, localPhoneDial.localEulerAngles.z - (300f * Time.deltaTime));
                }
                else if (localPhoneDial.localEulerAngles.z != 0f)
                {
                    reversingRotary = false;
                    localPhoneDial.localEulerAngles = Vector3.zero;
                    rotaryAudio.Stop();
                    rotaryAudio.PlayOneShot(PhoneAssetManager.phoneRotaryFinish);
                }
            }
        }

        public void SetPhoneLocalModelActive(bool enabled = false)
        {
            if (localPhoneModel)
            {
                SkinnedMeshRenderer mainRenderer = localPhoneModel.transform.Find("LocalPhoneModel").GetComponent<SkinnedMeshRenderer>();
                if (mainRenderer != null)
                {
                    mainRenderer.enabled = enabled;
                }
                SkinnedMeshRenderer antennaRenderer = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneAntenna").GetComponent<SkinnedMeshRenderer>();
                if (antennaRenderer != null)
                {
                    antennaRenderer.enabled = enabled;
                }
                MeshRenderer topRenderer = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneTop").GetComponent<MeshRenderer>();
                if (topRenderer != null)
                {
                    topRenderer.enabled = enabled;
                }
                MeshRenderer dialRenderer = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneDial").GetComponent<MeshRenderer>();
                if (dialRenderer != null)
                {
                    dialRenderer.enabled = enabled;
                }

                Canvas canvasRenderer = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneTop").Find("PhoneCanvas").GetComponent<Canvas>();
                if (canvasRenderer != null)
                {
                    canvasRenderer.enabled = enabled;
                }

                GameObject charmPoint = localPhoneModel.transform.Find("LocalPhoneModel").Find("CharmAttach").gameObject;
                if (charmPoint != null)
                {
                    charmPoint.SetActive(enabled);
                }
            }
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

        public void Revive()
        {
            SetPhoneLocalModelActive(false);
            SetPhoneServerModelActive(false);

            ringAudio = nonCorpseRingAudio;
        }

        public void Death(int causeOfDeath)
        {
            this.enabled = true;

            toggled = false;
            SetPhoneLocalModelActive(false);
            SetPhoneServerModelActive(false);

            if (IsOwner)
            {
                dialedNumbers.Clear();
                StartCoroutine(DelayDeathHangup());
                UpdateCallingUI();
                UpdateCallValues();
            }
        }

        public void ApplyCorpse()
        {
            if (player.deadBody != null)
            {
                GameObject corpsePhoneAudioPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhoneAudioExternal");
                GameObject tempCorpseAudio = GameObject.Instantiate(corpsePhoneAudioPrefab, player.deadBody.transform);
                ringAudio = tempCorpseAudio.GetComponent<AudioSource>();
            }
        }

        private IEnumerator DelayDeathHangup()
        {
            yield return new WaitForSeconds(Config.deathHangupTime.Value);

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

            UpdateCallingUI();
            UpdateCallValues();
        }

        public string GetFullDialNumber()
        {
            return String.Join("", dialedNumbers);
        }

        public void DialNumber(int number)
        {
            if (activeCall != null || incomingCall != null || outgoingCall != null)
            {
                return;
            }

            dialedNumbers.Enqueue(number);

            if (dialedNumbers.Count > 4)
            {
                dialedNumbers.Dequeue();
            }

            UpdateCallingUI();
        }

        public void HangupButtonPressed()
        {
            if (!toggled)
            {
                return;
            }

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
            else
            {
                // Clear numbers
                dialedNumbers.Clear();
                UpdateCallingUI();
            }

            if (IsOwner)
            {
                UpdateCallValues();
            }
        }

        public void CallButtonPressed()
        {
            if (!toggled || Plugin.InputActionInstance.DialPhoneKey.IsPressed())
            {
                return;
            }

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
                CallDialedNumber();
            }

            if (IsOwner)
            {
                UpdateCallValues();
            }
        }

        public void VolumeButtonPressed()
        {
            if (!toggled)
            {
                return;
            }

            thisAudio.Stop();
            thisAudio.PlayOneShot(PhoneAssetManager.phoneSwitch);

            switch (currentVolume)
            {
                case phoneVolume.Ring:
                    currentVolume = phoneVolume.Vibrate;
                    volumeRingUI.enabled = false;
                    volumeVibrateUI.enabled = true;
                    volumeSilentUI.enabled = false;
                    localPhoneModel.transform.Find("LocalPhoneModel").GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 50f);
                    break;
                case phoneVolume.Vibrate:
                    currentVolume = phoneVolume.Silent;
                    volumeRingUI.enabled = false;
                    volumeVibrateUI.enabled = false;
                    volumeSilentUI.enabled = true;
                    localPhoneModel.transform.Find("LocalPhoneModel").GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 100f);
                    break;
                case phoneVolume.Silent:
                    currentVolume = phoneVolume.Ring;
                    volumeRingUI.enabled = true;
                    volumeVibrateUI.enabled = false;
                    volumeSilentUI.enabled = false;
                    localPhoneModel.transform.Find("LocalPhoneModel").GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 0f);
                    break;
                default:
                    break;
            }

            if (IsOwner)
            {
                UpdateCallValues();
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
                UpdateCallingUI();
                return;
            }

            StartOutgoingRingingServerRpc();
            outgoingCall = number;
            dialedNumbers.Clear();

            UpdateCallingUI();

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number, NetworkObjectId);

            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);
            activeCallTimeoutCoroutine = CallTimeoutCoroutine(number);
            StartCoroutine(activeCallTimeoutCoroutine);
        }

        protected override void UpdateCallingUI()
        {
            incomingCallUI.enabled = (incomingCall != null);

            if (activeCall != null)
            {
                dialingNumberUI.text = activeCall;
                phoneStatusUI.text = "Connected";
            }
            else if (outgoingCall != null)
            {
                dialingNumberUI.text = outgoingCall;
                phoneStatusUI.text = "Dialing...";
            }
            else if (incomingCall != null)
            {
                dialingNumberUI.text = incomingCall;
                phoneStatusUI.text = "Incoming...";
            }
            else
            {
                string modifiedNumber = GetFullDialNumber();
                while (modifiedNumber.Length < 4)
                {
                    modifiedNumber = "-" + modifiedNumber;
                }

                dialingNumberUI.text = modifiedNumber;
                phoneStatusUI.text = "";
            }
        }

        protected override void ApplySkin(string skinId)
        {
            GameObject skinObject = CustomizationManager.skinCustomizations[skinId];
            if (skinObject == null) return;

            if (IsOwner)
            {
                if (localPhoneModel == null)
                {
                    localPhoneModel = player.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("LocalPhoneModel(Clone)").gameObject;
                }
                Transform localPhoneDisplay = localPhoneModel.transform.Find("LocalPhoneModel");

                // Main Mat
                localPhoneDisplay.GetComponent<Renderer>().materials = skinObject.GetComponent<Renderer>().materials;
                // Antenna Mat
                localPhoneDisplay.Find("PhoneAntenna").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneAntenna").GetComponent<Renderer>().materials;
                // Dial Mat
                localPhoneDisplay.Find("PhoneDial").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneDial").GetComponent<Renderer>().materials;
                // Top Mat
                localPhoneDisplay.Find("PhoneTop").GetComponent<Renderer>().materials = skinObject.transform.Find("PhoneTop").GetComponent<Renderer>().materials;
            } 
            else
            {
                if (serverPhoneModel == null)
                {
                    serverPhoneModel = player.lowerSpine.Find("spine.002").Find("spine.003").Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("ServerPhoneModel(Clone)").gameObject;
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
        }

        protected override void ApplyCharm(string charmId)
        {
            GameObject charmPrefab = CustomizationManager.charmCustomizations[charmId];
            if (charmPrefab == null) return;

            if (IsOwner)
            {
                if (localPhoneModel == null)
                {
                    localPhoneModel = player.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("LocalPhoneModel(Clone)").gameObject;
                }
                Transform localPhoneDisplay = localPhoneModel.transform.Find("LocalPhoneModel");

                if (localPhoneDisplay.Find("CharmAttach").childCount == 0)
                {
                    GameObject.Instantiate(charmPrefab, localPhoneDisplay.Find("CharmAttach"));
                }
            }
            else
            {
                if (serverPhoneModel == null)
                {
                    serverPhoneModel = player.lowerSpine.Find("spine.002").Find("spine.003").Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("ServerPhoneModel(Clone)").gameObject;
                }
                Transform serverPhoneDisplay = serverPhoneModel.transform.Find("ServerPhoneModel");

                if (serverPhoneDisplay.Find("CharmAttach").childCount == 0)
                {
                    GameObject.Instantiate(charmPrefab, serverPhoneDisplay.Find("CharmAttach"));
                }
            }
        }

        public override bool PhoneInsideFactory()
        {
            return player.isInsideFactory;
        }

        public override bool PhoneInsideShip()
        {
            return player.isInElevator && StartOfRound.Instance.hangarDoorsClosed;
        }

        protected override bool IsBeingSpectated()
        {
            return (GameNetworkManager.Instance.localPlayerController.isPlayerDead && GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript == player);
        }

        protected override void ManageConnectionQuality()
        {
            base.ManageConnectionQuality();

            UpdateConnectionQualityUI();
        }

        private void UpdateConnectionQualityUI()
        {
            if (connectionQuality.Value <= 0.25f)
            {
                connectionQualityNoUI.enabled = true;
                connectionQualityLowUI.enabled = false;
                connectionQualityMedUI.enabled = false;
                connectionQualityHighUI.enabled = false;
            }
            else if (connectionQuality.Value <= 0.5f)
            {
                connectionQualityNoUI.enabled = false;
                connectionQualityLowUI.enabled = true;
                connectionQualityMedUI.enabled = false;
                connectionQualityHighUI.enabled = false;
            }
            else if (connectionQuality.Value <= 0.75f)
            {
                connectionQualityNoUI.enabled = false;
                connectionQualityLowUI.enabled = false;
                connectionQualityMedUI.enabled = true;
                connectionQualityHighUI.enabled = false;
            }
            else
            {
                connectionQualityNoUI.enabled = false;
                connectionQualityLowUI.enabled = false;
                connectionQualityMedUI.enabled = false;
                connectionQualityHighUI.enabled = true;
            }
        }

        [ClientRpc]
        public override void SetNewPhoneNumberClientRpc(string number)
        {
            if (player == null)
            {
                player = transform.parent.GetComponent<PlayerControllerB>();
            }

            phoneNumber = number;

            if (IsOwner)
            {
                PhoneNetworkHandler.Instance.localPhone = this;
            }
        }

        [ClientRpc]
        public override void InvalidCallClientRpc(string reason)
        {
            StartCoroutine(PhoneBusyCoroutine(reason));
        }

        public void UpdateCallValues()
        {
            UpdateCallValuesServerRpc(
                   outgoingCall == null ? -1 : int.Parse(outgoingCall),
                   incomingCall == null ? -1 : int.Parse(incomingCall),
                   activeCall == null ? -1 : int.Parse(activeCall),
                   incomingCaller,
                   activeCaller,
                   (int)currentVolume);
        }

        [ServerRpc]
        public void UpdateCallValuesServerRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong activeCallerUpdate, int volumeUpdate)
        {
            UpdateCallValuesClientRpc(outgoingCallUpdate, incomingCallUpdate, activeCallUpdate, incomingCallerUpdate, activeCallerUpdate, volumeUpdate);
        }

        [ClientRpc]
        public void UpdateCallValuesClientRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, ulong incomingCallerUpdate, ulong activeCallerUpdate, int volumeUpdate)
        {
            // A little messy? I don't like this.
            outgoingCall = outgoingCallUpdate == -1 ? null : outgoingCallUpdate.ToString("D4");
            incomingCall = incomingCallUpdate == -1 ? null : incomingCallUpdate.ToString("D4");
            activeCall = activeCallUpdate == -1 ? null : activeCallUpdate.ToString("D4");
            incomingCaller = incomingCallerUpdate;
            activeCaller = activeCallerUpdate;
            currentVolume = (phoneVolume)volumeUpdate;
        }

        protected override void StartRinging()
        {
            ringAudio.Stop();
            switch (currentVolume)
            {
                case phoneVolume.Ring:
                    activePhoneRingCoroutine = PhoneRingCoroutine(4);
                    StartCoroutine(activePhoneRingCoroutine);
                    if (Config.disableRingtones.Value && !IsOwner)
                    {
                        ringAudio.clip = CustomizationManager.ringtoneCustomizations[CustomizationManager.DEFAULT_RINGTONE];
                    } 
                    else
                    {
                        ringAudio.clip = CustomizationManager.ringtoneCustomizations[phoneRingtoneId];
                    }
                    ringAudio.Play();
                    break;
                case phoneVolume.Vibrate:
                    thisAudio.Stop();
                    thisAudio.clip = PhoneAssetManager.phoneRingVibrate;
                    thisAudio.Play();
                    break;
                case phoneVolume.Silent:
                    // Nothing
                    break;
                default:
                    break;
            }
        }

        private IEnumerator CallTimeoutCoroutine(string number)
        {
            yield return new WaitForSeconds(14f);

            if (outgoingCall == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall, NetworkObjectId);
                StopOutgoingRingingServerRpc();
                outgoingCall = null;
                StartCoroutine(TemporaryStatusCoroutine("No Answer"));
                StartCoroutine(BusyHangupCoroutine());
            }
        }

        private IEnumerator PhoneBusyCoroutine(string status)
        {
            yield return new WaitForSeconds(2f);

            outgoingCall = null;
            PlayBusySound();
            StartCoroutine(TemporaryStatusCoroutine(status));
            StartCoroutine(BusyHangupCoroutine());
        }

        private IEnumerator TemporaryStatusCoroutine(string status)
        {
            phoneStatusUI.text = status;
            yield return new WaitForSeconds(4f);
            UpdateCallingUI();
        }

        private IEnumerator BusyHangupCoroutine()
        {
            yield return new WaitForSeconds(4f);
            PlayHangupSound();
        }

        private IEnumerator AnimateServerArm(bool active)
        {
            while (phoneEquipAnimProgress != 1f)
            {
                phoneEquipAnimProgress = Mathf.Clamp01(phoneEquipAnimProgress + (Time.deltaTime / phoneEquipAnimSpeed));

                if (active)
                {
                    serverLeftArmRig.weight = Mathf.Lerp(0f, 1f, phoneEquipAnimProgress);
                }
                else if (!active)
                {
                    serverLeftArmRig.weight = Mathf.Lerp(1f, 0f, phoneEquipAnimProgress);

                    if (phoneEquipAnimProgress == 1f)
                    {
                        SetPhoneServerModelActive(false);
                    }
                }

                yield return null;
            }
        }

        [ServerRpc]
        public void ToggleServerPhoneModelServerRpc(bool active)
        {
            ToggleServerPhoneModelClientRpc(active);
        }

        [ClientRpc]
        public void ToggleServerPhoneModelClientRpc(bool active)
        {
            if (IsOwner)
            {
                return;
            }

            if (active)
            {
                SetPhoneServerModelActive(active);
                serverPhoneModel.GetComponent<Animator>().Play("PhoneFlipOpen");
            }
            else
            {
                serverPhoneModel.GetComponent<Animator>().Play("PhoneFlipClosed");
            }

            phoneEquipAnimProgress = 0f;
            StartCoroutine(AnimateServerArm(active));
        }

        [ServerRpc]
        public void ChangeItemSlotServerRpc(int slot)
        {
            ChangeItemSlotClientRpc(slot);
        }

        [ClientRpc]
        public void ChangeItemSlotClientRpc(int slot)
        {
            if (IsOwner)
            {
                return;
            }

            player.SwitchToItemSlot(slot, null);
        }

        public override void ApplyPhoneVoiceEffect(float distance = 0f, float listeningDistance = 0f, float listeningAngle = 0f, float connectionQuality = 1f)
        {
            if (player == null)
            {
                return;
            }
            if (player.voiceMuffledByEnemy)
            {
                connectionQuality = 0f;
            }
            if (player.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }
            if (player.currentVoiceChatAudioSource == null)
            {
                Plugin.Log.LogInfo("Player " + player.name + " Voice Chat Audio Source still null after refresh? Something has gone wrong.");
                return;
            }

            AudioSource currentVoiceChatAudioSource = player.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = true;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = true;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 0f;
            player.currentVoiceChatIngameSettings.set2D = true;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[player.playerClientId];
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

            if (player.voiceMuffledByEnemy)
            {
                occludeAudio.lowPassOverride = 500f;
            }

            currentVoiceChatAudioSource.volume += Config.voiceSoundMod.Value;

            if ((staticMode && hardStatic) || player.isPlayerDead)
            {
                currentVoiceChatAudioSource.volume = 0f;
            }
        }

        public override void RemovePhoneVoiceEffect()
        {
            if (player == null)
            {
                return;
            }
            if (player.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }

            AudioSource currentVoiceChatAudioSource = player.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = false;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = player.voiceMuffledByEnemy;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 1f;
            player.currentVoiceChatIngameSettings.set2D = false;
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = 0f;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[player.playerClientId];
            lowPass.lowpassResonanceQ = 1f;
            highPass.highpassResonanceQ = 1f;
        }

        public static void UpdatePhoneSanity(PlayerControllerB playerController)
        {
            if (playerController != null)
            {
                PlayerPhone phone = playerController.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
                if (phone)
                {
                    if (phone.activeCall != null)
                    {
                        playerController.insanitySpeedMultiplier = -3f * phone.connectionQuality.Value;
                        playerController.isPlayerAlone = false;
                    }
                }
            }
        }
    }
}