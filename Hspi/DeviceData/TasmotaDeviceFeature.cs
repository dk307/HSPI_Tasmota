using Hspi.Utils;
using Newtonsoft.Json;
using NullGuard;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Hspi.DeviceData
{

    public readonly struct TasmotaDeviceFeature : IEquatable<TasmotaDeviceFeature>
    {
        public TasmotaDeviceFeature(string id, string name, FeatureSource source, [AllowNull] FeatureDataType? dataType)
        {
            Id = id;
            Name = name;
            this.Source = source;
            DataType = dataType;
            this.FullUniqueId = string.Join(".",
                                            EnumHelper.GetDescription(Source),
                                            Id,
                                            DataType != null ? EnumHelper.GetDescription(DataType) : string.Empty);
        }

        public enum FeatureDataType
        {
            [Description("None")]
            None = 0,

            [Description("Number")]
            GenericDouble = 100,

            [Description("Percentage")]
            [Unit("%")]
            GenericPercentage = 101,

            [Description("String")]
            GenericString = 102,

            [Description("Temperature(F)")]
            [Unit("F")]
            TemperatureF = 3,

            [Unit("%")]
            Humidity = 4,

            DewPoint = 5,

            [Description("IP Address")]
            IPAddress = 6,

            [Unit("V")]
            Voltage = 7,

            [Description("Duration in Seconds")]
            [Unit("seconds")]
            DurationSeconds = 8,

            Time = 9,

            [Description("On / Off State Sensor")]
            OnOffStateSensor = 10,

            [Description("On / Off State Power Control")]
            OnOffStateControl = 11,

            [Description("Luminance in Lux")]
            [Unit("Lux")]
            LuminanceLux = 12,

            [Description("Temperature(C)")]
            [Unit("C")]
            TemperatureC = 13,

            [Unit("A")]
            Amperage = 14,

            [Unit("Watts")]
            Power = 15,

            [Unit("VA")]
            ApparentPower = 16,

            [Unit("VAr")]
            ReactivePower = 17,

            [Unit("kWh")]
            EnergyHWhs = 18,
        }

        public enum FeatureSource
        {
            Sensor = 0,
            State = 1,
        };

        public FeatureDataType? DataType { get; }

        [JsonIgnore]
        public string FullUniqueId { get; }

        public string Id { get; }
        public string Name { get; }

        public FeatureSource Source { get; }

        public static bool operator !=(TasmotaDeviceFeature left, TasmotaDeviceFeature right)
        {
            return !(left == right);
        }

        public static bool operator ==(TasmotaDeviceFeature left, TasmotaDeviceFeature right)
        {
            return left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is TasmotaDeviceFeature feature &&
                   FullUniqueId == feature.FullUniqueId;
        }

        bool IEquatable<TasmotaDeviceFeature>.Equals(TasmotaDeviceFeature other)
        {
            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = -678952093;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FullUniqueId);
            return hashCode;
        }

        public override string ToString()
        {
            return FullUniqueId;
        }

        public TasmotaDeviceFeature WithNewDataType([AllowNull] FeatureDataType? dataType)
        {
            return new TasmotaDeviceFeature(this.Id, this.Name, this.Source, dataType);
        }
    };
}