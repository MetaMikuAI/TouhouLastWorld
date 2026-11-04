using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TouhouLastWorld;

public static class ResponseProtocol
{
    public static string Encode(string api, string json, string key)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        if (api != "game_init")
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(bytes);
            bytes = output.ToArray();
        }
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = new byte[16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        return Convert.ToBase64String(encryptor.TransformFinalBlock(bytes, 0, bytes.Length));
    }
}
