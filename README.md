# PiShock.Net
An unofficial .NET SDK for PiShock authentication, device lookup APIs, and Redis control.

[![NuGet](https://img.shields.io/nuget/v/pishock.net.svg)](https://www.nuget.org/packages/pishock.net)

## Features

- ✅ Cross-platform local login flow (tiny localhost webserver) to obtain **UserId** and **Token**
- ✅ Ability to fetch all **owned shockers** and all **shockers shared to you**
- ✅ Execute commands via PiShock Redis (shock, vibrate, beep, etc.)

---

## Install

```bash
dotnet add package pishock.net
```

---

## Quick Start

### Login
```csharp
using var login = new PiShockWebLogin();
var (userId, token) = await login.LoginAsync(); // Blocks until successful or throws TimeoutException

Console.WriteLine($"Logged in! UserId={userId}, Token={token}");
```

---

### Fetch shockers (owned + shared)
```csharp
using var http = new HttpClient();

var api = new PiShockApiClient(http);
var all = await api.GetAllShockersAsync(userId, token);

// Note: if you have shared your own devices, those shockers may also appear in the shared list.

Console.WriteLine($"Owned shockers:  {all.Owned.Count}");
Console.WriteLine($"Shared shockers: {all.Shared.Count}");

foreach (var s in all.Owned)
    Console.WriteLine($"[Owned]  {s.ShockerName} (ShockerId={s.ShockerId}, ClientId={s.ClientId})");

foreach (var s in all.Shared)
    Console.WriteLine($"[Shared] {s.OwnerUsername} -> {s.ShockerName} (ShockerId={s.ShockerId}, ClientId={s.ClientId}, ShareCode={s.ShareCode})");
```

### Send a command via Redis
To send a command to your own device, specify the `ClientId` and `ShockerId`:
```csharp
var redis = new PiShockRedisClient(new PiShockRedisOptions
{
    Host = "redis.pishock.com",
    Port = 6379,

    // IMPORTANT: Redis auth uses website token (from login), not API key
    UserId = userId,
    Token = token,
    Origin = "PiShock.Net"
});

await redis.ConnectAsync();

// Shock 10 intensity for 1 second
await redis.SendAsync(new PiShockCommand
{
    ClientId = all.Owned[0].ClientId,
    ShockerId = all.Owned[0].ShockerId,
    Mode = PiShockMode.Shock,
    Intensity = 10,
    DurationMs = 1000
});
```
To send a command to a shared device, specify the `ShareCode`, `ClientId`, and `ShockerId`:
```csharp
await redis.SendAsync(new PiShockCommand
{
    ShareCode = all.Shared[0].ShareCode,
    ClientId = all.Shared[0].ClientId,
    ShockerId = all.Shared[0].ShockerId,
    Mode = PiShockMode.Vibrate,
    Intensity = 20,
    DurationMs = 500,
});
```

---

### Notes
- The PiShock website token expires (documentation mentions roughly two weeks). If Redis/API calls fail, log in again.
- PiShock’s Redis API does not support API key authentication, which is why the local login webserver is required.
