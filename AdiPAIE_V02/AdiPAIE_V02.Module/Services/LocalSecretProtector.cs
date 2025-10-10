using System.Security.Cryptography;
using System.Text;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Coffre local basé sur DPAPI (CurrentUser) pour chiffrer/déchiffrer la clé PDF.
    /// </summary>
    public static class LocalSecretProtector
    {
        public static string Protect(string plain)
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return System.Convert.ToBase64String(enc);
        }

        public static string Unprotect(string encBase64)
        {
            var enc = System.Convert.FromBase64String(encBase64);
            var bytes = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
