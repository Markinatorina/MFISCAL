using MFISCAL_INF.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MFISCAL_INF.Environments
{
    /* 
     * Get Fiscal Signing Certificates here:
     * https://www.fina.hr/poslovni-digitalni-certifikati/poslovni-certifikati-za-fiskalizaciju
     * https://www.fina.hr/poslovni-digitalni-certifikati/poslovni-certifikati-za-fiskalizaciju/izdavanje-demo-aplikacijskog-certifikata-za-fiskalizaciju
     * 
     * WINDOWS (Windows Credential Manager):
     * -----------------------------
     * # Store credentials from an elevated or normal PowerShell prompt:
     * cmdkey /add:MFISCAL_PG_BASEDB_PASSWORD /user:ignore /pass:YourPostgresPassword
     * cmdkey /add:MFISCAL_SIGNINGCERT_PASSWORD /user:ignore /pass:YourFiscalSigningCertPassword
     * cmdkey /add:MFISCAL_CLIENTCERT_PASSWORD /user:ignore /pass:YourFiscalClientCertPassword
     * 
     * On Windows you can read the secrets by going to: 
     * Control Panel -> User Accounts -> Manage your credentials (left side) -> Windows Credentials
     * 
     * LINUX (pass - Password Store):
     * ------------------------------
     * sudo apt update && sudo apt install -y pass gnupg2
     * # generate a GPG key: follow prompts
     * gpg --full-generate-key
     * # initialize pass with your GPG identity (example: "you@example.com")
     * pass init "you@example.com"
     * # insert secrets:
     * pass insert MFISCAL_PG_BASEDB_PASSWORD
     * pass insert MFISCAL_SIGNINGCERT_PASSWORD
     * pass insert MFISCAL_CLIENTCERT_PASSWORD
     * 
     * On Linux call `pass show <name>` to read the secret.
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
                PostgresBaseDbUser = GetRequiredValue("postgres_basedb_user"),
                PostgresBaseDbPassword = GetSecurePassword("MFISCAL_PG_BASEDB_PASSWORD"),
                PostgresBaseDbHost = GetRequiredValue("postgres_basedb_host"),
                PostgresBaseDbPort = ParseRequiredInt("postgres_basedb_port"),
                PostgresBaseDbDbName = GetRequiredValue("postgres_basedb_dbname"),
                PostgresBaseDbSslMode = GetRequiredValue("postgres_basedb_ssl_mode"),
                FiscalSigningCertPath = GetRequiredValue("fiscal_signingcert_path"),
                FiscalSigningCertPassword = GetSecurePassword("MFISCAL_SIGNINGCERT_PASSWORD"),
                FiscalSigningCertThumbprint = GetRequiredValue("fiscal_signingcert_thumbprint"),
                FiscalClientCertPath = GetRequiredValue("fiscal_clientcert_path"),
                FiscalClientCertPassword = GetSecurePassword("MFISCAL_CLIENTCERT_PASSWORD"),
                FiscalEduEndpoint = GetRequiredValue("fiscal_eduendpoint"),
                FiscalAuditFolder = GetRequiredValue("fiscal_auditfolder")
            };
        }

        private string GetRequiredValue(string key)
        {
            if (_values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
            throw new InvalidOperationException($"Required environment variable '{key}' is missing or empty in {EnvFileName}");
        }

        private string GetSecurePassword(string credentialKey)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return WindowsCredentialManager.ReadCredential(credentialKey);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to read Windows credential '{credentialKey}': {ex.Message}", ex);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pass",
                    Arguments = $"show {credentialKey}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start 'pass' process. Is 'pass' installed and initialized?");
                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();

                var outReader = process.StandardOutput;
                var errReader = process.StandardError;

                var readTask = outReader.ReadToEndAsync();
                var errTask = errReader.ReadToEndAsync();

                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(); } catch { }
                    throw new InvalidOperationException($"Timeout when reading secret '{credentialKey}' from pass.");
                }

                var output = readTask.Result.Trim();
                var err = errTask.Result.Trim();

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"pass returned exit code {process.ExitCode} for key '{credentialKey}'. stderr: {err}");

                if (string.IsNullOrEmpty(output))
                    throw new InvalidOperationException($"pass returned empty output for key '{credentialKey}'.");

                var firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                return firstLine;
            }

            throw new PlatformNotSupportedException("Secure password retrieval is only supported on Windows and Linux.");
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

    internal static class WindowsCredentialManager
    {
        private const string AdvApi = "advapi32.dll";

        [DllImport(AdvApi, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport(AdvApi, SetLastError = true)]
        private static extern void CredFree(IntPtr buffer);

        private enum CredentialType : int
        {
            Generic = 1,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        public static string ReadCredential(string target)
        {
            if (string.IsNullOrEmpty(target)) throw new ArgumentNullException(nameof(target));

            if (!CredRead(target, CredentialType.Generic, 0, out var credPtr))
            {
                var err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"CredRead failed for '{target}' (Win32 error {err}). Make sure the credential exists (cmdkey /list).");
            }

            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);

                if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
                    return string.Empty;

                var blobSize = (int)cred.CredentialBlobSize;
                var blob = new byte[blobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, blobSize);

                try
                {
                    return Encoding.Unicode.GetString(blob).TrimEnd('\0');
                }
                catch
                {
                    return Encoding.UTF8.GetString(blob).TrimEnd('\0');
                }
            }
            finally
            {
                CredFree(credPtr);
            }
        }
    }
}
