using System.Security.Cryptography;
using System.Text;

namespace Nelderim.Utility;

public class Crypto
{
    public static string Sha1Hash(Stream stream)
    {
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] hash = sha1.ComputeHash(stream);
            StringBuilder sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }
    }
}