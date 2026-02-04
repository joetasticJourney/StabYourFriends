#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Godot;
using StabYourFriends.Networking.Messages;

namespace StabYourFriends.Networking;

public partial class WebSocketServer : Node
{
    [Export] public int Port { get; set; } = 9443;

    private TcpServer _tcpServer = new();
    private readonly List<ClientConnection> _clients = new();
    private readonly List<ClientConnection> _pendingClients = new();
    private readonly List<PendingTlsConnection> _pendingTls = new();

    public event Action<ClientConnection>? ClientConnected;
    public event Action<ClientConnection>? ClientDisconnected;
    public event Action<ClientConnection, IMessage>? MessageReceived;

    public IReadOnlyList<ClientConnection> Clients => _clients;

    private class PendingTlsConnection
    {
        public StreamPeerTcp TcpPeer { get; }
        public StreamPeerTls? TlsPeer { get; set; }
        public TlsState State { get; set; } = TlsState.Accepting;

        public PendingTlsConnection(StreamPeerTcp tcpPeer)
        {
            TcpPeer = tcpPeer;
        }
    }

    private enum TlsState
    {
        Accepting,
        Handshaking,
        Ready,
        Error
    }

    public override void _Ready()
    {
        GD.Print("=== WebSocketServer _Ready ===");
        TlsCertificateManager.EnsureCertificateExists();
        StartServer();
    }

    public void StartServer()
    {
        GD.Print($"=== Starting WSS server on port {Port} ===");
        var error = _tcpServer.Listen((ushort)Port, "*");
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to start server on port {Port}: {error}");
            return;
        }

        GD.Print($"WebSocket Secure (WSS) server listening on port {Port}");
        GD.Print($"Local IP: {GetLocalIpAddress()}");
        GD.Print("=== Server started successfully ===");
    }

    public void StopServer()
    {
        foreach (var client in _clients.ToList())
        {
            client.Close();
        }
        foreach (var client in _pendingClients.ToList())
        {
            client.Close();
        }
        _clients.Clear();
        _pendingClients.Clear();
        _pendingTls.Clear();
        _tcpServer.Stop();
        GD.Print("WebSocket server stopped");
    }

    public override void _Process(double delta)
    {
        if (!_tcpServer.IsListening()) return;

        // Accept new TCP connections
        while (_tcpServer.IsConnectionAvailable())
        {
            var tcpPeer = _tcpServer.TakeConnection();
            if (tcpPeer != null)
            {
                GD.Print("New TCP connection, starting TLS handshake...");
                _pendingTls.Add(new PendingTlsConnection(tcpPeer));
            }
        }

        // Process TLS handshakes
        for (int i = _pendingTls.Count - 1; i >= 0; i--)
        {
            var pending = _pendingTls[i];
            ProcessTlsHandshake(pending, i);
        }

        // Poll pending clients (waiting for WebSocket handshake)
        for (int i = _pendingClients.Count - 1; i >= 0; i--)
        {
            var client = _pendingClients[i];
            client.Poll();

            // The Poll/disconnect callback may have already removed this client
            if (i >= _pendingClients.Count)
                continue;

            // Remove if closed during handshake
            if (client.IsClosed && _pendingClients[i] == client)
            {
                GD.Print($"Client {client.Id} closed during handshake");
                CleanupClient(client);
                _pendingClients.RemoveAt(i);
            }
        }

        // Poll all connected clients
        foreach (var client in _clients.ToList())
        {
            client.Poll();
        }
    }

    private void ProcessTlsHandshake(PendingTlsConnection pending, int index)
    {
        pending.TcpPeer.Poll();

        switch (pending.State)
        {
            case TlsState.Accepting:
                var tcpStatus = pending.TcpPeer.GetStatus();
                if (tcpStatus == StreamPeerTcp.Status.Connected)
                {
                    // Start TLS handshake
                    pending.TlsPeer = new StreamPeerTls();
                    var tlsOptions = TlsCertificateManager.GetServerTlsOptions();
                    var err = pending.TlsPeer.AcceptStream(pending.TcpPeer, tlsOptions);
                    if (err != Error.Ok)
                    {
                        GD.PrintErr($"TLS accept failed: {err}");
                        pending.State = TlsState.Error;
                    }
                    else
                    {
                        pending.State = TlsState.Handshaking;
                    }
                }
                else if (tcpStatus == StreamPeerTcp.Status.Error || tcpStatus == StreamPeerTcp.Status.None)
                {
                    pending.State = TlsState.Error;
                }
                break;

            case TlsState.Handshaking:
                var tlsStatus = pending.TlsPeer!.GetStatus();
                // Only poll if still handshaking or connected
                if (tlsStatus == StreamPeerTls.Status.Handshaking || tlsStatus == StreamPeerTls.Status.Connected)
                {
                    pending.TlsPeer.Poll();
                    tlsStatus = pending.TlsPeer.GetStatus();
                }

                if (tlsStatus == StreamPeerTls.Status.Connected)
                {
                    pending.State = TlsState.Ready;
                }
                else if (tlsStatus == StreamPeerTls.Status.Error || tlsStatus == StreamPeerTls.Status.ErrorHostnameMismatch)
                {
                    GD.PrintErr("TLS handshake failed");
                    pending.State = TlsState.Error;
                }
                break;

            case TlsState.Ready:
                // TLS is ready, create WebSocket client connection
                GD.Print("TLS handshake complete, starting WebSocket handshake...");
                var client = new ClientConnection(pending.TlsPeer!);
                client.HandshakeCompleted += OnHandshakeCompleted;
                client.MessageReceived += OnClientMessage;
                client.Disconnected += OnClientDisconnected;
                _pendingClients.Add(client);
                _pendingTls.RemoveAt(index);
                return;

            case TlsState.Error:
                _pendingTls.RemoveAt(index);
                return;
        }
    }

    private void OnHandshakeCompleted(ClientConnection client)
    {
        GD.Print($"WebSocket handshake completed: {client.Id}");
        _pendingClients.Remove(client);
        _clients.Add(client);
        ClientConnected?.Invoke(client);
    }

    private void OnClientMessage(ClientConnection client, IMessage message)
    {
        MessageReceived?.Invoke(client, message);
    }

    private void OnClientDisconnected(ClientConnection client)
    {
        GD.Print($"Client disconnected: {client.Id}");
        _clients.Remove(client);
        _pendingClients.Remove(client);
        CleanupClient(client);
        ClientDisconnected?.Invoke(client);
    }

    private void CleanupClient(ClientConnection client)
    {
        client.HandshakeCompleted -= OnHandshakeCompleted;
        client.MessageReceived -= OnClientMessage;
        client.Disconnected -= OnClientDisconnected;
    }

    public void Broadcast(IMessage message)
    {
        foreach (var client in _clients)
        {
            client.Send(message);
        }
    }

    public void SendTo(string clientId, IMessage message)
    {
        var client = _clients.FirstOrDefault(c => c.Id == clientId);
        client?.Send(message);
    }

    public static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public override void _ExitTree()
    {
        StopServer();
    }
}
