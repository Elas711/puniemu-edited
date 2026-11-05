using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Puniemu.Src.Server.Web
{
    // Newer data downloads pack the Yo-kai pictures in .ayd files (bl_9002531.ayd): a zip holding one
    // AES-CBC encrypted file (16-byte IV first, the same key as the game's .ez files) that is itself a
    // zip of PNGs (bl_9002531.png, bl_9002531_o.png). This reads the PNGs out for the in-game wiki.
    // The files are treated as untrusted data: only the requested entry is read, with size limits.
    public static class PackedAssets
    {
        private static readonly byte[] EZ_KEY = { 0x2A, 0xB5, 0x11, 0xF4, 0x77, 0x97, 0x7D, 0x25, 0xCF, 0x6F, 0x7A, 0x8A, 0xE0, 0x49, 0xA1, 0x25 };
        private const long MAX_PACKED = 16 * 1024 * 1024;
        private const long MAX_PNG = 8 * 1024 * 1024;
        private const int CACHE_ENTRIES = 512;

        private static readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);

        // "bl_9002531.png" -> "bl_9002531.ayd" (the container has the same name as the picture)
        public static string ContainerName(string pngName) => Path.GetFileNameWithoutExtension(pngName) + ".ayd";

        public static byte[]? Extract(string aydPath, string pngName)
        {
            if (_cache.TryGetValue(pngName, out var hit)) return hit;
            try
            {
                if (new FileInfo(aydPath).Length > MAX_PACKED) return null;
                using var outer = ZipFile.OpenRead(aydPath);
                var enc = outer.Entries.FirstOrDefault();
                if (enc == null || enc.Length > MAX_PACKED || enc.Length < 32) return null;
                byte[] data;
                using (var s = enc.Open())
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    data = ms.ToArray();
                }
                if ((data.Length - 16) % 16 != 0) return null;
                byte[] plain;
                using (var aes = Aes.Create())
                {
                    aes.Key = EZ_KEY;
                    plain = aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16), PaddingMode.None);
                }
                // PKCS#7 padding, if present
                int pad = plain[^1];
                int len = plain.Length;
                if (pad is > 0 and <= 16 && plain.AsSpan(plain.Length - pad).ToArray().All(b => b == pad)) len -= pad;
                using var inner = new ZipArchive(new MemoryStream(plain, 0, len), ZipArchiveMode.Read);
                var entry = inner.Entries.FirstOrDefault(e => e.Name.Equals(pngName, StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.Length > MAX_PNG) return null;
                using var es = entry.Open();
                using var png = new MemoryStream();
                es.CopyTo(png);
                var bytes = png.ToArray();
                // must really be a PNG
                if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != (byte)'P') return null;
                if (_cache.Count >= CACHE_ENTRIES) _cache.Clear();
                _cache[pngName] = bytes;
                return bytes;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
