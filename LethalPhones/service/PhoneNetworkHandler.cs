using GameNetcodeStuff;
using Scoops.misc;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.service
{
    public class PhoneNetworkHandler : NetworkBehaviour
    {
        public static PhoneNetworkHandler Instance { get; private set; }

        private Dictionary<string, int> phoneNumberDict;
        public PlayerPhone localPhone;

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                Instance?.gameObject.GetComponent<NetworkObject>().Despawn();
            Instance = this;

            phoneNumberDict = new Dictionary<string, int>();

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
            int phoneNumber = Random.Range(0, 10000); ;
            string phoneString = phoneNumber.ToString("D4");
            while (phoneNumberDict.ContainsKey(phoneNumber.ToString()))
            {
                phoneNumber = Random.Range(0, 10000);
                phoneString = phoneNumber.ToString("D4");
            }

            phoneNumberDict.Add(phoneString, playerId);

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
    }
}