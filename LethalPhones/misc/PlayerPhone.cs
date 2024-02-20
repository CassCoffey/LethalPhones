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

        public void CallDialedNumber()
        {
            if (dialedNumbers.Count != 4)
            {
                Plugin.Log.LogInfo("Not enough numbers: " + GetFullDialNumber());
                return;
            }

            PhoneNetworkHandler.Instance.MakeOutgoingCall(GetFullDialNumber());
        }
    }
}