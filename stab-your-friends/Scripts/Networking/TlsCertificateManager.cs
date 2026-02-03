#nullable enable

using Godot;

namespace StabYourFriends.Networking;

/// <summary>
/// Manages TLS certificates for secure HTTPS and WSS connections.
/// Uses pre-generated certificates with SANs for Chrome compatibility.
/// </summary>
public static class TlsCertificateManager
{
	// Pre-generated self-signed certificate with SANs for localhost development
	// Valid for: DNS:localhost, IP:127.0.0.1
	// Subject: CN=localhost, O=StabYourFriends, C=US
	// Validity: 10 years (expires 2036)
	private const string PrivateKeyPem = @"-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQCoBQ9ZQZhRrZvg
LZQ55tb2TFAz69/5GLL482E6CWN+Y0fRIMoCQFHcvdNV82F9tpZZht4rlRcsjZ3q
mr0eqjDSzaqczugP7Amh/3RIjbD+xWUizAGJ5n9f1xBB2L5YBa9Oyk43yCH6sW1v
OJD+iGdjeid/9ot4KBy4lE2jv/i6oikMfPe52eStdzigy7u8zdiOmPr3fAswoyuO
Z0ms5ocZ7e2tpSL2qUMeDZvB+XJrg4oJ4Guh6holN3Kzv3vudO69/95nhtwXD7NT
deeCD00peLWN1f80UoGDtcxleX4Tz5FQYBdfFfkjbUd/3DcEjo9okUtG+qaRQnEQ
akc5WoZLAgMBAAECggEASySoLqT1eGIKeoIn7pVcwh5zKCVvr7nqIQjIDOqyAo5o
ShE+By+47N5ArAoDKfQE3dlBd4BgMt7LJ2c4+YPn/f9ZNHQcuTI5RKg421HzPJ4P
kzZl4cSiZryKWsiSqE8yjixQOwZYnAPKC5niztM2WfkBvWsICR73aV16M6fhl6XA
2vEQ9CyisLnrrXDIwZHwOsXV3mvxeV9IYOLyIc6WdA0qWYOJxYlU2JGZoD1RVTYA
a/La0DXTt7lemuI4GCQ2MhRDWwof/Zm2XrnyLJFRekNqCfmXyU1LqOa7UhT22xiE
xZllF444H7OXk+1pczh5vjESOA6qDoSajstTqBQUQQKBgQDVmkdpmGkgxpVOPpRF
nq1i/IHCrURaIOC0wYrJ16aeHIPtWJabLN8sO10SHSKvEw5GIHHOuBxQ1t4ytf8p
ZPeYXkxsoLJcnbxVEcC6KMhvvU3SAOz5miI7sdn/NYmlqce6N1RiQkGyE/A38qWH
Jn1a2DrcXE5p7Wq734MdiZvEuwKBgQDJXpfDgNtQr4UIRxoCBPLeTiNNbKq9XYXD
bsy3aTfxNkJ781JiU5abR5nTtiRToVebaWUNEaBVh3YACXucSSIYxw7GPiBoI3QR
wi9yyAOBmDcURnaWlSF389TP7DWSObfn4kxVrJm55hLFhkmX4LShs2g64sT0JzMT
Z1F0UR7zsQKBgQCsi6/cMO3bOA8r4YlMo91T9L2tUOHCHITn/t/qJHXHiylW9RTT
zO+Kf3mzRC3cVvxU0aidYEQfWTKsrC+udI09XA9IQJdUEWctIOYaew9OlBDk7zJ+
fm/g4M5ERi8mz3szhbZ4mSUQgOKvjyb/gawJUlpZ34bIMqzhjrAPkDVwOQKBgAff
PMuVw/0Zf3fdX7TogJ4UK+kg8GPKvinvsO0Sne8+EcdKsdPKNL8JQ4g1PKJQUl5u
9lZWdBAj5YcG1+A6M60ISxmQ4C9yA12WW8h+7TQpwS13u7cTSWWpEI64SzfWLcxQ
2m8W+kN8LQuvvjzDugwAOXjj2JM63RJLHbIQYcdhAoGAWtiCTp3IXv1PisN0Pn0f
+xoN4ZYty/9Z+AYh8j9N5RXwZDX8cLWbuW1Eq7o8pvDlyooapufow6HojNr1UnUO
h8tICjNQUcKDpdQ98YXVRjLIGYoQ0BJbQCN7J9PySJTvRV1hsQWmp6ZbYn1Z8lCM
rcTMfyfFE4hnu8Y6UD10LLc=
-----END PRIVATE KEY-----";

	private const string CertificatePem = @"-----BEGIN CERTIFICATE-----
MIIDczCCAlugAwIBAgIUJYvE2ZrMyuNkOjOj938JSW1f/vwwDQYJKoZIhvcNAQEL
BQAwOzESMBAGA1UEAwwJbG9jYWxob3N0MRgwFgYDVQQKDA9TdGFiWW91ckZyaWVu
ZHMxCzAJBgNVBAYTAlVTMB4XDTI2MDIwMzIwMzgzOVoXDTM2MDIwMTIwMzgzOVow
OzESMBAGA1UEAwwJbG9jYWxob3N0MRgwFgYDVQQKDA9TdGFiWW91ckZyaWVuZHMx
CzAJBgNVBAYTAlVTMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAqAUP
WUGYUa2b4C2UOebW9kxQM+vf+Riy+PNhOgljfmNH0SDKAkBR3L3TVfNhfbaWWYbe
K5UXLI2d6pq9Hqow0s2qnM7oD+wJof90SI2w/sVlIswBieZ/X9cQQdi+WAWvTspO
N8gh+rFtbziQ/ohnY3onf/aLeCgcuJRNo7/4uqIpDHz3udnkrXc4oMu7vM3Yjpj6
93wLMKMrjmdJrOaHGe3traUi9qlDHg2bwflya4OKCeBroeoaJTdys7977nTuvf/e
Z4bcFw+zU3Xngg9NKXi1jdX/NFKBg7XMZXl+E8+RUGAXXxX5I21Hf9w3BI6PaJFL
RvqmkUJxEGpHOVqGSwIDAQABo28wbTAdBgNVHQ4EFgQUyCHUJ1EM2dMps6vwKWMS
rbLJwbkwHwYDVR0jBBgwFoAUyCHUJ1EM2dMps6vwKWMSrbLJwbkwDwYDVR0TAQH/
BAUwAwEB/zAaBgNVHREEEzARgglsb2NhbGhvc3SHBH8AAAEwDQYJKoZIhvcNAQEL
BQADggEBAEGs9RcbGLDIK01CnNgYDq9lA+jjBSN9DS9G+Emt3EKR03JrObqVFeHm
egd8Ud11O6Al7SPtm7tOIA/enlFhQbUQ4/S2TWieJYG5zDrEfwaCo6o4cTeZnAuc
bUWP00b4H+fuhyc2lqNhoIJW/JJoeg8SmhfUSTX9q8wxXgtNIkXgawzN83qu1WB+
gBZyg+mFtnp9utXZfXp0vE6lyweDVM/42IZnkyyfIX8f7oxc8zJFika98fx8C8s2
a/uqxrjeQl+aqrqxuTCTrD/Cl2KzIpcWKeistlV14Nc4wdPqrsvDrOF9uYuRxHDa
aOruPk6I7o8tM6HsFAJWFP7QAYP3bPI=
-----END CERTIFICATE-----";

	private static CryptoKey? _key;
	private static X509Certificate? _cert;
	private static TlsOptions? _serverOptions;
	private static bool _initialized;

	/// <summary>
	/// Ensures the certificate is loaded from embedded PEM strings.
	/// Must be called before using GetServerTlsOptions().
	/// </summary>
	public static void EnsureCertificateExists()
	{
		if (_initialized) return;

		_key = new CryptoKey();
		var keyError = _key.LoadFromString(PrivateKeyPem);
		if (keyError != Error.Ok)
		{
			GD.PrintErr($"Failed to load embedded TLS key: {keyError}");
			return;
		}

		_cert = new X509Certificate();
		var certError = _cert.LoadFromString(CertificatePem);
		if (certError != Error.Ok)
		{
			GD.PrintErr($"Failed to load embedded TLS certificate: {certError}");
			return;
		}

		_serverOptions = TlsOptions.Server(_key, _cert);
		_initialized = true;

		GD.Print("TLS certificate loaded successfully");
	}

	/// <summary>
	/// Gets TLS options for server-side connections.
	/// Call EnsureCertificateExists() first.
	/// </summary>
	public static TlsOptions GetServerTlsOptions()
	{
		if (!_initialized || _serverOptions == null)
		{
			GD.PrintErr("TlsCertificateManager not initialized! Call EnsureCertificateExists() first.");
			EnsureCertificateExists();
		}

		return _serverOptions!;
	}
}
