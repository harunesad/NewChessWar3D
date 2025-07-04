using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class EncryptionUtility
{
    private static readonly string encryptionKey = "aBcD1234EfGh5678IjKl9012MnOp3456"; // 32 karakter uzunluðunda

    public static string EncryptString(string plainText)
    {
        byte[] key = Encoding.UTF8.GetBytes(encryptionKey);
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.GenerateIV();
            byte[] iv = aes.IV;

            using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(iv, 0, iv.Length);
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (StreamWriter sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    public static string DecryptString(string cipherText)
    {
        byte[] fullCipher = Convert.FromBase64String(cipherText);
        byte[] iv = new byte[16];
        byte[] cipher = new byte[fullCipher.Length - iv.Length];

        Array.Copy(fullCipher, 0, iv, 0, iv.Length);
        Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        byte[] key = Encoding.UTF8.GetBytes(encryptionKey);
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            using (MemoryStream ms = new MemoryStream(cipher))
            using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (StreamReader sr = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }
    }
}
