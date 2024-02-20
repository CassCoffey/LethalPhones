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

        private Dictionary<string, PlayerControllerB> phoneNumberDict;
        public PlayerPhone localPhone;

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                Instance?.gameObject.GetComponent<NetworkObject>().Despawn();
            Instance = this;

            phoneNumberDict = new Dictionary<string, PlayerControllerB>();

            base.OnNetworkSpawn();
        }

        public void CreateNewPhone(PlayerControllerB player)
        {
            CreateNewPhoneNumberServerRpc(player);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CreateNewPhoneNumberServerRpc(PlayerControllerB player, ServerRpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            int phoneNumber = 0;
            string phoneString = phoneNumber.ToString("D4");
            while (!phoneNumberDict.ContainsKey(phoneNumber.ToString()))
            {
                phoneNumber = Random.Range(0, 10000);
                phoneString = phoneNumber.ToString("D4");
            }

            phoneNumberDict.Add(phoneString, player);

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            ReturnNewPhoneNumberClientRpc(player, phoneString, clientRpcParams);
        }

        [ClientRpc]
        public void ReturnNewPhoneNumberClientRpc(PlayerControllerB player, string number, ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner) return;

            PlayerPhone phone = new PlayerPhone(player, number);
            localPhone = phone;

            Plugin.Log.LogInfo("New Phone for " + player.name + "! Your number is: " + phone.phoneNumber);
        }
    }
}