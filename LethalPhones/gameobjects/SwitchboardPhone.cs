using LethalLib.Modules;
using Scoops.misc;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using Scoops.service;

namespace Scoops.gameobjects
{
    public class SwitchboardPhone : PhoneBehavior
    {
        private Transform[] phoneInfoArray;

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

            PhoneNetworkHandler.Instance.RegisterSwitchboard(this.NetworkObjectId);
        }
    }
}
