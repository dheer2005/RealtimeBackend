using System.Security.Cryptography;
using System.Text;

namespace RealtimeChat.Services
{
    public static class EncryptionHelper
    {
        public static readonly string secretKey = "0123456789ABCDEF0123456789ABCDEF";

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(secretKey);
            aes.GenerateIV();

            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs)) 
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText) 
        {
            if(string.IsNullOrEmpty(cipherText)) return cipherText;

            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(secretKey);


            int ivLength = aes.BlockSize / 8;
            if (fullCipher.Length < ivLength)
                throw new ArgumentException("The cipher text is too short to contain an IV.");
            
            byte[] iv = new byte[ivLength];
            byte[] cipher = new byte[fullCipher.Length - iv.Length];

            //copying initialization vector into iv array
            Array.Copy(fullCipher, 0, iv, 0, ivLength);
            //copying encrypted bytes into ciper array
            Array.Copy(fullCipher, ivLength, cipher, 0, cipher.Length);

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
}
