using System.Security.Cryptography;
using System.Text;

namespace Nelderim.Utility;

public class Crypto
{
    public static string Sha1Hash(Stream stream)
    {
        return Hash(SHA1.Create(), stream);
    }

    public static string Md5Hash(Stream stream)
    {
        return Hash(MD5.Create(), stream);
    }

    public static string Hash(HashAlgorithm alg, Stream stream)
    {
        byte[] hash = alg.ComputeHash(stream);
        StringBuilder sb = new StringBuilder(hash.Length * 2);

        foreach (byte b in hash)
        {
            sb.Append(b.ToString("X"));
        }

        return sb.ToString();
    }
}