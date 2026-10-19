using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SchoolManagement.API.Services;

public class PublicHolidayException(string message) : Exception(message);

public class PublicHolidayService(HttpClient http, IConfiguration configuration, IMemoryCache cache)
{
    public record Holiday(DateOnly Date, string Name);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["PublicHolidays:CalendarificApiKey"]);

    public async Task<IReadOnlyList<Holiday>> Load(string country, int year, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new PublicHolidayException("Public holiday loading needs a Calendarific API key. Configure PublicHolidays:CalendarificApiKey on the API server, then restart the API.");
        if (year is < 1900 or > 2100 || country.Length != 2 || !country.All(char.IsAsciiLetter)) throw new PublicHolidayException("Save a valid country code and calendar year first.");
        country = country.ToUpperInvariant();
        var cacheKey = $"public-holidays:{country}:{year}";
        if (cache.TryGetValue<IReadOnlyList<Holiday>>(cacheKey, out var cached) && cached != null) return cached;
        try
        {
            var key = Uri.EscapeDataString(configuration["PublicHolidays:CalendarificApiKey"]!.Trim());
            // Fixed provider host; credentials stay server-side and HTTP request logging is disabled.
            using var response = await http.GetAsync($"https://calendarific.com/api/v2/holidays?api_key={key}&country={country}&year={year}&type=national", cancellationToken);
            if (!response.IsSuccessStatusCode) throw new PublicHolidayException(response.StatusCode switch {
                System.Net.HttpStatusCode.Unauthorized => "Calendarific rejected the API key. Ask Admin to check the server configuration.",
                System.Net.HttpStatusCode.Forbidden => "Calendarific access is unavailable. Check the provider account or subscription.",
                System.Net.HttpStatusCode.TooManyRequests => "The public holiday provider’s request limit has been reached. Try again later.",
                _ => "The public holiday provider is unavailable. No calendar dates were changed. Try again later."
            });
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!json.RootElement.TryGetProperty("meta", out var meta) || !meta.TryGetProperty("code", out var code) || code.GetInt32() != 200 ||
                !json.RootElement.TryGetProperty("response", out var payload) || !payload.TryGetProperty("holidays", out var holidays) || holidays.ValueKind != JsonValueKind.Array)
                throw new PublicHolidayException("The holiday provider returned an unsuccessful response. No dates were imported.");
            var result = new List<Holiday>();
            foreach (var item in holidays.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var types) || types.ValueKind != JsonValueKind.Array ||
                    !types.EnumerateArray().Any(t => t.GetString() is "National holiday" or "Public holiday" or "Federal Holiday" or "Federal holiday")) continue;
                // Ignore regional-only entries; the configured region is descriptive, not a provider region code.
                if (item.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.String && !string.Equals(locations.GetString(), "All", StringComparison.OrdinalIgnoreCase)) continue;
                if (!item.TryGetProperty("date", out var dateObject) || !dateObject.TryGetProperty("iso", out var iso) ||
                    !DateOnly.TryParseExact(iso.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year != year ||
                    !item.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString())) continue;
                result.Add(new(date, name.GetString()!.Trim()));
            }
            var grouped = result.GroupBy(h => h.Date).OrderBy(g => g.Key).Select(g => {
                var name = string.Join(" / ", g.Select(h => h.Name).Distinct());
                return new Holiday(g.Key, name.Length > 200 ? name[..200] : name);
            }).ToArray();
            cache.Set<IReadOnlyList<Holiday>>(cacheKey, grouped, TimeSpan.FromHours(12));
            return grouped;
        }
        catch (PublicHolidayException) { throw; }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException)
        {
            // Never expose provider URLs or credentials through exceptions/logs.
            throw new PublicHolidayException("Could not load public holidays from the provider. No calendar dates were changed. Try again later.");
        }
    }
}
