using HomeSeer.Jui.Views;
using Hspi.DeviceData;
using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using static System.FormattableString;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public override bool SupportsConfigDevice => true;

        public IDictionary<string, object> AddTasmotaDevice(IDictionary<string, string> tasmotaDataDict)
        {
            int? refId = null;
            var errors = new List<string>();
            try
            {
                logger.Debug("Creating new tasmota device");

                var deviceData = ScribanHelper.FromDictionary<TasmotaDeviceInfo>(tasmotaDataDict);

                if (errors.Count == 0)
                {
                    // add
                    refId = TasmotaDevice.CreateNew(HomeSeerSystem, deviceData, ShutdownCancellationToken).ResultForSync();
                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }

            var data = new Dictionary<string, object>();
            data.Add("refId", refId);
            data.Add("error", errors);

            return data;
        }

        public override string GetJuiDeviceConfigPage(int deviceRef)
        {
            var page = PageFactory.CreateDeviceConfigPage(PlugInData.ConfigPageId, "Tasmota Device Configuration");
            var tasmotaDevices = GetTasmotaDevices().ResultForSync();

            if (tasmotaDevices.TryGetValue(deviceRef, out var tasmotaDevice))
            {
                var data = tasmotaDevice.Data;
                page = page.WithInput(nameof(TasmotaDeviceInfo.Uri), "Http url of the device", data.Uri.ToString(), HomeSeer.Jui.Types.EInputType.Url)
                           .WithInput(nameof(TasmotaDeviceInfo.User), "User", data.User, HomeSeer.Jui.Types.EInputType.Text)
                           .WithInput(nameof(TasmotaDeviceInfo.Password), "Password", data.Password, HomeSeer.Jui.Types.EInputType.Password);

                try
                {
                    var tasmotaStatus = tasmotaDevice.GetStatus().ResultForSync();
                    var possibleFeatures = tasmotaStatus.GetPossibleFeatures();

                    var groups = possibleFeatures.GroupBy((x) => x.Type);

                    foreach (var group in groups)
                    {
                        AddFeatureEnabledOptions(EnumHelper.GetDescription(group.Key), group, data.EnabledFeatures);
                    }
                }
                catch (Exception ex)
                {
                    page.WithLabel("featureerror", Invariant($"Unable to contact device with {ex.GetFullMessage()}, cannot set feature of device."));
                }
            }
            else
            {
                page.WithLabel("notfound", "No Configuration Found");
            }

            return page.Page.ToJsonString();

            void AddFeatureEnabledOptions(string name,
                                          IEnumerable<TasmotaDeviceInfo.TasmotaEnabledFeature> possibleFeatures,
                                          ImmutableHashSet<TasmotaDeviceInfo.TasmotaEnabledFeature> enabledList)
            {
                page = page.WithLabel(name, "Create Features for " + name);
                foreach (var element in possibleFeatures)
                {
                    string id = CreateIdForFeatureType(element);
                    page = page.WithCheckBox(id, element.Name, enabledList.Contains(element));
                }
            }
        }

        protected override bool OnDeviceConfigChange(Page deviceConfigPage, int deviceRef)
        {
            var tasmotaDevices = GetTasmotaDevices().ResultForSync();

            if (tasmotaDevices.TryGetValue(deviceRef, out var tasmotaDevice))
            {
                try
                {
                    var changes = deviceConfigPage.ToValueMap();

                    //update the host/user/password first
                    var data = tasmotaDevice.Data;
                    data = data.CreateNew(changes, data.EnabledFeatures);
                    tasmotaDevice.Data = data;

                    // update enabled features later
                    var tasmotaStatus = tasmotaDevice.GetStatus().ResultForSync();
                    var possibleFeatures = tasmotaStatus.GetPossibleFeatures();

                    var newList = new HashSet<TasmotaDeviceInfo.TasmotaEnabledFeature>(data.EnabledFeatures);
                    foreach (var feature in possibleFeatures)
                    {
                        CheckToggleValue(changes, newList, feature);
                    }

                    tasmotaDevice.Data = data.CreateNew(null, newList);

                    return true;
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    logger.Warn(Invariant($"Failed to update device with {ExceptionHelper.GetFullMessage(ex)} for RefId: {deviceRef}"));
                    throw;
                }
                finally
                {
                    RestartProcessing();
                }
            }
            else
            {
                return false;
            }

            void CheckToggleValue(IDictionary<string, string> changes,
                                  HashSet<TasmotaDeviceInfo.TasmotaEnabledFeature> newList,
                                  TasmotaDeviceInfo.TasmotaEnabledFeature feature)
            {
                string id = CreateIdForFeatureType(feature);
                if (changes.TryGetValue(id, out var stringValue))
                {
                    var value = Convert.ToBoolean(stringValue, CultureInfo.InvariantCulture);

                    if (value)
                    {
                        newList.Add(feature);
                    }
                    else
                    {
                        newList.Remove(feature);
                    }
                }
            }
        }

        private static string CreateIdForFeatureType(TasmotaDeviceInfo.TasmotaEnabledFeature feature)
        {
            string id = EnumHelper.GetDescription(feature.Type) + "." + feature.Id;
            id = id.Replace('.', '_');
            return id;
        }
    }
}