using System.Security.Cryptography;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Générateur de clés robustes pour protéger les PDF (sans caractères ambigus).
    /// </summary>
    public static class PayslipKeyService
    {
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        public static string NewKey(int length = 12)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            var chars = new char[length];
            for (int i = 0; i < length; i++) chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            return new string(chars);
        }
    }
}
