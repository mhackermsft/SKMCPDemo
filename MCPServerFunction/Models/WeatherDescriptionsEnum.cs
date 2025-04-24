using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MCPServerFunction.Models
{
    public enum WeatherDescriptionsEnum
    {
        [Description("Clear sky")]
        ClearSky = 0,

        [Description("Mainly clear")]
        MainlyClear = 1,

        [Description("Partly cloudy")]
        PartlyCloudy = 2,

        [Description("Overcast")]
        Overcast = 3,

        [Description("Fog")]
        Fog = 45,

        [Description("Depositing rime fog")]
        DepositingRimeFog = 48,

        [Description("Light drizzle")]
        LightDrizzle = 51,

        [Description("Moderate drizzle")]
        ModerateDrizzle = 53,

        [Description("Dense drizzle")]
        DenseDrizzle = 55,

        [Description("Light freezing drizzle")]
        LightFreezingDrizzle = 56,

        [Description("Dense freezing drizzle")]
        DenseFreezingDrizzle = 57,

        [Description("Slight rain")]
        SlightRain = 61,

        [Description("Moderate rain")]
        ModerateRain = 63,

        [Description("Heavy rain")]
        HeavyRain = 65,

        [Description("Light freezing rain")]
        LightFreezingRain = 66,

        [Description("Heavy freezing rain")]
        HeavyFreezingRain = 67,

        [Description("Slight snow fall")]
        SlightSnowFall = 71,

        [Description("Moderate snow fall")]
        ModerateSnowFall = 73,

        [Description("Heavy snow fall")]
        HeavySnowFall = 75,

        [Description("Snow grains")]
        SnowGrains = 77,

        [Description("Slight rain showers")]
        SlightRainShowers = 80,

        [Description("Moderate rain showers")]
        ModerateRainShowers = 81,

        [Description("Violent rain showers")]
        ViolentRainShowers = 82,

        [Description("Slight snow showers")]
        SlightSnowShowers = 85,

        [Description("Heavy snow showers")]
        HeavySnowShowers = 86,

        [Description("Thunderstorm")]
        Thunderstorm = 95,

        [Description("Thunderstorm with slight hail")]
        ThunderstormWithSlightHail = 96,

        [Description("Thunderstorm with heavy hail")]
        ThunderstormWithHeavyHail = 99
    }

    public static class WeatherDescriptionsEnumExtensions
    {
        public static string GetDescription(this WeatherDescriptionsEnum value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());
            var descriptionAttribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();
            return descriptionAttribute?.Description ?? value.ToString();
        }
    }
}
