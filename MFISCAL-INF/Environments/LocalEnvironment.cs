using MFISCAL_INF.Models;
using MFISCAL_INF.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MFISCAL_INF.Environments
{
    /*
     * WINDOWS (Windows Credential Manager):
     * -----------------------------
     * # Store credentials from an elevated or normal PowerShell prompt:
     * # Production (required)
     * cmdkey /generic:MFISCAL_PG_BASEDB_USER /user:YourPostgresUser /pass:ignore
     * cmdkey /generic:MFISCAL_PG_BASEDB_PASSWORD /user:ignore /pass:YourPostgresPassword
     * cmdkey /generic:MFISCAL_SIGNINGCERT_PASSWORD /user:ignore /pass:YourFiscalSigningCertPassword
     * cmdkey /generic:MFISCAL_CLIENTCERT_PASSWORD /user:ignore /pass:YourFiscalClientCertPassword
     *
     * # Development-only credentials (use only in Development environment)
     * # Prefix with DEV_ to avoid accidental use in production:
     * cmdkey /generic:DEV_MFISCAL_PG_BASEDB_USER /user:DevPostgresUser /pass:ignore
     * cmdkey /generic:DEV_MFISCAL_PG_BASEDB_PASSWORD /user:ignore /pass:DevPostgresPassword
     * cmdkey /generic:DEV_MFISCAL_SIGNINGCERT_PASSWORD /user:ignore /pass:DevSigningCertPassword
     * cmdkey /generic:DEV_MFISCAL_CLIENTCERT_PASSWORD /user:ignore /pass:DevClientCertPassword
     *
     * On Windows you can inspect stored credentials at:
     * Control Panel -> User Accounts -> Manage your credentials -> Windows Credentials
     *
     * LINUX (pass - Password Store):
     * ------------------------------
     * sudo apt update && sudo apt install -y pass gnupg2
     * # generate a GPG key: follow prompts
     * gpg --full-generate-key
     * # initialize pass with your GPG identity (example: "you@example.com")
     * pass init "you@example.com"
     *
     * # Production (required)
     * pass insert MFISCAL_PG_BASEDB_USER       # first non-empty line = username
     * pass insert MFISCAL_PG_BASEDB_PASSWORD
     * pass insert MFISCAL_SIGNINGCERT_PASSWORD
     * pass insert MFISCAL_CLIENTCERT_PASSWORD
     *
     * # Development-only credentials (use only in Development environment)
     * pass insert DEV_MFISCAL_PG_BASEDB_USER
     * pass insert DEV_MFISCAL_PG_BASEDB_PASSWORD
     * pass insert DEV_MFISCAL_SIGNINGCERT_PASSWORD
     * pass insert DEV_MFISCAL_CLIENTCERT_PASSWORD
     *
     * # To read a secret:
     * pass show MFISCAL_PG_BASEDB_USER
     * pass show DEV_MFISCAL_PG_BASEDB_USER
     */

    public class LocalEnvironment : ILocalEnvironment
    {
        public LocalEnvironmentValues Values { get; }
        private readonly Dictionary<string, string> _values;
        private static readonly string EnvFolder = Path.GetDirectoryName(typeof(LocalEnvironment).Assembly.Location)!;
        private static readonly string EnvFileName = IsDevelopment() ? ".env.development" : ".env";
        private static readonly string EnvFilePath = Path.Combine(EnvFolder, EnvFileName);
        public static LocalEnvironment Instance { get; } = new LocalEnvironment();

        public LocalEnvironment()
        {
            _values = LoadEnvFile(EnvFilePath);
            Values = new LocalEnvironmentValues
            {
                JwtIssuerSigningKey = GetRequiredValue("jwt_issuer_signing_key"),
                JwtIssuerName = GetRequiredValue("jwt_issuer_name"),
                JwtIssuerAudience = GetRequiredValue("jwt_issuer_audience"),
                AdminUsername = GetRequiredValue("admin_username"),
                AdminPassword = GetRequiredValue("admin_password"),
                PostgresBaseDbUser = GetSecureUsername(ResolveCredentialKey("MFISCAL_PG_BASEDB_USER")),
                PostgresBaseDbPassword = GetSecureSecret(ResolveCredentialKey("MFISCAL_PG_BASEDB_PASSWORD")),
                PostgresBaseDbHost = GetRequiredValue("postgres_basedb_host"),
                PostgresBaseDbPort = ParseRequiredInt("postgres_basedb_port"),
                PostgresBaseDbDbName = GetRequiredValue("postgres_basedb_dbname"),
                PostgresBaseDbSslMode = GetRequiredValue("postgres_basedb_ssl_mode"),
                FiscalSigningCertPath = GetRequiredValue("fiscal_signingcert_path"),
                FiscalSigningCertPassword = GetSecureSecret(ResolveCredentialKey("MFISCAL_SIGNINGCERT_PASSWORD")),
                FiscalSigningCertThumbprint = GetFiscalSigningCertThumbprint(),
                FiscalClientCertPath = GetRequiredValue("fiscal_clientcert_path"),
                FiscalClientCertPassword = GetSecureSecret(ResolveCredentialKey("MFISCAL_CLIENTCERT_PASSWORD")),
                FiscalEduEndpoint = GetRequiredValue("fiscal_eduendpoint"),
                FiscalAuditFolder = GetRequiredValue("fiscal_auditfolder")
            };
        }

        private string? GetFiscalSigningCertThumbprint()
        {
            string signingCertPath = GetRequiredValue("fiscal_signingcert_path");
            string? signingCertPassword = GetSecureSecret(ResolveCredentialKey("MFISCAL_SIGNINGCERT_PASSWORD"));
            if (string.IsNullOrWhiteSpace(signingCertPath) || !File.Exists(signingCertPath))
                return null;
            var ext = Path.GetExtension(signingCertPath).ToLowerInvariant();
            if (ext == ".p12" || ext == ".pfx")
            {
                return EnvironmentUtils.ComputeSha1ThumbprintFromPfx(signingCertPath, signingCertPassword);
            }
            if (ext == ".cer" || ext == ".pem" || ext == ".crt")
            {
                return EnvironmentUtils.ComputeSha1ThumbprintFromPemOrDer(signingCertPath);
            }
            return null;
        }

        private static string ResolveCredentialKey(string baseKey)
        {
            if (IsDevelopment())
            {
                if (baseKey.StartsWith("DEV_", StringComparison.OrdinalIgnoreCase))
                    return baseKey;
                return "DEV_" + baseKey;
            }
            return baseKey;
        }

        private string GetSecureUsername(string credentialKey)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var u = WindowsCredentialsUtils.ReadCredentialUsername(credentialKey);
                    if (!string.IsNullOrWhiteSpace(u)) return u;
                    throw new InvalidOperationException($"Windows credential '{credentialKey}' exists but contains empty username. Ensure UserName is set for the credential.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to read Windows credential username '{credentialKey}': {ex.Message}", ex);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var output = LinuxCredentialsUtils.RunPassShow(credentialKey);
                if (string.IsNullOrWhiteSpace(output))
                    throw new InvalidOperationException($"pass returned empty output for key '{credentialKey}'. Ensure the secret exists and contains the username on the first non-empty line.");

                var firstLine = EnvironmentUtils.GetFirstNonEmptyLine(output);
                if (string.IsNullOrWhiteSpace(firstLine))
                    throw new InvalidOperationException($"pass returned only empty lines for key '{credentialKey}'. Ensure the secret contains a non-empty username.");
                return firstLine;
            }

            throw new PlatformNotSupportedException("Secure username retrieval is only supported on Windows and Linux.");
        }

        private string GetSecureSecret(string credentialKey)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var p = WindowsCredentialsUtils.ReadCredential(credentialKey);
                    if (!string.IsNullOrWhiteSpace(p)) return p;
                    throw new InvalidOperationException($"Windows credential '{credentialKey}' exists but returned an empty secret.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to read Windows credential '{credentialKey}': {ex.Message}", ex);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var output = LinuxCredentialsUtils.RunPassShow(credentialKey);
                if (string.IsNullOrWhiteSpace(output))
                    throw new InvalidOperationException($"pass returned empty output for key '{credentialKey}'. Ensure the secret exists and contains the secret on the first non-empty line.");

                var firstLine = EnvironmentUtils.GetFirstNonEmptyLine(output);
                if (string.IsNullOrWhiteSpace(firstLine))
                    throw new InvalidOperationException($"pass returned only empty lines for key '{credentialKey}'. Ensure the secret contains a non-empty secret.");
                return firstLine;
            }

            throw new PlatformNotSupportedException("Secure secret retrieval is only supported on Windows and Linux.");
        }

        private string GetRequiredValue(string key)
        {
            if (_values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
            throw new InvalidOperationException($"Required environment variable '{key}' is missing or empty in {EnvFileName}");
        }

        private int ParseRequiredInt(string key)
        {
            var value = GetRequiredValue(key);
            if (int.TryParse(value, out var result))
                return result;
            throw new InvalidOperationException($"Environment variable '{key}' could not be parsed as int: '{value}'");
        }

        public static LocalEnvironment GetInstance() => Instance;

        public static bool IsDevelopment()
        {
            var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return string.Equals(aspnetEnv, "Development", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> LoadEnvFile(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return dict;
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;
                var idx = trimmed.IndexOf('=');
                if (idx <= 0) continue;
                var key = trimmed.Substring(0, idx).Trim();
                var value = trimmed.Substring(idx + 1).Trim();
                dict[key] = value;
            }
            return dict;
        }

        public byte[] GetSigningKeyBytes()
        {
            string key = Values.JwtIssuerSigningKey;
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException($"'jwt_issuer_signing_key' is missing or empty in {EnvFileName}");
            return Encoding.UTF8.GetBytes(key);
        }
    }
}