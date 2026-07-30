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
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
        var builder = new StringBuilder(hashBytes.Length * 2);
        
        foreach (byte b in hashBytes)
        {
            builder.Append(b.ToString("x2"));
        }

        var hash = builder.ToString();
        var url = $"https://www.gravatar.com/avatar/{hash}?s={size}&d={defaultImage}";
        
        Console.WriteLine($"Getting gravatar image from email: {email}, and hash: {hash}, final url: {url}");

        // www.gravatar.com, gravatar.com, and 0.gravatar.com all work
        return url;
    }
}