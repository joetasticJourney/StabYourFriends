#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using StabYourFriends.Networking.Messages;

namespace StabYourFriends.Networking;

/// <summary>
/// Manages a single WebSocket client connection using manual frame encoding/decoding.
/// Expects the HTTP 101 upgrade to have already been completed before construction.
/// </summary>
public class ClientConnection
{
	public string Id { get; }

	private readonly StreamPeerTcp _tcpPeer;
	private readonly StreamPeerTls _tlsPeer;
	private readonly List<byte> _readBuffer = new();
	private bool _open;
	private bool _disconnectedFired;

	public bool IsConnected => _open;
	public bool IsClosed => !_open;

	public event Action<ClientConnection, IMessage>? MessageReceived;
	public event Action<ClientConnection>? Disconnected;

	public ClientConnection(StreamPeerTcp tcpPeer, StreamPeerTls tlsPeer)
	{
		Id = Guid.NewGuid().ToString("N")[..8];
		_tcpPeer = tcpPeer;
		_tlsPeer = tlsPeer;
		_open = true;
	}

	public void Poll()
	{
		if (!_open)
		{
			FireDisconnected();
			return;
		}

		_tcpPeer.Poll();
		_tlsPeer.Poll();

		// Check connection status
		if (_tcpPeer.GetStatus() != StreamPeerTcp.Status.Connected ||
			_tlsPeer.GetStatus() != StreamPeerTls.Status.Connected)
		{
			_open = false;
			FireDisconnected();
			return;
		}

		// Buffer all available data from the TLS stream
		int available = _tlsPeer.GetAvailableBytes();
		for (int i = 0; i < available; i++)
		{
			_readBuffer.Add((byte)_tlsPeer.GetU8());
		}

		// Parse and handle complete WebSocket frames
		while (TryParseFrame(out var opcode, out var payload, out var consumed))
		{
			_readBuffer.RemoveRange(0, consumed);
			HandleFrame(opcode, payload);
			if (!_open) break;
		}
	}

	private bool TryParseFrame(out byte opcode, out byte[] payload, out int consumed)
	{
		opcode = 0;
		payload = Array.Empty<byte>();
		consumed = 0;

		if (_readBuffer.Count < 2) return false;

		int pos = 0;
		opcode = (byte)(_readBuffer[0] & 0x0F);
		bool masked = (_readBuffer[1] & 0x80) != 0;
		long length = _readBuffer[1] & 0x7F;
		pos = 2;

		if (length == 126)
		{
			if (_readBuffer.Count < 4) return false;
			length = (_readBuffer[2] << 8) | _readBuffer[3];
			pos = 4;
		}
		else if (length == 127)
		{
			if (_readBuffer.Count < 10) return false;
			length = 0;
			for (int i = 0; i < 8; i++)
				length = (length << 8) | _readBuffer[2 + i];
			pos = 10;
		}

		// Reject absurdly large frames
		if (length > 1_000_000)
		{
			GD.PrintErr($"WebSocket frame too large ({length} bytes), closing connection");
			_open = false;
			return false;
		}

		int maskSize = masked ? 4 : 0;
		int totalNeeded = pos + maskSize + (int)length;
		if (_readBuffer.Count < totalNeeded) return false;

		byte[]? mask = null;
		if (masked)
		{
			mask = new byte[] { _readBuffer[pos], _readBuffer[pos + 1], _readBuffer[pos + 2], _readBuffer[pos + 3] };
			pos += 4;
		}

		payload = new byte[(int)length];
		for (int i = 0; i < (int)length; i++)
		{
			payload[i] = _readBuffer[pos + i];
			if (masked) payload[i] ^= mask![i % 4];
		}

		consumed = totalNeeded;
		return true;
	}

	private void HandleFrame(byte opcode, byte[] payload)
	{
		switch (opcode)
		{
			case 0x1: // Text frame
				var json = Encoding.UTF8.GetString(payload);
				var message = MessageSerializer.Deserialize(json);
				if (message != null)
				{
					MessageReceived?.Invoke(this, message);
				}
				break;

			case 0x8: // Close frame
				SendFrame(0x8, payload.Length >= 2 ? payload[..2] : Array.Empty<byte>());
				_open = false;
				FireDisconnected();
				break;

			case 0x9: // Ping
				SendFrame(0xA, payload); // Pong
				break;

			case 0xA: // Pong — ignore
				break;
		}
	}

	public void Send(IMessage message)
	{
		if (!_open) return;

		var json = MessageSerializer.Serialize(message);
		var payload = Encoding.UTF8.GetBytes(json);
		SendFrame(0x1, payload);
	}

	private void SendFrame(byte opcode, byte[] payload)
	{
		try
		{
			// Server-to-client frames are NOT masked
			byte fin = (byte)(0x80 | opcode);
			byte[] frame;

			if (payload.Length < 126)
			{
				frame = new byte[2 + payload.Length];
				frame[0] = fin;
				frame[1] = (byte)payload.Length;
				payload.CopyTo(frame, 2);
			}
			else if (payload.Length < 65536)
			{
				frame = new byte[4 + payload.Length];
				frame[0] = fin;
				frame[1] = 126;
				frame[2] = (byte)(payload.Length >> 8);
				frame[3] = (byte)(payload.Length & 0xFF);
				payload.CopyTo(frame, 4);
			}
			else
			{
				frame = new byte[10 + payload.Length];
				frame[0] = fin;
				frame[1] = 127;
				var len = (long)payload.Length;
				for (int i = 0; i < 8; i++)
					frame[2 + i] = (byte)(len >> (56 - i * 8));
				payload.CopyTo(frame, 10);
			}

			_tlsPeer.PutData(frame);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to send WebSocket frame: {e.Message}");
			_open = false;
		}
	}

	public void Close()
	{
		if (!_open) return;
		SendFrame(0x8, Array.Empty<byte>());
		_open = false;
	}

	private void FireDisconnected()
	{
		if (_disconnectedFired) return;
		_disconnectedFired = true;
		Disconnected?.Invoke(this);
	}
}
