namespace PiShock.Net;

public sealed class PiShockRedisOptions
{
    public string Host { get; init; } = "redis.pishock.com";
    public int Port { get; init; } = 6379;

    /// <summary>User ID (numeric)</summary>
    public long UserId { get; init; }

    /// <summary>Redis username is "user" + UserId</summary>
    public string RedisUsername => $"user{UserId}";

    /// <summary>Your PiShock API Token</summary>
    public string Token { get; init; } = "";

    /// <summary>Name shown in logs ("o").</summary>
    public string Origin { get; init; } = "PiShock.Net";

    /// <summary>Connection timeout in ms.</summary>
    public int ConnectTimeoutMs { get; init; } = 5000;
}