using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed partial class TasmotaDeviceInfo
    {
        public TasmotaDeviceInfo(Uri uri,
                                [AllowNull] string user,
                                [AllowNull] string password,
                                [AllowNull] IEnumerable<TasmotaEnabledFeature> enabledFeatures)
        {
            Uri = uri;
            User = user;
            Password = password;
            EnabledFeatures = enabledFeatures?.ToImmutableHashSet() ?? ImmutableHashSet<TasmotaEnabledFeature>.Empty;
        }

        public ImmutableHashSet<TasmotaEnabledFeature> EnabledFeatures { get; }
        public string Password { get; }
        public Uri Uri { get; }
        public string User { get; }

#pragma warning disable CA1822 // Mark members as static
        public int Version => 1;
#pragma warning restore CA1822 // Mark members as static

        public TasmotaDeviceInfo CreateNew([AllowNull] IDictionary<string, string> changes,
                                           [AllowNull] IEnumerable<TasmotaEnabledFeature> enabledFeatures)
        {
            string user = null;
            string password = null;
            string uri = null;

            if (changes != null)
            {
                changes.TryGetValue(nameof(User), out user);
                changes.TryGetValue(nameof(Password), out password);
                changes.TryGetValue(nameof(Uri), out uri);
            }

            return new TasmotaDeviceInfo(uri != null ? new Uri(uri) : Uri,
                                         user ?? User,
                                         password ?? Password,
                                         enabledFeatures);
        }
    }
}