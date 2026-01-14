using EyeExamParser.DTO;
using EyeExamParser.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

public sealed class ScheduleServices : IScheduleServices
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IScheduleParser _parser;
    private readonly ILogger<ScheduleServices> _logger;

    private const string ParsedCacheKey = "parsed_schedule_cache";

    public ScheduleServices(
        HttpClient httpClient,
        IMemoryCache cache,
        IScheduleParser parser,
        ILogger<ScheduleServices> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// Fetch raw schedule data, parse into ScheduleDTO, cache result
    /// </summary>
    public async Task<IReadOnlyList<ScheduleDTO>> GetSchedulesAsync()
    {
        if (_cache.TryGetValue(ParsedCacheKey, out IReadOnlyList<ScheduleDTO> cached))
        {
            _logger.LogInformation("Cache hit for {CacheKey}. Returning {Count} schedules.", ParsedCacheKey, cached.Count);
            return cached;
        }

        _logger.LogInformation("Cache miss for {CacheKey}. Fetching raw schedules from /schedules.", ParsedCacheKey);

        var start = DateTimeOffset.UtcNow;
        List<RawScheduleDTO>? raw;

        try
        {
            raw = await _httpClient.GetFromJsonAsync<List<RawScheduleDTO>>("/schedules");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch raw schedules from /schedules.");
            throw;
        }

        raw ??= new List<RawScheduleDTO>();

        var fetchMs = (DateTimeOffset.UtcNow - start).TotalMilliseconds;
        _logger.LogInformation("Fetched {RawCount} raw schedules from /schedules in {ElapsedMs}ms.", raw.Count, (int)fetchMs);

        IReadOnlyList<ScheduleDTO> parsed;
        try
        {
            var parseStart = DateTimeOffset.UtcNow;
            parsed = _parser.Parse(raw).ToList().AsReadOnly();
            var parseMs = (DateTimeOffset.UtcNow - parseStart).TotalMilliseconds;

            _logger.LogInformation("Parsed {ParsedCount}/{RawCount} schedules in {ElapsedMs}ms.", parsed.Count, raw.Count, (int)parseMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parsing failed for raw schedules count={RawCount}.", raw.Count);
            throw;
        }

        _cache.Set(
            ParsedCacheKey,
            parsed,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        _logger.LogInformation("Cached {Count} parsed schedules for {Minutes} minutes. CacheKey={CacheKey}.",
            parsed.Count, 10, ParsedCacheKey);

        return parsed;
    }

    /// <summary>
    /// Verify results from cache against external API under /results
    /// </summary>
    public async Task<string> VerifyAgainstExternalResultsAsync()
    {
        if (!_cache.TryGetValue(ParsedCacheKey, out IReadOnlyList<ScheduleDTO> cached))
        {
            _logger.LogWarning("Verify requested but cache is empty. CacheKey={CacheKey}.", ParsedCacheKey);
            return "NO - cache is empty";
        }

        _logger.LogInformation("Verify started. CachedCount={Count}. Calling /results.", cached.Count);

        List<ScheduleDTO>? external;
        var start = DateTimeOffset.UtcNow;

        try
        {
            external = await _httpClient.GetFromJsonAsync<List<ScheduleDTO>>("/results");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch external results from /results.");
            return "NO - failed to fetch external results";
        }

        external ??= new List<ScheduleDTO>();
        var fetchMs = (DateTimeOffset.UtcNow - start).TotalMilliseconds;

        _logger.LogInformation("Fetched {ExternalCount} external results from /results in {ElapsedMs}ms.",
            external.Count, (int)fetchMs);

        // Detect duplicates instead of throwing
        var cachedDupes = cached.GroupBy(x => x.EntryNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var externalDupes = external.GroupBy(x => x.EntryNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (cachedDupes.Count > 0 || externalDupes.Count > 0)
        {
            _logger.LogWarning("Duplicate EntryNumbers detected. CachedDupes={CachedDupes}. ExternalDupes={ExternalDupes}.",
                string.Join(",", cachedDupes), string.Join(",", externalDupes));
        }

        var sb = new StringBuilder();

        var oursByEntry = cached.GroupBy(x => x.EntryNumber).ToDictionary(g => g.Key, g => g.First());
        var extByEntry = external.GroupBy(x => x.EntryNumber).ToDictionary(g => g.Key, g => g.First());

        var missing = extByEntry.Keys.Except(oursByEntry.Keys).OrderBy(x => x).ToList();
        var extra = oursByEntry.Keys.Except(extByEntry.Keys).OrderBy(x => x).ToList();

        foreach (var entry in missing)
            sb.AppendLine($"Missing EntryNumber {entry}");

        foreach (var entry in extra)
            sb.AppendLine($"Extra EntryNumber {entry}");

        int diffCount = 0;
        foreach (var entry in oursByEntry.Keys.Intersect(extByEntry.Keys))
        {
            var beforeLen = sb.Length;
            CompareEntry(entry, oursByEntry[entry], extByEntry[entry], sb);
            if (sb.Length != beforeLen) diffCount++;
        }

        _logger.LogInformation("Verify finished. Missing={Missing} Extra={Extra} Diffs={Diffs}.",
            missing.Count, extra.Count, diffCount);

        return sb.Length == 0 ? "Yup 😃" : $"NO\n{sb}";
    }

    #region Verify Results Helpers
    private static void CompareEntry(
    int entryNumber,
    ScheduleDTO ours,
    ScheduleDTO expected,
    StringBuilder sb)
    {
        CompareField("RegistrationDateAndPlanRef",
            ours.RegistrationDateAndPlanRef,
            expected.RegistrationDateAndPlanRef,
            entryNumber,
            sb);

        CompareField("PropertyDescription",
            ours.PropertyDescription,
            expected.PropertyDescription,
            entryNumber,
            sb);

        CompareField("DateOfLeaseAndTerm",
            ours.DateOfLeaseAndTerm,
            expected.DateOfLeaseAndTerm,
            entryNumber,
            sb);

        CompareField("LesseesTitle",
            ours.LesseesTitle,
            expected.LesseesTitle,
            entryNumber,
            sb);

        CompareNotes(entryNumber, ours.Notes, expected.Notes, sb);
    }

    private static void CompareField(
    string field,
    string? ours,
    string? expected,
    int entryNumber,
    StringBuilder sb)
    {
        var a = JsonHelper.Normalize(ours);
        var b = JsonHelper.Normalize(expected);

        if (a != b)
        {
            sb.AppendLine(
                $"Entry {entryNumber} - {field} differs:\n" +
                $"  OURS:     '{a}'\n" +
                $"  EXPECTED: '{b}'"
            );
        }
    }

    private static void CompareNotes(
    int entryNumber,
    IReadOnlyList<string>? ours,
    IReadOnlyList<string>? expected,
    StringBuilder sb)
    {
        ours ??= Array.Empty<string>();
        expected ??= Array.Empty<string>();

        if (ours.Count != expected.Count)
        {
            sb.AppendLine(
                $"Entry {entryNumber} - Notes count differs (ours={ours.Count}, expected={expected.Count})"
            );
            return;
        }

        for (int i = 0; i < ours.Count; i++)
        {
            var a = JsonHelper.Normalize(ours[i]);
            var b = JsonHelper.Normalize(expected[i]);

            if (a != b)
            {
                sb.AppendLine(
                    $"Entry {entryNumber} - Note[{i}] differs:\n" +
                    $"  OURS:     '{a}'\n" +
                    $"  EXPECTED: '{b}'"
                );
            }
        }
    }
    #endregion
}
