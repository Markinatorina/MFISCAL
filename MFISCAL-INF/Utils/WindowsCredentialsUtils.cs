using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MFISCAL_INF.Utils
{
    public static class WindowsCredentialsUtils
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

        public static string ReadCredentialUsername(string target)
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

                if (cred.UserName == IntPtr.Zero)
                    return string.Empty;

                var username = Marshal.PtrToStringUni(cred.UserName);
                return username ?? string.Empty;
            }
            finally
            {
                CredFree(credPtr);
            }
        }
    }
}
