using System;
using System.IO;
using System.Security.Cryptography;

namespace ParvaMirabilia.Presentation
{
    internal static class PrivateReleaseUpdater
    {
        internal const string Owner = "sakri9616-coder";
        internal const string Repository = "DiceYahzee";
        internal const string ArchiveName = "DiceYahzee-win64.zip";

        [Serializable] internal sealed class Release
        {
            public string tag_name;
            public Asset[] assets;
        }

        [Serializable] internal sealed class Asset
        {
            public string name;
            public string browser_download_url;
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

        internal static bool IsReleaseAssetUrl(string url, string tag)
        {
            if (string.IsNullOrEmpty(tag) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            string expected = "/" + Owner + "/" + Repository + "/releases/download/" + tag + "/" + ArchiveName;
            return uri.Scheme == Uri.UriSchemeHttps && uri.Host == "github.com" &&
                uri.AbsolutePath == expected && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        }

        internal static bool IsGitHubDownloadRedirect(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;
            return uri.Host == "release-assets.githubusercontent.com" ||
                uri.Host == "objects.githubusercontent.com";
        }
    }
}
