using System.Collections.Generic;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.Utils
{
    internal static class MiscHelper
    {
        public static TValue GetValueOrDefault<TKey, TValue> (
                this IDictionary<TKey, TValue> dictionary,
                TKey key,
                TValue defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static TValue GetValueOrDefault<TKey, TValue> (
                this ImmutableDictionary<TKey, TValue> dictionary,
                TKey key,
                TValue defaultValue) where TKey: notnull
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}