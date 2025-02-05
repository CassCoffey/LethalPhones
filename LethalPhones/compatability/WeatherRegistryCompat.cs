using System.Runtime.CompilerServices;
using WeatherRegistry;

namespace Scoops.compatability
{
    internal static class WeatherRegistryCompat
    {
        public static bool Enabled =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("mrov.WeatherRegistry");

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static string CurrentWeatherName()
        {
            if (WeatherManager.GetCurrentLevelWeather() != null)
            {
                return WeatherManager.GetCurrentLevelWeather().Name;
            }

            return "";
        }
    }
}
