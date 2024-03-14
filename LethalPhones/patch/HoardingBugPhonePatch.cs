using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Scoops.service;
using System;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using System.ComponentModel;
using System.Collections.Generic;
using Scoops.misc;
using System.Reflection;
using System.Reflection.Emit;

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
        Plugin.Log.LogInfo("A new bug has appeared. There are " + phoneBugs + " existing phone bugs.");

        PhoneManager = PhoneNetworkHandler.Instance;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            if (phoneBugs < 1)
            {
                phoneBugs++;
                Plugin.Log.LogInfo("Making a bug phone.");
                GameObject bugPhone = GameObject.Instantiate(NetworkObjectManager.bugPhonePrefab, Vector3.zero, Quaternion.identity);
                bugPhone.GetComponent<NetworkObject>().Spawn();
                bugPhone.GetComponent<NetworkObject>().TrySetParent(__instance.transform, false);

                PhoneManager.CreateNewPhone(bugPhone.GetComponent<NetworkObject>().NetworkObjectId);
            }
        }
    }
}