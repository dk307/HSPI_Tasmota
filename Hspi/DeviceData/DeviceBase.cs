using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;

namespace Hspi.DeviceData
{
    internal abstract class DeviceBase<T> where T : class
    {
        public DeviceBase(IHsController HS, int refId)
        {
            this.HS = HS;
            RefId = refId;
        }

        public abstract string DeviceType { get; }

        public T Data
        {
            get
            {
                var plugInExtra = HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) as PlugExtraData;
                var stringData = plugInExtra?[PlugInData.DevicePlugInDataNamedKey];
                return JsonConvert.DeserializeObject<T>(stringData);
            }

            set
            {
                UpdateDevice(value);
            }
        }

        public string Name => HSDeviceHelper.GetName(HS, RefId);
        public int RefId { get; }

        protected IHsController HS { get; private set; }

        public static PlugExtraData CreatePlugInExtraData(T importDeviceData, string deviceType)
        {
            string data = JsonConvert.SerializeObject(importDeviceData, Formatting.Indented);
            var plugExtra = HSDeviceHelper.CreatePlugInExtraDataForDeviceType(deviceType);
            plugExtra.AddNamed(PlugInData.DevicePlugInDataNamedKey, data);
            return plugExtra;
        }

        protected virtual void UpdateDevice(T data)
        {
            PlugExtraData extraData = CreatePlugInExtraData(data, DeviceType);
            HS.UpdatePropertyByRef(RefId, EProperty.PlugExtraData, extraData);
        }
    }
}