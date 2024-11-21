using System.Security.Cryptography;
using System.Text;

namespace EFCoreCache.Extensions;
internal static class StringExtensions
{
    /// <summary>
    ///     Determines if a collection contains an item which ends with the given value
    /// </summary>
    internal static bool EndsWith(this IEnumerable<string>? collection, string? value,
                                StringComparison stringComparison)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return collection?.Any(item => item.EndsWith(value, stringComparison)) == true;
    }

    /// <summary>
    ///     Determines if a collection contains an item which starts with the given value
    /// </summary>
    internal static bool StartsWith(this IEnumerable<string>? collection, string? value,
                                  StringComparison stringComparison)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return collection?.Any(item => item.StartsWith(value, stringComparison)) == true;
    }

    /// <summary>
    ///     Determines if a collection exclusively contains every item in the given collection
    /// </summary>
    internal static bool ContainsEvery(this IEnumerable<string>? source, IEnumerable<string>? collection,
                                     StringComparer stringComparison)
    {
        if (source is null || collection is null)
        {
            return false;
        }

        return source.OrderBy(fElement => fElement, stringComparison).SequenceEqual(
         collection.OrderBy(sElement => sElement, stringComparison),
         stringComparison);
    }

    /// <summary>
    ///     Determines if a collection contains items only in the given collection
    /// </summary>
    internal static bool ContainsOnly(this IEnumerable<string>? source, IEnumerable<string>? collection,
                                    StringComparer stringComparison)
    {
        if (source is null || collection is null)
        {
            return false;
        }

        return source.All(sElement => collection.Contains(sElement, stringComparison));
    }

    internal static (bool, string) ToHashKey(this string key, string cacheKeyPrefix = "_EFCache.Data_")
    {
        bool hashed = false;
        // Uncomment the following to see the real queries to database
        ////return key;

        //Looking up large Keys in Redis can be expensive (comparing Large Strings), so if keys are large, hash them, otherwise if keys are short just use as-is
        if (key.Length <= 128) return (hashed, key.StartsWith(cacheKeyPrefix) ? key : cacheKeyPrefix + key);
        using (var sha = SHA1.Create())
        {
            key = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key)));
            hashed = true;
            return (hashed, cacheKeyPrefix + key);
        }
    }
}
