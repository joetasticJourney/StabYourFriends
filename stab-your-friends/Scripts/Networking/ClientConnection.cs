using System;
using Godot;
using StabYourFriends.Networking.Messages;

namespace StabYourFriends.Networking;

public class ClientConnection
{
    public string Id { get; }
    public WebSocketPeer Peer { get; }
    public bool IsConnected => Peer.GetReadyState() == WebSocketPeer.State.Open;
    public bool IsConnecting => Peer.GetReadyState() == WebSocketPeer.State.Connecting;
    public bool IsClosed => Peer.GetReadyState() == WebSocketPeer.State.Closed;

    public event Action<ClientConnection, IMessage>? MessageReceived;
    public event Action<ClientConnection>? Disconnected;
    public event Action<ClientConnection>? HandshakeCompleted;

    private bool _handshakeComplete;

    public ClientConnection(StreamPeerTcp tcpPeer)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Peer = new WebSocketPeer();

        // Set supported protocols if needed
        var err = Peer.AcceptStream(tcpPeer);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Failed to accept stream: {err}");
        }
    }

    public void Poll()
    {
        Peer.Poll();

        var state = Peer.GetReadyState();

        if (state == WebSocketPeer.State.Open)
        {
            if (!_handshakeComplete)
            {
                _handshakeComplete = true;
                HandshakeCompleted?.Invoke(this);
            }

            while (Peer.GetAvailablePacketCount() > 0)
            {
                var packet = Peer.GetPacket();
                var json = System.Text.Encoding.UTF8.GetString(packet);
                var message = MessageSerializer.Deserialize(json);
                if (message != null)
                {
                    MessageReceived?.Invoke(this, message);
                }
            }
        }
        else if (state == WebSocketPeer.State.Closed)
        {
            Disconnected?.Invoke(this);
        }
    }

    public void Send(IMessage message)
    {
        if (!IsConnected) return;

        var json = MessageSerializer.Serialize(message);
        Peer.SendText(json);
    }

    public void Close()
    {
        Peer.Close();
    }
}
