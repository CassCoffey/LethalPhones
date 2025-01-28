using LethalLib.Modules;
using Scoops.compatability;
using Scoops.customization;
using Scoops.gameobjects;
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
    public class MaskedPhone : EnemyPhone
    {
        public MaskedPlayerEnemy masked;
        public GameObject serverPhoneModel;

        public TextMeshProUGUI serverPersonalPhoneNumberUI;

        private Transform upperSpine;

        private bool armsActive = false;

        public override void Start()
        {
            base.Start();

            masked = (MaskedPlayerEnemy)enemy;

            upperSpine = masked.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003");

            GameObject serverPhoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerPhoneModel");
            serverPhoneModel = GameObject.Instantiate(serverPhoneModelPrefab, upperSpine.Find("shoulder.L/arm.L_upper/arm.L_lower/hand.L"), false);

            Transform ServerArmsRig = masked.transform.Find("ScavengerModel/metarig/Rig 1");
            GameObject leftArmServerPhoneRigPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerLeftArmPhone");
            GameObject leftArmServerPhoneTargetPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ServerPhoneTargetHolder");

            GameObject serverLeftArmPhoneRig = GameObject.Instantiate(leftArmServerPhoneRigPrefab, ServerArmsRig, false);

            GameObject serverLeftArmPhoneTarget = GameObject.Instantiate(leftArmServerPhoneTargetPrefab, upperSpine, false);

            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.root = upperSpine.Find("shoulder.L/arm.L_upper");
            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.tip = upperSpine.Find("shoulder.L/arm.L_upper/arm.L_lower/hand.L");
            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().data.target = serverLeftArmPhoneTarget.transform.Find("ServerPhoneTarget");

            serverLeftArmPhoneRig.GetComponent<ChainIKConstraint>().MarkDirty();

            masked.transform.Find("ScavengerModel/metarig").GetComponent<RigBuilder>().Build();

            SetPhoneServerModelActive(false);

            ChainIKConstraint LeftArmRig = ServerArmsRig.Find("ServerLeftArmPhone(Clone)").GetComponent<ChainIKConstraint>();
            LeftArmRig.weight = 0f;

            Transform serverPhoneCanvas = serverPhoneModel.transform.Find("ServerPhoneModel/PhoneTop/PhoneCanvas");
            this.serverPersonalPhoneNumberUI = serverPhoneCanvas.Find("PersonalNumber").GetComponent<TextMeshProUGUI>();

            if (IsOwner)
            {
                StartCoroutine(CustomizationCoroutine());
            }
        }

        // wait for a moment before syncing the phone customizations
        public IEnumerator CustomizationCoroutine()
        {
            yield return new WaitForSeconds(2f);

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

        public override void Death()
        {
            MaskedPhonePatch.phoneMasks--;

            base.Death();
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
                }
                else if (activeCall != null && !preppingHangup)
                {
                    preppingHangup = true;
                    activeCallHangupCoroutine = CallHangupCoroutine(activeCall, 30f);
                    StartCoroutine(activeCallHangupCoroutine);
                }
            }

            base.Update();
        }

        protected override void ApplySkin(string skinId)
        {
            GameObject skinObject = CustomizationManager.skinCustomizations[skinId];
            if (skinObject == null || serverPhoneModel == null) return;

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
            if (charmPrefab == null || serverPhoneModel == null) return;

            Transform serverPhoneDisplay = serverPhoneModel.transform.Find("ServerPhoneModel");

            if (serverPhoneDisplay.Find("CharmAttach").childCount == 0)
            {
                GameObject.Instantiate(charmPrefab, serverPhoneDisplay.Find("CharmAttach"));
            }
        }

        protected override IEnumerator PickupDelayCoroutine(float time)
        {
            preppingPickup = true;
            yield return new WaitForSeconds(time);

            if (incomingCall != null && outgoingCall == null && activeCall == null && !enemy.isEnemyDead)
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

        protected override IEnumerator CallDelayCoroutine(float time)
        {
            preppingCall = true;
            yield return new WaitForSeconds(time);

            if (incomingCall == null && outgoingCall == null && activeCall == null && !enemy.isEnemyDead)
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

        protected override IEnumerator CallTimeoutCoroutine(string number)
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

        [ServerRpc]
        public void ToggleServerPhoneModelServerRpc(bool active)
        {
            ToggleServerPhoneModelClientRpc(active);
        }

        [ClientRpc]
        public void ToggleServerPhoneModelClientRpc(bool active)
        {
            SetPhoneServerModelActive(active);

            Transform ServerArmsRig = masked.transform.Find("ScavengerModel/metarig/Rig 1");
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
    }
}