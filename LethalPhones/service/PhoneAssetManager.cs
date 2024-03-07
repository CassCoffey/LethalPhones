
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Scoops.service
{
    public class AudioSourceStorage
    {
        public AudioSource audioSource;
        private GameObject audioSourceHolder;
        private float origVolume;
        private float origPan;
        private bool hadDistortion;
        private bool hadLowPass;
        private bool hadHighPass;
        private float origDistortion;
        private float origLowPass;
        private float origHighPass;
        
        public AudioSourceStorage(AudioSource audioSource)
        {
            this.audioSource = audioSource;
            this.audioSourceHolder = audioSource.gameObject;
            this.origVolume = audioSource.volume;
            this.origPan = audioSource.panStereo;
            this.hadDistortion = audioSourceHolder.GetComponent<AudioDistortionFilter>() != null;
            this.hadLowPass = audioSourceHolder.GetComponent<AudioLowPassFilter>() != null;
            this.hadHighPass = audioSourceHolder.GetComponent<AudioHighPassFilter>() != null;

            if (hadDistortion)
            {
                origDistortion = audioSourceHolder.GetComponent<AudioDistortionFilter>().distortionLevel;
            }
            if (hadLowPass)
            {
                origLowPass = audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency;
            }
            if (hadHighPass)
            {
                origHighPass = audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency;
            }
        }

        public void InitAudio()
        {
            audioSource.spatialBlend = 0f;
            audioSource.panStereo = -0.4f;

            if (!hadDistortion)
            {
                audioSourceHolder.AddComponent<AudioDistortionFilter>();
            }
            if (!hadLowPass)
            {
                audioSourceHolder.AddComponent<AudioLowPassFilter>();
            }
            if (!hadHighPass)
            {
                audioSourceHolder.AddComponent<AudioHighPassFilter>();
            }

            audioSourceHolder.GetComponent<AudioDistortionFilter>().distortionLevel = 0.4f;
            audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = 2899f;
            audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency = 1613f;
        }

        public void ApplyPhone(Vector3 position)
        {
            float dist = Vector3.Distance(position, audioSource.transform.position);
            if (audioSource.rolloffMode == AudioRolloffMode.Linear)
            {
                float mod = Mathf.Clamp01(Mathf.InverseLerp(audioSource.minDistance, audioSource.maxDistance, dist) - 0.1f);
                audioSource.volume = origVolume * mod;
            } 
            else
            {
                float mod = Mathf.Clamp01((audioSource.minDistance * (1 / (2 * (dist - 1)))) - 0.1f);
                audioSource.volume = origVolume * mod;
            }
        }

        public void ApplyPhone(float dist)
        {
            if (audioSource.rolloffMode == AudioRolloffMode.Linear)
            {
                float mod = Mathf.Clamp01(Mathf.InverseLerp(audioSource.minDistance, audioSource.maxDistance, dist) - 0.1f);
                audioSource.volume = origVolume * mod;
            }
            else
            {
                float mod = Mathf.Clamp01((audioSource.minDistance * (1 / (2 * (dist - 1)))) - 0.1f);
                audioSource.volume = origVolume * mod;
            }
        }

        public void Reset()
        {
            audioSource.spatialBlend = 1f;
            audioSource.panStereo = origPan;
            audioSource.volume = origVolume;
            if (hadDistortion)
            {
                audioSourceHolder.GetComponent<AudioDistortionFilter>().distortionLevel = origDistortion;
            } 
            else
            {
                GameObject.Destroy(audioSourceHolder.GetComponent<AudioDistortionFilter>());
            }

            if (hadLowPass)
            {
                audioSourceHolder.GetComponent<AudioLowPassFilter>().cutoffFrequency = origLowPass;
            }
            else
            {
                GameObject.Destroy(audioSourceHolder.GetComponent<AudioLowPassFilter>());
            }

            if (hadHighPass)
            {
                audioSourceHolder.GetComponent<AudioHighPassFilter>().cutoffFrequency = origHighPass;
            }
            else
            {
                GameObject.Destroy(audioSourceHolder.GetComponent<AudioHighPassFilter>());
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

        public static void Init()
        {
            Plugin.Log.LogInfo($"Loading Assets...");
            Plugin.Log.LogInfo(String.Join(", ", Plugin.LethalPhoneAssets.GetAllAssetNames()));
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
        }
    }
}
