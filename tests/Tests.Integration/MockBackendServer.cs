using System.Net;
using System.Net.Sockets;

/// <summary>
/// 模拟后端服务器（用于测试转发）
/// </summary>
public class MockBackendServer
{
    private HttpListener _listener;
    private CancellationTokenSource _cts;
    private readonly Dictionary<string, Func<HttpListenerRequest, HttpResponseMessage>> _handlers = new();

    public string BaseUrl { get; private set; }
    public int Port { get; private set; }

    public async Task StartAsync()
    {
        Port = FindFreePort();
        BaseUrl = $"http://127.0.0.1:{Port}";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();

        _cts = new CancellationTokenSource();
        _ = ListenAsync(_cts.Token);

        await Task.Delay(100);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        await Task.Delay(50);
    }

    public void RegisterHandler(
        string path,
        Func<HttpListenerRequest, HttpResponseMessage> handler)
    {
        _handlers[path] = handler;
    }

    private async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context, token);
            }
        }
        catch { }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            var path = context.Request.Url.AbsolutePath;
            var response = _handlers.TryGetValue(path, out var handler)
                ? handler(context.Request)
                : new HttpResponseMessage(HttpStatusCode.NotFound);

            context.Response.StatusCode = (int)response.StatusCode;

            if (response.Content != null)
            {
                var data = await response.Content.ReadAsByteArrayAsync();
                context.Response.ContentLength64 = data.Length;
                await context.Response.OutputStream.WriteAsync(data, 0, data.Length);
            }

            context.Response.Close();
        }
        catch { }
    }

    private static int FindFreePort()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)socket.LocalEndPoint).Port;
        socket.Close();
        return port;
    }
}