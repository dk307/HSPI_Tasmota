using Hspi.Utils;
using Newtonsoft.Json;
using NullGuard;
using System.ComponentModel;

namespace Hspi.DeviceData.Tasmota
{
    public sealed record TasmotaDeviceFeature
    {
        public TasmotaDeviceFeature(string id, string name, FeatureSource sourceType, FeatureDataType? dataType)
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

        public TasmotaDeviceFeature WithNewDataType(FeatureDataType? dataType)
        {
            return new TasmotaDeviceFeature(this.Id, this.Name, this.SourceType, dataType);
        }
    };
}