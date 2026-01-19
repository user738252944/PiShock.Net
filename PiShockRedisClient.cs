using System.Text.Json;
using StackExchange.Redis;

namespace PiShock.Net;

public sealed class PiShockRedisClient : IAsyncDisposable
{
    private readonly PiShockRedisOptions _opt;
    private ConnectionMultiplexer? _mux;
    private ISubscriber? _sub;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PiShockRedisClient(PiShockRedisOptions options)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        if (_opt.UserId <= 0) throw new ArgumentOutOfRangeException(nameof(options.UserId), "UserId must be > 0.");
        if (string.IsNullOrWhiteSpace(_opt.Token)) throw new ArgumentException("Token cannot be empty.", nameof(options.Token));
    }

    public bool IsConnected => _mux?.IsConnected == true;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_mux is { IsConnected: true }) return;

        var config = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = _opt.ConnectTimeoutMs,
            AsyncTimeout = _opt.ConnectTimeoutMs,
            User = _opt.RedisUsername,
            Password = _opt.Token,
            Ssl = false
        };
        config.EndPoints.Add(_opt.Host, _opt.Port);

        _mux = await ConnectionMultiplexer.ConnectAsync(config);
        _sub = _mux.GetSubscriber();

        var db = _mux.GetDatabase();
        _ = await db.PingAsync();
    }

    public async Task<long> SendAsync(PiShockCommand command, CancellationToken ct = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        EnsureConnected();

        ValidateCommand(command);

        var log = command.Log ?? (command.ShareCode == null
            ? PiShockLog.Api(_opt.UserId, warning: false, held: false, origin: _opt.Origin)
            : PiShockLog.ShareCode(_opt.UserId, warning: false, held: false, origin: _opt.Origin));

        var payload = new PiShockPayload
        {
            Id = command.ShockerId,
            Mode = command.Mode.ToMap(),
            Intensity = command.Intensity,
            DurationMs = command.DurationMs,
            Repeating = true,
            Log = log
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        
        var channel = command.ShareCode == null
            ? $"c{command.ClientId}-ops"
            : $"c{command.ClientId}-sops-{command.ShareCode}";
        return await _sub!.PublishAsync(channel, json);
    }
 
    private void EnsureConnected()
    {
        if (_mux is null || _sub is null || !_mux.IsConnected)
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");
    }

    private static void ValidateCommand(PiShockCommand cmd)
    {
        if (cmd.ShockerId <= 0) throw new ArgumentOutOfRangeException(nameof(cmd.ShockerId), "ShockerId must be > 0.");
        if (cmd.Intensity < 0) throw new ArgumentOutOfRangeException(nameof(cmd.Intensity), "Intensity must be >= 0.");
        if (cmd.DurationMs < 0) throw new ArgumentOutOfRangeException(nameof(cmd.DurationMs), "DurationMs must be >= 0.");
        if (cmd.ClientId <= 0) throw new ArgumentOutOfRangeException(nameof(cmd.ClientId), "ClientId must be > 0.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_mux is not null)
        {
            await _mux.CloseAsync();
            _mux.Dispose();
        }
    }
}
