using HarmonyLib;
using Scoops.misc;
using Scoops.service;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.patch
{
    [HarmonyPatch]
    public class NetworkObjectManager
    {
        static GameObject networkPrefab = null;
        public static GameObject phonePrefab = null;
        public static GameObject bugPhonePrefab = null;

        [HarmonyPostfix, HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
        public static void Init()
        {
            if (networkPrefab == null)
            {
                networkPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhoneNetworkHandler");
                networkPrefab.AddComponent<PhoneNetworkHandler>();

                NetworkManager.Singleton.AddNetworkPrefab(networkPrefab);
            }

            if (phonePrefab == null)
            {
                phonePrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhonePrefab");
                phonePrefab.AddComponent<PlayerPhone>();

                NetworkManager.Singleton.AddNetworkPrefab(phonePrefab);
            }

            if (bugPhonePrefab == null)
            {
                bugPhonePrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("BugPhonePrefab");
                bugPhonePrefab.AddComponent<HoardingPhone>();

                NetworkManager.Singleton.AddNetworkPrefab(bugPhonePrefab);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        static void SpawnNetworkHandler()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                var networkHandlerHost = Object.Instantiate(networkPrefab, Vector3.zero, Quaternion.identity);
                networkHandlerHost.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
}
