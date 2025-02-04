using Dissonance;
using GameNetcodeStuff;
using LethalLib.Modules;
using Scoops.gameobjects;
using Scoops.misc;
using Scoops.patch;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Scoops.service
{
    public class PhoneNetworkHandler : NetworkBehaviour
    {
        public static PhoneNetworkHandler Instance { get; private set; }

        public static Clipboard PhonebookClipboard;

        public static NetworkVariable<bool> Locked = new NetworkVariable<bool>();

        public static List<PhoneBehavior> allPhoneBehaviors = new List<PhoneBehavior>();

        private Dictionary<short, ulong> phoneNumberDict;
        private Dictionary<short, PhoneBehavior> phoneObjectDict;

        public PlayerPhone localPhone;
        public SwitchboardPhone switchboard;

        public UnityEvent phoneListUpdateEvent;

        public void Start()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                if (Config.enableStartClipboard.Value)
                {
                    // when loading a save, don't spawn a clipboard if there already is one
                    Clipboard savedClipboard = FindAnyObjectByType<Clipboard>();
                    if (!savedClipboard && StartOfRound.Instance.gameStats.daysSpent == 0)
                    {
                        SpawnClipboard();
                    }
                    else if (savedClipboard)
                    {
                        PhonebookClipboard = savedClipboard;
                    }
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                if (Instance != null)
                {
                    Instance.gameObject.GetComponent<NetworkObject>().Despawn();
                }
            }

            Instance = this;

            phoneNumberDict = new Dictionary<short, ulong>();
            phoneObjectDict = new Dictionary<short, PhoneBehavior>();

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                CheckPhoneUnlock();
            }

            base.OnNetworkSpawn();
        }

        public static void CheckPhoneUnlock()
        {
            if (!Config.phonePurchase.Value)
            {
                Locked.Value = false;
                return;
            }

            bool locked = true;
            foreach (UnlockableItem unlockable in StartOfRound.Instance.unlockablesList.unlockables)
            {
                if (unlockable.unlockableName == PhoneAssetManager.PHONE_UNLOCK_NAME)
                {
                    locked = !unlockable.hasBeenUnlockedByPlayer;
                }
            }
            Locked.Value = locked;
        }

        public void RegisterSwitchboard(ulong switchboardId)
        {
            RegisterSwitchboardServerRpc(switchboardId);
        }

        public void CreateNewPhone(ulong phoneId, string skinId, string charmId, string ringtoneId)
        {
            CreateNewPhoneNumberServerRpc(phoneId, skinId, charmId, ringtoneId);
        }

        public void RequestClientUpdates()
        {
            UpdateAllClientsServerRpc();
            UpdateAllPhonesListServerRpc();
        }

        public void RequestPhoneListUpdates()
        {
            UpdateAllPhonesListServerRpc();
        }

        public void SpawnClipboard()
        {
            var phonebookClipboard = Object.Instantiate(NetworkObjectManager.clipboardPrefab, StartOfRound.Instance.elevatorTransform);
            phonebookClipboard.GetComponent<NetworkObject>().Spawn();
            PhonebookClipboard = phonebookClipboard.GetComponent<Clipboard>();
        }

        public void CheckClipboardRespawn()
        {
            if (Config.enableStartClipboard.Value && PhonebookClipboard == null)
            {
                SpawnClipboard();
                UpdateAllPhonesList();
            }
        }

        public void UpdateAllPhonesList()
        {
            List<ulong> phones = new List<ulong>();

            foreach (PhoneBehavior phone in phoneObjectDict.Values)
            {
                phones.Add(phone.NetworkObjectId);
            }

            UpdateAllPhonesListClientRpc(phones.ToArray());
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdateAllPhonesListServerRpc()
        {
            UpdateAllPhonesList();
        }

        [ClientRpc]
        public void UpdateAllPhonesListClientRpc(ulong[] phoneIds)
        {
            if (allPhoneBehaviors == null)
            {
                allPhoneBehaviors = new List<PhoneBehavior>();
            }

            allPhoneBehaviors.Clear();

            foreach (ulong phoneId in phoneIds)
            {
                PhoneBehavior phone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
                allPhoneBehaviors.Add(phone);
            }

            phoneListUpdateEvent.Invoke();
        }


        [ServerRpc(RequireOwnership = false)]
        public void UpdateAllClientsServerRpc()
        {
            foreach (PhoneBehavior phoneObj in phoneObjectDict.Values)
            {
                phoneObj.PropogateInformation();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RegisterSwitchboardServerRpc(ulong SwitchboardId, ServerRpcParams serverRpcParams = default)
        {
            if (switchboard != null)
            {
                Plugin.Log.LogInfo($"Tried to register more than one switchboard.");
                return;
            }

            short number = short.Parse(Config.switchboardNumber.Value);

            switchboard = GetNetworkObject(SwitchboardId).GetComponent<SwitchboardPhone>();
            Plugin.Log.LogInfo($"New switchboard for object: " + SwitchboardId);

            switchboard.GetComponent<NetworkObject>().ChangeOwnership(NetworkManager.ServerClientId);
            phoneNumberDict.Add(number, switchboard.NetworkObjectId);
            phoneObjectDict.Add(number, switchboard);

            switchboard.SetNewPhoneNumberClientRpc(number);

            RequestClientUpdates();
        }

        [ServerRpc(RequireOwnership = false)]
        public void CreateNewPhoneNumberServerRpc(ulong phoneId, string skinId, string charmId, string ringtoneId, ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;

            int maxNumber = Config.maxPhoneNumber.Value;
            if (phoneNumberDict.Count >= maxNumber)
            {
                Plugin.Log.LogError("Cannot create a new unique phone number. Not enough numbers remaining. Please increase the maxPhoneNumber config.");
                Plugin.Log.LogError("maxPhoneNumber = " + maxNumber + ", current Phone Numbers = " + phoneNumberDict.Count);
                return;
            }

            short phoneNumber = (short)Random.Range(0, maxNumber);
            string phoneString = phoneNumber.ToString("D4");
            while (phoneNumberDict.ContainsKey(phoneNumber) || phoneString == Config.switchboardNumber.Value)
            {
                phoneNumber = (short)Random.Range(0, maxNumber);
                phoneString = phoneNumber.ToString("D4");
            }

            PhoneBehavior phone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
            Plugin.Log.LogInfo($"New phone for object: " + phoneId);

            phone.GetComponent<NetworkObject>().ChangeOwnership(clientId);
            phoneNumberDict.Add(phoneNumber, phone.NetworkObjectId);
            phoneObjectDict.Add(phoneNumber, phone);

            phone.phoneSkinId = skinId;
            phone.phoneCharmId = charmId;
            phone.phoneRingtoneId = ringtoneId;

            phone.SetNewPhoneNumberClientRpc(phoneNumber);

            RequestClientUpdates();
        }

        public void DeletePlayerPhone(int playerId)
        {
            PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
            Plugin.Log.LogInfo("Deleting phone for player: " + playerController.name);
            PlayerPhone phone = playerController.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
            short number = phoneNumberDict.FirstOrDefault(x => x.Value == phone.NetworkObjectId).Key;
            if (!NetworkManager.Singleton.ShutdownInProgress)
            {
                phone.GetComponent<NetworkObject>().RemoveOwnership();
            }
            RemoveNumber(number);
        }

        public void DeletePhone(ulong phoneId)
        {
            Plugin.Log.LogInfo("Deleting phone with ID: " + phoneId);
            PhoneBehavior phone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
            short number = phoneNumberDict.FirstOrDefault(x => x.Value == phone.NetworkObjectId).Key;
            if (!NetworkManager.Singleton.ShutdownInProgress)
            {
                phone.GetComponent<NetworkObject>().RemoveOwnership();
            }
            RemoveNumber(number);
        }

        public void DeleteSwitchboard()
        {
            Plugin.Log.LogInfo("Deleting switchboard");
            short number = switchboard.phoneNumber;

            RemoveNumber(number);
            switchboard = null;
        }

        public void RemoveNumber(short number)
        {
            if (number != -1)
            {
                Plugin.Log.LogInfo("Removing number: " + number);

                phoneObjectDict.Remove(number);
                phoneNumberDict.Remove(number);

                UpdateAllPhonesList();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void MakeOutgoingCallServerRpc(short number, ulong senderId, ServerRpcParams serverRpcParams = default)
        {
            // No calling until phones are unlocked
            if (Locked.Value)
            {
                return;
            }

            short senderPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == senderId).Key;

            if (phoneNumberDict.ContainsKey(number))
            {
                // Successful call
                phoneObjectDict[number].RecieveCallClientRpc(senderId, senderPhoneNumber);
            }
            else
            {
                // No matching number, failed call
                phoneObjectDict[senderPhoneNumber].InvalidCallClientRpc("Invalid #");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AcceptIncomingCallServerRpc(short number, ulong accepterId, ServerRpcParams serverRpcParams = default)
        {
            short accepterPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == accepterId).Key;

            phoneObjectDict[number].CallAcceptedClientRpc(accepterId, accepterPhoneNumber);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HangUpCallServerRpc(short number, ulong cancellerId, ServerRpcParams serverRpcParams = default)
        {
            if (phoneNumberDict.ContainsKey(number))
            {
                short cancellerPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == cancellerId).Key;

                phoneObjectDict[number].HangupCallClientRpc(cancellerId, cancellerPhoneNumber);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferCallServerRpc(short number, short transferNumber, ulong transferrerId, ServerRpcParams serverRpcParams = default)
        {
            if (phoneNumberDict.ContainsKey(number) && phoneNumberDict.ContainsKey(transferNumber))
            {
                short transferrerPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == transferrerId).Key;

                phoneObjectDict[number].TransferCallClientRpc(transferrerId, transferrerPhoneNumber, transferNumber);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void LineBusyServerRpc(short number, ServerRpcParams serverRpcParams = default)
        {
            phoneObjectDict[number].InvalidCallClientRpc("Line Busy");
        }
    }
}