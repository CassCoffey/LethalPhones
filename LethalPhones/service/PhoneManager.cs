using GameNetcodeStuff;
using Scoops.misc;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.service;

public class PhoneManager : NetworkBehaviour
{
    private Dictionary<string, PlayerControllerB> phoneNumberDict;
    public PlayerPhone localPhone;

    public PhoneManager()
    {
        phoneNumberDict = new Dictionary<string, PlayerControllerB>();
    }

    public void AddPhone(PlayerPhone phone)
    {
        phoneNumberDict.Add(phone.phoneNumber, phone.player);

        localPhone = phone;
    }

    public void CreateNewPhone(PlayerControllerB player)
    {
        PlayerPhone phone = new PlayerPhone(player, CreateNewPhoneNumber());
        AddPhone(phone);

        Plugin.Log.LogInfo("New Phone for " + player.name + "! Your number is: " + phone.phoneNumber);
    }

    public string CreateNewPhoneNumber()
    {
        int phoneNumber = Random.Range(0, 10000);
        string phoneString = phoneNumber.ToString("D4");

        return phoneString;
    }
}
