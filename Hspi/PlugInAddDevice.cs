﻿using HomeSeer.Jui.Views;
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

                    var groups = possibleFeatures.GroupBy((x) => x.Source);

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

                    page = page.WithDropDownSelectList(id,
                                                       Invariant($"{name}:{element.Name}"),
                                                       typeOptions,
                                                       typeOptionKeys,
                                                       selected);
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

                    var newList = new HashSet<TasmotaDeviceFeature>(data.EnabledFeatures);
                    foreach (var feature in possibleFeatures)
                    {
                        CheckValue(changes, newList, feature);
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
                .                   OrderBy<TasmotaDeviceFeature.FeatureDataType, string>((x) => x.ToString()).ToArray();

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
    }
}