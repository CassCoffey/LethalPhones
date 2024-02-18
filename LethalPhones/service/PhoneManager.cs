using GameNetcodeStuff;
using Scoops.misc;
using System.Collections.Generic;
using UnityEngine;

namespace Scoops.service;

public class PhoneManager
{
    private List<PlayerPhone> allPhones;
    private Dictionary<PlayerControllerB, PlayerPhone> playerPhoneDict;
    private Dictionary<string, PlayerPhone> numberPhoneDict;

    public PhoneManager()
    {
        allPhones = new List<PlayerPhone>();
        playerPhoneDict = new Dictionary<PlayerControllerB, PlayerPhone>();
        numberPhoneDict = new Dictionary<string, PlayerPhone>();
    }

    public void AddPhone(PlayerPhone phone)
    {
        allPhones.Add(phone);
        playerPhoneDict.Add(phone.player, phone);
        numberPhoneDict.Add(phone.phoneNumber, phone);
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
