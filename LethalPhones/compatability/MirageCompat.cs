using System.Runtime.CompilerServices;
using WeatherRegistry;

namespace Scoops.compatability
{
    internal static class MirageCompat
    {
        public static bool Enabled =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("qwbarch.Mirage");

    }
}
