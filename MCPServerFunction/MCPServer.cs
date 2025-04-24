using MCPServerFunction.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace MCPServerFunction
{
    public class MCPServer
    {
        private readonly ILogger<MCPServer> _logger;

        public MCPServer(ILogger<MCPServer> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GetDateTimeUTCString))]
        public string GetDateTimeUTCString([McpToolTrigger("GetDateTimeUTCString","Get the current date and time in UTC string format")] ToolInvocationContext context)
        {
            _logger.LogInformation("GetDateTimeUTCString called");
            return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        [Function(nameof(GetWeatherForCity))]
        public async Task<string> GetWeatherForCity(
            [McpToolTrigger("GetWeatherForCity", "Get the current weather for the specified city")] ToolInvocationContext context,
            [McpToolProperty("city","string","Name of the city")] string city
            ) 
        {


            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage geoResponse = await client.GetAsync($"https://geocoding-api.open-meteo.com/v1/search?name={city}");

                if (!geoResponse.IsSuccessStatusCode)
                {
                    return $"Error: Could not retrieve coordinates for {city}.";
                }

                string responseContent = await geoResponse.Content.ReadAsStringAsync();
                JsonDocument geoData = JsonDocument.Parse(responseContent);

                if (!geoData.RootElement.TryGetProperty("results", out JsonElement results) || results.GetArrayLength() == 0)
                {
                    return $"Error: Could not retrieve coordinates for {city}.";
                }

                JsonElement firstResult = results[0];
                double latitude = firstResult.GetProperty("latitude").GetDouble();
                double longitude = firstResult.GetProperty("longitude").GetDouble();

                // Get the weather data
                HttpResponseMessage weatherResponse = await client.GetAsync(
                 $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&hourly=temperature_2m,weather_code&timezone=auto"
                 );

                if (!weatherResponse.IsSuccessStatusCode)
                {
                    return $"Error: Could not retrieve weather information for {city}.";
                }

                responseContent = await weatherResponse.Content.ReadAsStringAsync();
                JsonDocument weatherData = JsonDocument.Parse(responseContent);

                JsonElement hourly = weatherData.RootElement.GetProperty("hourly");
                JsonElement timeArray = hourly.GetProperty("time");
                JsonElement temperatureArray = hourly.GetProperty("temperature_2m");
                JsonElement weatherCodeArray = hourly.GetProperty("weather_code");

                int currentHourIndex = 0;
                DateTime maxTime = DateTime.MinValue;

                for (int i = 0; i < timeArray.GetArrayLength(); i++)
                {
                    if (DateTime.TryParse(timeArray[i].GetString(), out DateTime time))
                    {
                        if (time > maxTime)
                        {
                            maxTime = time;
                            currentHourIndex = i;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Invalid date format encountered in timeArray at index {i}: {timeArray[i].GetString()}");
                    }
                }

                double temperature = temperatureArray[currentHourIndex].GetDouble();
                double temperatureFahrenheit = (temperature * 9 / 5) + 32; // Convert Celsius to Fahrenheit
                int weatherCode = weatherCodeArray[currentHourIndex].GetInt32();

                // Convert weather code to description using WeatherDescriptionsEnum
                string weatherDescription = Enum.IsDefined(typeof(WeatherDescriptionsEnum), weatherCode)
                    ? ((WeatherDescriptionsEnum)weatherCode).GetDescription()
                    : "Unknown";

                return $"Current weather in {city}:\n" +
                       $"Temperature: {temperatureFahrenheit:F1}°F ({temperature:F1}°C)\n" + 
                       $"Weather: {weatherDescription}\n" +
                       $"Time: {maxTime.ToString("yyyy-MM-dd HH:mm:ss")}";


            }
        }
    }
}
