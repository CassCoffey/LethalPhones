using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Scoops.service;
using Scoops.misc;
using Scoops.customization;
using Scoops.compatability;

namespace Scoops.patch;

[HarmonyPatch(typeof(MaskedPlayerEnemy))]
public class MaskedPhonePatch
{
    public static int phoneMasks = 0;

    public static PhoneNetworkHandler PhoneManager;

    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void CreatePhoneAssets(ref MaskedPlayerEnemy __instance)
    {
        PhoneManager = PhoneNetworkHandler.Instance;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            if (MirageCompat.Enabled)
            {
                if (phoneMasks < Config.maxPhoneMasked.Value)
                {
                    if (Random.Range(0f, 1f) <= Config.chancePhoneMask.Value)
                    {
                        phoneMasks++;
                        GameObject maskPhone = GameObject.Instantiate(NetworkObjectManager.maskPhonePrefab, Vector3.zero, Quaternion.identity);
                        maskPhone.GetComponent<NetworkObject>().Spawn();
                        maskPhone.GetComponent<NetworkObject>().TrySetParent(__instance.transform, false);
                    }
                }
            }
        }
    }

    [HarmonyPatch("KillEnemy")]
    [HarmonyPrefix]
    private static void MaskDead(ref MaskedPlayerEnemy __instance, bool destroy = false)
    {
        if (!__instance.IsServer)
        {
            return;
        }

        if (__instance != null && __instance.transform.Find("MaskPhonePrefab(Clone)") != null)
        {
            MaskedPhone phone = __instance.transform.Find("MaskPhonePrefab(Clone)").GetComponent<MaskedPhone>();
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

        MaskedPhone[] array = Object.FindObjectsOfType<MaskedPhone>();
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