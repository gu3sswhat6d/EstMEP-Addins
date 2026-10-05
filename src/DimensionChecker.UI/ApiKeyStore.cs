using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EstMepAddins.DimensionChecker.UI
{
    /// <summary>
    /// Finds the Anthropic API key: the ANTHROPIC_API_KEY environment variable first, otherwise a key
    /// the user saved from the dialog, encrypted with Windows DPAPI for their account only.
    /// </summary>
    public static class ApiKeyStore
    {
        private static readonly string KeyFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EstMepAddins", "anthropic.key");

        public static string? Load()
        {
            var fromEnv = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv.Trim();

            if (!File.Exists(KeyFile))
                return null;

            try
            {
                var bytes = ProtectedData.Unprotect(File.ReadAllBytes(KeyFile), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        public static void Save(string apiKey)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(KeyFile)!);
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey.Trim()), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFile, bytes);
        }
    }
}
