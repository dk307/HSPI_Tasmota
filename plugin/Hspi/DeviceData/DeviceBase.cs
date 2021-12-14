using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal abstract class DeviceBase<T> where T : class
    {
        protected DeviceBase(IHsController HS, int refId)
        {
            this.HS = HS;
            RefId = refId;
        }

        public abstract string DeviceType { get; }

        [DisallowNull]
        public T? Data
        {
            get
            {
                var plugInExtra = HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) as PlugExtraData;
                var stringData = plugInExtra?[PlugInData.DevicePlugInDataNamedKey];
                if (stringData != null)
                {
                    return JsonConvert.DeserializeObject<T>(stringData);
                }
                return null;
            }

            set
            {
                UpdateDevice(value);
            }
        }

        public T GetValidatedData()
        {
            return Data ?? throw new InvalidOperationException(Invariant($"Plugin Data is not valid for {Name}"));
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