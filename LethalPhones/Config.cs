using BepInEx.Configuration;

namespace Scoops
{
    public class Config
    {
        public static ConfigEntry<float> ringtoneVolume;
        public static ConfigEntry<float> recordingDist;
        public static ConfigEntry<float> listeningDist;
        public static ConfigEntry<float> backgroundSoundAdjust;
        public static ConfigEntry<float> voiceSoundAdjust;
        public static ConfigEntry<float> staticSoundAdjust;
        public static ConfigEntry<float> deathHangupTime;
        public static ConfigEntry<bool> hideHands;
        public static ConfigEntry<bool> disableRingtones;
        public static ConfigEntry<bool> removeBaseSkins;
        public static ConfigEntry<bool> removeBaseCharms;
        public static ConfigEntry<bool> removeBaseRingtones;
        public static ConfigEntry<string> customizationBlacklist;

        public static ConfigEntry<bool> hangupOnPutaway;
        public static ConfigEntry<bool> respawnClipboard;
        public static ConfigEntry<bool> enableStartClipboard;
        public static ConfigEntry<int> maxPhoneNumber;

        public static ConfigEntry<int> maxPhoneBugs;
        public static ConfigEntry<float> chancePhoneBug;
        public static ConfigEntry<float> minPhoneBugInterval;
        public static ConfigEntry<float> maxPhoneBugInterval;

        public static ConfigEntry<int> maxPhoneMasked;
        public static ConfigEntry<float> chancePhoneMask;
        public static ConfigEntry<float> minPhoneMaskedInterval;
        public static ConfigEntry<float> maxPhoneMaskedInterval;

        public static ConfigEntry<bool> phonePurchase;
        public static ConfigEntry<int> phonePrice;
        public static ConfigEntry<bool> clipboardPurchase;
        public static ConfigEntry<int> clipboardPrice;
        public static ConfigEntry<bool> switchboardPurchase;
        public static ConfigEntry<int> switchboardPrice;
        public static ConfigEntry<string> switchboardNumber;

        public Config(ConfigFile cfg)
        {
            // General
            ringtoneVolume = cfg.Bind(
                    "General",
                    "ringtoneVolume",
                    0.6f,
                    "The volume of phone ringtones (0-1)."
            );
            voiceSoundAdjust = cfg.Bind(
                    "General",
                    "voiceSoundAdjust",
                    1f,
                    "All voices on calls have their volume multiplied by this value."
            );
            backgroundSoundAdjust = cfg.Bind(
                    "General",
                    "backgroundSoundAdjust",
                    0.8f,
                    "All background noises on calls have their volume multiplied by this value."
            );
            staticSoundAdjust = cfg.Bind(
                    "General",
                    "staticSoundAdjust",
                    0.6f,
                    "All static noises on calls have their volume multiplied by this value."
            );
            recordingDist = cfg.Bind(
                    "General",
                    "recordingDist",
                    200f,
                    "The distance at which phones are able to record audio."
            );
            listeningDist = cfg.Bind(
                    "General",
                    "listeningDist",
                    15f,
                    "The distance at which you can hear audio from a phone."
            );
            deathHangupTime = cfg.Bind(
                    "General",
                    "deathHangupTime",
                    0.5f,
                    "The time it takes (in seconds) for a call to auto-hangup after death."
            );
            hideHands = cfg.Bind(
                    "General",
                    "hideHands",
                    false,
                    "If true, the model's right hand will not be used for dialing. (useful if using a custom model with hands too big/small for the phone)"
            );
            disableRingtones = cfg.Bind(
                    "General",
                    "disableRingtones",
                    false,
                    "If true, ringtone customizations will not be used for other players and their phones will always use the default ringtone. You can still customize your own."
            );
            removeBaseSkins = cfg.Bind(
                    "General",
                    "removeBaseSkins",
                    false,
                    "If true, only the default skin from the mod will be shown in the customization screen. Addon mods will still show."
            );
            removeBaseCharms = cfg.Bind(
                    "General",
                    "removeBaseCharms",
                    false,
                    "If true, only the default charm from the mod will be shown in the customization screen. Addon mods will still show."
            );
            removeBaseRingtones = cfg.Bind(
                    "General",
                    "removeBaseRingtones",
                    false,
                    "If true, only the default ringtone from the mod will be shown in the customization screen. Addon mods will still show."
            );
            customizationBlacklist = cfg.Bind(
                    "General",
                    "customizationBlacklist",
                    "",
                    "A comma-separated list of lowercase customization names that you do not want loaded. This works for base customizations and addons. Include the bundle name, eg: 'lethalphones.customizations.buggybuddy,lethalphones.customizations.rust'"
            );

            // Balance
            hangupOnPutaway = cfg.Bind(
                    "Balance",
                    "hangupOnPutaway",
                    false,
                    "If true, the phone will hang up any active calls when it is put away, so you cannot talk on the phone without actively holding it."
            );
            respawnClipboard = cfg.Bind(
                    "Balance",
                    "respawnClipboard",
                    false,
                    "If true, the phonebook clipboard will respawn back on the ship if it is lost or sold."
            );
            enableStartClipboard = cfg.Bind(
                    "Balance",
                    "enableStartClipboard",
                    true,
                    "If true, one free Phonebook Clipboard will be added to the ship at the start of new saves."
            );
            maxPhoneNumber = cfg.Bind(
                    "Balance",
                    "maxPhoneNumber",
                    10000,
                    "This is the number of phone numbers that can be generated. The default of 10000 means numbers 0000 - 9999. You can lower this if you don't want to have to remember 4 digits, remember that the leading 0s will always be there. So putting 10 here will generate numbers from 0000 - 0009."
            );

            // Enemies
            // Hoarding Bugs
            maxPhoneBugs = cfg.Bind(
                    "Enemies.HoardingBugs",
                    "maxPhoneBugs",
                    1,
                    "Maximum number of Hoarding Bugs that can spawn with phones."
            );
            chancePhoneBug = cfg.Bind(
                    "Enemies.HoardingBugs",
                    "chancePhoneBug",
                    0.1f,
                    "The chance (0 - 1) that a Hoarding Bug will be spawned with a phone."
            );
            minPhoneBugInterval = cfg.Bind(
                    "Enemies.HoardingBugs",
                    "minPhoneBugInterval",
                    15f,
                    "The shortest time (in seconds) between calls from each Hoarding Bug."
            );
            maxPhoneBugInterval = cfg.Bind(
                    "Enemies.HoardingBugs",
                    "maxPhoneBugInterval",
                    120f,
                    "The longest time (in seconds) between calls from each Hoarding Bug."
            );

            // Masked
            maxPhoneMasked = cfg.Bind(
                    "Enemies.Masked",
                    "maxPhoneMasked",
                    2,
                    "Maximum number of Masked that can spawn with phones. NOTE: This only functions if you are using Mirage."
            );
            chancePhoneMask = cfg.Bind(
                    "Enemies.Masked",
                    "chancePhoneMask",
                    0.75f,
                    "The chance (0 - 1) that a Masked will be spawned with a phone."
            );
            minPhoneMaskedInterval = cfg.Bind(
                    "Enemies.Masked",
                    "minPhoneMaskedInterval",
                    60f,
                    "The shortest time (in seconds) between calls from each Masked."
            );
            maxPhoneMaskedInterval = cfg.Bind(
                    "Enemies.Masked",
                    "maxPhoneMaskedInterval",
                    180f,
                    "The longest time (in seconds) between calls from each Masked."
            );

            // Unlockables
            phonePurchase = cfg.Bind(
                    "Unlockables",
                    "phonePurchase",
                    false,
                    "Do phones need to be unlocked at the Shop to be used?"
            );
            phonePrice = cfg.Bind(
                    "Unlockables",
                    "phonePrice",
                    200,
                    "The cost of unlocking Phones."
            );
            clipboardPurchase = cfg.Bind(
                    "Unlockables",
                    "clipboardPurchase",
                    true,
                    "Can additional Phonebook Clipboards be purchased?"
            );
            clipboardPrice = cfg.Bind(
                    "Unlockables",
                    "clipboardPrice",
                    10,
                    "The cost of Phonebook Clipboards."
            );
            switchboardPurchase = cfg.Bind(
                    "Unlockables",
                    "switchboardPurchase",
                    true,
                    "Can the switchboard be purchased?"
            );
            switchboardPrice = cfg.Bind(
                    "Unlockables",
                    "switchboardPrice",
                    500,
                    "The cost of the Phone Switchboard."
            );
            switchboardNumber = cfg.Bind(
                    "Unlockables",
                    "switchboardNumber",
                    "1111",
                    "The phone number of the Switchboard. This should always be 4 digits."
            );
        }
    }
}
