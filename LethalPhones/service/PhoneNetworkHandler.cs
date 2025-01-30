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

namespace Scoops.service
{
    public class PhoneNetworkHandler : NetworkBehaviour
    {
        public static PhoneNetworkHandler Instance { get; private set; }

        public static Clipboard PhonebookClipboard;

        public static NetworkVariable<bool> Locked = new NetworkVariable<bool>();

        private Dictionary<string, ulong> phoneNumberDict;
        private Dictionary<string, PhoneBehavior> phoneObjectDict;

        public PlayerPhone localPhone;
        public SwitchboardPhone switchboard;

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

            phoneNumberDict = new Dictionary<string, ulong>();
            phoneObjectDict = new Dictionary<string, PhoneBehavior>();

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
        }

        public void RequestSwitchboardUpdates()
        {
            UpdateAllClientsServerRpc();
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
                UpdateClipboardText();
            }
        }

        public void UpdateClipboardText()
        {
            List<ulong> phones = new List<ulong>();

            foreach (PhoneBehavior phone in phoneObjectDict.Values)
            {
                if (phone is PlayerPhone)
                {
                    PlayerPhone playerPhone = (PlayerPhone)phone;
                    phones.Add(playerPhone.NetworkObjectId);
                }
            }

            foreach (Clipboard clipboard in FindObjectsOfType<Clipboard>())
            {
                clipboard.UpdateTextClientRpc(phones.ToArray());
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSwitchboardUpdatesServerRpc()
        {
            UpdateSwitchboardPhones();
        }

        public void UpdateSwitchboardPhones()
        {
            List<ulong> phones = new List<ulong>();
            List<PhoneBehavior> phoneBehaviors = phoneObjectDict.Values.ToList<PhoneBehavior>();

            if (phoneBehaviors.Count > 1)
            {
                phoneBehaviors.Sort((e,f) => { return e.phoneNumber.CompareTo(f.phoneNumber); });
            }

            foreach (PhoneBehavior phone in phoneBehaviors)
            {
                if (!(phone is MaskedPhone || phone is SwitchboardPhone)) {
                    phones.Add(phone.NetworkObjectId);
                }
            }

            switchboard.UpdatePhoneListClientRpc(phones.ToArray());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RegisterSwitchboardServerRpc(ulong SwitchboardId, ServerRpcParams serverRpcParams = default)
        {
            if (switchboard != null)
            {
                Plugin.Log.LogInfo($"Tried to register more than one switchboard.");
                return;
            }

            string number = Config.switchboardNumber.Value;

            switchboard = GetNetworkObject(SwitchboardId).GetComponent<SwitchboardPhone>();
            Plugin.Log.LogInfo($"New switchboard for object: " + SwitchboardId);

            switchboard.GetComponent<NetworkObject>().ChangeOwnership(NetworkManager.ServerClientId);
            phoneNumberDict.Add(number, switchboard.NetworkObjectId);
            phoneObjectDict.Add(number, switchboard);

            switchboard.SetNewPhoneNumberClientRpc(number);

            RequestClientUpdates();

            UpdateSwitchboardPhones();
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

            int phoneNumber = Random.Range(0, maxNumber);
            string phoneString = phoneNumber.ToString("D4");
            while (phoneNumberDict.ContainsKey(phoneNumber.ToString()) || phoneNumber.ToString() == Config.switchboardNumber.Value)
            {
                phoneNumber = Random.Range(0, maxNumber);
                phoneString = phoneNumber.ToString("D4");
            }

            PhoneBehavior phone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
            Plugin.Log.LogInfo($"New phone for object: " + phoneId);

            phone.GetComponent<NetworkObject>().ChangeOwnership(clientId);
            phoneNumberDict.Add(phoneString, phone.NetworkObjectId);
            phoneObjectDict.Add(phoneString, phone);

            phone.phoneSkinId = skinId;
            phone.phoneCharmId = charmId;
            phone.phoneRingtoneId = ringtoneId;

            phone.SetNewPhoneNumberClientRpc(phoneString);

            RequestClientUpdates();

            if (switchboard != null)
            {
                UpdateSwitchboardPhones();
            }
        }

        public void DeletePlayerPhone(int playerId)
        {
            PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
            Plugin.Log.LogInfo("Deleting phone for player: " + playerController.name);
            PlayerPhone phone = playerController.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
            string number = phoneNumberDict.FirstOrDefault(x => x.Value == phone.NetworkObjectId).Key;
            if (!NetworkManager.Singleton.ShutdownInProgress)
            {
                phone.GetComponent<NetworkObject>().RemoveOwnership();
            }
            RemoveNumber(number);

            if (switchboard != null)
            {
                UpdateSwitchboardPhones();
            }
        }

        public void DeletePhone(ulong phoneId)
        {
            Plugin.Log.LogInfo("Deleting phone with ID: " + phoneId);
            PhoneBehavior phone = GetNetworkObject(phoneId).GetComponent<PhoneBehavior>();
            string number = phoneNumberDict.FirstOrDefault(x => x.Value == phone.NetworkObjectId).Key;
            if (!NetworkManager.Singleton.ShutdownInProgress)
            {
                phone.GetComponent<NetworkObject>().RemoveOwnership();
            }
            RemoveNumber(number);

            if (switchboard != null)
            {
                UpdateSwitchboardPhones();
            }
        }

        public void DeleteSwitchboard()
        {
            Plugin.Log.LogInfo("Deleting switchboard");
            string number = switchboard.phoneNumber;

            RemoveNumber(number);
            switchboard = null;
        }

        public void RemoveNumber(string number)
        {
            if (number != null)
            {
                Plugin.Log.LogInfo("Removing number: " + number);

                phoneObjectDict.Remove(number);
                phoneNumberDict.Remove(number);

                if (switchboard != null)
                {
                    UpdateSwitchboardPhones();
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void MakeOutgoingCallServerRpc(string number, ulong senderId, ServerRpcParams serverRpcParams = default)
        {
            // No calling until phones are unlocked
            if (Locked.Value)
            {
                return;
            }

            string senderPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == senderId).Key;

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
        public void AcceptIncomingCallServerRpc(string number, ulong accepterId, ServerRpcParams serverRpcParams = default)
        {
            string accepterPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == accepterId).Key;

            phoneObjectDict[number].CallAcceptedClientRpc(accepterId, accepterPhoneNumber);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HangUpCallServerRpc(string number, ulong cancellerId, ServerRpcParams serverRpcParams = default)
        {
            if (phoneNumberDict.ContainsKey(number))
            {
                string cancellerPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == cancellerId).Key;

                phoneObjectDict[number].HangupCallClientRpc(cancellerId, cancellerPhoneNumber);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferCallServerRpc(string number, string transferNumber, ulong transferrerId, ServerRpcParams serverRpcParams = default)
        {
            if (phoneNumberDict.ContainsKey(number) && phoneNumberDict.ContainsKey(transferNumber))
            {
                string transferrerPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == transferrerId).Key;

                phoneObjectDict[number].TransferCallClientRpc(transferrerId, transferrerPhoneNumber, transferNumber);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void LineBusyServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            phoneObjectDict[number].InvalidCallClientRpc("Line Busy");
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdateAllClientsServerRpc()
        {
            foreach (PhoneBehavior phoneObj in phoneObjectDict.Values)
            {
                phoneObj.PropogateInformation();
            }

            UpdateClipboardText();

            if (switchboard != null)
            {
                UpdateSwitchboardPhones();
            }
        }
    }
}