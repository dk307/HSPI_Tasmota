using Hspi.Utils;
using Newtonsoft.Json;
using NullGuard;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]

    public sealed class TasmotaDeviceFeature : IEquatable<TasmotaDeviceFeature>
    {
        public TasmotaDeviceFeature(string id, string name, FeatureSource sourceType, [AllowNull] FeatureDataType? dataType)
        {
            Id = id;
            Name = name;
            this.SourceType = sourceType;
            DataType = dataType;
            this.FullUniqueId = string.Join(".",
                                            EnumHelper.GetDescription(SourceType),
                                            Id,
                                            DataType != null ? EnumHelper.GetDescription(DataType) : string.Empty);
        }

        public enum FeatureDataType
        {
            [Description("None")]
            None = 0,

            [Description("Number")]
            [ValueType(ValueType.ValueType)]
            GenericDouble = 100,

            [Description("Percentage")]
            [Unit("%")]
            [ValueType(ValueType.ValueType)]
            GenericPercentage = 101,

            [Description("String")]
            [ValueType(ValueType.StringType)]
            GenericString = 102,

            [Description("Temperature(F)")]
            [Unit("F")]
            [ValueType(ValueType.ValueType)]
            TemperatureF = 3,

            [Unit("%")]
            [ValueType(ValueType.ValueType)]
            Humidity = 4,

            [ValueType(ValueType.ValueType)]
            DewPoint = 5,

            [Description("IP Address")]
            [ValueType(ValueType.StringType)]
            IPAddress = 6,

            [Unit("V")]
            [ValueType(ValueType.ValueType)]
            Voltage = 7,

            [Description("Duration in Seconds")]
            [Unit("seconds")]
            [ValueType(ValueType.ValueType)]
            DurationSeconds = 8,

            [Description("On / Off State Sensor")]
            [ValueType(ValueType.OnOff)]
            OnOffStateSensor = 10,

            [Description("On / Off State Power Control")]
            [ValueType(ValueType.OnOff)]
            OnOffStateControl = 11,

            [Description("Luminance in Lux")]
            [Unit("Lux")]
            [ValueType(ValueType.ValueType)]
            LuminanceLux = 12,

            [Description("Temperature(C)")]
            [Unit("C")]
            [ValueType(ValueType.ValueType)]
            TemperatureC = 13,

            [Unit("A")]
            [ValueType(ValueType.ValueType)]
            Amperage = 14,

            [Unit("Watts")]
            [ValueType(ValueType.ValueType)]
            Power = 15,

            [Unit("VA")]
            [ValueType(ValueType.ValueType)]
            ApparentPower = 16,

            [Unit("VAr")]
            [ValueType(ValueType.ValueType)]
            ReactivePower = 17,

            [Unit("kWh")]
            [ValueType(ValueType.ValueType)]
            EnergyKWh = 18,
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

        public FeatureSource SourceType { get; }

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
            return new TasmotaDeviceFeature(this.Id, this.Name, this.SourceType, dataType);
        }
    };
}