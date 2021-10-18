using HomeSeer.Jui.Types;
using HomeSeer.Jui.Views;
using Hspi.DeviceData;
using Hspi.DeviceData.Tasmota;
using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using static System.FormattableString;

#nullable enable

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
            if (refId.HasValue)
            {
                data.Add("refId", refId);
            }
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

                var networkInfoView = new GridView("id_network", "Network");
        
                networkInfoView.AddView(new InputView(nameof(TasmotaDeviceInfo.Uri), "Http url of the device", data?.Uri?.ToString() ?? string.Empty, EInputType.Url));
                networkInfoView.AddView(new InputView(nameof(TasmotaDeviceInfo.User), "User", data?.User ?? string.Empty, EInputType.Text));
                networkInfoView.AddView(new InputView(nameof(TasmotaDeviceInfo.Password), "Password", data?.Password ?? string.Empty, EInputType.Password));

                page = page.WithView(networkInfoView);
   
                try
                {
                    if (data != null)
                    {
                        var tasmotaStatus = TasmotaDeviceInterface.GetStatus(data, ShutdownCancellationToken).ResultForSync();
                        var possibleFeatures = tasmotaStatus.GetPossibleFeatures();
                        var telePeriod = TasmotaDeviceInterface.GetTelePeriod(data, ShutdownCancellationToken).ResultForSync();

                        var groups = possibleFeatures.GroupBy((x) => x.SourceType);

                        var settingView = new GridView("id_settings", "Settings");

                        settingView.AddView(new InputView(TelePeriodId, TelePeriodId,
                                               telePeriod.ToString(CultureInfo.InvariantCulture), EInputType.Number));

                        page = page.WithView(settingView);

                        foreach (var group in groups)
                        {
                            var groupView = new GridView("id_" + group.Key.ToString(), group.Key.ToString());
                            AddFeatureEnabledOptions(groupView, EnumHelper.GetDescription(group.Key), group, data?.EnabledFeatures ?? ImmutableHashSet<TasmotaDeviceFeature>.Empty);
                            page = page.WithView(groupView);
                        }
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

            static void AddFeatureEnabledOptions(GridView view, string name,
                                          IEnumerable<TasmotaDeviceFeature> possibleFeatures,
                                          ImmutableHashSet<TasmotaDeviceFeature> enabledList)
            {
                foreach (var element in possibleFeatures)
                {
                    string id = CreateIdForFeatureType(element);
                    var typeOptions = new List<string>();
                    var typeOptionKeys = new List<string>();

                    var features = EnumHelper.GetValues<TasmotaDeviceFeature.FeatureDataType>()
                                    .OrderBy<TasmotaDeviceFeature.FeatureDataType, string>((x) => x.ToString());

                    int selected = -1;
                    int i = 0;
                    int selectNone = -1;
                    foreach (var value in features)
                    {
                        typeOptions.Add(EnumHelper.GetDescription(value));
                        typeOptionKeys.Add(value.ToString());

                        if (enabledList.Contains(element.WithNewDataType(value)))
                        {
                            selected = i;
                        }

                        if (value == TasmotaDeviceFeature.FeatureDataType.None)
                        {
                            selectNone = i;
                        }

                        i++;
                    }

                    if (selected == -1)
                    {
                        selected = selectNone;
                    }

                   
                    view.AddView( new SelectListView(id,
                                                       Invariant($"{element.Name}"),
                                                       typeOptions,
                                                       typeOptionKeys,
                                                       ESelectListType.DropDown,
                                                       selected));
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
                    data = TasmotaDeviceInfo.CreateNew(data, changes, data?.EnabledFeatures);
                    tasmotaDevice.Data = data;

                    // update enabled features later
                    var tasmotaStatus = TasmotaDeviceInterface.GetStatus(data, ShutdownCancellationToken).ResultForSync();
                    var possibleFeatures = tasmotaStatus.GetPossibleFeatures();

                    var newList = new HashSet<TasmotaDeviceFeature>(data.EnabledFeatures);
                    foreach (var feature in possibleFeatures)
                    {
                        CheckValue(changes, newList, feature);
                    }

                    tasmotaDevice.Data = TasmotaDeviceInfo.CreateNew(data, null, newList);

                    if (changes.ContainsKey(TelePeriodId))
                    {
                        var teleperiod = int.Parse(changes[TelePeriodId], NumberStyles.Any, CultureInfo.InvariantCulture);
                        TasmotaDeviceInterface.SetTelePeriod(data, teleperiod, ShutdownCancellationToken).ResultForSync();
                    }

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

            void CheckValue(IDictionary<string, string> changes,
                                  HashSet<TasmotaDeviceFeature> newList,
                                  TasmotaDeviceFeature feature)
            {
                string id = CreateIdForFeatureType(feature);
                if (changes.TryGetValue(id, out var stringValue))
                {
                    int intValue = Convert.ToInt32(stringValue, CultureInfo.InvariantCulture);

                    //always remove and add later to remove old data type
                    var featureNoDataType = feature.WithNewDataType(null);
                    newList.RemoveWhere(x => x.WithNewDataType(null) == featureNoDataType);

                    var featureDataTypes = EnumHelper.GetValues<TasmotaDeviceFeature.FeatureDataType>()
                .OrderBy<TasmotaDeviceFeature.FeatureDataType, string>((x) => x.ToString()).ToArray();

                    var value = featureDataTypes[intValue];

                    if (value != TasmotaDeviceFeature.FeatureDataType.None)
                    {
                        newList.Add(feature.WithNewDataType(value));
                    }
                }
            }
        }

        private static string CreateIdForFeatureType(TasmotaDeviceFeature feature)
        {
            return feature.FullUniqueId.Replace('.', '_');
        }

        private const string TelePeriodId = "TelePeriod";
    }
}