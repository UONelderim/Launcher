using System.Security.Cryptography;

namespace Nelderim.Utility;

public class Crypto
{
    public static string Sha1Hash(Stream stream)
    {
        return Convert.ToHexString(SHA1.HashData(stream)).ToUpper();
    }
}