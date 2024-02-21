using GameNetcodeStuff;
using Scoops.service;
using System;
using System.Collections.Generic;
using System.Text;

namespace Scoops.misc
{
    public class PlayerPhone
    {
        public PlayerControllerB player;
        public string phoneNumber;
        public bool toggled = false;

        public Queue<int> dialedNumbers;

        public string incomingCall;
        public string activeCall;
        public string outgoingCall;

        public PlayerPhone(PlayerControllerB player, string phoneNumber)
        {
            this.player = player;
            this.phoneNumber = phoneNumber;

            dialedNumbers = new Queue<int>(4);
        }

        public string GetFullDialNumber()
        {
            return String.Join("", dialedNumbers);
        }

        public void DialNumber(int number)
        {
            dialedNumbers.Enqueue(number);

            if (dialedNumbers.Count > 4)
            {
                dialedNumbers.Dequeue();
            }

            Plugin.Log.LogInfo("Current dialing number: " + GetFullDialNumber());
        }

        public void CallButtonPressed()
        {
            if (activeCall != null)
            {
                // We're on a call, hang up
                Plugin.Log.LogInfo("Hanging Up: " + activeCall);
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(activeCall);
                activeCall = null;
            }
            else if (outgoingCall != null)
            {
                // We're calling, cancel
                Plugin.Log.LogInfo("Canceling: " + outgoingCall);
                PhoneNetworkHandler.Instance.HangUpCallServerRpc(outgoingCall);
                outgoingCall = null;
            } 
            else if (incomingCall != null)
            {
                // We have an incoming call, pick up
                activeCall = incomingCall;
                incomingCall = null;
                PhoneNetworkHandler.Instance.AcceptIncomingCallServerRpc(activeCall);
                Plugin.Log.LogInfo("Picking up: " + activeCall);
            }
            else
            {
                // No calls of any sort are happening, make a new one
                CallDialedNumber();
            }
        }

        public void CallDialedNumber()
        {
            string number = GetFullDialNumber();
            if (dialedNumbers.Count != 4)
            {
                Plugin.Log.LogInfo("Not enough digits: " + number);
                return;
            }
            if (number == phoneNumber)
            {
                Plugin.Log.LogInfo("You cannot call yourself yet. Messages will be here later.");
            }

            PhoneNetworkHandler.Instance.MakeOutgoingCall(number);
            outgoingCall = number;

            dialedNumbers.Clear();
        }

        public void RecieveCall(string number)
        {
            if (incomingCall == null && activeCall == null)
            {
                incomingCall = number;
            }
            else
            {
                // Line is busy
            }
        }

        public void OutgoingCallAccepted(string number)
        {
            if (outgoingCall != number)
            {
                // Whoops, how did we get this call? Send back a no.
                return;
            }

            outgoingCall = null;
            activeCall = number;
        }

        public void HangUpCall(string number)
        {
            if (activeCall != number)
            {
                // No you can't hang up a call you're not on.
                return;
            }

            activeCall = null;
        }
    }
}