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
        public override void Start()
        {
            base.Start();

            this.ringAudio = transform.Find("RingerAudio").GetComponent<AudioSource>();
            ringAudio.volume = Config.ringtoneVolume.Value;

            PhoneNetworkHandler.Instance.RegisterSwitchboard(this.NetworkObjectId);
        }
    }
}
