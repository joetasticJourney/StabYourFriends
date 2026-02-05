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
/// </summary>
public static class TlsCertificateManager
{
	private static CryptoKey? _key;
	private static GodotX509? _cert;
	private static TlsOptions? _serverOptions;
	private static bool _initialized;
	private static string _generatedForIp = "";

	/// <summary>
	/// Generates (or reuses) a self-signed certificate whose SANs include
	/// localhost, 127.0.0.1, and the provided LAN IP address.
	/// </summary>
	public static void EnsureCertificateExists(string lanIp)
	{
		if (_initialized && _generatedForIp == lanIp) return;

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
