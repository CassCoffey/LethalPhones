﻿using Scoops.compatability;
using Scoops.gameobjects;
using Scoops.patch;
using Scoops.service;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.misc
{
    public class HoardingPhone : EnemyPhone
    {
        public HoarderBugAI bug;
        public GameObject serverPhoneModel;

        private float chitterInterval = 0f;
        private float randomChitterTime = 3f;

        public override void Start()
        {
            base.Start();

            bug = (HoarderBugAI)enemy;

            GameObject serverPhoneModelPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("BugServerPhoneModel");
            serverPhoneModel = GameObject.Instantiate(serverPhoneModelPrefab, bug.animationContainer.Find("Armature").Find("Abdomen").Find("Chest").Find("Head").Find("Bone.03").Find("Bone.04").Find("Bone.04_end"), false);
        }

        public override void Death()
        {
            HoardingBugPhonePatch.phoneBugs--;

            base.Death();
        }

        public override void Update()
        {
            if (activeCall != null && !(MirageCompat.Enabled && MirageCompat.IsEnemyMimicking(bug)))
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
    }
}