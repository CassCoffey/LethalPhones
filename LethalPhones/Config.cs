using BepInEx.Configuration;

namespace Scoops
{
    public class Config
    {
        public static ConfigEntry<float> recordingStartDist;
        public static ConfigEntry<float> backgroundVoiceDist;
        public static ConfigEntry<float> eavesdropDist;
        public static ConfigEntry<float> backgroundSoundMod;

        public static ConfigEntry<int> maxPhoneBugs;
        public static ConfigEntry<float> chancePhoneBug;
        public static ConfigEntry<float> minPhoneBugInterval;
        public static ConfigEntry<float> maxPhoneBugInterval;

        public Config(ConfigFile cfg)
        {
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
        }
    }
}
