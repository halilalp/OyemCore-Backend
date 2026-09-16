using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OyemCore.DataLayer.Helpers
{
    // referans: webportal DataLayer/ClsEncryption.cs — BİREBİR aynı algoritma, key ve IV.
    // Mobil aynı YBS DB'sini web ile paylaştığı için, web'in yazdığı mesajları çözebilmek ve
    // web'in mobil mesajlarını çözebilmesi için anahtarlar birebir aynı olmak ZORUNDA.
    // AES-256-CBC, "AES:" öntakılı Base64. Öntakı yoksa geriye dönük (eski düz metin) korunur.
    public static class ClsEncryption
    {
        // 32-byte (256-bit) anahtar.
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("IsikTarimWebPortalSecKey2026AES!"); // 32 karakter
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("PortalAesIV2026!"); // 16 karakter

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Key;
                    aesAlg.IV = IV;

                    ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                            {
                                swEncrypt.Write(plainText);
                            }
                            return "AES:" + Convert.ToBase64String(msEncrypt.ToArray());
                        }
                    }
                }
            }
            catch
            {
                return plainText;
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            // Geriye dönük eski şifresiz mesajlar için koruma
            if (!cipherText.StartsWith("AES:"))
                return cipherText;

            try
            {
                string rawBase64 = cipherText.Substring(4);
                byte[] cipherBytes = Convert.FromBase64String(rawBase64);

                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Key;
                    aesAlg.IV = IV;

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                return cipherText;
            }
        }
    }
}
