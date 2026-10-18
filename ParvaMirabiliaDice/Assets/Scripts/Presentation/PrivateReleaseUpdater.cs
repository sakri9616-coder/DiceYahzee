using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    // The credential belongs to the Windows user. It is never included in the build, a save,
    // a command line, or a diagnostic log. Only GitHub API requests receive it.
    internal static class PrivateReleaseUpdater
    {
        internal const string Owner = "sakri9616-coder";
        internal const string Repository = "DiceYahzee";
        internal const string ArchiveName = "DiceYahzee-win64.zip";
        internal const string CredentialTarget = "DiceYahzee.GitHub.ReleaseRead";

        [Serializable] internal sealed class Release
        {
            public string tag_name;
            public Asset[] assets;
        }

        [Serializable] internal sealed class Asset
        {
            public string name;
            public string url;
            public string digest;
            public long size;
        }

        internal static bool TryVersion(string tag, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(tag)) return false;
            return Version.TryParse(tag[0] == 'v' || tag[0] == 'V' ? tag.Substring(1) : tag, out version);
        }

        internal static bool TrySha256(string digest, out string hex)
        {
            hex = null;
            if (string.IsNullOrEmpty(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return false;
            string value = digest.Substring(7);
            if (value.Length != 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            hex = value.ToLowerInvariant();
            return true;
        }

        internal static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal static bool IsReleaseAssetUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttps && uri.Host == "api.github.com" &&
                uri.AbsolutePath.StartsWith("/repos/" + Owner + "/" + Repository + "/releases/assets/", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsGitHubDownloadRedirect(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;
            return uri.Host == "release-assets.githubusercontent.com" ||
                uri.Host == "objects.githubusercontent.com";
        }

        internal static string ReadCredential()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!CredReadW(CredentialTarget, 1, 0, out IntPtr pointer)) return null;
            try
            {
                var credential = Marshal.PtrToStructure<Credential>(pointer);
                if (credential.CredentialBlobSize == 0 || credential.CredentialBlobSize > 4096) return null;
                var bytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                try { return Encoding.UTF8.GetString(bytes); }
                finally { Array.Clear(bytes, 0, bytes.Length); }
            }
            finally { CredFree(pointer); }
#else
            return null;
#endif
        }

        internal static bool SaveCredential(string value)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) return false;
            byte[] bytes = Encoding.UTF8.GetBytes(value.Trim());
            IntPtr blob = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                var credential = new Credential
                {
                    Type = 1, TargetName = CredentialTarget, CredentialBlobSize = (uint)bytes.Length,
                    CredentialBlob = blob, Persist = 2, UserName = Environment.UserName
                };
                return CredWriteW(ref credential, 0);
            }
            finally
            {
                for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0);
                Marshal.FreeHGlobal(blob);
                Array.Clear(bytes, 0, bytes.Length);
            }
#else
            return false;
#endif
        }

        internal static bool DeleteCredential()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return CredDeleteW(CredentialTarget, 1, 0);
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public uint Flags;
            public uint Type;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
            [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredReadW(string target, uint type, uint reserved, out IntPtr credential);

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWriteW(ref Credential credential, uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredDeleteW(string target, uint type, uint flags);

        [DllImport("Advapi32.dll")]
        private static extern void CredFree(IntPtr credential);
#endif
    }
}
