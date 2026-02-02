using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace StabYourFriends.Networking;

public partial class HttpFileServer : Node
{
    [Export] public int Port { get; set; } = 8080;
    [Export] public string WebRootPath { get; set; } = "res://WebClient";

    private TcpServer _tcpServer = new();
    private readonly List<StreamPeerTcp> _clients = new();

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
    };

    public override void _Ready()
    {
        StartServer();
    }

    public void StartServer()
    {
        var error = _tcpServer.Listen((ushort)Port, "*");
        if (error != Error.Ok)
        {
            GD.PrintErr($"HTTP server failed to start on port {Port}: {error}");
            return;
        }

        GD.Print($"HTTP server listening on port {Port}");
        GD.Print($"Open http://localhost:{Port} or http://{WebSocketServer.GetLocalIpAddress()}:{Port}");
    }

    public override void _Process(double delta)
    {
        if (!_tcpServer.IsListening()) return;

        // Accept new connections
        while (_tcpServer.IsConnectionAvailable())
        {
            var client = _tcpServer.TakeConnection();
            if (client != null)
            {
                _clients.Add(client);
            }
        }

        // Process clients
        for (int i = _clients.Count - 1; i >= 0; i--)
        {
            var client = _clients[i];
            client.Poll();

            var status = client.GetStatus();
            if (status == StreamPeerTcp.Status.Connected)
            {
                if (client.GetAvailableBytes() > 0)
                {
                    HandleRequest(client);
                    _clients.RemoveAt(i);
                }
            }
            else if (status == StreamPeerTcp.Status.Error || status == StreamPeerTcp.Status.None)
            {
                _clients.RemoveAt(i);
            }
        }
    }

    private void HandleRequest(StreamPeerTcp client)
    {
        var data = client.GetUtf8String(client.GetAvailableBytes());
        var lines = data.Split('\n');

        if (lines.Length == 0) return;

        var requestLine = lines[0].Trim();
        var parts = requestLine.Split(' ');

        if (parts.Length < 2) return;

        var method = parts[0];
        var path = parts[1];

        if (method != "GET")
        {
            SendResponse(client, 405, "Method Not Allowed", "text/plain", "Method Not Allowed");
            return;
        }

        // Default to index.html
        if (path == "/") path = "/index.html";

        // Remove query string
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0) path = path[..queryIndex];

        // Security: prevent directory traversal
        path = path.Replace("..", "");

        ServeFile(client, path);
    }

    private void ServeFile(StreamPeerTcp client, string path)
    {
        var fullPath = WebRootPath + path;

        // Try to load the file using Godot's resource system
        if (!Godot.FileAccess.FileExists(fullPath))
        {
            SendResponse(client, 404, "Not Found", "text/plain", "File not found");
            return;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var mimeType = MimeTypes.GetValueOrDefault(extension, "application/octet-stream");

        byte[] content;

        // For text files, read as text
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
            // Binary files
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

    private void SendResponse(StreamPeerTcp client, int statusCode, string statusText, string contentType, string body)
    {
        SendResponse(client, statusCode, statusText, contentType, Encoding.UTF8.GetBytes(body));
    }

    private void SendResponse(StreamPeerTcp client, int statusCode, string statusText, string contentType, byte[] body)
    {
        var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                     $"Content-Type: {contentType}\r\n" +
                     $"Content-Length: {body.Length}\r\n" +
                     "Connection: close\r\n" +
                     "Access-Control-Allow-Origin: *\r\n" +
                     "\r\n";

        var headerBytes = Encoding.UTF8.GetBytes(header);
        var response = new byte[headerBytes.Length + body.Length];
        headerBytes.CopyTo(response, 0);
        body.CopyTo(response, headerBytes.Length);

        client.PutData(response);
        client.DisconnectFromHost();
    }

    public override void _ExitTree()
    {
        _tcpServer.Stop();
    }
}
