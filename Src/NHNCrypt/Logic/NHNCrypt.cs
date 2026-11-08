using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Puniemu.Src.NHNCrypt.Logic
{
    public class NHNCrypt
    {
        private static readonly byte[] NHN_KEY = [0xa8, 0x65, 0xd7, 0xe5, 0xe2, 0x45, 0x8f, 0x8c, 0xe1, 0xb5, 0xec, 0xd0, 0x87, 0xe5, 0x45, 0x94];
        private static readonly byte[] DIGEST_SALT = Encoding.UTF8.GetBytes("0bk2kvtFE2");
        //Decrypts a request using NHN's cryption scheme.
        public static string? DecryptRequest(string content)
        {
            var input = content.Replace('-', '+').Replace('_', '/');
            var inputBytes = Convert.FromBase64String(input);
            var aes = Aes.Create();
            aes.Key = NHN_KEY;
            //Trim the SHA1 hash by skipping 20 bytes
            var decrypted = aes.DecryptEcb(inputBytes, PaddingMode.PKCS7).Skip(20).ToArray();
            return Encoding.UTF8.GetString(decrypted);
        }

        //Encrypts and compresses a response using NHN's cryption scheme.
        public static string EncryptResponse(string decryptedContent)
        {
            //We need to put a SHA1 digest of the compressed decrypted content before it
            var compressedJson = GzipCompress(Encoding.UTF8.GetBytes(decryptedContent));
            var digest = CalcDigest(compressedJson);
            using var encryptionInputMs = new MemoryStream();
            encryptionInputMs.Write(digest);
            encryptionInputMs.Write(compressedJson);
            var encryptionInput = encryptionInputMs.ToArray();
            //Encrypt
            var aes = Aes.Create();
            aes.Key = NHN_KEY;
            var encrypted = aes.EncryptEcb(encryptionInput.ToArray(), PaddingMode.PKCS7);
            return Convert.ToBase64String(encrypted).Replace('+', '-').Replace('/', '_');
        }
        // Client side (talking to the official server, Tools/GameDataFetcher): a request is
        // digest(json) + json, AES-ECB, URL-safe base64; an answer is AES-ECB of digest + gzip(json).
        public static string EncryptRequest(string json)
        {
            var body = Encoding.UTF8.GetBytes(json);
            var digest = CalcDigest(body);
            var aes = Aes.Create();
            aes.Key = NHN_KEY;
            var encrypted = aes.EncryptEcb(digest.Concat(body).ToArray(), PaddingMode.PKCS7);
            return Convert.ToBase64String(encrypted).Replace('+', '-').Replace('/', '_');
        }

        public static string DecryptResponse(string content)
        {
            var t = content.Trim().Replace('-', '+').Replace('_', '/');
            t += new string('=', (4 - t.Length % 4) % 4);
            var aes = Aes.Create();
            aes.Key = NHN_KEY;
            var plain = aes.DecryptEcb(Convert.FromBase64String(t), PaddingMode.PKCS7).Skip(20).ToArray();
            using var gz = new GZipStream(new MemoryStream(plain), CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return Encoding.UTF8.GetString(outMs.ToArray());
        }

        private static byte[] GzipCompress(byte[] input)
        {
            using (var result = new MemoryStream())
            {

                using (var compressionStream = new GZipStream(result,
                    CompressionMode.Compress))
                {
                    compressionStream.Write(input.ToArray(), 0, input.Length);
                    compressionStream.Flush();

                }
                return result.ToArray();
            }
        }
        // Calculates a double SHA1 hash with a specific salt
        private static byte[] CalcDigest(byte[] content)
        {
            byte space = (byte)' ';

            byte[] CombineWithSaltAndSpace(byte[] salt, byte[] data, bool addSpace)
            {
                byte[] combined;
                if (addSpace)
                {
                    combined = new byte[salt.Length + 1 + data.Length];
                    Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
                    combined[salt.Length] = space;
                    Buffer.BlockCopy(data, 0, combined, salt.Length + 1, data.Length);
                }
                else
                {
                    combined = new byte[salt.Length + data.Length];
                    Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
                    Buffer.BlockCopy(data, 0, combined, salt.Length, data.Length);
                }
                return combined;
            }

            byte[] saltedContent = CombineWithSaltAndSpace(DIGEST_SALT, content, true);
            byte[] firstHash = SHA1.HashData(saltedContent);

            byte[] saltedFirstHash = CombineWithSaltAndSpace(DIGEST_SALT, firstHash, false);
            return SHA1.HashData(saltedFirstHash);
        }


    }
}
