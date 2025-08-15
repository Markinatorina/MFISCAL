using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;

namespace MFISCAL_INF.Utils
{
    public static class EnvironmentUtils
    {
        public static string ComputeSha1ThumbprintFromPfx(string path, string? password = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException($"Certificate file not found: {path}");

            var flags = X509KeyStorageFlags.EphemeralKeySet;
            using var cert = password is null
                ? new X509Certificate2(path, (string?)null, flags)
                : new X509Certificate2(path, password, flags);

            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(cert.RawData);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }

        public static string ComputeSha1ThumbprintFromPemOrDer(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            var raw = File.ReadAllBytes(path);
            X509Certificate2 cert;

            var text = Encoding.ASCII.GetString(raw);
            if (text.StartsWith("-----BEGIN"))
            {
                var pem = text;
                var b64 = pem
                    .Replace("-----BEGIN CERTIFICATE-----", "")
                    .Replace("-----END CERTIFICATE-----", "")
                    .Replace("\r", "")
                    .Replace("\n", "");
                var der = Convert.FromBase64String(b64);
                cert = new X509Certificate2(der);
            }
            else
            {
                cert = new X509Certificate2(raw);
            }

            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(cert.RawData);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }

        public static string GetFirstNonEmptyLine(string s)
        {
            foreach (var line in s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = line.Trim();
                if (!string.IsNullOrEmpty(t)) return t;
            }
            return string.Empty;
        }
    }
}
