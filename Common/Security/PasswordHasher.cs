using System.Security.Cryptography;
using System.Text;

namespace Common.Security;

public class PasswordHasher
{
    public string Hash(string password)
    {
        var salt = GenerateSalt();
        var hashedPassword = HashWithSalt(password, salt);
        return $"{Convert.ToBase64String(salt)}:{hashedPassword}";
    }

    public bool Check(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        var salt = Convert.FromBase64String(parts[0]);
        var hash = parts[1];
        var hashedPassword = HashWithSalt(password, salt);
        return hashedPassword == hash;
    }

    private byte[] GenerateSalt()
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            var salt = new byte[16];
            rng.GetBytes(salt);
            return salt;
        }
    }

    private string HashWithSalt(string password, byte[] salt)
    {
        using (var sha256 = SHA256.Create())
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var saltedPassword = new byte[salt.Length + passwordBytes.Length];

            Buffer.BlockCopy(salt, 0, saltedPassword, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, saltedPassword, salt.Length, passwordBytes.Length);

            return Convert.ToBase64String(sha256.ComputeHash(saltedPassword));
        }
    }
}