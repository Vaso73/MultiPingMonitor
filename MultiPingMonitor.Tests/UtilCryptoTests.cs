using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MultiPingMonitor.Tests
{
    /// <summary>
    /// Tests for the AES encrypt/decrypt logic in Util.cs.
    ///
    /// The primary goal is to verify that replacing the obsolete
    ///   Rfc2898DeriveBytes(string, byte[])
    /// with the explicit
    ///   Rfc2898DeriveBytes(string, byte[], 1000, HashAlgorithmName.SHA1)
    /// produces a bit-for-bit identical derived key, preserving backward
    /// compatibility for any ciphertext that was persisted before the fix.
    /// </summary>
    public class UtilCryptoTests
    {
        // Fixed inputs to make tests deterministic (not machine/user specific).
        private const string TestPassword = "https://github.com/R-Smith/MultiPingMonitorTEST_MACHINE";
        private static readonly byte[] TestSalt = Encoding.ASCII.GetBytes("testuser@@vmping-salt@@");

        // ---------------------------------------------------------------------------
        // Backward-compatibility: old implicit constructor == new explicit constructor
        // ---------------------------------------------------------------------------

        [Fact]
        public void Rfc2898_ExplicitSha1_1000iter_ProducesSameKeyAsObsoleteConstructor()
        {
            // Arrange
            int keyLengthBytes = 32; // AES-256

            // Old (obsolete) constructor – kept here solely as the reference "expected" value.
            // The implicit defaults are SHA1 and 1000 iterations.
#pragma warning disable SYSLIB0041
            byte[] expectedKey;
            using (var oldKey = new Rfc2898DeriveBytes(TestPassword, TestSalt))
            {
                expectedKey = oldKey.GetBytes(keyLengthBytes);
            }
#pragma warning restore SYSLIB0041

            // New (non-obsolete) constructor with the same algorithm and iteration count.
            byte[] actualKey;
            using (var newKey = new Rfc2898DeriveBytes(TestPassword, TestSalt, 1000, HashAlgorithmName.SHA1))
            {
                actualKey = newKey.GetBytes(keyLengthBytes);
            }

            // Assert – must be identical so existing ciphertexts remain decryptable.
            Assert.Equal(expectedKey, actualKey);
        }

        // ---------------------------------------------------------------------------
        // Round-trip: encrypt then decrypt returns original plaintext
        // ---------------------------------------------------------------------------

        [Theory]
        [InlineData("hello world")]
        [InlineData("p@ssw0rd!#$%")]
        [InlineData("a")]
        [InlineData("longer test string with unicode: ÁáÉéÍíÓóÚú")]
        public void EncryptDecrypt_RoundTrip_ReturnsOriginalPlaintext(string plainText)
        {
            string cipherText = EncryptStringAES(plainText);
            string decrypted = DecryptStringAES(cipherText);
            Assert.Equal(plainText, decrypted);
        }

        [Fact]
        public void EncryptStringAES_NullOrEmpty_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => EncryptStringAES(string.Empty));
        }

        [Fact]
        public void DecryptStringAES_NullOrWhitespace_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DecryptStringAES("   "));
        }

        [Fact]
        public void DecryptStringAES_TamperedCiphertext_ThrowsException()
        {
            string cipherText = EncryptStringAES("original");
            // Corrupt the ciphertext (flip last base64 char)
            char[] chars = cipherText.ToCharArray();
            chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
            string tampered = new string(chars);

            Assert.ThrowsAny<Exception>(() => DecryptStringAES(tampered));
        }

        // ---------------------------------------------------------------------------
        // Inline implementations matching Util.cs exactly (using the fixed constructor)
        // ---------------------------------------------------------------------------

        private static string EncryptStringAES(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));

            using var key = new Rfc2898DeriveBytes(TestPassword, TestSalt, 1000, HashAlgorithmName.SHA1);
            using var aes = Aes.Create();
            aes.Key = key.GetBytes(aes.KeySize / 8);
            aes.GenerateIV();
            aes.Padding = PaddingMode.PKCS7;
            aes.Mode = CipherMode.CBC;

            using var memoryStream = new MemoryStream();
            memoryStream.Write(BitConverter.GetBytes(aes.IV.Length), 0, sizeof(int));
            memoryStream.Write(aes.IV, 0, aes.IV.Length);

            using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream, Encoding.UTF8))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(memoryStream.ToArray());
        }

        private static string DecryptStringAES(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                throw new ArgumentNullException(nameof(cipherText));

            try
            {
                var bytes = Convert.FromBase64String(cipherText);

                using var key = new Rfc2898DeriveBytes(TestPassword, TestSalt, 1000, HashAlgorithmName.SHA1);
                using var memoryStream = new MemoryStream(bytes);
                using var aes = Aes.Create();
                aes.Key = key.GetBytes(aes.KeySize / 8);
                aes.IV = ReadByteArray(memoryStream);
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;

                using var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var reader = new StreamReader(cryptoStream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw new Exception("Error decrypting value: " + ex.Message);
            }
        }

        private static byte[] ReadByteArray(Stream stream)
        {
            byte[] rawLength = new byte[sizeof(int)];
            if (stream.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
                throw new SystemException("Stream did not contain properly formatted byte array");

            byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                throw new SystemException("Did not read byte array properly");

            return buffer;
        }
    }
}
