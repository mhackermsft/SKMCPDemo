using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPServer.KernelFunctions
{
    internal class CurrentWeather
    {
        [KernelFunction, Description("Gets the current weather for the specified city and specified date time.")]
        public static string GetWeatherForCity(string cityName, string currentDateTimeInUtc)
        {
            return cityName switch
            {
                "Boston" => $"61 and rainy",
                "London" => $"55 and cloudy",
                "Detroit" => $"67 and mostly cloudy",
                _ => "Unknown city"
            };
        }
    }
}
