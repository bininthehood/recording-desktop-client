using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RecordClient.Services.Login
{
    public static class AESService
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890123456"); // 16 bytes key
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456"); // 16 bytes IV

        public static string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var ms = new MemoryStream();
            using var encryptor = aes.CreateEncryptor();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using var sw = new StreamWriter(cs);

            sw.Write(plainText);
            sw.Flush();
            cs.FlushFinalBlock();

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
    }
}
