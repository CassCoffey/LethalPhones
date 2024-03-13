using Dissonance;
using GameNetcodeStuff;
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
    public class PlayerPhone : NetworkBehaviour
    {
        public static float RECORDING_START_DIST = 15f;
        public static float BACKGROUND_VOICE_DIST = 20f;
        public static float EAVESDROP_DIST = 5f;

        public bool spectatorClear = false;

        public PlayerControllerB player;
        public GameObject localPhoneModel;
        public Transform localPhoneInteractionNode;
        public Vector3 localPhoneInteractionBase;
        public Transform localPhoneStopperNode;
        public Transform localPhoneDial;
        public List<GameObject> localPhoneDialNumbers;
        public GameObject serverPhoneModel;

        public string phoneNumber;
        public bool toggled = false;

        public TextMeshProUGUI dialingNumberUI;
        public TextMeshProUGUI phoneStatusUI;
        public TextMeshProUGUI personalPhoneNumberUI;

        public Image incomingCallUI;
        public Image volumeRingUI;
        public Image volumeSilentUI;
        public Image volumeVibrateUI;
        public Image connectionQualityNoUI;
        public Image connectionQualityLowUI;
        public Image connectionQualityMedUI;
        public Image connectionQualityHighUI;

        private bool isLocalPhone = false;

        private Queue<int> dialedNumbers = new Queue<int>(4);
        private Transform currentDialingNumber;

        public int activeCaller = -1;
        public int incomingCaller = -1;

        private AudioSource ringAudio;
        private AudioSource thisAudio;
        private AudioSource rotaryAudio;
        private AudioSource target;

        private string incomingCall = null;
        private string activeCall = null;
        private string outgoingCall = null;

        private List<AudioSource> untrackedAudioSources = new List<AudioSource>();
        private List<AudioSourceStorage> audioSourcesInRange = new List<AudioSourceStorage>();

        private HashSet<PlayerControllerB> modifiedPlayerVoices = new HashSet<PlayerControllerB>();

        private float updateInterval;
        private float connectionInterval = 0f;
        private bool staticMode = false;
        private bool hardStatic = false;
        private float staticChance = 0f;

        private float timeSinceRotaryMoved = 0f;
        private bool reversingRotary = false;
        private bool previousToggled = false;
        private bool stoppered = false;

        public float targetConnectionQuality = 1f;
        public float currentConnectionQuality = 1f;
        public NetworkVariable<float> connectionQuality = new NetworkVariable<float>(1f);

        public enum phoneVolume { Ring = 1, Silent = 2, Vibrate = 3 };
        private phoneVolume currentVolume = phoneVolume.Ring;

        private IEnumerator activePhoneRingCoroutine;

        public void Start()
        {
            this.thisAudio = GetComponent<AudioSource>();
            this.target = transform.Find("Target").gameObject.GetComponent<AudioSource>();

            this.GetAllAudioSourcesToUpdate();

            this.player = transform.parent.GetComponent<PlayerControllerB>();
            this.ringAudio = player.transform.Find("Audios").Find("PhoneAudioExternal(Clone)").GetComponent<AudioSource>();

            this.localPhoneModel = player.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("LocalPhoneModel(Clone)").gameObject;
            SetPhoneLocalModelActive(false);

            this.serverPhoneModel = player.lowerSpine.Find("spine.002").Find("spine.003").Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("ServerPhoneModel(Clone)").gameObject;
            SetPhoneServerModelActive(false);
            Transform ServerArmsRig = player.meshContainer.Find("metarig").Find("Rig 1");
            ChainIKConstraint LeftArmRig = ServerArmsRig.Find("ServerLeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();
            LeftArmRig.weight = 0f;

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
            toggled = active;
            if (active)
            {
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
            } 
            else
            {
                HUDManager.Instance.ClearControlTips();
            }

            ToggleServerPhoneModelServerRpc(active);
        }

        // Here's where we break some bones
        public void LateUpdate()
        {
            if (isLocalPhone && toggled)
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

        public void Update()
        {
            if (isLocalPhone)
            {
                this.ManageInputs();
            }

            if (this.activeCall == null || spectatorClear)
            {
                if (!spectatorClear) activeCaller = -1;

                if (target.isPlaying)
                {
                    target.Stop();
                }

                if (audioSourcesInRange.Count > 0)
                {
                    foreach (AudioSourceStorage storage in this.audioSourcesInRange)
                    {
                        storage.Reset();
                    }

                    this.audioSourcesInRange.Clear();
                }
                if (modifiedPlayerVoices.Count > 0)
                {
                    foreach (PlayerControllerB player in modifiedPlayerVoices)
                    {
                        RemovePhoneVoiceEffect(player);
                    }

                    this.modifiedPlayerVoices.Clear();
                }

                spectatorClear = false;
            }

            this.UpdatePlayerVoices();
            this.UpdateAllAudioSources();
            this.GetAllAudioSourcesToUpdate();

            previousToggled = toggled;

            if (isLocalPhone)
            {
                if (this.connectionInterval >= 0.75f)
                {
                    this.connectionInterval = 0f;
                    ManageConnectionQuality();
                }
                else
                {
                    this.connectionInterval += Time.deltaTime;
                }

                if (this.updateInterval >= 0f)
                {
                    this.updateInterval -= Time.deltaTime;
                    return;
                }
                this.updateInterval = 1f;

                this.UpdateConnectionQualityServerRpc(currentConnectionQuality);
            }
        }

        private void ManageInputs()
        {
            Transform ArmsRig = player.localArmsTransform.Find("RigArms");
            ChainIKConstraint RightArmRig = ArmsRig.Find("RightArmPhone(Clone)").GetComponent<ChainIKConstraint>();
            ChainIKConstraint LeftArmRig = ArmsRig.Find("LeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();

            if (toggled && LeftArmRig.weight < 0.9f)
            {
                LeftArmRig.weight = Mathf.Lerp(LeftArmRig.weight, 1f, 25f * Time.deltaTime);

                if (LeftArmRig.weight >= 0.9f)
                {
                    LeftArmRig.weight = 1f;
                }
            }
            else if (!toggled && LeftArmRig.weight > 0.1f)
            {
                LeftArmRig.weight = Mathf.Lerp(LeftArmRig.weight, 0f, 25f * Time.deltaTime);

                if (LeftArmRig.weight <= 0.1f)
                {
                    LeftArmRig.weight = 0f;

                    SetPhoneLocalModelActive(false);
                }
            }

            if (toggled && Plugin.InputActionInstance.DialPhoneKey.IsPressed())
            {
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

                Plugin.Log.LogInfo("MovementVector: " + vector);

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

                RightArmRig.transform.Find("ArmsRightArm_target").position = localPhoneInteractionNode.Find("HandLoc").position;
                RightArmRig.transform.Find("ArmsRightArm_target").rotation = localPhoneInteractionNode.Find("HandLoc").rotation;
            } 
            else if (Plugin.InputActionInstance.DialPhoneKey.WasReleasedThisFrame() || (previousToggled && !toggled))
            {
                HUDManager.Instance.SetNearDepthOfFieldEnabled(!Plugin.InputActionInstance.DialPhoneKey.IsPressed());
                player.disableLookInput = false;
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

            if (!reversingRotary && localPhoneDial.localEulerAngles.z != 0f && (Plugin.InputActionInstance.DialPhoneKey.WasReleasedThisFrame() || Plugin.InputActionInstance.PickupPhoneKey.WasReleasedThisFrame()))
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

                currentDialingNumber = null;
                reversingRotary = true;
                stoppered = false;

                rotaryAudio.Stop();
                rotaryAudio.clip = PhoneAssetManager.phoneRotaryBackward;
                rotaryAudio.Play();
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
            }
        }

        public void Death(int causeOfDeath)
        {
            if (activeCall != null)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall);
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
            if (outgoingCall != null)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall);
                outgoingCall = null;
            }
            if (incomingCall != null)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(incomingCall);
                incomingCall = null;
            }

            dialedNumbers.Clear();
            UpdateCallingUI();

            toggled = false;
            SetPhoneLocalModelActive(false);
            ToggleServerPhoneModelServerRpc(false);

            if (isLocalPhone)
            {
                UpdateCallValues();
            }
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
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall);
                PlayHangupSound();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
                UpdateCallingUI();
            }
            else if (outgoingCall != null)
            {
                // We're calling, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall);
                PlayHangupSound();
                outgoingCall = null;
                UpdateCallingUI();
            } 
            else if (incomingCall != null) 
            {
                // We're being called, cancel
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(incomingCall);
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

            if (isLocalPhone)
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
                    PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall);
                    RemovePhoneVoiceEffect(activeCaller);
                }
                activeCall = incomingCall;
                activeCaller = incomingCaller;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall);
                StopRingingServerRpc();
                PlayPickupSound();

                UpdateCallingUI();

                PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[activeCaller];
                Plugin.Log.LogInfo("Accepted call from " + caller.name);
                if (caller.currentVoiceChatAudioSource != null)
                {
                    Plugin.Log.LogInfo("With voice chat audio source " + caller.currentVoiceChatAudioSource.name);
                }
                else
                {
                    Plugin.Log.LogInfo("With no voice chat audio source?");
                }
            }
            else
            {
                // No calls of any sort are happening, make a new one
                CallDialedNumber();
            }

            if (isLocalPhone)
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

            if (isLocalPhone)
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

            thisAudio.Play();
            outgoingCall = number;
            dialedNumbers.Clear();

            UpdateCallingUI();

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number);
            StartCoroutine(CallTimeoutCoroutine(number));
        }

        public void StopLocalSound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
            }
        }

        public void PlayHangupSound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneAssetManager.phoneHangup);
            }
        }

        public void PlayPickupSound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneAssetManager.phonePickup);
            }
        }

        public void PlayBusySound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneAssetManager.phoneBusy);
            }
        }

        private void GetAllAudioSourcesToUpdate()
        {
            if (isLocalPhone || PhoneNetworkHandler.Instance.localPhone == null || player == null)
            {
                return;
            }
            if (activeCall != PhoneNetworkHandler.Instance.localPhone.phoneNumber && !(GameNetworkManager.Instance.localPlayerController.isPlayerDead && GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript == player))
            {
                return;
            }
            untrackedAudioSources = StartOfRoundPhonePatch.GetAllAudioSourcesInRange(player.transform.position);
            foreach (AudioSource source in untrackedAudioSources)
            {
                if (source != null && source != player.currentVoiceChatAudioSource && source.spatialBlend != 0f)
                {
                    AudioSourceStorage storage = new AudioSourceStorage(source);
                    storage.InitAudio();
                    audioSourcesInRange.Add(storage);
                }
            }
        }

        private void UpdateAllAudioSources()
        {
            if (activeCaller == -1) return;
            if (!isLocalPhone)
            {
                return;
            }

            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[activeCaller];
            if (caller == null)
            {
                return;
            }
            PlayerPhone callerPhone = caller.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
            if (callerPhone == null)
            {
                return;
            }

            float worseConnection = callerPhone.connectionQuality.Value < this.connectionQuality.Value ? callerPhone.connectionQuality.Value : this.connectionQuality.Value;

            if (activeCall != null)
            {
                for (int j = callerPhone.audioSourcesInRange.Count - 1; j >= 0; j--)
                {
                    AudioSourceStorage storage = callerPhone.audioSourcesInRange[j];
                    AudioSource source = storage.audioSource;
                    if (source != null)
                    {
                        float callerDist = Vector3.Distance(source.transform.position, caller.transform.position);
                        float playerDist = Vector3.Distance(source.transform.position, player.transform.position);
                        float playerToCallerDist = Vector3.Distance(caller.transform.position, player.transform.position);

                        if (playerToCallerDist <= RECORDING_START_DIST || callerDist > playerDist || callerDist > source.maxDistance)
                        {
                            storage.Reset();
                            callerPhone.audioSourcesInRange.RemoveAt(j);
                        }
                        else
                        {
                            storage.ApplyPhone(callerDist, worseConnection, staticMode && hardStatic);
                        }
                    } 
                    else
                    {
                        callerPhone.audioSourcesInRange.RemoveAt(j);
                    }
                }
            }
            else if (activeCall == null)
            {
                for (int j = callerPhone.audioSourcesInRange.Count - 1; j >= 0; j--)
                {
                    AudioSourceStorage storage = callerPhone.audioSourcesInRange[j];
                    storage.Reset();
                    callerPhone.audioSourcesInRange.RemoveAt(j);
                }
            }
        }

        private void UpdatePlayerVoices()
        {
            if (player == null || GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null)
            {
                return;
            }
            
            if (activeCaller != -1)
            {
                if (activeCall != null)
                {
                    float listenDist = 0f;
                    float listenAngle = 0f;
                    if (!isLocalPhone)
                    {
                        if (!(GameNetworkManager.Instance.localPlayerController.isPlayerDead && GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript == player))
                        {
                            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                            listenDist = Vector3.Distance(localPlayer.transform.position, player.transform.position);
                            if (listenDist > EAVESDROP_DIST)
                            {
                                return;
                            }
                            Vector3 directionTo = player.transform.position - localPlayer.transform.position;
                            directionTo = directionTo / listenDist;
                            listenAngle = Vector3.Dot(directionTo, localPlayer.transform.right);
                        }
                    }
                    PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[activeCaller];
                    if (caller == GameNetworkManager.Instance.localPlayerController)
                    {
                        return;
                    }

                    PlayerPhone callerPhone = caller.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();

                    float worseConnection = callerPhone.connectionQuality.Value < this.connectionQuality.Value ? callerPhone.connectionQuality.Value : this.connectionQuality.Value;

                    if (isLocalPhone || listenDist > 0f)
                    {
                        UpdateStatic(worseConnection, listenDist);
                    }

                    float dist = Vector3.Distance(caller.transform.position, player.transform.position);

                    if (dist > RECORDING_START_DIST)
                    {
                        modifiedPlayerVoices.Add(caller);
                        ApplyPhoneVoiceEffect(caller, 0f, listenDist, listenAngle, worseConnection);
                    }
                    else
                    {
                        if (modifiedPlayerVoices.Contains(caller))
                        {
                            modifiedPlayerVoices.Remove(caller);
                            RemovePhoneVoiceEffect(caller);
                        }
                    }

                    for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                    {
                        PlayerControllerB background = StartOfRound.Instance.allPlayerScripts[i];
                        if (background != null && background.isPlayerControlled && !background.isPlayerDead && !background.IsLocalPlayer)
                        {
                            if (background != caller && background != player)
                            {
                                float callDist = Vector3.Distance(background.transform.position, caller.transform.position);
                                float localDist = Vector3.Distance(background.transform.position, player.transform.position);
                                if (localDist > RECORDING_START_DIST && callDist < BACKGROUND_VOICE_DIST)
                                {
                                    modifiedPlayerVoices.Add(background);
                                    ApplyPhoneVoiceEffect(background, callDist, listenDist, listenAngle, worseConnection);
                                }
                                else
                                {
                                    if (modifiedPlayerVoices.Contains(background))
                                    {
                                        modifiedPlayerVoices.Remove(background);
                                        RemovePhoneVoiceEffect(background);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void UpdateCallingUI()
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

        public void InfluenceConnectionQuality(float change)
        {
            currentConnectionQuality = Mathf.Clamp01(currentConnectionQuality + change);
        }

        private void ManageConnectionQuality()
        {
            float newTargetConnectionQuality = 1f;
            LevelWeatherType[] badWeathers = { LevelWeatherType.Flooded, LevelWeatherType.Rainy, LevelWeatherType.Foggy };
            LevelWeatherType[] worseWeathers = { LevelWeatherType.Stormy };
            if (badWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
            {
                newTargetConnectionQuality -= 0.25f;
            }
            if (worseWeathers.Contains(TimeOfDay.Instance.currentLevelWeather))
            {
                newTargetConnectionQuality -= 0.5f;
            }

            if (player.isInsideFactory)
            {
                newTargetConnectionQuality -= 0.1f;
                float entranceDist = 300f;

                EntranceTeleport[] entranceArray = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>(false);
                for (int i = 0; i < entranceArray.Length; i++)
                {
                    if (!entranceArray[i].isEntranceToBuilding)
                    {
                        float newDist = Vector3.Distance(entranceArray[i].transform.position, player.transform.position);
                        if (newDist < entranceDist)
                        {
                            entranceDist = newDist;
                        }
                    }
                }

                newTargetConnectionQuality -= Mathf.Lerp(0f, 0.4f, Mathf.InverseLerp(0f, 300f, entranceDist));

                float apparatusDist = 300f;

                LungProp[] apparatusArray = UnityEngine.Object.FindObjectsOfType<LungProp>(false);
                for (int i = 0; i < apparatusArray.Length; i++)
                {
                    if (apparatusArray[i].isLungDocked)
                    {
                        float newDist = Vector3.Distance(apparatusArray[i].transform.position, player.transform.position);
                        if (newDist < apparatusDist)
                        {
                            apparatusDist = newDist;
                        }
                    }
                }

                if (apparatusDist <= 50f)
                {
                    newTargetConnectionQuality -= Mathf.Lerp(0.4f, 0f, Mathf.InverseLerp(0f, 50f, apparatusDist));
                }
            }

            targetConnectionQuality = Mathf.Clamp01(newTargetConnectionQuality);

            if (targetConnectionQuality < currentConnectionQuality)
            {
                currentConnectionQuality = targetConnectionQuality;
            } 
            else if (targetConnectionQuality > currentConnectionQuality)
            {
                currentConnectionQuality += 0.005f;
            }

            if (staticChance > 0f)
            {
                // we are in the static zone
                float staticChanceMod = Mathf.Lerp(0.15f, 0.85f, staticChance);

                staticMode = UnityEngine.Random.Range(0f, 1f) < staticChanceMod;
                hardStatic = UnityEngine.Random.Range(0f, 1f) < staticChanceMod;
            } 
            else
            {
                staticMode = false;
                hardStatic = false;
            }

            UpdateConnectionQualityUI();
        }

        private void UpdateStatic(float quality, float dist = 0f)
        {
            if (quality <= 0.5f)
            {
                staticChance = Mathf.InverseLerp(0.5f, 0f, quality);

                if (staticMode)
                {
                    float listenerMod = 1f;
                    if (dist != 0f)
                    {
                        listenerMod = Mathf.InverseLerp(EAVESDROP_DIST, 0f, dist);
                        target.panStereo = 0f;
                    } 
                    else
                    {
                        target.panStereo = -0.4f;
                    }

                    if (hardStatic)
                    {
                        target.GetComponent<AudioLowPassFilter>().cutoffFrequency = 2899f;
                        target.volume = 1f * listenerMod;
                    }
                    else
                    {
                        target.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(1000f, 2800f, staticChance);
                        target.volume = Mathf.Clamp01(staticChance + 0.75f) * listenerMod;
                    }

                    if (!target.isPlaying)
                    {
                        switch (UnityEngine.Random.Range(1, 4))
                        {
                            case (1):
                                target.clip = PhoneAssetManager.phoneStaticOne;
                                break;

                            case (2):
                                target.clip = PhoneAssetManager.phoneStaticTwo;
                                break;

                            case (3):
                                target.clip = PhoneAssetManager.phoneStaticThree;
                                break;

                            default:
                                break;
                        }

                        target.Play();
                    }
                }
                else
                {
                    if (target.isPlaying) target.Stop();
                }
            } 
            else
            {
                staticChance = 0f;
                if (target.isPlaying) target.Stop();
            }
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

        [ServerRpc]
        private void UpdateConnectionQualityServerRpc(float currentConnectionQuality)
        {
            connectionQuality.Value = currentConnectionQuality;
        }

        [ClientRpc]
        public void SetNewPhoneNumberClientRpc(string number)
        {
            if (player == null)
            {
                player = transform.parent.GetComponent<PlayerControllerB>();
            }

            this.phoneNumber = number;

            if (this.IsOwner)
            {
                PhoneNetworkHandler.Instance.localPhone = this;
                isLocalPhone = true;
            }
        }

        [ClientRpc]
        public void InvalidCallClientRpc(string reason)
        {
            StartCoroutine(PhoneBusyCoroutine(reason));
        }

        [ClientRpc]
        public void RecieveCallClientRpc(int callerId, string callerNumber)
        {
            if (incomingCall == null)
            {
                StartRinging();

                incomingCall = callerNumber;
                incomingCaller = callerId;
                dialedNumbers.Clear();
                UpdateCallingUI();
            }
            else if (isLocalPhone)
            {
                // Line is busy
                PhoneNetworkHandler.Instance.LineBusyServerRpc(callerNumber);
            }
        }

        [ClientRpc]
        public void CallAcceptedClientRpc(int accepterId, string accepterNumber)
        {
            if (outgoingCall != accepterNumber)
            {
                // Whoops, how did we get this call? Send back a no.
                return;
            }

            StopRinging();
            PlayPickupSound();

            outgoingCall = null;
            activeCall = accepterNumber;
            activeCaller = accepterId;
            UpdateCallingUI();

            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[accepterId];
            Plugin.Log.LogInfo("Accepted call from " + caller.name);
            if (caller.currentVoiceChatAudioSource != null)
            {
                Plugin.Log.LogInfo("With voice chat audio source " + caller.currentVoiceChatAudioSource.name);
            } 
            else
            {
                Plugin.Log.LogInfo("With no voice chat audio source?");
            }
        }

        [ClientRpc]
        public void HangupCallClientRpc(int cancellerId, string cancellerNumber)
        {
            if (activeCall == cancellerNumber)
            {
                PlayHangupSound();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
                UpdateCallingUI();
            }
            else if (outgoingCall == cancellerNumber)
            {
                // Line busy
                PlayHangupSound();
                outgoingCall = null;
                UpdateCallingUI();
            }
            else if (incomingCall == cancellerNumber)
            {
                // incoming call cancelled
                StopRinging();
                thisAudio.Stop();
                incomingCall = null;
                UpdateCallingUI();
            }
            else
            {
                // No you can't hang up a call you're not on.
            }
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
        public void UpdateCallValuesServerRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, int incomingCallerUpdate, int activeCallerUpdate, int volumeUpdate)
        {
            UpdateCallValuesClientRpc(outgoingCallUpdate, incomingCallUpdate, activeCallUpdate, incomingCallerUpdate, activeCallerUpdate, volumeUpdate);
        }

        [ClientRpc]
        public void UpdateCallValuesClientRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, int incomingCallerUpdate, int activeCallerUpdate, int volumeUpdate)
        {
            // A little messy? I don't like this.
            outgoingCall = outgoingCallUpdate == -1 ? null : outgoingCallUpdate.ToString("D4");
            incomingCall = incomingCallUpdate == -1 ? null : incomingCallUpdate.ToString("D4");
            activeCall = activeCallUpdate == -1 ? null : activeCallUpdate.ToString("D4");
            incomingCaller = incomingCallerUpdate;
            activeCaller = activeCallerUpdate;
            currentVolume = (phoneVolume)volumeUpdate;
        }

        [ServerRpc]
        public void StopRingingServerRpc()
        {
            StopRingingClientRpc();
        }

        [ClientRpc]
        public void StopRingingClientRpc()
        {
            StopRinging();
        }

        private void StartRinging()
        {
            ringAudio.Stop();
            switch (currentVolume)
            {
                case phoneVolume.Ring:
                    activePhoneRingCoroutine = PhoneRingCoroutine(4);
                    StartCoroutine(activePhoneRingCoroutine);
                    ringAudio.clip = PhoneAssetManager.phoneRingReciever;
                    ringAudio.Play();
                    break;
                case phoneVolume.Vibrate:
                    if (isLocalPhone)
                    {
                        ringAudio.clip = PhoneAssetManager.phoneRingVibrate;
                        ringAudio.Play();
                    }
                    break;
                case phoneVolume.Silent:
                    // Nothing
                    break;
                default:
                    break;
            }
        }

        private void StopRinging()
        {
            if (activePhoneRingCoroutine != null) StopCoroutine(activePhoneRingCoroutine);
            ringAudio.Stop();
        }

        private IEnumerator PhoneRingCoroutine(int repeats)
        {
            for (int i = 0; i < repeats; i++)
            {
                RoundManager.Instance.PlayAudibleNoise(player.serverPlayerPosition, 50f, 0.95f, i, player.isInElevator && StartOfRound.Instance.hangarDoorsClosed, 0);
                yield return new WaitForSeconds(4f);
            }
        }

        private IEnumerator CallTimeoutCoroutine(string number)
        {
            yield return new WaitForSeconds(14f);

            if (outgoingCall == number)
            {
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall);
                StopLocalSound();
                outgoingCall = null;
                StartCoroutine(TemporaryStatusCoroutine("No Answer"));
            }
        }

        private IEnumerator PhoneBusyCoroutine(string status)
        {
            yield return new WaitForSeconds(2f);

            outgoingCall = null;
            PlayBusySound();
            StartCoroutine(TemporaryStatusCoroutine(status));
        }

        private IEnumerator TemporaryStatusCoroutine(string status)
        {
            phoneStatusUI.text = status;
            yield return new WaitForSeconds(2f);
            UpdateCallingUI();
        }

        [ServerRpc]
        public void ToggleServerPhoneModelServerRpc(bool active)
        {
            ToggleServerPhoneModelClientRpc(active);
        }

        [ClientRpc]
        public void ToggleServerPhoneModelClientRpc(bool active)
        {
            if (isLocalPhone)
            {
                return;
            }

            SetPhoneServerModelActive(active);

            Transform ServerArmsRig = player.meshContainer.Find("metarig").Find("Rig 1");
            ChainIKConstraint LeftArmRig = ServerArmsRig.Find("ServerLeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();

            if (active)
            {
                LeftArmRig.weight = 1f;
            } else
            {
                LeftArmRig.weight = 0f;

                SetPhoneLocalModelActive(false);
            }
        }

        [ServerRpc]
        public void ChangeItemSlotServerRpc(int slot)
        {
            ChangeItemSlotClientRpc(slot);
        }

        [ClientRpc]
        public void ChangeItemSlotClientRpc(int slot)
        {
            if (isLocalPhone)
            {
                return;
            }

            player.SwitchToItemSlot(slot, null);
        }

        private void ApplyPhoneVoiceEffect(PlayerControllerB playerController, float distance = 0f, float listeningDistance = 0f, float listeningAngle = 0f, float connectionQuality = 1f)
        {
            if (playerController == null)
            {
                return;
            }
            if (playerController.voiceMuffledByEnemy)
            {
                connectionQuality = 0f;
            }
            if (playerController.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }
            if (playerController.currentVoiceChatAudioSource == null)
            {
                Plugin.Log.LogInfo("Player " + playerController.name + " Voice Chat Audio Source still null after refresh? Something has gone wrong.");
                return;
            }

            AudioSource currentVoiceChatAudioSource = playerController.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = true;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = true;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 0f;
            playerController.currentVoiceChatIngameSettings.set2D = true;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[playerController.playerClientId];
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = GameNetworkManager.Instance.localPlayerController.isPlayerDead ? 0f : -0.4f;
            occludeAudio.lowPassOverride = Mathf.Lerp(6000f, 3000f, connectionQuality);
            lowPass.lowpassResonanceQ = Mathf.Lerp(6f, 3f, connectionQuality);
            highPass.highpassResonanceQ = Mathf.Lerp(3f, 1f, connectionQuality);

            if (distance != 0f)
            {
                float mod = Mathf.InverseLerp(BACKGROUND_VOICE_DIST, 0f, distance);
                currentVoiceChatAudioSource.volume = currentVoiceChatAudioSource.volume * mod;
                occludeAudio.lowPassOverride = 1500f;
            }

            if (listeningDistance != 0f)
            {
                float mod = Mathf.InverseLerp(EAVESDROP_DIST, 0f, listeningDistance);
                currentVoiceChatAudioSource.volume = currentVoiceChatAudioSource.volume * mod;
                occludeAudio.lowPassOverride = 750f;
                currentVoiceChatAudioSource.panStereo = listeningAngle;
            }

            if (playerController.voiceMuffledByEnemy)
            {
                occludeAudio.lowPassOverride = 500f;
            }

            if (staticMode && hardStatic)
            {
                currentVoiceChatAudioSource.volume = 0f;
            }
        }

        private void RemovePhoneVoiceEffect(int playerId)
        {
            PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
            RemovePhoneVoiceEffect(playerController);
        }

        private void RemovePhoneVoiceEffect(PlayerControllerB playerController)
        {
            if (playerController == null)
            {
                return;
            }
            if (playerController.currentVoiceChatAudioSource == null)
            {
                StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
            }

            AudioSource currentVoiceChatAudioSource = playerController.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = false;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = playerController.voiceMuffledByEnemy;

            currentVoiceChatAudioSource.volume = 1f;
            currentVoiceChatAudioSource.spatialBlend = 1f;
            playerController.currentVoiceChatIngameSettings.set2D = false;
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = 0f;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[playerController.playerClientId];
            lowPass.lowpassResonanceQ = 1f;
            highPass.highpassResonanceQ = 1f;
        }

        public static void UpdatePhoneSanity(PlayerControllerB playerController)
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

        public override void OnDestroy()
        {
            Plugin.Log.LogInfo("AAAAAAAA");

            if (target.isPlaying)
            {
                target.Stop();
            }

            if (audioSourcesInRange.Count > 0)
            {
                foreach (AudioSourceStorage storage in this.audioSourcesInRange)
                {
                    storage.Reset();
                }

                this.audioSourcesInRange.Clear();
            }
            if (modifiedPlayerVoices.Count > 0)
            {
                foreach (PlayerControllerB player in modifiedPlayerVoices)
                {
                    RemovePhoneVoiceEffect(player);
                }

                this.modifiedPlayerVoices.Clear();
            }

            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }
    }
}