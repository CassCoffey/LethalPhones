
using Scoops.misc;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Scoops.service
{
    public class AudioSourceStorage
    {
        public static float GLOBAL_SOUND_MOD = -0.1f;

        public AudioSource audioSource;
        private GameObject audioSourceHolder;
        private float origVolume;
        private float origPan;
        private bool hadLowPass;
        private bool hadHighPass;
        private bool hadOcclude;
        private float origLowPass;
        private float origLowPassResQ;
        private float origHighPass;
        private float origHighPassResQ;

        public AudioSourceStorage(AudioSource audioSource)
        {
            this.audioSource = audioSource;
            this.audioSourceHolder = audioSource.gameObject;
            this.origVolume = audioSource.volume;
            this.origPan = audioSource.panStereo;
            this.hadLowPass = audioSourceHolder.GetComponent<AudioLowPassFilter>() != null;
            this.hadHighPass = audioSourceHolder.GetComponent<AudioHighPassFilter>() != null;

            this.hadOcclude = audioSourceHolder.GetComponent<OccludeAudio>() != null;

            if (hadLowPass)
            {
                origLowPass = audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency;
                origLowPassResQ = audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ;
            }
            if (hadHighPass)
            {
                origHighPass = audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency;
                origHighPassResQ = audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ;
            }
        }

        public void InitAudio()
        {
            if (audioSource != null)
            {
                audioSource.spatialBlend = 0f;
                audioSource.panStereo = -0.4f;

                if (audioSourceHolder != null)
                {
                    if (!hadLowPass)
                    {
                        audioSourceHolder.AddComponent<AudioLowPassFilter>();
                    }
                    if (!hadHighPass)
                    {
                        audioSourceHolder.AddComponent<AudioHighPassFilter>();
                    }

                    if (hadOcclude)
                    {
                        audioSourceHolder.GetComponent<OccludeAudio>().enabled = false;
                    }

                    audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = 2899f;
                    audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 3f;
                    audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency = 1613f;
                    audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ = 1f;
                }
            }
        }

        public void ApplyPhone(float dist, float callQuality = 1f, float listenDist = 0f, float listenAngle = 0f, bool staticMode = false)
        {
            if (audioSourceHolder != null && audioSource != null)
            {
                float mod = 0f;

                if (audioSource.rolloffMode == AudioRolloffMode.Linear)
                {
                    mod = Mathf.Clamp01(Mathf.InverseLerp(audioSource.maxDistance, audioSource.minDistance, dist) + GLOBAL_SOUND_MOD);
                }
                else if (audioSource.rolloffMode == AudioRolloffMode.Custom)
                {
                    AnimationCurve audioRolloffCurve = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                    if (audioRolloffCurve != null)
                    {
                        mod = Mathf.Clamp01(audioRolloffCurve.Evaluate(dist / audioSource.maxDistance) + GLOBAL_SOUND_MOD);
                    }
                }
                else
                {
                    mod = Mathf.Clamp01((audioSource.minDistance * (1 / (1 + (dist - 1)))) + GLOBAL_SOUND_MOD);
                }

                audioSource.volume = origVolume * mod;

                if (staticMode)
                {
                    audioSource.volume = 0f;
                }

                if (audioSourceHolder.GetComponent<AudioLowPassFilter>())
                {
                    audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(2000f, 2899f, callQuality);
                    audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = Mathf.Lerp(5f, 3f, callQuality);
                }
                if (audioSourceHolder.GetComponent<AudioHighPassFilter>())
                {
                    audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ = Mathf.Lerp(2f, 1f, callQuality);
                }

                if (listenDist != 0f)
                {
                    float listenMod = Mathf.InverseLerp(Config.eavesdropDist.Value, 0f, listenDist);
                    audioSource.volume = audioSource.volume * listenMod;
                    if (audioSourceHolder.GetComponent<AudioLowPassFilter>()) audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = 750f;
                    audioSource.panStereo = listenAngle;
                }
            }
        }

        public void Reset()
        {
            if (audioSourceHolder != null)
            {
                if (audioSource != null)
                {
                    audioSource.spatialBlend = 1f;
                    audioSource.panStereo = origPan;
                    audioSource.volume = origVolume;
                }

                if (hadOcclude && audioSourceHolder.GetComponent<OccludeAudio>())
                {
                    audioSourceHolder.GetComponent<OccludeAudio>().enabled = true;
                }

                if (audioSourceHolder.GetComponent<AudioLowPassFilter>())
                {
                    if (hadLowPass)
                    {
                        audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = origLowPass;
                        audioSourceHolder.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = origLowPassResQ;
                    }
                    else
                    {
                        GameObject.Destroy(audioSourceHolder.GetComponent<AudioLowPassFilter>());
                    }
                    
                }

                if (audioSourceHolder.GetComponent<AudioHighPassFilter>())
                {
                    if (hadHighPass)
                    {
                        audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency = origHighPass;
                        audioSourceHolder.GetComponent<AudioHighPassFilter>().highpassResonanceQ = origHighPassResQ;
                    }
                    else
                    {
                        GameObject.Destroy(audioSourceHolder.GetComponent<AudioHighPassFilter>());
                    }
                }
            }
        }
    }

    public class PhoneAssetManager
    {
        public static AudioClip phoneRingCaller;
        public static AudioClip phoneRingReciever;
        public static AudioClip phonePickup;
        public static AudioClip phoneHangup;
        public static AudioClip phoneRotaryForward;
        public static AudioClip phoneRotaryBackward;
        public static AudioClip phoneRotaryStopper;
        public static AudioClip phoneRotaryFinish;
        public static AudioClip phoneBusy;
        public static AudioClip phoneRingVibrate;
        public static AudioClip phoneSwitch;
        public static AudioClip phoneStaticOne;
        public static AudioClip phoneStaticTwo;
        public static AudioClip phoneStaticThree;

        public static void Init()
        {
            Plugin.Log.LogInfo($"Loading Assets...");
            phoneRingCaller = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneRing_Caller");
            phoneRingReciever = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneRing_Reciever");
            phonePickup = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhonePickup");
            phoneHangup = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneHangup");
            phoneRotaryForward = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryDialForwardOptionTwo");
            phoneRotaryBackward = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryDialBackOptionOne");
            phoneRotaryStopper = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryStopperOptionOne");
            phoneRotaryFinish = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("RotaryFinishOptionOne");
            phoneBusy = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneBusy");
            phoneRingVibrate = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneRing_Vibrate");
            phoneSwitch = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneSwitch");
            phoneStaticOne = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneStaticOne");
            phoneStaticTwo = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneStaticTwo");
            phoneStaticThree = (AudioClip)Plugin.LethalPhoneAssets.LoadAsset("PhoneStaticThree");
        }
    }
}
