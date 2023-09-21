namespace EFCoreCache.Extensions;
internal static class StringExtensions
{
    /// <summary>
    ///     Determines if a collection contains an item which ends with the given value
    /// </summary>
    public static bool EndsWith(this IEnumerable<string>? collection, string? value,
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
    public static bool StartsWith(this IEnumerable<string>? collection, string? value,
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
    public static bool ContainsEvery(this IEnumerable<string>? source, IEnumerable<string>? collection,
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
    public static bool ContainsOnly(this IEnumerable<string>? source, IEnumerable<string>? collection,
                                    StringComparer stringComparison)
    {
        if (source is null || collection is null)
        {
            return false;
        }

        return source.All(sElement => collection.Contains(sElement, stringComparison));
    }
}
