using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed record TasmotaDeviceInfo
    {
        public TasmotaDeviceInfo(Uri uri,
                                 string? user,
                                 string? password,
                                 IEnumerable<TasmotaDeviceFeature>? enabledFeatures)
        {
            Uri = uri;
            User = user;
            Password = password;
            EnabledFeatures = enabledFeatures?.ToImmutableHashSet() ?? ImmutableHashSet<TasmotaDeviceFeature>.Empty;
        }

        public ImmutableHashSet<TasmotaDeviceFeature> EnabledFeatures { get; }
        public string? Password { get; }
        public Uri Uri { get; }
        public string? User { get; }

#pragma warning disable CA1822 // Mark members as static
        public int Version => 1;
#pragma warning restore CA1822 // Mark members as static

        public static TasmotaDeviceInfo CreateNew(TasmotaDeviceInfo? existing,
                                                   IDictionary<string, string>? changes,
                                                   IEnumerable<TasmotaDeviceFeature>? enabledFeatures)
        {
            string? user = null;
            string? password = null;
            string? uri = null;

            if (changes != null)
            {
                changes.TryGetValue(nameof(User), out user);
                changes.TryGetValue(nameof(Password), out password);
                changes.TryGetValue(nameof(Uri), out uri);
            }

            Uri uri1 = uri != null ? new Uri(uri) : (existing?.Uri ?? throw new Exception("uri not valid"));
            return new TasmotaDeviceInfo(uri1,
                                         user ?? existing?.User,
                                         password ?? existing?.Password,
                                         enabledFeatures);
        }
    }
}