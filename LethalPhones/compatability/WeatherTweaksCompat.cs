using System.Runtime.CompilerServices;
using UnityEngine;
using WeatherTweaks;
using WeatherTweaks.Definitions;

namespace Scoops.compatability
{
    internal static class WeatherTweaksCompat
    {
        public static bool Enabled =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("WeatherTweaks");

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static string CurrentWeatherName()
        {
            if (WeatherTweaks.Variables.IsSetupFinished)
            {
                if (WeatherTweaks.Variables.CurrentLevelWeather.CustomType == CustomWeatherType.Progressing)
                {
                    if (ChangeMidDay.CurrentEntry != null)
                    {
                        return ChangeMidDay.CurrentEntry.GetWeather().Name;
                    }
                }

                return WeatherTweaks.Variables.CurrentLevelWeather.Name;
            }

            return "";
        }
    }
}
