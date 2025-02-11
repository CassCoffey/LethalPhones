using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalCompanyInputUtils.Api;
using Scoops.compatability;
using Scoops.customization;
using Scoops.patch;
using Scoops.service;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scoops;

public class LethalPhonesInputClass : LcInputActions
{
    [InputAction("<Keyboard>/y", Name = "Toggle Phone", GamepadPath = "<Gamepad>/leftStickPress")]
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

public static class PluginInformation
{
    public const string PLUGIN_GUID = "LethalPhones";
    public const string PLUGIN_NAME = "LethalPhones";
    public const string PLUGIN_VERSION = "1.3.2";
}

[BepInPlugin(PluginInformation.PLUGIN_GUID, PluginInformation.PLUGIN_NAME, PluginInformation.PLUGIN_VERSION)]
[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("evaisa.lethallib", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("mrov.WeatherRegistry", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("WeatherTweaks", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("qwbarch.Mirage", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("OpJosMod.ReviveCompany", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance { get; set; }

    public static Config PhoneConfig { get; internal set; }

    public static ManualLogSource Log => Instance.Logger;
    public static AssetBundle LethalPhoneAssets;
    public static AssetBundle LethalPhoneCustomization;

    public static string customizationSavePath;

    private readonly Harmony _harmony = new(PluginInformation.PLUGIN_GUID);

    internal static LethalPhonesInputClass InputActionInstance;

    public Plugin()
    {
        Instance = this;
    }

    private void Awake()
    {
        InputActionInstance = new LethalPhonesInputClass();

        Log.LogInfo("Loading LethalPhones Version " + PluginInformation.PLUGIN_VERSION);

        NetcodePatcher(); // ONLY RUN ONCE

        var dllFolderPath = System.IO.Path.GetDirectoryName(Info.Location);
        var assetBundleFilePath = System.IO.Path.Combine(dllFolderPath, "lethalphonesassets");
        LethalPhoneAssets = AssetBundle.LoadFromFile(assetBundleFilePath);

        var customizationBundleFilePath = System.IO.Path.Combine(dllFolderPath, "lethalphonecustomizations");
        LethalPhoneCustomization = AssetBundle.LoadFromFile(customizationBundleFilePath);

        customizationSavePath = $"{Application.persistentDataPath}/lethalphonescustomization.txt";

        PhoneConfig = new(base.Config);

        PhoneAssetManager.Init();

        CustomizationManager.customizationBlacklist.AddRange(Scoops.Config.customizationBlacklist.Value.Replace(" ", string.Empty).Split(','));

        Log.LogInfo("Loading default phone customization...");
        CustomizationManager.LoadSkinCustomizations(LethalPhoneCustomization, "lethalphones.customizations");
        CustomizationManager.LoadCharmCustomizations(LethalPhoneCustomization, "lethalphones.customizations");
        CustomizationManager.LoadRingtoneCustomizations(LethalPhoneCustomization, "lethalphones.customizations");

        Log.LogInfo("Loading user phone customization...");
        CustomizationManager.RecursiveCustomizationLoad(Paths.PluginPath);

        ReadCustomizationFromFile();

        Log.LogInfo($"Applying patches...");
        ApplyPluginPatch();
        Log.LogInfo($"Patches applied");

        if (WeatherRegistryCompat.Enabled)
        {
            Log.LogInfo("Loaded Weather Registry Compatability");
        }

        if (WeatherTweaksCompat.Enabled)
        {
            Log.LogInfo("Loaded Weather Tweaks Compatability");
        }

        if (MirageCompat.Enabled)
        {
            Log.LogInfo("Loaded Mirage Compatability");
        }

        if (ReviveCompanyCompat.Enabled)
        {
            Log.LogInfo("Loaded ReviveCompany Compatability");
        }
    }

    /// <summary>
    /// Applies the patch to the game.
    /// </summary>
    private void ApplyPluginPatch()
    {
        _harmony.PatchAll(typeof(MainMenuPatch));
        _harmony.PatchAll(typeof(PlayerPhonePatch));
        _harmony.PatchAll(typeof(PlayerControllerB_SetPlayerSanityLevel_Patch));
        _harmony.PatchAll(typeof(HoardingBugPhonePatch));
        _harmony.PatchAll(typeof(MaskedPhonePatch));
        _harmony.PatchAll(typeof(StartOfRoundPhonePatch));
        _harmony.PatchAll(typeof(NetworkObjectManager));
        _harmony.PatchAll(typeof(ShipTeleporterPhonePatch));
        _harmony.PatchAll(typeof(AudioSourceManager));
        _harmony.PatchAll(typeof(ConnectionQualityManager));
        if (ReviveCompanyCompat.Enabled)
        {
            _harmony.PatchAll(typeof(ReviveCompanyCompat));
        }
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

    private void ReadCustomizationFromFile()
    {
        if (System.IO.File.Exists(customizationSavePath))
        {
            string[] lines = System.IO.File.ReadAllLines(customizationSavePath);

            if (lines.Length != 3) return;

            CustomizationManager.SelectedSkin = lines[0];
            CustomizationManager.SelectedCharm = lines[1];
            CustomizationManager.SelectedRingtone = lines[2];

            if (!CustomizationManager.skinIds.Contains(CustomizationManager.SelectedSkin))
            {
                CustomizationManager.SelectedSkin = CustomizationManager.DEFAULT_SKIN;
            }
            if (!CustomizationManager.charmIds.Contains(CustomizationManager.SelectedCharm))
            {
                CustomizationManager.SelectedCharm = CustomizationManager.DEFAULT_CHARM;
            }
            if (!CustomizationManager.ringtoneIds.Contains(CustomizationManager.SelectedRingtone))
            {
                CustomizationManager.SelectedRingtone = CustomizationManager.DEFAULT_RINGTONE;
            }
        }
    }

    public static void WriteCustomizationToFile()
    {
        string built = "";
        built += CustomizationManager.SelectedSkin + "\n";
        built += CustomizationManager.SelectedCharm + "\n";
        built += CustomizationManager.SelectedRingtone + "\n";

        System.IO.File.WriteAllText(customizationSavePath, built);
    }
}
