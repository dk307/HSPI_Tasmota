namespace Hspi.DeviceData.Tasmota
{
    [System.AttributeUsage(System.AttributeTargets.All)
]
    public class UnitAttribute : System.Attribute
    {
        public UnitAttribute(string unit)
        {
            Unit = unit;
        }

        public string Unit { get; }
    }

    public enum ValueType
    {
        ValueType = 0,
        StringType = 1,
        OnOff =2,
    }

    [System.AttributeUsage(System.AttributeTargets.All)]
    public class ValueTypeAttribute : System.Attribute
    {
        public ValueTypeAttribute(ValueType type)
        {
            Type = type;
        }

        public ValueType Type { get; }
    }
}