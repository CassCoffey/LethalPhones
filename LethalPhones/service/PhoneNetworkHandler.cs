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
            int playerId = StartOfRound.Instance.ClientPlayerList[clientId];
            PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[playerId];
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

            playerController.transform.Find("CellPhonePrefab(Clone)").GetComponent<NetworkObject>().ChangeOwnership(clientId);

            ReturnNewPhoneNumberClientRpc(phoneString, clientRpcParams);
        }

        [ClientRpc]
        public void ReturnNewPhoneNumberClientRpc(string number, ClientRpcParams clientRpcParams = default)
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;

            PlayerPhone phone = player.transform.Find("CellPhonePrefab(Clone)").gameObject.AddComponent<PlayerPhone>();
            phone.Init(player, number);
            localPhone = phone;

            GameObject phoneAudioPrefab = (GameObject)Plugin.LethalPhoneAssets.LoadAsset("PhoneAudioInternal");
            GameObject phoneAudio = GameObject.Instantiate(phoneAudioPrefab, player.transform.Find("Audios"));

            localPhone.localPhoneAudio = phoneAudio.GetComponent<AudioSource>();

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
                int recieverPlayerId = StartOfRound.Instance.ClientPlayerList[recieverClientId];

                ClientRpcParams validCallClientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { recieverClientId }
                    }
                };

                RingPhoneClientRpc(recieverPlayerId);
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

            localPhone.InvalidNumber();
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

            StopRingingPhoneClientRpc(accepterPlayerId);
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
            int recieverPlayerId = StartOfRound.Instance.ClientPlayerList[recieverClientId];

            ClientRpcParams hangupCallClientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { recieverClientId }
                }
            };

            StopRingingPhoneClientRpc(recieverPlayerId);
            HangupCallClientRpc(cancellerPlayerId, cancellerPhoneNumber, hangupCallClientRpcParams);
        }

        [ClientRpc]
        public void HangupCallClientRpc(int cancellerId, string cancellerNumber, ClientRpcParams clientRpcParams = default)
        {
            PlayerControllerB canceller = StartOfRound.Instance.allPlayerScripts[cancellerId];

            Plugin.Log.LogInfo("Your call was hung up by " + canceller.name + " with number " + cancellerNumber);

            localPhone.HangUpCall(cancellerNumber);
        }

        [ClientRpc]
        public void RingPhoneClientRpc(int playerId)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];

            RoundManager.Instance.PlayAudibleNoise(player.serverPlayerPosition, 16f, 0.9f, 0, player.isInElevator && StartOfRound.Instance.hangarDoorsClosed, 0);

            AudioSource phoneServerAudio = player.transform.Find("Audios").Find("PhoneAudioExternal(Clone)").GetComponent<AudioSource>();
            phoneServerAudio.Play();
        }

        [ClientRpc]
        public void StopRingingPhoneClientRpc(int playerId)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];

            AudioSource phoneServerAudio = player.transform.Find("Audios").Find("PhoneAudioExternal(Clone)").GetComponent<AudioSource>();
            phoneServerAudio.Stop();
        }
    }
}