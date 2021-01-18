namespace Hspi
{
    /// <summary>
    /// Class to store static data
    /// </summary>
    internal static class PlugInData
    {
        /// <summary>
        /// The plugin Id
        /// </summary>
        public const string PlugInId = @"Tasmota";

        /// <summary>
        /// The plugin name
        /// </summary>
        public const string PlugInName = @"Tasmota";

        /// <summary>
        /// The plugin name
        /// </summary>
        public const string Hs3PlugInName = @"Tasmota";

        /// <summary>
        /// The plugin Id
        /// </summary>
        public const string SettingFileName = @"HSPI_Tasmota.ini";

#pragma warning disable CA1308 // Normalize strings to uppercase
        public static readonly string DevicePlugInDataNamedKey = PlugInId.ToLowerInvariant() + ".plugindata";

        public static readonly string DevicePlugInDataTypeKey = PlugInId.ToLowerInvariant() + ".plugindatatype";
#pragma warning restore CA1308 // Normalize strings to uppercase



        public const string ConfigPageId = "conflig-page-id-tasmota";
    }
}