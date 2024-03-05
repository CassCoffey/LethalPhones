using GameNetcodeStuff;
using Scoops.service;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

namespace Scoops.misc
{
    public class PlayerPhone : NetworkBehaviour
    {
        public PlayerControllerB player;
        public GameObject localPhoneModel;
        public Transform localPhoneInteractionNode;
        public Vector3 localPhoneInteractionBase;
        public Transform localPhoneStopperNode;
        public Transform localPhoneDial;
        public List<GameObject> localPhoneDialNumbers;
        public string phoneNumber;
        public bool toggled = false;

        public TextMeshProUGUI dialingNumberUI;
        public TextMeshProUGUI personalPhoneNumberUI;

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

        private float recordingRange = 6f;
        private float maxVolume = 0.6f;

        private List<AudioSource> audioSourcesToReplay = new List<AudioSource>();
        private Dictionary<AudioSource, AudioSource> audioSourcesReceiving = new Dictionary<AudioSource, AudioSource>();

        public Collider[] collidersInRange = new Collider[30];

        private float cleanUpInterval;
        private float updateInterval;

        private float timeSinceRotaryMoved = 0f;
        private bool reversingRotary = false;
        private bool previousToggled = false;

        public void Start()
        {
            this.thisAudio = GetComponent<AudioSource>();
            this.target = transform.Find("Target").gameObject.GetComponent<AudioSource>();

            this.GetAllAudioSourcesToReplay();
            this.SetupAudiosourceClip();

            this.player = transform.parent.GetComponent<PlayerControllerB>();
            this.ringAudio = player.transform.Find("Audios").Find("PhoneAudioExternal(Clone)").GetComponent<AudioSource>();

            this.localPhoneModel = player.localArmsTransform.Find("shoulder.L").Find("arm.L_upper").Find("arm.L_lower").Find("hand.L").Find("LocalPhoneModel(Clone)").gameObject;
            localPhoneModel.SetActive(false);

            rotaryAudio = localPhoneModel.GetComponent<AudioSource>();

            this.localPhoneInteractionNode = localPhoneModel.transform.Find("LocalPhoneModel").Find("InteractionNode");
            localPhoneInteractionBase = new Vector3(localPhoneInteractionNode.localPosition.x, localPhoneInteractionNode.localPosition.y, localPhoneInteractionNode.localPosition.z);
            this.localPhoneStopperNode = localPhoneModel.transform.Find("LocalPhoneModel").Find("StopperNode");
            this.dialingNumberUI = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneTop").Find("PhoneCanvas").Find("DialingNumber").GetComponent<TextMeshProUGUI>();
            dialingNumberUI.text = "";

            this.personalPhoneNumberUI = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneTop").Find("PhoneCanvas").Find("PersonalNumber").GetComponent<TextMeshProUGUI>();

            localPhoneDial = localPhoneModel.transform.Find("LocalPhoneModel").Find("PhoneDial");
            this.localPhoneDialNumbers = new List<GameObject>(10);
            foreach (Transform child in localPhoneDial)
            {
                this.localPhoneDialNumbers.Add(child.gameObject);
            }
        }

        private void SetupAudiosourceClip()
        {
            this.target.Stop();
        }

        public void ToggleActive(bool active)
        {
            toggled = active;
            if (active)
            {
                localPhoneModel.SetActive(active);
                personalPhoneNumberUI.text = phoneNumber;
            }
        }

        // Here's where we break some bones
        public void LateUpdate()
        {
            if (isLocalPhone && Plugin.InputActionInstance.DialPhoneKey.IsPressed())
            {
                Transform handR = player.localArmsTransform.Find("shoulder.R").Find("arm.R_upper").Find("arm.R_lower").Find("hand.R");
                Transform finger1 = handR.Find("finger1.R");
                finger1.localEulerAngles = new Vector3(32.4089966f, 57.2649841f, 319.315002f);
                finger1.Find("finger1.R.001").localEulerAngles = new Vector3(288.475983f, 341.432007f, 4.1400032f);
                Transform finger2 = handR.Find("finger2.R");
                finger2.localEulerAngles = new Vector3(13.0880003f, 90f, 335.346008f);
                finger2.Find("finger2.R.001").localEulerAngles = new Vector3(351.102997f, 359.130005f, 345.136993f);
                Transform finger3 = handR.Find("finger3.R");
                finger3.localEulerAngles = new Vector3(5.7479949f, 89.9990005f, 262.154968f);
                finger3.Find("finger3.R.001").localEulerAngles = new Vector3(353.714996f, 0.0349977836f, 248.507996f);
                Transform finger4 = handR.Find("finger4.R");
                finger4.localEulerAngles = new Vector3(359.694f, 90f, 256.720001f);
                finger4.Find("finger4.R.001").localEulerAngles = new Vector3(351.419006f, 0.498000294f, 248.522003f);
                Transform finger5 = handR.Find("finger5.R");
                finger5.localEulerAngles = new Vector3(358.115021f, 90f, 241.14502f);
                finger5.Find("finger5.R.001").localEulerAngles = new Vector3(327.817017f, 329.139008f, 267.781006f);
            }
        }

        public void Update()
        {
            if (isLocalPhone)
            {
                ManageInputs();
            }

            if (this.cleanUpInterval >= 0f)
            {
                this.cleanUpInterval -= Time.deltaTime;
            }
            else
            {
                this.cleanUpInterval = 15f;
                if (this.audioSourcesReceiving.Count > 10)
                {
                    foreach (KeyValuePair<AudioSource, AudioSource> keyValuePair in this.audioSourcesReceiving)
                    {
                        if (keyValuePair.Key == null)
                        {
                            this.audioSourcesReceiving.Remove(keyValuePair.Key);
                        }
                    }
                }
            }
            if (this.updateInterval >= 0f)
            {
                this.updateInterval -= Time.deltaTime;
                return;
            }
            this.updateInterval = 0.3f;
            this.GetAllAudioSourcesToReplay();
            this.TimeAllAudioSources();
            this.UpdatePlayerVoices();

            previousToggled = toggled;
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

                    localPhoneModel.SetActive(false);
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


                if (!Plugin.InputActionInstance.PickupPhoneKey.IsPressed())
                {
                    Vector3 localPosition = localPhoneInteractionNode.localPosition;
                    localPosition.x = Mathf.Clamp(localPosition.x + vector.x, localPhoneInteractionBase.x - 0.0075f, localPhoneInteractionBase.x + 0.0075f);
                    localPosition.y = Mathf.Clamp(localPosition.y + vector.y, localPhoneInteractionBase.y - 0.0075f, localPhoneInteractionBase.y + 0.0075f);
                    localPhoneInteractionNode.localPosition = new Vector3(localPosition.x, localPosition.y, localPhoneInteractionNode.localPosition.z);
                }
                else if (!reversingRotary && Plugin.InputActionInstance.PickupPhoneKey.WasPressedThisFrame())
                {
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

                    if (closestDist <= 0.03f)
                    {
                        Plugin.Log.LogInfo("Clicking on: " + int.Parse(closestNum.name));
                        currentDialingNumber = closestNum.transform;

                        rotaryAudio.Stop();
                        rotaryAudio.clip = PhoneSoundManager.phoneRotaryForward;
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
                            rotaryAudio.PlayOneShot(PhoneSoundManager.phoneRotaryStopper);
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
                currentDialingNumber = null;
                reversingRotary = true;

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

                rotaryAudio.Stop();
                rotaryAudio.clip = PhoneSoundManager.phoneRotaryBackward;
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
                    rotaryAudio.PlayOneShot(PhoneSoundManager.phoneRotaryFinish);
                }
            }
        }

        public string GetFullDialNumber()
        {
            return String.Join("", dialedNumbers);
        }

        public void DialNumber(int number)
        {
            dialedNumbers.Enqueue(number);

            if (dialedNumbers.Count > 4)
            {
                dialedNumbers.Dequeue();
            }

            dialingNumberUI.text = GetFullDialNumber();

            Plugin.Log.LogInfo("Current dialing number: " + GetFullDialNumber());
        }

        public void HangupButtonPressed()
        {
            if (activeCall != null)
            {
                // We're on a call, hang up
                Plugin.Log.LogInfo("Hanging Up: " + activeCall);
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall);
                PlayHangupSound();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
            else if (outgoingCall != null)
            {
                // We're calling, cancel
                Plugin.Log.LogInfo("Canceling: " + outgoingCall);
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall);
                PlayHangupSound();
                outgoingCall = null;
            }

            if (isLocalPhone)
            {
                UpdateCallValues();
            }
        }

        public void CallButtonPressed()
        {
            if (Plugin.InputActionInstance.DialPhoneKey.IsPressed())
            {
                return;
            }

            if (incomingCall != null)
            {
                // We have an incoming call, pick up
                activeCall = incomingCall;
                activeCaller = incomingCaller;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall);
                StopRingingServerRpc();
                PlayPickupSound();
                Plugin.Log.LogInfo("Picking up: " + activeCall);
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
                return;
            }

            thisAudio.Play();
            outgoingCall = number;
            dialedNumbers.Clear();

            Plugin.Log.LogInfo("Dialing: " + number);

            PhoneNetworkHandler.Instance.MakeOutgoingCallServerRpc(number);
        }

        public void PlayHangupSound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneSoundManager.phoneHangup);
            }
        }

        public void PlayPickupSound()
        {
            if (isLocalPhone)
            {
                thisAudio.Stop();
                thisAudio.PlayOneShot(PhoneSoundManager.phonePickup);
            }
        }

        private void GetAllAudioSourcesToReplay()
        {
            if (activeCall == null)
            {
                return;
            }
            int num = Physics.OverlapSphereNonAlloc(base.transform.position, this.recordingRange, this.collidersInRange, Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < num; i++)
            {
                if (!this.collidersInRange[i].gameObject.GetComponent<WalkieTalkie>())
                {
                    AudioSource component = this.collidersInRange[i].GetComponent<AudioSource>();
                    if (component != null && component.isPlaying && component.clip != null && component.time > 0f && !this.audioSourcesToReplay.Contains(component))
                    {
                        this.audioSourcesToReplay.Add(component);
                    }
                }
            }
        }

        private void TimeAllAudioSources()
        {
            if (activeCaller == -1) return;

            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[activeCaller];
            PlayerPhone callerPhone = caller.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();

            if (activeCall != null)
            {
                for (int j = callerPhone.audioSourcesToReplay.Count - 1; j >= 0; j--)
                {
                    AudioSource audioSource = callerPhone.audioSourcesToReplay[j];
                    if (!(audioSource == null))
                    {
                        if (this.audioSourcesReceiving.TryAdd(audioSource, null))
                        {
                            this.audioSourcesReceiving[audioSource] = this.target.gameObject.AddComponent<AudioSource>();
                            this.audioSourcesReceiving[audioSource].clip = audioSource.clip;
                            try
                            {
                                if (audioSource.time >= audioSource.clip.length)
                                {
                                    Plugin.Log.LogInfo(string.Format("phone: {0}, {1}, {2}", audioSource.time, audioSource.clip.length, audioSource.clip.name));
                                    if (audioSource.time - 0.05f < audioSource.clip.length)
                                    {
                                        this.audioSourcesReceiving[audioSource].time = Mathf.Clamp(audioSource.time - 0.05f, 0f, 1000f);
                                    }
                                    else
                                    {
                                        this.audioSourcesReceiving[audioSource].time = audioSource.time / 5f;
                                    }
                                    Plugin.Log.LogInfo(string.Format("sourcetime: {0}", this.audioSourcesReceiving[audioSource].time));
                                }
                                else
                                {
                                    this.audioSourcesReceiving[audioSource].time = audioSource.time;
                                }
                                this.audioSourcesReceiving[audioSource].spatialBlend = 1f;
                                this.audioSourcesReceiving[audioSource].Play();
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.LogInfo(string.Format("Error while playing audio clip in phone. Clip name: {0} object: {1}; time: {2}; {3}", new object[]
                                {
                                        audioSource.clip.name,
                                        audioSource.gameObject.name,
                                        audioSource.time,
                                        ex
                                }));
                            }
                        }
                        float num = Vector3.Distance(audioSource.transform.position, callerPhone.transform.position);
                        Plugin.Log.LogInfo(string.Format("Receiving audiosource with name: {0}; recording distance: {1}", audioSource.gameObject.name, num));
                        if (num > this.recordingRange + 7f)
                        {
                            Plugin.Log.LogInfo("Recording distance out of range; removing audio with name: " + audioSource.gameObject.name);
                            AudioSource obj;
                            this.audioSourcesReceiving.Remove(audioSource, out obj);
                            UnityEngine.Object.Destroy(obj);
                            callerPhone.audioSourcesToReplay.RemoveAt(j);
                        }
                        else
                        {
                            this.audioSourcesReceiving[audioSource].volume = Mathf.Lerp(this.maxVolume, 0f, num / (this.recordingRange + 3f));
                            if ((audioSource.isPlaying && !this.audioSourcesReceiving[audioSource].isPlaying) || audioSource.clip != this.audioSourcesReceiving[audioSource].clip)
                            {
                                this.audioSourcesReceiving[audioSource].clip = audioSource.clip;
                                this.audioSourcesReceiving[audioSource].Play();
                            }
                            else if (!audioSource.isPlaying)
                            {
                                this.audioSourcesReceiving[audioSource].Stop();
                            }
                            this.audioSourcesReceiving[audioSource].time = audioSource.time;
                        }
                    }
                }
            }
            else if (activeCall == null)
            {
                activeCaller = -1;
                foreach (AudioSource key in callerPhone.audioSourcesToReplay)
                {
                    if (this.audioSourcesReceiving.ContainsKey(key))
                    {
                        AudioSource obj;
                        this.audioSourcesReceiving.Remove(key, out obj);
                        UnityEngine.Object.Destroy(obj);
                    }
                }
                callerPhone.audioSourcesToReplay.Clear();
            }
        }

        private void UpdatePlayerVoices()
        {
            if (player == null || GameNetworkManager.Instance == null || player != GameNetworkManager.Instance.localPlayerController || GameNetworkManager.Instance.localPlayerController == null || !isLocalPhone)
            {
                return;
            }

            if (activeCaller != -1)
            {
                PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[activeCaller];

                applyPhoneVoiceEffect(caller);

                // Later we'll hear others in the background
                for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                {

                }
            }
        }

        [ClientRpc]
        public void SetNewPhoneNumberClientRpc(string number)
        {
            if (player == null)
            {
                player = transform.parent.GetComponent<PlayerControllerB>();
            }

            Plugin.Log.LogInfo("New Phone Setup");

            this.phoneNumber = number;

            if (this.IsOwner)
            {
                Plugin.Log.LogInfo("This is our phone, setting local");
                PhoneNetworkHandler.Instance.localPhone = this;
                isLocalPhone = true;
            }

            if (isLocalPhone) Plugin.Log.LogInfo("New Phone for " + player.name + "! Your number is: " + phoneNumber);
        }

        [ClientRpc]
        public void InvalidCallClientRpc()
        {
            Plugin.Log.LogInfo("Invalid number.");

            PlayHangupSound();
            outgoingCall = null;
        }

        [ClientRpc]
        public void RecieveCallClientRpc(int callerId, string callerNumber)
        {
            Plugin.Log.LogInfo("Someone is calling with ID " + callerId + " with number " + callerNumber);
            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[callerId];

            RoundManager.Instance.PlayAudibleNoise(player.serverPlayerPosition, 16f, 0.9f, 0, player.isInElevator && StartOfRound.Instance.hangarDoorsClosed, 0);
            ringAudio.Play();

            if (isLocalPhone) Plugin.Log.LogInfo("You've got a call from " + caller.name + " with number " + callerNumber);

            if (incomingCall == null && activeCall == null)
            {
                Plugin.Log.LogInfo("Updating call values for " + player.name);
                incomingCall = callerNumber;
                incomingCaller = callerId;
            }
            else if (isLocalPhone)
            {
                // Line is busy
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(callerNumber);
            }
        }

        [ClientRpc]
        public void CallAcceptedClientRpc(int accepterId, string accepterNumber)
        {
            PlayerControllerB accepter = StartOfRound.Instance.allPlayerScripts[accepterId];

            if (isLocalPhone) Plugin.Log.LogInfo("Your call was accepted by " + accepter.name + " with number " + accepterNumber);

            if (outgoingCall != accepterNumber)
            {
                Plugin.Log.LogInfo("We got a call we never made? " + player.name);
                // Whoops, how did we get this call? Send back a no.
                return;
            }

            ringAudio.Stop();
            PlayPickupSound();

            outgoingCall = null;
            activeCall = accepterNumber;
            activeCaller = accepterId;
        }

        [ClientRpc]
        public void HangupCallClientRpc(int cancellerId, string cancellerNumber)
        {
            PlayerControllerB canceller = StartOfRound.Instance.allPlayerScripts[cancellerId];

            if (isLocalPhone) Plugin.Log.LogInfo("Your call was hung up by " + canceller.name + " with number " + cancellerNumber);

            if (activeCall == cancellerNumber)
            {
                PlayHangupSound();
                activeCall = null;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();
            }
            else if (outgoingCall == cancellerNumber)
            {
                // outgoing call was invalid
                outgoingCall = null;
            }
            else if (incomingCall == cancellerNumber)
            {
                // incoming call cancelled
                ringAudio.Stop();
                thisAudio.Stop();
                incomingCall = null;
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
                   activeCaller);
        }

        [ServerRpc]
        public void UpdateCallValuesServerRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, int incomingCallerUpdate, int activeCallerUpdate)
        {
            UpdateCallValuesClientRpc(outgoingCallUpdate, incomingCallUpdate, activeCallUpdate, incomingCallerUpdate, activeCallerUpdate);
        }

        [ClientRpc]
        public void UpdateCallValuesClientRpc(int outgoingCallUpdate, int incomingCallUpdate, int activeCallUpdate, int incomingCallerUpdate, int activeCallerUpdate)
        {
            // A little messy? I don't like this.
            outgoingCall = outgoingCallUpdate == -1 ? null : outgoingCallUpdate.ToString("D4");
            incomingCall = incomingCallUpdate == -1 ? null : incomingCallUpdate.ToString("D4");
            activeCall = activeCallUpdate == -1 ? null : activeCallUpdate.ToString("D4");
            incomingCaller = incomingCallerUpdate;
            activeCaller = activeCallerUpdate;
        }

        [ServerRpc]
        public void StopRingingServerRpc()
        {
            StopRingingClientRpc();
        }

        [ClientRpc]
        public void StopRingingClientRpc()
        {
            ringAudio.Stop();
        }

        private static void applyPhoneVoiceEffect(PlayerControllerB playerController)
        {
            AudioSource currentVoiceChatAudioSource = playerController.currentVoiceChatAudioSource;
            AudioLowPassFilter lowPass = currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>();
            AudioHighPassFilter highPass = currentVoiceChatAudioSource.GetComponent<AudioHighPassFilter>();
            OccludeAudio occludeAudio = currentVoiceChatAudioSource.GetComponent<OccludeAudio>();

            highPass.enabled = true;
            lowPass.enabled = true;
            occludeAudio.overridingLowPass = true;

            currentVoiceChatAudioSource.spatialBlend = 0f;
            playerController.currentVoiceChatIngameSettings.set2D = true;
            currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[playerController.playerClientId];
            currentVoiceChatAudioSource.bypassListenerEffects = false;
            currentVoiceChatAudioSource.bypassEffects = false;
            currentVoiceChatAudioSource.panStereo = GameNetworkManager.Instance.localPlayerController.isPlayerDead ? 0f : 0.4f;
            occludeAudio.lowPassOverride = 4000f;
            lowPass.lowpassResonanceQ = 3f;
        }
    }
}