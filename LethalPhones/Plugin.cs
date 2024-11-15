﻿using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalCompanyInputUtils.Api;
using Scoops.patch;
using Scoops.service;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scoops;

public class LethalPhonesInputClass : LcInputActions
{
    [InputAction("<Keyboard>/backquote", Name = "Toggle Phone", GamepadPath = "<Gamepad>/leftStickPress")]
    public InputAction TogglePhoneKey { get; set; }

    [InputAction("<Keyboard>/q", Name = "Hangup Phone", GamepadPath = "<Gamepad>/dpad/down")]
    public InputAction HangupPhoneKey { get; set; }

    [InputAction("<Mouse>/leftButton", Name = "Pickup Phone", GamepadPath = "<Gamepad>/rightTrigger")]
    public InputAction PickupPhoneKey { get; set; }

    [InputAction("<Keyboard>/z", Name = "Dial Phone", GamepadPath = "<Gamepad>/leftShoulder")]
    public InputAction DialPhoneKey { get; set; }

    [InputAction("<Keyboard>/g", Name = "Toggle Phone Volume", GamepadPath = "<Gamepad>/buttonEast")]
    public InputAction VolumePhoneKey { get; set; }
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, "1.0.11")]
[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance { get; set; }

    public static Config PhoneConfig { get; internal set; }

    public static ManualLogSource Log => Instance.Logger;
    public static AssetBundle LethalPhoneAssets;

    private readonly Harmony _harmony = new(PluginInfo.PLUGIN_GUID);

    internal static LethalPhonesInputClass InputActionInstance = new LethalPhonesInputClass();

    public Plugin()
    {
        Instance = this;
    }

    private void Awake()
    {
        NetcodePatcher(); // ONLY RUN ONCE

        var dllFolderPath = System.IO.Path.GetDirectoryName(Info.Location);
        var assetBundleFilePath = System.IO.Path.Combine(dllFolderPath, "lethalphonesassets");
        LethalPhoneAssets = AssetBundle.LoadFromFile(assetBundleFilePath);

        PhoneAssetManager.Init();

        PhoneConfig = new(base.Config);

        Log.LogInfo($"Applying patches...");
        ApplyPluginPatch();
        Log.LogInfo($"Patches applied");
    }

    /// <summary>
    /// Applies the patch to the game.
    /// </summary>
    private void ApplyPluginPatch()
    {
        _harmony.PatchAll(typeof(PlayerPhonePatch));
        _harmony.PatchAll(typeof(PlayerControllerB_SetPlayerSanityLevel_Patch));
        _harmony.PatchAll(typeof(HoardingBugPhonePatch));
        _harmony.PatchAll(typeof(StartOfRoundPhonePatch));
        _harmony.PatchAll(typeof(NetworkObjectManager));
        _harmony.PatchAll(typeof(ShipTeleporterPhonePatch));
    }

    private static void NetcodePatcher()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }
}
