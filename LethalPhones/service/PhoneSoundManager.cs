
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Scoops.service
{
    public class PhoneSoundManager
    {
        public static AudioClip phoneRing;

        public static void Init()
        {
            Plugin.Log.LogInfo($"Loading Sounds...");
            phoneRing = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneRing");
        }
    }
}
