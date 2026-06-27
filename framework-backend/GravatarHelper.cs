using System;
using System.Security.Cryptography;
using System.Text;

public static class GravatarHelper
{
    public static string GetGravatarUrl(string email, int size = 80, string defaultImage = "identicon")
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        // Gravatar requires: trim + lowercase + SHA-256 (as of 2025+)
        // MD5 is still supported for legacy hashes but should not be used for new code.
        string normalizedEmail = email.Trim().ToLowerInvariant();

        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));

        var builder = new StringBuilder(hashBytes.Length * 2);
        foreach (byte b in hashBytes)
        {
            builder.Append(b.ToString("x2"));
        }

        string hash = builder.ToString();

        // www.gravatar.com, gravatar.com, and 0.gravatar.com all work
        return $"https://www.gravatar.com/avatar/{hash}?s={size}&d={defaultImage}";
    }
}