using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;

namespace Scoops.misc
{
    public class PlayerPhone
    {
        public PlayerControllerB player;
        public string phoneNumber;

        public PlayerPhone(PlayerControllerB player, string phoneNumber)
        {
            this.player = player;
            this.phoneNumber = phoneNumber;
        }
    }
}