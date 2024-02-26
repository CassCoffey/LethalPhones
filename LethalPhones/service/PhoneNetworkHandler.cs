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
        private Dictionary<int, AudioSource> playerPhoneAudioSources;

        public PlayerPhone localPhone;

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                Instance?.gameObject.GetComponent<NetworkObject>().Despawn();
            Instance = this;

            phoneNumberDict = new Dictionary<string, ulong>();
            playerPhoneAudioSources = new Dictionary<int, AudioSource>();

            base.OnNetworkSpawn();
        }

        public void CreateNewPhone()
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            int playerId = StartOfRound.Instance.ClientPlayerList[player.actualClientId];

            AudioSource localPhoneAudio = player.itemAudio.gameObject.AddComponent<AudioSource>();
            localPhone.localPhoneAudio = localPhoneAudio;
            CreateNewPhoneNumberServerRpc();
            SetupPhoneAudioSourceClientRpc(playerId);
        }

        [ClientRpc]
        public void SetupPhoneAudioSourceClientRpc(int playerId)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];
            AudioSource serverPhoneAudio = player.itemAudio.gameObject.AddComponent<AudioSource>();
            
            playerPhoneAudioSources.Add(playerId, serverPhoneAudio);
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
            string senderPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == senderClientId).Key;

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

                RecieveCallClientRpc(senderPlayerId, senderPhoneNumber, validCallClientRpcParams);
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
        public void RecieveCallClientRpc(int callerId, string callerNumber, ClientRpcParams clientRpcParams = default)
        {
            PlayerControllerB caller = StartOfRound.Instance.allPlayerScripts[callerId];

            Plugin.Log.LogInfo("You've got a call from " + caller.name + " with number " + callerNumber);

            localPhone.RecieveCall(callerNumber);
        }

        [ClientRpc]
        public void InvalidCallClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Plugin.Log.LogInfo("Invalid number.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void AcceptIncomingCallServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            ulong accepterClientId = serverRpcParams.Receive.SenderClientId;
            int accepterPlayerId = StartOfRound.Instance.ClientPlayerList[accepterClientId];
            string accepterPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == accepterClientId).Key;

            ulong recieverClientId = phoneNumberDict[number];

            ClientRpcParams acceptCallClientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { recieverClientId }
                }
            };

            CallAcceptedClientRpc(accepterPlayerId, accepterPhoneNumber, acceptCallClientRpcParams);
        }

        [ClientRpc]
        public void CallAcceptedClientRpc(int accepterId, string accepterNumber, ClientRpcParams clientRpcParams = default)
        {
            PlayerControllerB accepter = StartOfRound.Instance.allPlayerScripts[accepterId];

            Plugin.Log.LogInfo("Your call was accepted by " + accepter.name + " with number " + accepterNumber);

            localPhone.OutgoingCallAccepted(accepterNumber);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HangUpCallServerRpc(string number, ServerRpcParams serverRpcParams = default)
        {
            ulong cancellerClientId = serverRpcParams.Receive.SenderClientId;
            int cancellerPlayerId = StartOfRound.Instance.ClientPlayerList[cancellerClientId];
            string cancellerPhoneNumber = phoneNumberDict.FirstOrDefault(x => x.Value == cancellerClientId).Key;

            ulong recieverClientId = phoneNumberDict[number];

            ClientRpcParams hangupCallClientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { recieverClientId }
                }
            };

            HangupCallClientRpc(cancellerPlayerId, cancellerPhoneNumber, hangupCallClientRpcParams);
        }

        [ClientRpc]
        public void HangupCallClientRpc(int cancellerId, string cancellerNumber, ClientRpcParams clientRpcParams = default)
        {
            PlayerControllerB caneller = StartOfRound.Instance.allPlayerScripts[cancellerId];

            Plugin.Log.LogInfo("Your call was hung up by " + caneller.name + " with number " + cancellerNumber);

            localPhone.HangUpCall(cancellerNumber);
        }
    }
}