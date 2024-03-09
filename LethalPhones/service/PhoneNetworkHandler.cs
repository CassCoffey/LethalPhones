using Dissonance;
using GameNetcodeStuff;
using Scoops.misc;
using System.Collections.Generic;
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

        private Dictionary<string, ulong> phoneNumberDict;
        private Dictionary<string, PlayerPhone> phoneObjectDict;

        private List<PlayerPhone> phoneList;

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

            phoneNumberDict = new Dictionary<string, ulong>();
            phoneObjectDict = new Dictionary<string, PlayerPhone>();
            phoneList = new List<PlayerPhone>();

            base.OnNetworkSpawn();
        }

        public void CreateNewPhone()
        {
            CreateNewPhoneNumberServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void CreateNewPhoneNumberServerRpc(ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;
            int playerId = StartOfRound.Instance.ClientPlayerList[clientId];
            Plugin.Log.LogInfo($"New phone for player: " + playerId);
            PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
            int phoneNumber = Random.Range(0, 10000); ;
            string phoneString = phoneNumber.ToString("D4");
            while (phoneNumberDict.ContainsKey(phoneNumber.ToString()))
            {
                phoneNumber = Random.Range(0, 10000);
                phoneString = phoneNumber.ToString("D4");
            }

            phoneNumberDict.Add(phoneString, clientId);

            PlayerPhone phone = playerController.transform.Find("PhonePrefab(Clone)").GetComponent<PlayerPhone>();
            phone.GetComponent<NetworkObject>().ChangeOwnership(clientId);

            phoneObjectDict.Add(phoneString, phone);

            phone.SetNewPhoneNumberClientRpc(phoneString);
        }

        [ServerRpc(RequireOwnership = false)]
        public void MakeOutgoingCallServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            ulong senderClientId = serverRpcParams.Receive.SenderClientId;
            int senderPlayerId = StartOfRound.Instance.ClientPlayerList[senderClientId];
            string senderPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == senderClientId).Key;

            if (phoneNumberDict.ContainsKey(number))
            {
                // Successful call
                phoneObjectDict[number].RecieveCallClientRpc(senderPlayerId, senderPhoneNumber);
            }
            else
            {
                // No matching number, failed call
                phoneObjectDict[senderPhoneNumber].InvalidCallClientRpc("Invalid #");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AcceptIncomingCallServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            ulong accepterClientId = serverRpcParams.Receive.SenderClientId;
            int accepterPlayerId = StartOfRound.Instance.ClientPlayerList[accepterClientId];
            string accepterPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == accepterClientId).Key;

            phoneObjectDict[number].CallAcceptedClientRpc(accepterPlayerId, accepterPhoneNumber);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HangUpCallServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            ulong cancellerClientId = serverRpcParams.Receive.SenderClientId;
            int cancellerPlayerId = StartOfRound.Instance.ClientPlayerList[cancellerClientId];
            string cancellerPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == cancellerClientId).Key;

            phoneObjectDict[number].HangupCallClientRpc(cancellerPlayerId, cancellerPhoneNumber);
        }

        [ServerRpc(RequireOwnership = false)]
        public void LineBusyServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            phoneObjectDict[number].InvalidCallClientRpc("Line Busy");
        }
    }
}