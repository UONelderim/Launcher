using System.Security.Cryptography;
using System.Text;

namespace Nelderim.Utility;

public class Crypto
{
    public static string Sha1Hash(Stream stream)
    {
        byte[] hash = SHA1.Create().ComputeHash(stream);
        StringBuilder sb = new StringBuilder(hash.Length * 2);

        foreach (byte b in hash)
        {
            sb.Append(b.ToString("X"));
        }

        return sb.ToString();
    }
}