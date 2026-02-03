#nullable enable

using Godot;

namespace StabYourFriends.Networking;

/// <summary>
/// Manages TLS certificates for secure HTTPS and WSS connections.
/// Generates self-signed certificates on first run and persists them.
/// </summary>
public static class TlsCertificateManager
{
    private const string KeyPath = "user://server.key";
    private const string CertPath = "user://server.crt";

    private static CryptoKey? _key;
    private static X509Certificate? _cert;
    private static TlsOptions? _serverOptions;
    private static bool _initialized;

    /// <summary>
    /// Ensures a certificate exists, loading from disk or generating a new one.
    /// Must be called before using GetServerTlsOptions().
    /// </summary>
    public static void EnsureCertificateExists()
    {
        if (_initialized) return;

        if (FileAccess.FileExists(KeyPath) && FileAccess.FileExists(CertPath))
        {
            LoadExistingCertificate();
        }
        else
        {
            GenerateNewCertificate();
        }

        _serverOptions = TlsOptions.Server(_key, _cert);
        _initialized = true;

        GD.Print("TLS certificate ready");
    }

    private static void LoadExistingCertificate()
    {
        GD.Print("Loading existing TLS certificate...");

        _key = new CryptoKey();
        var keyError = _key.Load(KeyPath);
        if (keyError != Error.Ok)
        {
            GD.PrintErr($"Failed to load key: {keyError}, regenerating...");
            GenerateNewCertificate();
            return;
        }

        _cert = new X509Certificate();
        var certError = _cert.Load(CertPath);
        if (certError != Error.Ok)
        {
            GD.PrintErr($"Failed to load certificate: {certError}, regenerating...");
            GenerateNewCertificate();
            return;
        }

        GD.Print("TLS certificate loaded from disk");
    }

    private static void GenerateNewCertificate()
    {
        GD.Print("Generating new self-signed TLS certificate...");

        var crypto = new Crypto();
        _key = crypto.GenerateRsa(2048);
        _cert = crypto.GenerateSelfSignedCertificate(
            _key,
            "CN=localhost,O=StabYourFriends,C=US"
        );

        var keyError = _key.Save(KeyPath);
        if (keyError != Error.Ok)
        {
            GD.PrintErr($"Failed to save key: {keyError}");
        }

        var certError = _cert.Save(CertPath);
        if (certError != Error.Ok)
        {
            GD.PrintErr($"Failed to save certificate: {certError}");
        }

        GD.Print("TLS certificate generated and saved");
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
