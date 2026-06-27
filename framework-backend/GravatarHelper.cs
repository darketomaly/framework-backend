using System;
using System.Security.Cryptography;
using System.Text;

public static class GravatarHelper
{
    public static string GetGravatarUrl(string email, int size = 80, string defaultImage = "identicon")
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;

        // Step 1: Clean and normalize the email string per Gravatar rules
        string cleanedEmail = email.Trim().ToLowerInvariant();

        // Step 2: Generate SHA-256 hash (or MD5 if using older legacy systems)
        byte[] inputBytes = Encoding.UTF8.GetBytes(cleanedEmail);
        byte[] hashBytes = SHA256.HashData(inputBytes);

        // Step 3: Convert byte array to hex string
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < hashBytes.Length; i++)
        {
            builder.Append(hashBytes[i].ToString("x2"));
        }
        string emailHash = builder.ToString();

        // Step 4: Construct the absolute image endpoint URL
        return $"https://www.gravatar.com/avatar/{emailHash}?s={size}&d={defaultImage}";
    }
}