#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace StabYourFriends.Networking;

/// <summary>
/// HTTPS server that serves web client files and detects WebSocket upgrade
/// requests, handing them off to WebSocketServer. Runs on a single port (8443)
/// so iOS Safari only needs to trust one self-signed certificate endpoint.
/// </summary>
public partial class HttpFileServer : Node
{
    [Export] public int Port { get; set; } = 8443;
    [Export] public string WebRootPath { get; set; } = "res://WebClient";

    private TcpServer _tcpServer = new();
    private readonly List<PendingConnection> _clients = new();

    /// <summary>
    /// Fired when a WebSocket upgrade request is detected and the 101 handshake
    /// has been completed. The WebSocketServer should subscribe to this to take
    /// ownership of the connection.
    /// </summary>
    public event Action<StreamPeerTcp, StreamPeerTls>? WebSocketUpgraded;

    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".html", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".ogg", "audio/ogg" },
    };

    private enum ConnState { TcpAccept, TlsHandshake, WaitRequest, Done, Upgraded, Error }

    private class PendingConnection
    {
        public StreamPeerTcp TcpPeer { get; }
        public StreamPeerTls? TlsPeer { get; set; }
        public ConnState State { get; set; } = ConnState.TcpAccept;

        public PendingConnection(StreamPeerTcp tcp) { TcpPeer = tcp; }
    }

    public override void _Ready()
    {
        StartServer();
    }

    public void StartServer()
    {
        var error = _tcpServer.Listen((ushort)Port, "*");
        if (error != Error.Ok)
        {
            GD.PrintErr($"HTTPS file server failed to start on port {Port}: {error}");
            return;
        }

        GD.Print($"HTTPS file server listening on port {Port}");
        GD.Print($"Open https://{WebSocketServer.GetLocalIpAddress()}:{Port}");
    }

    public override void _Process(double delta)
    {
        if (!_tcpServer.IsListening()) return;

        while (_tcpServer.IsConnectionAvailable())
        {
            var tcpPeer = _tcpServer.TakeConnection();
            if (tcpPeer != null)
            {
                _clients.Add(new PendingConnection(tcpPeer));
            }
        }

        for (int i = _clients.Count - 1; i >= 0; i--)
        {
            var c = _clients[i];
            c.TcpPeer.Poll();

            switch (c.State)
            {
                case ConnState.TcpAccept:
                    if (c.TcpPeer.GetStatus() == StreamPeerTcp.Status.Connected)
                    {
                        c.TlsPeer = new StreamPeerTls();
                        var err = c.TlsPeer.AcceptStream(c.TcpPeer, TlsCertificateManager.GetServerTlsOptions());
                        c.State = err == Error.Ok ? ConnState.TlsHandshake : ConnState.Error;
                    }
                    else if (c.TcpPeer.GetStatus() != StreamPeerTcp.Status.Connecting)
                        c.State = ConnState.Error;
                    break;

                case ConnState.TlsHandshake:
                    c.TlsPeer!.Poll();
                    var s = c.TlsPeer.GetStatus();
                    if (s == StreamPeerTls.Status.Connected)
                        c.State = ConnState.WaitRequest;
                    else if (s != StreamPeerTls.Status.Handshaking)
                        c.State = ConnState.Error;
                    break;

                case ConnState.WaitRequest:
                    c.TlsPeer!.Poll();
                    if (c.TlsPeer.GetStatus() != StreamPeerTls.Status.Connected)
                    {
                        c.State = ConnState.Error;
                        break;
                    }
                    if (c.TlsPeer.GetAvailableBytes() > 0)
                    {
                        var isUpgrade = HandleRequest(c);
                        c.State = isUpgrade ? ConnState.Upgraded : ConnState.Done;
                    }
                    break;

                case ConnState.Done:
                case ConnState.Error:
                    _clients.RemoveAt(i);
                    break;

                case ConnState.Upgraded:
                    // Connection handed off to WebSocketServer — remove without closing
                    _clients.RemoveAt(i);
                    break;
            }
        }
    }

    /// <summary>
    /// Returns true if this was a WebSocket upgrade (connection handed off),
    /// false for a normal HTTP request (connection can be closed).
    /// </summary>
    private bool HandleRequest(PendingConnection conn)
    {
        var tls = conn.TlsPeer!;
        var data = tls.GetUtf8String(tls.GetAvailableBytes());
        var lines = data.Split('\n');

        if (lines.Length == 0) return false;

        var requestLine = lines[0].Trim();
        var parts = requestLine.Split(' ');

        if (parts.Length < 2) return false;

        var method = parts[0];
        var path = parts[1];

        // Parse headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) break;
            var colonIdx = line.IndexOf(':');
            if (colonIdx > 0)
            {
                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..].Trim();
                headers[key] = value;
            }
        }

        // Check for WebSocket upgrade
        if (method == "GET" &&
            headers.TryGetValue("Upgrade", out var upgrade) &&
            upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase) &&
            headers.TryGetValue("Sec-WebSocket-Key", out var wsKey))
        {
            CompleteWebSocketUpgrade(conn, wsKey);
            return true;
        }

        // Normal HTTP request
        if (method != "GET")
        {
            SendResponse(tls, 405, "Method Not Allowed", "text/plain", "Method Not Allowed");
            return false;
        }

        if (path == "/") path = "/index.html";

        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0) path = path[..queryIndex];

        // Security: prevent directory traversal
        path = path.Replace("..", "");

        ServeFile(tls, path);
        return false;
    }

    private void CompleteWebSocketUpgrade(PendingConnection conn, string wsKey)
    {
        // Compute Sec-WebSocket-Accept per RFC 6455
        var magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        var combined = wsKey + magic;
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(combined));
        var accept = Convert.ToBase64String(hash);

        var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                       "Upgrade: websocket\r\n" +
                       "Connection: Upgrade\r\n" +
                       $"Sec-WebSocket-Accept: {accept}\r\n" +
                       "\r\n";

        conn.TlsPeer!.PutData(Encoding.UTF8.GetBytes(response));

        GD.Print("WebSocket upgrade handshake completed, handing off connection...");
        WebSocketUpgraded?.Invoke(conn.TcpPeer, conn.TlsPeer);
    }

    private void ServeFile(StreamPeerTls client, string path)
    {
        var fullPath = WebRootPath + path;

        if (!Godot.FileAccess.FileExists(fullPath))
        {
            SendResponse(client, 404, "Not Found", "text/plain", "File not found");
            return;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var mimeType = MimeTypes.GetValueOrDefault(extension, "application/octet-stream");

        byte[] content;

        if (mimeType.StartsWith("text/") || mimeType == "application/javascript" || mimeType == "application/json")
        {
            var file = Godot.FileAccess.Open(fullPath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                SendResponse(client, 500, "Internal Server Error", "text/plain", "Failed to read file");
                return;
            }
            var text = file.GetAsText();
            file.Close();
            content = Encoding.UTF8.GetBytes(text);
        }
        else
        {
            var file = Godot.FileAccess.Open(fullPath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                SendResponse(client, 500, "Internal Server Error", "text/plain", "Failed to read file");
                return;
            }
            content = file.GetBuffer((long)file.GetLength());
            file.Close();
        }

        SendResponse(client, 200, "OK", mimeType, content);
    }

    private void SendResponse(StreamPeerTls client, int statusCode, string statusText, string contentType, string body)
    {
        SendResponse(client, statusCode, statusText, contentType, Encoding.UTF8.GetBytes(body));
    }

    private void SendResponse(StreamPeerTls client, int statusCode, string statusText, string contentType, byte[] body)
    {
        var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                     $"Content-Type: {contentType}\r\n" +
                     $"Content-Length: {body.Length}\r\n" +
                     "Connection: close\r\n" +
                     "Access-Control-Allow-Origin: *\r\n" +
                     "\r\n";

        var headerBytes = Encoding.UTF8.GetBytes(header);
        var responseBytes = new byte[headerBytes.Length + body.Length];
        headerBytes.CopyTo(responseBytes, 0);
        body.CopyTo(responseBytes, headerBytes.Length);

        client.PutData(responseBytes);
        client.DisconnectFromStream();
    }

    public override void _ExitTree()
    {
        _tcpServer.Stop();
    }
}
