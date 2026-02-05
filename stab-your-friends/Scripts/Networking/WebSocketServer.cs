#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Godot;
using StabYourFriends.Networking.Messages;

namespace StabYourFriends.Networking;

/// <summary>
/// Manages WebSocket client connections. Does not listen on its own port —
/// receives pre-upgraded connections from HttpFileServer via AcceptUpgradedConnection().
/// </summary>
public partial class WebSocketServer : Node
{
	private readonly List<ClientConnection> _clients = new();

	public event Action<ClientConnection>? ClientConnected;
	public event Action<ClientConnection>? ClientDisconnected;
	public event Action<ClientConnection, IMessage>? MessageReceived;

	public IReadOnlyList<ClientConnection> Clients => _clients;

	public override void _Process(double delta)
	{
		foreach (var client in _clients.ToList())
		{
			client.Poll();
		}
	}

	/// <summary>
	/// Accept a WebSocket connection whose HTTP 101 upgrade has already been completed.
	/// Called by HttpFileServer when it detects a WebSocket upgrade request.
	/// </summary>
	public void AcceptUpgradedConnection(StreamPeerTcp tcpPeer, StreamPeerTls tlsPeer)
	{
		GD.Print("WebSocket upgrade accepted, creating client connection...");
		var client = new ClientConnection(tcpPeer, tlsPeer);
		client.MessageReceived += OnClientMessage;
		client.Disconnected += OnClientDisconnected;
		_clients.Add(client);
		GD.Print($"Client connected: {client.Id}");
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
		client.MessageReceived -= OnClientMessage;
		client.Disconnected -= OnClientDisconnected;
		ClientDisconnected?.Invoke(client);
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

	public void StopServer()
	{
		foreach (var client in _clients.ToList())
		{
			client.Close();
		}
		_clients.Clear();
	}

	public override void _ExitTree()
	{
		StopServer();
	}
}
