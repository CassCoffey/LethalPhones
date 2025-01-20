using System;
using System.Collections.Generic;
using System.Text;
using WeatherRegistry;
using WeatherRegistry.Definitions;

namespace Scoops.compatability
{
    internal static class WeatherRegistryCompat
    {
        public static bool Enabled =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("mrov.WeatherRegistry");

        public static Weather CurrentWeather => WeatherManager.GetCurrentLevelWeather();
    }
}
