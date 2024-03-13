using Dissonance;
using GameNetcodeStuff;
using Scoops.misc;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.service
{
    public class PhoneNetworkHandler : NetworkBehaviour
    {
        public static PhoneNetworkHandler Instance { get; private set; }

        private Dictionary<string, ushort> phoneNumberDict;
        private Dictionary<string, PhoneBehavior> phoneObjectDict;

        public PlayerPhone localPhone;

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

            phoneNumberDict = new Dictionary<string, ushort>();
            phoneObjectDict = new Dictionary<string, PhoneBehavior>();

            base.OnNetworkSpawn();
        }

        public void CreateNewPlayerPhone()
        {
            CreateNewPhoneNumberServerRpc(true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CreateNewPhoneNumberServerRpc(bool player, ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;
            int phoneNumber = Random.Range(0, 10000); ;
            string phoneString = phoneNumber.ToString("D4");
            while (phoneNumberDict.ContainsKey(phoneNumber.ToString()))
            {
                phoneNumber = Random.Range(0, 10000);
                phoneString = phoneNumber.ToString("D4");
            }

            PhoneBehavior phone = null;
            if (player)
            {
                int playerId = StartOfRound.Instance.ClientPlayerList[clientId];
                Plugin.Log.LogInfo($"New phone for player: " + playerId);
                PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
                phone = playerController.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
            }

            phoneNumberDict.Add(phoneString, phone.NetworkBehaviourId);

            phone.GetComponent<NetworkObject>().ChangeOwnership(clientId);

            phoneObjectDict.Add(phoneString, phone);

            phone.SetNewPhoneNumberClientRpc(phoneString);
        }

        public void DeletePlayerPhone(int playerId, ulong clientId)
        {
            PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
            Plugin.Log.LogInfo("Deleting phone for player: " + playerController.name);
            PlayerPhone phone = playerController.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
            phone.GetComponent<NetworkObject>().RemoveOwnership();
            string number = phoneNumberDict.FirstOrDefault(x => x.Value == clientId).Key;
            if (number != null)
            {
                Plugin.Log.LogInfo("Removing number: " + number);

                phoneObjectDict.Remove(number);
                phoneNumberDict.Remove(number);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void MakeOutgoingCallServerRpc(string number, ushort senderId, ServerRpcParams serverRpcParams = default)
        {
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
        public void AcceptIncomingCallServerRpc(string number, ushort accepterId, ServerRpcParams serverRpcParams = default)
        {
            string accepterPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == accepterId).Key;

            phoneObjectDict[number].CallAcceptedClientRpc(accepterId, accepterPhoneNumber);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HangUpCallServerRpc(string number, ushort cancellerId, ServerRpcParams serverRpcParams = default)
        {
            if (phoneNumberDict.ContainsKey(number))
            {
                string cancellerPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == cancellerId).Key;

                phoneObjectDict[number].HangupCallClientRpc(cancellerId, cancellerPhoneNumber);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void LineBusyServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            phoneObjectDict[number].InvalidCallClientRpc("Line Busy");
        }
    }
}