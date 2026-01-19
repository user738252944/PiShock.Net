using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PiShock.Net;

/// <summary>
/// Handles PiShock web login via a local HTTP server and browser interaction.
/// </summary>
public sealed class PiShockWebLogin : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly TaskCompletionSource<(long userId, string token)> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly int _port;
    private readonly string _baseUrl;

    public PiShockWebLogin(int? port = null)
    {
        _port = port ?? GetFreeTcpPort();
        _baseUrl = $"http://127.0.0.1:{_port}/";
        _listener.Prefixes.Add(_baseUrl);
    }

    /// <summary>
    /// Starts a tiny local webserver, opens a browser page, and blocks until login completes.
    /// </summary>
    public async Task<(long userId, string token)> LoginAsync(
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        timeout ??= TimeSpan.FromMinutes(2);

        _listener.Start();
        _ = Task.Run(() => ServeLoopAsync(ct), ct);

        OpenBrowser(_baseUrl);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout.Value);

        try
        {
            return await _tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Timed out waiting for PiShock login after {timeout.Value}.");
        }
    }

    private async Task ServeLoopAsync(CancellationToken ct)
    {
        try
        {
            while (_listener.IsListening && !ct.IsCancellationRequested && !_tcs.Task.IsCompleted)
            {
                var ctx = await _listener.GetContextAsync();
                
                var path = ctx.Request.Url?.AbsolutePath ?? "/";
                if (path == "/" && ctx.Request.HttpMethod == "GET")
                {
                    await RespondHtmlAsync(ctx.Response, BuildHtml(_baseUrl));
                }
                else if (path == "/callback" && ctx.Request.HttpMethod == "POST")
                {
                    await HandleCallbackAsync(ctx);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    await RespondTextAsync(ctx.Response, "Not found");
                }
            }
        }
        catch when (ct.IsCancellationRequested)
        {
            // ignore
        }
        catch (HttpListenerException)
        {
            // listener stopped
        }
        catch (Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }

    private async Task HandleCallbackAsync(HttpListenerContext ctx)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            
            var payload = JsonSerializer.Deserialize<CallbackPayload>(body);
            if (payload is null || payload.Id <= 0 || string.IsNullOrWhiteSpace(payload.Token))
            {
                ctx.Response.StatusCode = 400;
                await RespondTextAsync(ctx.Response, "Invalid payload");
                return;
            }

            _tcs.TrySetResult((payload.Id, payload.Token));

            ctx.Response.StatusCode = 200;
            await RespondTextAsync(ctx.Response, "OK");
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await RespondTextAsync(ctx.Response, "Error: " + ex.Message);
        }
    }

    private static async Task RespondHtmlAsync(HttpListenerResponse res, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        res.ContentType = "text/html; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        res.OutputStream.Close();
    }

    private static async Task RespondTextAsync(HttpListenerResponse res, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        res.ContentType = "text/plain; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        res.OutputStream.Close();
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static int GetFreeTcpPort()
    {
        // Locate a free TCP port by binding to port 0
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string BuildHtml(string baseUrl)
    {
        var callbackUrl = baseUrl.TrimEnd('/') + "/callback";

        return $@"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <title>PiShock Login</title>
  <style>
    body {{ font-family: system-ui, sans-serif; margin: 24px; }}
    code {{ background: #f3f3f3; padding: 2px 6px; border-radius: 6px; }}
    button {{ padding: 10px 14px; font-size: 14px; }}
    #status {{ margin-top: 14px; }}
  </style>
</head>
<body>
  <h2>PiShock Login</h2>
  <p>Click the button to login. This page will close the popup automatically.</p>
  <button id=""btn"">Login with PiShock</button>
  <div id=""status""></div>

<script>
let loginWindow = null;

function setStatus(msg) {{
  document.getElementById('status').textContent = msg;
}}

async function sendToLocal(payload) {{
  const res = await fetch('{EscapeJs(callbackUrl)}', {{
    method: 'POST',
    headers: {{ 'Content-Type': 'application/json' }},
    body: JSON.stringify(payload)
  }});
  return res.ok;
}}

function receiveMessage(event) {{
  if (event.origin !== 'https://login.pishock.com') {{
    return;
  }}
  const data = event.data || {{}};
  const userId = data.id;
  const token = data.token;

  if (!userId || !token) {{
    setStatus('Received message, but missing id/token.');
    return;
  }}

  setStatus('Sending credentials to application...');
  sendToLocal({{ id: userId, token: token }})
    .then(ok => {{
      if (ok)
      {{
            setStatus('Success! You can now return to the application.');
            setTimeout(() => {{ window.close(); }}, 2000);
      }}
      else setStatus('Local callback failed.');
      try {{ if (loginWindow) loginWindow.close(); }} catch {{}}
    }})
    .catch(err => {{
      setStatus('Error posting to local app: ' + err);
      try {{ if (loginWindow) loginWindow.close(); }} catch {{}}
    }});
}}

window.addEventListener('message', receiveMessage, false);

function openLoginWindow() {{
  setStatus('Opening login window...');
  loginWindow = window.open(
    'https://login.pishock.com?proto=web',
    'pishock_login',
    'toolbar=yes,scrollbars=yes,resizable=yes,width=500,height=800'
  );
  if (!loginWindow) {{
    setStatus('Popup blocked. Please allow popups and try again.');
  }}
}}

document.getElementById('btn').addEventListener('click', () => {{
    openLoginWindow();
}});
</script>
</body>
</html>";
    }

    private static string EscapeJs(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");

    public async ValueTask DisposeAsync()
    {
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        await Task.CompletedTask;
    }

    private sealed class CallbackPayload
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";
    }
}
