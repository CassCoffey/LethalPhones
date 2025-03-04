using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Scoops.service;
using Scoops.misc;
using Scoops.customization;

namespace Scoops.patch;

[HarmonyPatch(typeof(HoarderBugAI))]
public class HoardingBugPhonePatch
{
    public static int phoneBugs = 0;

    public static PhoneNetworkHandler PhoneManager;

    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void CreatePhoneAssets(ref HoarderBugAI __instance)
    {
        PhoneManager = PhoneNetworkHandler.Instance;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            if (!PhoneNetworkHandler.Locked.Value && phoneBugs < Config.maxPhoneBugs.Value)
            {
                if (Random.Range(0f, 1f) <= Config.chancePhoneBug.Value)
                {
                    phoneBugs++;
                    GameObject bugPhone = GameObject.Instantiate(NetworkObjectManager.bugPhonePrefab, Vector3.zero, Quaternion.identity);
                    bugPhone.GetComponent<NetworkObject>().Spawn();
                    bugPhone.GetComponent<NetworkObject>().TrySetParent(__instance.transform, false);
                }
            }
        }
    }

    [HarmonyPatch("KillEnemy")]
    [HarmonyPrefix]
    private static void BugDead(ref HoarderBugAI __instance, bool destroy = false)
    {
        if (!__instance.IsServer)
        {
            return;
        }

        if (__instance != null && __instance.transform.Find("BugPhonePrefab(Clone)") != null)
        {
            HoardingPhone phone = __instance.transform.Find("BugPhonePrefab(Clone)").GetComponent<HoardingPhone>();
            if (phone != null)
            {
                phone.Death();
                phone.NetworkObject.Despawn(true);
            }
        }
    }


    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.UnloadSceneObjectsEarly))]
    [HarmonyPrefix]
    private static void UnloadScene(ref RoundManager __instance)
    {
        if (!__instance.IsServer)
        {
            return;
        }

        HoardingPhone[] array = Object.FindObjectsOfType<HoardingPhone>();
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].IsSpawned)
            {
                array[i].Death();
                array[i].NetworkObject.Despawn(true);
            }
        }
    }
}