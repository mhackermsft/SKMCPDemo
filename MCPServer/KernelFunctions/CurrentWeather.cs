using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MCPServer.KernelFunctions
{
    internal class CurrentWeather
    {
        [KernelFunction, Description("Gets the current weather for the specified city and state. State is optional but if provided must be the full name of the state and not the abbreviation")]
        public static async Task<string> GetWeatherForCity(string city, string state)
        {

            // Validate the 'city' parameter
            if (string.IsNullOrWhiteSpace(city))
            {
                return "Error: City name cannot be null or empty.";
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Get geocoding data
                    HttpResponseMessage geoResponse = await client.GetAsync($"https://geocoding-api.open-meteo.com/v1/search?name={city}&countryCode=US&count=50");
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

                    // If the state is not empty, filter the results by state, which is in the Admin1 field in the JSON
                    JsonElement firstResult = results[0];
                    if (!string.IsNullOrEmpty(state))
                    {
                        JsonElement? filteredResult = results.EnumerateArray()
                            .FirstOrDefault(result => result.TryGetProperty("admin1", out JsonElement admin1) &&
                                                      admin1.GetString().Equals(state, StringComparison.OrdinalIgnoreCase));

                        if (filteredResult == null || filteredResult.Value.ValueKind == JsonValueKind.Undefined)
                        {
                            return $"Error: Could not find coordinates for {city} in {state}.";
                        }

                        firstResult = filteredResult.Value;
                    }

                    double latitude = firstResult.GetProperty("latitude").GetDouble();
                    double longitude = firstResult.GetProperty("longitude").GetDouble();
                    string determinedState = firstResult.GetProperty("admin1").GetString() ?? string.Empty;

                    // Get the weather data
                    HttpResponseMessage weatherResponse = await client.GetAsync(
                        $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&forecast_days=7&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max&current=temperature_2m,precipitation,weather_code,relative_humidity_2m,wind_speed_10m,wind_direction_10m,wind_gusts_10m&timezone=auto"
                    );

                    if (!weatherResponse.IsSuccessStatusCode)
                    {
                        return $"Error: Could not retrieve weather information for {city}.";
                    }

                    responseContent = await weatherResponse.Content.ReadAsStringAsync();
                    JsonDocument weatherData = JsonDocument.Parse(responseContent);

                    // Parse current weather
                    if (!weatherData.RootElement.TryGetProperty("current", out JsonElement current) ||
                        !weatherData.RootElement.TryGetProperty("current_units", out JsonElement currentUnits))
                    {
                        return "Error: Could not parse current weather data.";
                    }

                    double temperatureC = current.GetProperty("temperature_2m").GetDouble();
                    double temperatureF = temperatureC * 9 / 5 + 32;

                    double windSpeedKmh = current.GetProperty("wind_speed_10m").GetDouble();
                    double windSpeedMph = windSpeedKmh * 0.621371;

                    double precipitationMm = current.GetProperty("precipitation").GetDouble();
                    double precipitationIn = precipitationMm * 0.0393701;

                    string currentWeather = $"Current Weather in {city}, {determinedState}:\n" +
                                            $"- Temperature: {temperatureF:F1}°F\n" +
                                            $"- Precipitation: {precipitationIn:F2} in\n" +
                                            $"- Wind Speed: {windSpeedMph:F1} mph\n";

                    // Parse daily forecast
                    if (!weatherData.RootElement.TryGetProperty("daily", out JsonElement daily) ||
                        !weatherData.RootElement.TryGetProperty("daily_units", out JsonElement dailyUnits))
                    {
                        return "Error: Could not parse daily forecast data.";
                    }

                    string dailyForecast = "7-Day Forecast:\n";
                    if (!daily.TryGetProperty("time", out JsonElement timeArray) || timeArray.ValueKind != JsonValueKind.Array)
                    {
                        return "Error: Could not parse daily forecast data.";
                    }

                    for (int i = 0; i < timeArray.GetArrayLength(); i++)
                    {
                        string date = timeArray[i].GetString();
                        if (string.IsNullOrEmpty(date))
                        {
                            continue;
                        }

                        double maxTempC = daily.GetProperty("temperature_2m_max")[i].GetDouble();
                        double minTempC = daily.GetProperty("temperature_2m_min")[i].GetDouble();
                        double maxTempF = maxTempC * 9 / 5 + 32;
                        double minTempF = minTempC * 9 / 5 + 32;

                        double dailyPrecipitationMm = daily.GetProperty("precipitation_sum")[i].GetDouble();
                        double dailyPrecipitationIn = dailyPrecipitationMm * 0.0393701;

                        dailyForecast += $"- {date}: High {maxTempF:F1}°F, Low {minTempF:F1}°F, Precipitation {dailyPrecipitationIn:F2} in\n";
                    }

                    return currentWeather + "\n" + dailyForecast;
                }
            }
            catch (Exception ex)
            {
                return "Error: An unexpected error occurred. Please try again later.";
            }

        }
    }
}
