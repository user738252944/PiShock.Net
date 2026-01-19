using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiShock.Net;

public sealed class PiShockApiClient
{
    private readonly HttpClient _http;

    public PiShockApiClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public sealed record OwnedShocker(
        long ClientId,
        string HubName,
        long UserId,
        string Username,
        long ShockerId,
        string ShockerName,
        bool IsPaused
    );

    public sealed record SharedShocker(
        string OwnerUsername,
        long ShareId,
        long ClientId,
        long ShockerId,
        string ShockerName,
        bool IsPaused,
        int MaxIntensity,
        bool CanContinuous,
        bool CanShock,
        bool CanVibrate,
        bool CanBeep,
        bool CanLog,
        string ShareCode
    );

    public sealed record AllShockers(
        IReadOnlyList<OwnedShocker> Owned,
        IReadOnlyList<SharedShocker> Shared
    );
    
    public async Task<AllShockers> GetAllShockersAsync(long userId, string token, CancellationToken ct = default)
    {
        var owned = await GetOwnedShockersAsync(userId, token, ct);
        var shared = await GetSharedShockersAsync(userId, token, ct);
        return new AllShockers(owned, shared);
    }
    
    /// <summary>
    /// Retrieves a list of all devices owned by you.
    /// </summary>
    public async Task<IReadOnlyList<OwnedShocker>> GetOwnedShockersAsync(long userId, string token, CancellationToken ct = default)
    {
        var url =
            $"https://ps.pishock.com/PiShock/GetUserDevices?UserId={userId}&Token={Uri.EscapeDataString(token)}";

        var devices = await _http.GetFromJsonAsync<List<UserDeviceDto>>(url, JsonOpts, ct)
                      ?? new List<UserDeviceDto>();

        var result = new List<OwnedShocker>(capacity: 64);

        foreach (var d in devices)
        {
            if (d.shockers is null) continue;

            foreach (var s in d.shockers)
            {
                result.Add(new OwnedShocker(
                    ClientId: d.clientId,
                    HubName: d.name ?? "",
                    UserId: d.userId,
                    Username: d.username ?? "",
                    ShockerId: s.shockerId,
                    ShockerName: s.name ?? "",
                    IsPaused: s.isPaused
                ));
            }
        }

        return result;
    }

    /// <summary>
    /// Retrieves a list of all devices shared to you. If you have share codes generated, your own devices will also show up here.
    /// </summary>
    public async Task<IReadOnlyList<SharedShocker>> GetSharedShockersAsync(long userId, string token, CancellationToken ct = default)
    {
        var shareCodesUrl =
            $"https://ps.pishock.com/PiShock/GetShareCodesByOwner?UserId={userId}&Token={Uri.EscapeDataString(token)}";

        var byOwner = await _http.GetFromJsonAsync<Dictionary<string, List<long>>>(shareCodesUrl, JsonOpts, ct)
                     ?? new Dictionary<string, List<long>>();

        var allShareIds = byOwner.Values.SelectMany(x => x).Distinct().ToArray();
        if (allShareIds.Length == 0)
            return Array.Empty<SharedShocker>();
        
        var qs = string.Concat(allShareIds.Select(id => $"&shareIds={id}"));
        var shockersUrl =
            $"https://ps.pishock.com/PiShock/GetShockersByShareIds?UserId={userId}&Token={Uri.EscapeDataString(token)}{qs}";
        
        var json = await _http.GetStringAsync(shockersUrl, ct);

        var parsed =
            JsonSerializer.Deserialize<Dictionary<string, List<SharedShockerDto>>>(json, JsonOpts)
            ?? new Dictionary<string, List<SharedShockerDto>>();

        var result = new List<SharedShocker>(capacity: 128);

        foreach (var (ownerUsername, list) in parsed)
        {
            if (list is null) continue;

            foreach (var s in list)
            {
                result.Add(new SharedShocker(
                    OwnerUsername: ownerUsername,
                    ShareId: s.shareId,
                    ClientId: s.clientId,
                    ShockerId: s.shockerId,
                    ShockerName: s.shockerName ?? "",
                    IsPaused: s.isPaused,
                    MaxIntensity: s.maxIntensity,
                    CanContinuous: s.canContinuous,
                    CanShock: s.canShock,
                    CanVibrate: s.canVibrate,
                    CanBeep: s.canBeep,
                    CanLog: s.canLog,
                    ShareCode: s.shareCode ?? ""
                ));
            }
        }

        return result;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class UserDeviceDto
    {
        public long clientId { get; set; }
        public string? name { get; set; }
        public long userId { get; set; }
        public string? username { get; set; }
        public List<UserDeviceShockerDto>? shockers { get; set; }
    }

    private sealed class UserDeviceShockerDto
    {
        public string? name { get; set; }
        public long shockerId { get; set; }
        public bool isPaused { get; set; }
    }

    private sealed class SharedShockerDto
    {
        public long shareId { get; set; }
        public long clientId { get; set; }
        public long shockerId { get; set; }
        public string? shockerName { get; set; }
        public bool isPaused { get; set; }
        public int maxIntensity { get; set; }
        public bool canContinuous { get; set; }
        public bool canShock { get; set; }
        public bool canVibrate { get; set; }
        public bool canBeep { get; set; }
        public bool canLog { get; set; }
        public string? shareCode { get; set; }
    }
}
