
using LethalLib.Extras;
using LethalLib.Modules;
using Scoops.misc;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Scoops.service
{
    public class PhoneAssetManager
    {
        public static AudioClip phoneRingCaller;
        public static AudioClip phoneRingReciever;
        public static AudioClip switchboardRing;
        public static AudioClip phonePickup;
        public static AudioClip phoneHangup;
        public static AudioClip phoneRotaryForward;
        public static AudioClip phoneRotaryBackward;
        public static AudioClip phoneRotaryStopper;
        public static AudioClip phoneRotaryFinish;
        public static AudioClip phoneFlipOpen;
        public static AudioClip phoneFlipClosed;
        public static AudioClip phoneBusy;
        public static AudioClip phoneRingVibrate;
        public static AudioClip phoneSwitch;

        public static Material greenLight;
        public static Material redLight;
        public static Material offLight;

        public static GameObject customizationGUI;
        public static GameObject headphoneDisplayPrefab;

        public const string PHONE_UNLOCK_NAME = "Personal Phones";

        public static void Init()
        {
            Plugin.Log.LogInfo($"Loading Assets...");

            if (Config.phonePurchase.Value)
            {
                UnlockableItem personalPhones = new UnlockableItem();
                personalPhones.unlockableName = PHONE_UNLOCK_NAME;
                personalPhones.IsPlaceable = false;
                personalPhones.spawnPrefab = false;
                personalPhones.alwaysInStock = true;
                personalPhones.canBeStored = false;
                personalPhones.unlockableType = 1;
                personalPhones.maxNumber = 1;

                TerminalNode itemInfo = ScriptableObject.CreateInstance<TerminalNode>();
                itemInfo.name = "PersonalPhonesInfoNode";
                itemInfo.displayText = "Personal Phones for the whole crew! These do not take up an item slot or require battery, but tend to be difficult to work with in stressful situations.\n\n";
                itemInfo.clearPreviousText = true;
                itemInfo.maxCharactersToType = 25;

                Unlockables.RegisterUnlockable(personalPhones, StoreType.ShipUpgrade, null, null, itemInfo, Config.phonePrice.Value);
            }

            customizationGUI = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhoneCustomization_GUI");
            headphoneDisplayPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("HeadsetMicrophone");

            phoneRingCaller = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneRing_Caller");
            phoneRingReciever = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneRing_Reciever");
            switchboardRing = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("SwitchboardRing");
            phonePickup = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhonePickup");
            phoneHangup = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneHangup");
            phoneRotaryForward = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryDialForwardOptionTwo");
            phoneRotaryBackward = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryDialBackOptionOne");
            phoneRotaryStopper = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryStopperOptionOne");
            phoneRotaryFinish = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryFinishOptionOne");
            phoneFlipOpen = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneFlipOpen");
            phoneFlipClosed = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneFlipClosed");
            phoneBusy = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneBusy");
            phoneRingVibrate = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneRing_Vibrate");
            phoneSwitch = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneSwitch");

            greenLight = (Material)Plugin.LethalPhoneAssets.LoadAsset("GreenLight");
            redLight = (Material)Plugin.LethalPhoneAssets.LoadAsset("RedLight");
            offLight = (Material)Plugin.LethalPhoneAssets.LoadAsset("OffLight");
        }
    }
}
