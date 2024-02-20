using GameNetcodeStuff;
using Scoops.misc;
using System.Collections.Generic;
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
        public PlayerPhone localPhone;

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                Instance?.gameObject.GetComponent<NetworkObject>().Despawn();
            Instance = this;

            phoneNumberDict = new Dictionary<string, ulong>();

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
            int phoneNumber = Random.Range(0, 10000); ;
            string phoneString = phoneNumber.ToString("D4");
            while (phoneNumberDict.ContainsKey(phoneNumber.ToString()))
            {
                phoneNumber = Random.Range(0, 10000);
                phoneString = phoneNumber.ToString("D4");
            }

            phoneNumberDict.Add(phoneString, clientId);

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            ReturnNewPhoneNumberClientRpc(phoneString, clientRpcParams);
        }

        [ClientRpc]
        public void ReturnNewPhoneNumberClientRpc(string number, ClientRpcParams clientRpcParams = default)
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;

            PlayerPhone phone = new PlayerPhone(player, number);
            localPhone = phone;

            Plugin.Log.LogInfo("New Phone for " + player.name + "! Your number is: " + phone.phoneNumber);
        }

        public void MakeOutgoingCall(string number)
        {
            MakeOutgoingCallServerRpc(number);
        }

        [ServerRpc(RequireOwnership = false)]
        public void MakeOutgoingCallServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            ulong senderClientId = serverRpcParams.Receive.SenderClientId;
            int senderPlayerId = StartOfRound.Instance.ClientPlayerList[senderClientId];

            if (phoneNumberDict.ContainsKey(number))
            {
                // Successful call
                ulong recieverClientId = phoneNumberDict[number];

                ClientRpcParams validCallClientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { recieverClientId }
                    }
                };

                RecieveCallClientRpc(senderPlayerId, validCallClientRpcParams);
            }
            else
            {
                // No matching number, failed call
                ClientRpcParams invalidCallClientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { senderClientId }
                    }
                };

                InvalidCallClientRpc(invalidCallClientRpcParams);
            }
        }

        [ClientRpc]
        public void RecieveCallClientRpc(int callerId, ClientRpcParams clientRpcParams = default)
        {
            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[callerId];

            Plugin.Log.LogInfo("You've got a call from " + caller.name);
        }

        [ClientRpc]
        public void InvalidCallClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Plugin.Log.LogInfo("Invalid number.");
        }
    }
}