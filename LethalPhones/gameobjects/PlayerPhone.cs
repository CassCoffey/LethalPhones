﻿using Dissonance;
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
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;

namespace Scoops.misc
{
    public class PlayerPhone : PhoneBehavior
    {
        public enum phoneVolume { Ring = 1, Silent = 2, Vibrate = 3 };
        private NetworkVariable<phoneVolume> currentVolume = new NetworkVariable<phoneVolume>(phoneVolume.Ring, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public PlayerControllerB player;
        public GameObject localPhoneModel;
        public Transform localPhoneInteractionNode;
        public Vector3 localPhoneInteractionBase;
        public Transform localPhoneStopperNode;
        public Transform localPhoneDial;
        public List<GameObject> localPhoneDialNumbers;
        public List<Vector3> dialNumbersDefaultPos;
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
        private AudioSource corpseRingAudio;

        private Transform currentDialingNumber;

        private float maxDialingZ = 0f;

        private float timeSinceRotaryMoved = 0f;
        private bool reversingRotary = false;
        private bool previousToggled = false;
        private bool stoppered = false;

        private float phoneEquipAnimSpeed = 0.2f;
        private float phoneEquipAnimProgress = 1f;

        private Transform serverArmsRig;
        private ChainIKConstraint serverLeftArmRig;

        protected IEnumerator activeCallTimeoutCoroutine;

        protected IEnumerator closeDelayCoroutine;

        public override void Start()
        {
            base.Start();

            this.player = transform.parent.GetComponent<PlayerControllerB>();
            this.ringAudio = player.transform.Find("Audios/PhoneAudioExternal(Clone)").GetComponent<AudioSource>();
            ringAudio.volume = Config.ringtoneVolume.Value;
            this.nonCorpseRingAudio = ringAudio;

            this.localPhoneModel = player.localArmsTransform.Find("shoulder.L/arm.L_upper/arm.L_lower/hand.L/LocalPhoneModel(Clone)").gameObject;
            SetPhoneLocalModelActive(false);

            this.serverPhoneModel = player.lowerSpine.Find("spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/ServerPhoneModel(Clone)").gameObject;
            SetPhoneServerModelActive(false);
            serverArmsRig = player.meshContainer.Find("metarig/Rig 1");
            serverLeftArmRig = serverArmsRig.Find("ServerLeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();
            serverLeftArmRig.weight = 0f;

            rotaryAudio = localPhoneModel.GetComponent<AudioSource>();

            this.localPhoneInteractionNode = localPhoneModel.transform.Find("LocalPhoneModel/InteractionNode");
            localPhoneInteractionBase = new Vector3(localPhoneInteractionNode.localPosition.x, localPhoneInteractionNode.localPosition.y, localPhoneInteractionNode.localPosition.z);
            this.localPhoneStopperNode = localPhoneModel.transform.Find("LocalPhoneModel/StopperNode");

            Transform phoneCanvas = localPhoneModel.transform.Find("LocalPhoneModel/PhoneTop/PhoneCanvas");
            this.dialingNumberUI = phoneCanvas.Find("DialingNumber").GetComponent<TextMeshProUGUI>();
            dialingNumberUI.text = "----";
            this.phoneStatusUI = phoneCanvas.Find("PhoneState").GetComponent<TextMeshProUGUI>();
            phoneStatusUI.text = "";
            this.personalPhoneNumberUI = phoneCanvas.Find("PersonalNumber").GetComponent<TextMeshProUGUI>();

            Transform serverPhoneCanvas = serverPhoneModel.transform.Find("ServerPhoneModel/PhoneTop/PhoneCanvas");
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

            localPhoneDial = localPhoneModel.transform.Find("LocalPhoneModel/PhoneDial");
            this.localPhoneDialNumbers = new List<GameObject>(10);
            this.dialNumbersDefaultPos = new List<Vector3>(10);
            foreach (Transform child in localPhoneDial)
            {
                this.localPhoneDialNumbers.Add(child.gameObject);
                this.dialNumbersDefaultPos.Add(child.localPosition);
            }
        }

        public override string GetPhoneName()
        {
            return player.playerUsername;
        }

        public void ToggleActive(bool active)
        {
            if (!active && Config.hangupOnPutaway.Value)
            {
                CancelAllCalls();
            }
            
            if (active)
            {
                if (closeDelayCoroutine != null) StopCoroutine(closeDelayCoroutine);

                toggled = active;
                phoneEquipAnimProgress = 0f;

                SetPhoneLocalModelActive(active);
                personalPhoneNumberUI.text = phoneNumber.ToString("D4");
                
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
                Transform handL = player.localArmsTransform.Find("shoulder.L/arm.L_upper/arm.L_lower/hand.L");
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
                    Transform handR = player.localArmsTransform.Find("shoulder.R/arm.R_upper/arm.R_lower/hand.R");
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

            UpdateConnectionQualityUI();

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
                vector *= 0.000005f;
                // Divide by deltatime since mouse movements increase with lower framerate
                vector /= Time.deltaTime;

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

                        Vector3 dialVect3 = dialNumbersDefaultPos[localPhoneDialNumbers.IndexOf(currentDialingNumber.gameObject)] - localPhoneInteractionBase;
                        Vector2 dialVect2 = new Vector2(dialVect3.x, dialVect3.y).normalized;
                        Vector3 stopperVect3 = localPhoneStopperNode.transform.localPosition - localPhoneInteractionBase;
                        Vector2 stopperVect2 = new Vector2(stopperVect3.x, stopperVect3.y).normalized;

                        float sign = Mathf.Sign(dialVect2.x * stopperVect2.y - dialVect2.y * stopperVect2.x);
                        maxDialingZ = Vector2.Angle(dialVect2, stopperVect2) * sign;
                        if (maxDialingZ < 0)
                        {
                            maxDialingZ = 360f + maxDialingZ;
                        }
                        // A little magic number flavor
                        maxDialingZ -= 10f;

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
                    Vector3 localNumberLocation = localPhoneInteractionNode.parent.InverseTransformPoint(currentDialingNumber.position);
                    localPhoneInteractionNode.localPosition = new Vector3(localNumberLocation.x, localNumberLocation.y, localPhoneInteractionNode.localPosition.z);

                    if (localPhoneDial.localEulerAngles.z < maxDialingZ)
                    {
                        timeSinceRotaryMoved += Time.deltaTime;

                        Vector2 mouseVect = vector.normalized;
                        Vector3 radialVect3 = localPhoneInteractionNode.localPosition - localPhoneInteractionBase;
                        Vector2 radialVect2 = new Vector2(radialVect3.x, radialVect3.y).normalized;
                        Vector2 perpVect2 = Vector2.Perpendicular(radialVect2);

                        float rotationPower = Mathf.Clamp01(Vector2.Dot(mouseVect, perpVect2));
                        rotationPower *= vector.magnitude;
                        rotationPower *= 750000f;
                        rotationPower *= Time.deltaTime;

                        float newZ = localPhoneDial.localEulerAngles.z + rotationPower;
                        if (newZ > maxDialingZ)
                        {
                            newZ = maxDialingZ;
                        }

                        localPhoneDial.localEulerAngles = new Vector3(0, 0, newZ);

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
                SkinnedMeshRenderer antennaRenderer = localPhoneModel.transform.Find("LocalPhoneModel/PhoneAntenna").GetComponent<SkinnedMeshRenderer>();
                if (antennaRenderer != null)
                {
                    antennaRenderer.enabled = enabled;
                }
                MeshRenderer topRenderer = localPhoneModel.transform.Find("LocalPhoneModel/PhoneTop").GetComponent<MeshRenderer>();
                if (topRenderer != null)
                {
                    topRenderer.enabled = enabled;
                }
                MeshRenderer dialRenderer = localPhoneModel.transform.Find("LocalPhoneModel/PhoneDial").GetComponent<MeshRenderer>();
                if (dialRenderer != null)
                {
                    dialRenderer.enabled = enabled;
                }

                Canvas canvasRenderer = localPhoneModel.transform.Find("LocalPhoneModel/PhoneTop/PhoneCanvas").GetComponent<Canvas>();
                if (canvasRenderer != null)
                {
                    canvasRenderer.enabled = enabled;
                }

                GameObject charmPoint = localPhoneModel.transform.Find("LocalPhoneModel/CharmAttach").gameObject;
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
                SkinnedMeshRenderer antennaRenderer = serverPhoneModel.transform.Find("ServerPhoneModel/PhoneAntenna").GetComponent<SkinnedMeshRenderer>();
                if (antennaRenderer != null)
                {
                    antennaRenderer.enabled = enabled;
                }
                MeshRenderer topRenderer = serverPhoneModel.transform.Find("ServerPhoneModel/PhoneTop").GetComponent<MeshRenderer>();
                if (topRenderer != null)
                {
                    topRenderer.enabled = enabled;
                }
                MeshRenderer dialRenderer = serverPhoneModel.transform.Find("ServerPhoneModel/PhoneDial").GetComponent<MeshRenderer>();
                if (dialRenderer != null)
                {
                    dialRenderer.enabled = enabled;
                }

                Canvas canvasRenderer = serverPhoneModel.transform.Find("ServerPhoneModel/PhoneTop/PhoneCanvas").GetComponent<Canvas>();
                if (canvasRenderer != null)
                {
                    canvasRenderer.enabled = enabled;
                }

                GameObject charmPoint = serverPhoneModel.transform.Find("ServerPhoneModel/CharmAttach").gameObject;
                if (charmPoint != null)
                {
                    charmPoint.SetActive(enabled);
                }

                if (serverPersonalPhoneNumberUI != null)
                {
                    serverPersonalPhoneNumberUI.text = phoneNumber.ToString("D4");
                }
            }
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
            }
        }

        public void ApplyCorpse()
        {
            if (player.deadBody != null)
            {
                GameObject corpsePhoneAudioPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhoneAudioExternal");
                GameObject tempCorpseAudio = GameObject.Instantiate(corpsePhoneAudioPrefab, player.deadBody.transform);
                corpseRingAudio = tempCorpseAudio.GetComponent<AudioSource>();
            }
        }

        private IEnumerator DelayDeathHangup()
        {
            yield return new WaitForSeconds(Config.deathHangupTime.Value);

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

            UpdateCallingUI();
        }

        public string GetFullDialNumber()
        {
            return String.Join("", dialedNumbers);
        }

        public void DialNumber(int number)
        {
            if (IsBusy())
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

        public void CancelAllCalls()
        {
            if (IsBusy())
            {
                PlayHangupSoundServerRpc();
            }
            if (incomingCall.Value != -1)
            {
                // We're being called, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(incomingCall.Value, NetworkObjectId);
                StopRingingServerRpc();
                incomingCall.Value = -1;
            }
            if (activeCall.Value != -1)
            {
                // We're on a call, hang up
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall.Value, NetworkObjectId);
                activeCall.Value = -1;
            }
            if (outgoingCall.Value != -1)
            {
                // We're calling, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall.Value, NetworkObjectId);
                outgoingCall.Value = -1;
            }

            UpdateCallingUI();
        }

        public void HangupButtonPressed()
        {
            if (!toggled)
            {
                return;
            }

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
            else
            {
                // Clear numbers
                dialedNumbers.Clear();
                UpdateCallingUI();
            }
        }

        public void CallButtonPressed()
        {
            if (!toggled || Plugin.InputActionInstance.DialPhoneKey.IsPressed())
            {
                return;
            }

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
            else
            {
                // No calls of any sort are happening, make a new one
                CallDialedNumber();
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

            switch (currentVolume.Value)
            {
                case phoneVolume.Ring:
                    currentVolume.Value = phoneVolume.Vibrate;
                    volumeRingUI.enabled = false;
                    volumeVibrateUI.enabled = true;
                    volumeSilentUI.enabled = false;
                    localPhoneModel.transform.Find("LocalPhoneModel").GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 50f);
                    break;
                case phoneVolume.Vibrate:
                    currentVolume.Value = phoneVolume.Silent;
                    volumeRingUI.enabled = false;
                    volumeVibrateUI.enabled = false;
                    volumeSilentUI.enabled = true;
                    localPhoneModel.transform.Find("LocalPhoneModel").GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 100f);
                    break;
                case phoneVolume.Silent:
                    currentVolume.Value = phoneVolume.Ring;
                    volumeRingUI.enabled = true;
                    volumeVibrateUI.enabled = false;
                    volumeSilentUI.enabled = false;
                    localPhoneModel.transform.Find("LocalPhoneModel").GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 0f);
                    break;
                default:
                    break;
            }
        }

        public override void CallNumber(short number)
        {
            StartOutgoingRingingServerRpc();
            outgoingCall.Value = number;
            dialedNumbers.Clear();

            UpdateCallingUI();

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number, NetworkObjectId);

            if (activeCallTimeoutCoroutine != null) StopCoroutine(activeCallTimeoutCoroutine);
            activeCallTimeoutCoroutine = CallTimeoutCoroutine(number);
            StartCoroutine(activeCallTimeoutCoroutine);
        }

        public void CallDialedNumber()
        {
            string numberString = GetFullDialNumber();
            if (dialedNumbers.Count != 4)
            {
                Plugin.Log.LogInfo("Not enough digits: " + numberString);
                return;
            }
            short number = short.Parse(numberString);
            if (number == phoneNumber)
            {
                Plugin.Log.LogInfo("You cannot call yourself yet.");
                dialedNumbers.Clear();
                UpdateCallingUI();
                return;
            }

            CallNumber(number);
        }

        protected override void UpdateCallingUI()
        {
            incomingCallUI.enabled = (incomingCall.Value != -1);

            if (incomingCall.Value != -1)
            {
                dialingNumberUI.text = incomingCall.Value.ToString("D4");
                phoneStatusUI.text = "Incoming...";
            }
            else if (activeCall.Value != -1)
            {
                dialingNumberUI.text = activeCall.Value.ToString("D4");
                phoneStatusUI.text = "Connected";
            }
            else if (outgoingCall.Value != -1)
            {
                dialingNumberUI.text = outgoingCall.Value.ToString("D4");
                phoneStatusUI.text = "Dialing...";
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
                    localPhoneModel = player.localArmsTransform.Find("shoulder.L/arm.L_upper/arm.L_lower/hand.L/LocalPhoneModel(Clone)").gameObject;
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
                    serverPhoneModel = player.lowerSpine.Find("spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/ServerPhoneModel(Clone)").gameObject;
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
                    localPhoneModel = player.localArmsTransform.Find("shoulder.L/arm.L_upper/arm.L_lower/hand.L/LocalPhoneModel(Clone)").gameObject;
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
                    serverPhoneModel = player.lowerSpine.Find("spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/ServerPhoneModel(Clone)").gameObject;
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

        public override bool IsBeingSpectated()
        {
            return (GameNetworkManager.Instance.localPlayerController.isPlayerDead && GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript == player);
        }

        private void UpdateConnectionQualityUI()
        {
            float interference = GetTotalInterference();

            if (interference <= 0.25f)
            {
                connectionQualityNoUI.enabled = false;
                connectionQualityLowUI.enabled = false;
                connectionQualityMedUI.enabled = false;
                connectionQualityHighUI.enabled = true;
            }
            else if (interference <= 0.5f)
            {
                connectionQualityNoUI.enabled = false;
                connectionQualityLowUI.enabled = false;
                connectionQualityMedUI.enabled = true;
                connectionQualityHighUI.enabled = false;
            }
            else if (interference <= 0.75f)
            {
                connectionQualityNoUI.enabled = false;
                connectionQualityLowUI.enabled = true;
                connectionQualityMedUI.enabled = false;
                connectionQualityHighUI.enabled = false;
            }
            else
            {
                connectionQualityNoUI.enabled = true;
                connectionQualityLowUI.enabled = false;
                connectionQualityMedUI.enabled = false;
                connectionQualityHighUI.enabled = false;
            }
        }

        [ClientRpc]
        public override void SetNewPhoneNumberClientRpc(short number)
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

        protected override void StartRinging()
        {
            AudioSource currentRingAudio = ringAudio;

            if (player.isPlayerDead && corpseRingAudio != null)
            {
                currentRingAudio = corpseRingAudio;
            }

            ringAudio.Stop();
            currentRingAudio.Stop();
            switch (currentVolume.Value)
            {
                case phoneVolume.Ring:
                    activePhoneRingCoroutine = PhoneRingCoroutine(4);
                    StartCoroutine(activePhoneRingCoroutine);
                    if ((Config.disableRingtones.Value && !IsOwner) || !CustomizationManager.ringtoneCustomizations.ContainsKey(phoneRingtoneId))
                    {
                        currentRingAudio.clip = CustomizationManager.ringtoneCustomizations[CustomizationManager.DEFAULT_RINGTONE];
                    } 
                    else
                    {
                        currentRingAudio.clip = CustomizationManager.ringtoneCustomizations[phoneRingtoneId];
                    }
                    currentRingAudio.Play();
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

        protected override void StopRinging()
        {
            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
            ringAudio.Stop();
            if (corpseRingAudio != null) corpseRingAudio.Stop();
        }

        protected override IEnumerator PhoneRingCoroutine(int repeats)
        {
            AudioSource currentRingAudio = ringAudio;

            if (player.isPlayerDead && corpseRingAudio != null)
            {
                currentRingAudio = corpseRingAudio;
            }

            for (int i = 0; i < repeats; i++)
            {
                RoundManager.Instance.PlayAudibleNoise(currentRingAudio.transform.position, 50f, 0.95f, i, PhoneInsideShip(), 0);
                yield return new WaitForSeconds(4f);
            }
        }

        private IEnumerator CallTimeoutCoroutine(short number)
        {
            yield return new WaitForSeconds(15f);

            if (outgoingCall.Value == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall.Value, NetworkObjectId);
                StopOutgoingRingingServerRpc();
                outgoingCall.Value = -1;
                StartCoroutine(TemporaryStatusCoroutine("No Answer"));
                StartCoroutine(BusyHangupCoroutine());
            }
        }

        private IEnumerator PhoneBusyCoroutine(string status)
        {
            yield return new WaitForSeconds(2f);

            if (IsOwner)
            {
                outgoingCall.Value = -1;
                StartCoroutine(TemporaryStatusCoroutine(status));
            }
            
            PlayBusySound();
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

        public static void UpdatePhoneSanity(PlayerControllerB playerController)
        {
            if (playerController != null && playerController.transform.Find("PhonePrefab(Clone)"))
            {
                PlayerPhone phone = playerController.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
                if (phone)
                {
                    if (phone.activeCall.Value != -1)
                    {
                        float totalInterference = phone.GetTotalInterference();
                        playerController.insanitySpeedMultiplier = -3f * (1f - totalInterference);
                        if (totalInterference != 1f)
                        {
                            playerController.isPlayerAlone = false;
                        }
                    }
                }
            }
        }
    }
}