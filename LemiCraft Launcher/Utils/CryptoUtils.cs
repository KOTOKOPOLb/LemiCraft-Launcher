using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LemiCraft_Launcher.Utils
{
    public static class CryptoUtils
    {
        public static void SaveEncryptedStringToFile(string path, string plain)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var bytes = Encoding.UTF8.GetBytes(plain);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }

        public static string? LoadEncryptedStringFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var encrypted = File.ReadAllBytes(path);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }
    }
}
