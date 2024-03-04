using BepInEx;
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
    [InputAction("<Keyboard>/0", Name = "Toggle Phone")]
    public InputAction TogglePhoneKey { get; set; }

    [InputAction("<Mouse>/rightButton", Name = "Hangup Phone")]
    public InputAction HangupPhoneKey { get; set; }

    [InputAction("<Mouse>/leftButton", Name = "Pickup Phone")]
    public InputAction PickupPhoneKey { get; set; }

    [InputAction("<Keyboard>/z", Name = "Dial Phone")]
    public InputAction DialPhoneKey { get; set; }
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance { get; set; }

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

        PhoneSoundManager.Init();

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
        _harmony.PatchAll(typeof(NetworkObjectManager));
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
