using System.Security.Cryptography;

namespace Nelderim;

public static class Utils
{
    public static string Sha1Hash(string file)
    {
        Console.WriteLine("Hashing: " + file);
        return Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(file))).ToUpper();
    }
}