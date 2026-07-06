using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OyemCore.BusinessLayer.Common
{
    public static class SecurityHelper
    {
        /// <summary>
        /// Legacy MD5 password encryption matching ASP.NET 4.5 DataLayer
        /// </summary>
        public static string EncryptPassword(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            using (var md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static string KarakterDuzen(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("?", "I").Replace("?", "??").Replace("??", "??").Replace("?", "i").Replace("?", "s").Replace("?", "g").Replace("\'fe", "s");
        }

        public static string OzelKarakterDuzen(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("<", " ").Replace(">", " ").Replace("'", " ");
        }

        public static string SafeString(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return Regex.Replace(str, "[\"'<>?]", "");
        }

        public static string EncryptString(string plainText, string keyString)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] keyBytes = new byte[32]; // 256-bit key
            byte[] providedKeyBytes = Encoding.UTF8.GetBytes(keyString);
            Array.Copy(providedKeyBytes, keyBytes, Math.Min(providedKeyBytes.Length, keyBytes.Length));

            using (var aesAlgorithm = Aes.Create())
            {
                aesAlgorithm.Key = keyBytes;
                aesAlgorithm.GenerateIV();
                var encryptor = aesAlgorithm.CreateEncryptor(aesAlgorithm.Key, aesAlgorithm.IV);

                using (var msEncrypt = new System.IO.MemoryStream())
                {
                    msEncrypt.Write(aesAlgorithm.IV, 0, aesAlgorithm.IV.Length); // Prepend IV
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new System.IO.StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        public static string DecryptString(string cipherText, string keyString)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                byte[] fullCipher = Convert.FromBase64String(cipherText);
                byte[] keyBytes = new byte[32];
                byte[] providedKeyBytes = Encoding.UTF8.GetBytes(keyString);
                Array.Copy(providedKeyBytes, keyBytes, Math.Min(providedKeyBytes.Length, keyBytes.Length));

                using (var aesAlgorithm = Aes.Create())
                {
                    byte[] iv = new byte[aesAlgorithm.BlockSize / 8];
                    byte[] cipher = new byte[fullCipher.Length - iv.Length];

                    Array.Copy(fullCipher, iv, iv.Length);
                    Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

                    aesAlgorithm.Key = keyBytes;
                    aesAlgorithm.IV = iv;

                    var decryptor = aesAlgorithm.CreateDecryptor(aesAlgorithm.Key, aesAlgorithm.IV);

                    using (var msDecrypt = new System.IO.MemoryStream(cipher))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback if not encrypted or wrong format
                Console.WriteLine($"[DECRYPT ERROR] Failed to decrypt. Exception: {ex.Message}");
                return cipherText;
            }
        }
    }
}
