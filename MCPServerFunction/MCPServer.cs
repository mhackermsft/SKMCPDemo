using MCPServerFunction.Models;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
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


        [Function(nameof(GetWebInstantAnswer))]
        public async Task<string> GetWebInstantAnswer([McpToolTrigger("GetWebInstantAnswer", "Get an instant answer from the web based on the supplied query.")] ToolInvocationContext context,
            [McpToolProperty("query", "string", "Question to search for an instant answer")] string query)
        {
            _logger.LogInformation($"GetWebInstantAnswer called with prompt: {query}");


            string url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_redirect=1";

            using HttpClient client = new HttpClient();
            try
            {
                string response = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(response);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("AbstractText", out JsonElement abstractText) && !string.IsNullOrWhiteSpace(abstractText.GetString()))
                {
                    return "Result: " + abstractText.GetString();
                }
                else
                {
                    return "No direct answer found. Try a web search.";
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error fetching instant answer from the web.");
                return "Error: Could not retrieve data from the web. Please try again later.";
            }
        }

        [Function(nameof(SearchTheWebForSites))]
        public async Task<string> SearchTheWebForSites([McpToolTrigger("SearchTheWebForSites", "Search the Internet (web) for web sites that have content based on the supplied query.")] ToolInvocationContext context,
            [McpToolProperty("query", "string", "keyword search query")] string query)
        {
            string results = string.Empty;
            _logger.LogInformation($"SearchTheWebForSites called with prompt: {query}");

            // https://brave.com/search/api/
            string apiKey = Environment.GetEnvironmentVariable("BraveSearchApiKey")??string.Empty;
            string url = $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}";

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            using HttpClient client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("X-Subscription-Token", apiKey);

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                results ="Top Results:\n";
                foreach (var result in root.GetProperty("web").GetProperty("results").EnumerateArray())
                {
                    results+=$"- {result.GetProperty("title").GetString()}]\n";
                    results += Environment.NewLine + $"  {result.GetProperty("url").GetString()}\n";
                }
                return results; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching the web for sites. {ex.Message}");
                return "SearchTheWebForSites error. unable to get search results.";
            }
        }

        [Function(nameof(GetStringFromURL))]
        public string GetStringFromURL([McpToolTrigger("GetStringFromURL", "Gets string content from supplied URL")] ToolInvocationContext context,
            [McpToolProperty("url", "string", "URL to fetch content from")] string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = client.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"GetStringFromURL called with URL: {url}");
                    return response.Content.ReadAsStringAsync().Result;
                }
                return $"Could not retrieve data from {url}. Error {response.StatusCode}. Is there another URL to try?";
            }
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
            [McpToolProperty("city", "string", "Name of the city")] string city,
            [McpToolProperty("state", "string", "Optional full name of the state and not the abbreviation; empty if no state available")] string state

        )
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
                    string determinedState = firstResult.GetProperty("admin1").GetString()??string.Empty;

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
                        _logger.LogError("Error: 'time' property is missing or not an array in the daily forecast data.");
                        return "Error: Could not parse daily forecast data.";
                    }

                    for (int i = 0; i < timeArray.GetArrayLength(); i++)
                    {
                        string date = timeArray[i].GetString();
                        if (string.IsNullOrEmpty(date))
                        {
                            _logger.LogWarning($"Warning: Missing or invalid date at index {i} in the 'time' array.");
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
                _logger.LogError(ex, "An error occurred while processing the weather request.");
                return "Error: An unexpected error occurred. Please try again later.";
            }
        }
    }
}
