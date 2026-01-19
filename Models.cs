using System.Text.Json.Serialization;

namespace PiShock.Net;

public enum PiShockMode
{
    Vibrate,
    Shock,
    Beep,
    End
}

internal static class PiShockModeExtensions
{
    public static string ToMap(this PiShockMode mode) => mode switch
    {
        PiShockMode.Vibrate => "v",
        PiShockMode.Shock   => "s",
        PiShockMode.Beep    => "b",
        PiShockMode.End     => "e",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };
}

public sealed class PiShockLog
{
    [JsonPropertyName("u")]
    public long UserId { get; init; }

    /// <summary>'sc' or 'api'</summary>
    [JsonPropertyName("ty")]
    public string Type { get; init; } = "api";

    [JsonPropertyName("w")]
    public bool Warning { get; init; }

    [JsonPropertyName("h")]
    public bool Held { get; init; }

    [JsonPropertyName("o")]
    public string Origin { get; init; } = "PiShock.Net";

    public static PiShockLog Api(long userId, bool warning, bool held, string origin)
        => new() { UserId = userId, Type = "api", Warning = warning, Held = held, Origin = origin };

    public static PiShockLog ShareCode(long userId, bool warning, bool held, string origin)
        => new() { UserId = userId, Type = "sc", Warning = warning, Held = held, Origin = origin };
}

public sealed class PiShockCommand
{
    public long ShockerId { get; init; }
    public PiShockMode Mode { get; init; }

    /// <summary>Intensity value (0-100)</summary>
    public int Intensity { get; init; }

    /// <summary>Duration in milliseconds</summary>
    public int DurationMs { get; init; }

    /// <summary>Always set to true per documentation</summary>
    public bool IsRepeating { get; init; } = true;

    public PiShockLog? Log { get; init; } = null;
    
    public long ClientId { get; init; }

    public string? ShareCode { get; init; } = null;
}

internal sealed class PiShockPayload
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("m")]
    public string Mode { get; init; } = "";

    [JsonPropertyName("i")]
    public int Intensity { get; init; }

    [JsonPropertyName("d")]
    public int DurationMs { get; init; }

    [JsonPropertyName("r")]
    public bool Repeating { get; init; }

    [JsonPropertyName("l")]
    public PiShockLog Log { get; init; } = default!;
}
