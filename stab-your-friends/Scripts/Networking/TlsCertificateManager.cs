#nullable enable

using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Godot;
using GodotX509 = Godot.X509Certificate;

namespace StabYourFriends.Networking;

/// <summary>
/// Manages TLS certificates for secure HTTPS and WSS connections.
/// Dynamically generates a self-signed certificate with the server's LAN IP in SANs
/// so that iOS Safari (and other strict clients) can connect.
/// Certificates are persisted to disk and reused across launches.
/// </summary>
public static class TlsCertificateManager
{
	private static CryptoKey? _key;
	private static GodotX509? _cert;
	private static TlsOptions? _serverOptions;
	private static bool _initialized;
	private static string _generatedForIp = "";

	private const string CertPath = "user://tls_cert.pem";
	private const string KeyPath = "user://tls_key.pem";
	private const string IpPath = "user://tls_ip.txt";

	/// <summary>
	/// Forces regeneration of the TLS certificate on next EnsureCertificateExists call.
	/// </summary>
	public static void ForceRegenerate()
	{
		GD.Print("Forcing TLS certificate regeneration...");
		_initialized = false;
		_generatedForIp = "";
		_key = null;
		_cert = null;
		_serverOptions = null;

		// Delete persisted files
		if (FileAccess.FileExists(CertPath))
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(CertPath));
		if (FileAccess.FileExists(KeyPath))
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(KeyPath));
		if (FileAccess.FileExists(IpPath))
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(IpPath));

		GD.Print("TLS certificate files deleted. New certificate will be generated on next use.");
	}

	/// <summary>
	/// Generates (or reuses) a self-signed certificate whose SANs include
	/// localhost, 127.0.0.1, and the provided LAN IP address.
	/// </summary>
	public static void EnsureCertificateExists(string lanIp)
	{
		if (_initialized && _generatedForIp == lanIp) return;

		// Try to load from disk if IP matches
		if (TryLoadFromDisk(lanIp))
		{
			GD.Print($"Loaded TLS certificate from disk for LAN IP: {lanIp}");
			return;
		}

		GD.Print($"Generating TLS certificate for LAN IP: {lanIp}");

		try
		{
			using var rsa = RSA.Create(2048);

			var subject = new X500DistinguishedName("CN=StabYourFriends, O=StabYourFriends, C=US");
			var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			// Key usage
			request.CertificateExtensions.Add(
				new X509KeyUsageExtension(
					X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
					critical: false));

			// Subject Alternative Names
			var sanBuilder = new SubjectAlternativeNameBuilder();
			sanBuilder.AddDnsName("localhost");
			sanBuilder.AddIpAddress(IPAddress.Parse("127.0.0.1"));
			if (lanIp != "127.0.0.1" && lanIp != "localhost")
			{
				sanBuilder.AddIpAddress(IPAddress.Parse(lanIp));
			}
			request.CertificateExtensions.Add(sanBuilder.Build());

			// Self-sign, valid for 398 days (iOS rejects certs with validity > 398 days)
			var notBefore = DateTimeOffset.UtcNow;
			var notAfter = notBefore.AddDays(398);
			using var cert = request.CreateSelfSigned(notBefore, notAfter);

			// Export private key as PKCS#8 PEM
			var keyPem = ExportPrivateKeyPem(rsa);

			// Export certificate as PEM
			var certPem = ExportCertificatePem(cert);

			// Load into Godot types
			_key = new CryptoKey();
			var keyError = _key.LoadFromString(keyPem);
			if (keyError != Error.Ok)
			{
				GD.PrintErr($"Failed to load generated TLS key: {keyError}");
				return;
			}

			_cert = new GodotX509();
			var certError = _cert.LoadFromString(certPem);
			if (certError != Error.Ok)
			{
				GD.PrintErr($"Failed to load generated TLS certificate: {certError}");
				return;
			}

			_serverOptions = TlsOptions.Server(_key, _cert);
			_initialized = true;
			_generatedForIp = lanIp;

			// Save to disk for reuse across launches
			SaveToDisk(keyPem, certPem, lanIp);

			GD.Print($"TLS certificate generated successfully (SANs: localhost, 127.0.0.1, {lanIp})");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to generate TLS certificate: {ex.Message}");
		}
	}

	/// <summary>
	/// Gets TLS options for server-side connections.
	/// Call EnsureCertificateExists(lanIp) first.
	/// </summary>
	public static TlsOptions GetServerTlsOptions()
	{
		if (!_initialized || _serverOptions == null)
		{
			GD.PrintErr("TlsCertificateManager not initialized! Call EnsureCertificateExists(lanIp) first.");
			EnsureCertificateExists("127.0.0.1");
		}

		return _serverOptions!;
	}

	private static bool TryLoadFromDisk(string lanIp)
	{
		if (!FileAccess.FileExists(CertPath) || !FileAccess.FileExists(KeyPath) || !FileAccess.FileExists(IpPath))
			return false;

		try
		{
			// Check if the stored IP matches
			using var ipFile = FileAccess.Open(IpPath, FileAccess.ModeFlags.Read);
			if (ipFile == null) return false;
			var storedIp = ipFile.GetAsText().Trim();
			if (storedIp != lanIp)
			{
				GD.Print($"Stored TLS certificate is for different IP ({storedIp}), regenerating for {lanIp}");
				return false;
			}

			// Load the certificate
			using var certFile = FileAccess.Open(CertPath, FileAccess.ModeFlags.Read);
			if (certFile == null) return false;
			var certPem = certFile.GetAsText();

			// Load the key
			using var keyFile = FileAccess.Open(KeyPath, FileAccess.ModeFlags.Read);
			if (keyFile == null) return false;
			var keyPem = keyFile.GetAsText();

			// Load into Godot types
			_key = new CryptoKey();
			var keyError = _key.LoadFromString(keyPem);
			if (keyError != Error.Ok)
			{
				GD.PrintErr($"Failed to load TLS key from disk: {keyError}");
				return false;
			}

			_cert = new GodotX509();
			var certError = _cert.LoadFromString(certPem);
			if (certError != Error.Ok)
			{
				GD.PrintErr($"Failed to load TLS certificate from disk: {certError}");
				return false;
			}

			_serverOptions = TlsOptions.Server(_key, _cert);
			_initialized = true;
			_generatedForIp = lanIp;

			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to load TLS certificate from disk: {ex.Message}");
			return false;
		}
	}

	private static void SaveToDisk(string keyPem, string certPem, string lanIp)
	{
		try
		{
			using var certFile = FileAccess.Open(CertPath, FileAccess.ModeFlags.Write);
			certFile?.StoreString(certPem);

			using var keyFile = FileAccess.Open(KeyPath, FileAccess.ModeFlags.Write);
			keyFile?.StoreString(keyPem);

			using var ipFile = FileAccess.Open(IpPath, FileAccess.ModeFlags.Write);
			ipFile?.StoreString(lanIp);

			GD.Print($"TLS certificate saved to disk at {ProjectSettings.GlobalizePath(CertPath)}");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to save TLS certificate to disk: {ex.Message}");
		}
	}

	private static string ExportPrivateKeyPem(RSA rsa)
	{
		var pkcs8Bytes = rsa.ExportPkcs8PrivateKey();
		var base64 = Convert.ToBase64String(pkcs8Bytes, Base64FormattingOptions.InsertLineBreaks);
		return $"-----BEGIN PRIVATE KEY-----\n{base64}\n-----END PRIVATE KEY-----";
	}

	private static string ExportCertificatePem(X509Certificate2 cert)
	{
		var derBytes = cert.RawData;
		var base64 = Convert.ToBase64String(derBytes, Base64FormattingOptions.InsertLineBreaks);
		return $"-----BEGIN CERTIFICATE-----\n{base64}\n-----END CERTIFICATE-----";
	}
}
