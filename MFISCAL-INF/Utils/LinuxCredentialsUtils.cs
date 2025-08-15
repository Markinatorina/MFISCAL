using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MFISCAL_INF.Utils
{
    public static class LinuxCredentialsUtils
    {
        public static string RunPassShow(string credentialKey)
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

            return output;
        }
    }
}
