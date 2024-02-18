using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalCompanyInputUtils.Api;
using Scoops.patch;
using Scoops.service;
using UnityEngine.InputSystem;

namespace Scoops;

public class LethalPhonesInputClass : LcInputActions
{
    [InputAction("<Keyboard>/0", Name = "Toggle Phone")]
    public InputAction TogglePhoneKey { get; set; }
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance { get; set; }

    public static ManualLogSource Log => Instance.Logger;

    private readonly Harmony _harmony = new(PluginInfo.PLUGIN_GUID);

    internal static LethalPhonesInputClass InputActionInstance = new LethalPhonesInputClass();
    public PhoneManager PhoneManager;

    public Plugin()
    {
        Instance = this;
    }

    private void Awake()
    {
        PhoneManager = new PhoneManager();

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
    }
}
