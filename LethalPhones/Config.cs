using BepInEx.Configuration;

namespace Scoops
{
    public class Config
    {
        public static ConfigEntry<float> recordingStartDist;
        public static ConfigEntry<float> backgroundVoiceDist;
        public static ConfigEntry<float> eavesdropDist;
        public static ConfigEntry<float> backgroundSoundMod;
        public static ConfigEntry<float> voiceSoundMod;
        public static ConfigEntry<float> deathHangupTime;
        public static ConfigEntry<bool> hideHands;
        public static ConfigEntry<bool> disableRingtones;
        public static ConfigEntry<bool> removeBaseSkins;
        public static ConfigEntry<bool> removeBaseCharms;
        public static ConfigEntry<bool> removeBaseRingtones;

        public static ConfigEntry<bool> hangupOnPutaway;
        public static ConfigEntry<bool> respawnClipboard;

        public static ConfigEntry<int> maxPhoneBugs;
        public static ConfigEntry<float> chancePhoneBug;
        public static ConfigEntry<float> minPhoneBugInterval;
        public static ConfigEntry<float> maxPhoneBugInterval;

        public static ConfigEntry<bool> phonePurchase;
        public static ConfigEntry<int> phonePrice;

        public Config(ConfigFile cfg)
        {
            // General
            voiceSoundMod = cfg.Bind(
                    "General",
                    "voiceSoundMod",
                    0f,
                    "All voices on calls have their volume adjusted by this value."
            );
            backgroundSoundMod = cfg.Bind(
                    "General",
                    "backgroundSoundMod",
                    -0.1f,
                    "All background noises on calls have their volume adjusted by this value."
            );
            recordingStartDist = cfg.Bind(
                    "General",
                    "recordingStartDist",
                    15f,
                    "Disables phones while in this distance to the person you're calling."
            );
            backgroundVoiceDist = cfg.Bind(
                    "General",
                    "backgroundVoiceDist",
                    20f,
                    "The distance at which you can hear other players in the background of a call."
            );
            eavesdropDist = cfg.Bind(
                    "General",
                    "eavesdropDist",
                    5f,
                    "The distance at which you can listen in on someone else's call."
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

            // Enemies
            maxPhoneBugs = cfg.Bind(
                    "Enemies.HoardingBugs",                                             // Config section
                    "maxPhoneBugs",                                                     // Key of this config
                    1,                                                                  // Default value
                    "Maximum number of Hoarding Bugs that can spawn with phones."       // Description
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
                    10f,
                    "The shortest time (in seconds) between calls from each Hoarding Bug."
            );
            maxPhoneBugInterval = cfg.Bind(
                    "Enemies.HoardingBugs",
                    "maxPhoneBugInterval",
                    100f,
                    "The longest time (in seconds) between calls from each Hoarding Bug."
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
        }
    }
}
