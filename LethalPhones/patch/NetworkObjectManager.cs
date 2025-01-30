using HarmonyLib;
using LethalLib.Modules;
using Scoops.gameobjects;
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
        public static GameObject maskPhonePrefab = null;
        public static GameObject clipboardPrefab = null;
        public static GameObject switchboardPrefab = null;

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

            if (maskPhonePrefab == null)
            {
                maskPhonePrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("MaskPhonePrefab");
                maskPhonePrefab.AddComponent<MaskedPhone>();

                NetworkManager.Singleton.AddNetworkPrefab(maskPhonePrefab);
            }

            if (clipboardPrefab == null)
            {
                clipboardPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("ClipboardPhoneBook");
                clipboardPrefab.AddComponent<Clipboard>();

                NetworkManager.Singleton.AddNetworkPrefab(clipboardPrefab);

                if (Config.clipboardPurchase.Value)
                {
                    Item clipboardItem = clipboardPrefab.GetComponent<PhysicsProp>().itemProperties;
                    clipboardItem.spawnPrefab = clipboardPrefab;
                    
                    TerminalNode itemInfo = ScriptableObject.CreateInstance<TerminalNode>();
                    itemInfo.name = "ClipboardInfoNode";
                    itemInfo.displayText = "A clipboard with everyone's phone numbers written on it. Will auto update if players join or leave.\n\n";
                    itemInfo.clearPreviousText = true;
                    itemInfo.maxCharactersToType = 25;
                    
                    Items.RegisterShopItem(clipboardItem, null, null, itemInfo, Config.clipboardPrice.Value);
                }
            }

            if (switchboardPrefab == null)
            {
                switchboardPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("SwitchboardContainer");
                switchboardPrefab.AddComponent<SwitchboardPhone>();

                NetworkManager.Singleton.AddNetworkPrefab(switchboardPrefab);

                if (Config.switchboardPurchase.Value)
                {
                    UnlockableItem Switchboard = new UnlockableItem();
                    Switchboard.prefabObject = switchboardPrefab;
                    Switchboard.unlockableName = "Switchboard";
                    Switchboard.IsPlaceable = true;
                    Switchboard.spawnPrefab = true;
                    Switchboard.alwaysInStock = true;
                    Switchboard.canBeStored = true;
                    Switchboard.unlockableType = 1;
                    Switchboard.maxNumber = 1;

                    TerminalNode switchboardInfo = ScriptableObject.CreateInstance<TerminalNode>();
                    switchboardInfo.name = "PhoneSwitchboardInfoNode";
                    switchboardInfo.displayText = "A Switchboard for your ship. Enjoy easy routing and management of your co-workers' Personal Phones from the safety of your ship.\n\n";
                    switchboardInfo.clearPreviousText = true;
                    switchboardInfo.maxCharactersToType = 25;

                    Unlockables.RegisterUnlockable(Switchboard, StoreType.ShipUpgrade, null, null, switchboardInfo, Config.switchboardPrice.Value);
                }
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
