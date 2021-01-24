namespace Hspi.DeviceData
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
}