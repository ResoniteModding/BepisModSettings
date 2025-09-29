using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using BepisLocaleLoader;
using BepisModSettings.ConfigAttributes;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables;

namespace BepisModSettings.DataFeeds;

public static class BepisConfigsPage
{
    internal static readonly Dictionary<string, Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>>> CategoryHandlers = new Dictionary<string, Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>>>();

    public static event Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> CustomPluginConfigsPages;

    internal static async IAsyncEnumerable<DataFeedItem> Enumerate(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        string pluginId = path[1];

        if (!DataFeedHelpers.DoesPluginExist(pluginId))
        {
            if (CustomPluginConfigsPages != null)
            {
                foreach (Delegate del in CustomPluginConfigsPages.GetInvocationList())
                {
                    if (del is not Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> handler) continue;

                    await foreach (DataFeedItem item in handler(path))
                    {
                        yield return item;
                    }
                }
            }

            yield break;
        }

        if (!DataFeedHelpers.TryGetPluginData(pluginId, out ConfigFile configFile, out ModMeta metadata)) yield break;

        if (string.IsNullOrWhiteSpace(metadata.Name))
            metadata.Name = "<i>Unknown</i>";
        if (string.IsNullOrWhiteSpace(metadata.Version))
            metadata.Version = "<i>Unknown</i>";
        if (string.IsNullOrWhiteSpace(metadata.ID))
            metadata.ID = "<i>Unknown</i>";

        if (DataFeedHelpers.IsEmpty(configFile))
        {
            DataFeedLabel noConfigs = new DataFeedLabel();
            noConfigs.InitBase("NoConfigs", path, null, "Settings.BepInEx.Plugins.NoConfigs".AsLocaleKey());
            yield return noConfigs;
        }
        else
        {
            IAsyncEnumerable<DataFeedItem> configs = EnumerateConfigs(configFile, metadata, path);
            await foreach (DataFeedItem item in configs)
            {
                yield return item;
            }
        }

        // Metadata
        DataFeedGroup modGroup = new DataFeedGroup();
        modGroup.InitBase("Metadata", path, null, "Settings.BepInEx.Plugins.Metadata".AsLocaleKey());
        yield return modGroup;

        string[] metadataGroup = ["Metadata"];

        DataFeedIndicator<string> idIndicator = new DataFeedIndicator<string>();
        idIndicator.InitBase("Id", path, metadataGroup, "Settings.BepInEx.Plugins.Guid".AsLocaleKey());
        idIndicator.InitSetupValue(field => field.Value = metadata.ID);
        yield return idIndicator;

        if (!string.IsNullOrWhiteSpace(metadata.Author))
        {
            DataFeedIndicator<string> authorsIndicator = new DataFeedIndicator<string>();
            authorsIndicator.InitBase("Author", path, metadataGroup, "Settings.BepInEx.Plugins.Author".AsLocaleKey());
            authorsIndicator.InitSetupValue(field => field.Value = metadata.Author);
            yield return authorsIndicator;
        }

        DataFeedIndicator<string> versionIndicator = new DataFeedIndicator<string>();
        versionIndicator.InitBase("Version", path, metadataGroup, "Settings.BepInEx.Plugins.Version".AsLocaleKey());
        versionIndicator.InitSetupValue(field => field.Value = metadata.Version);
        yield return versionIndicator;

        if (!string.IsNullOrWhiteSpace(metadata.Link) && Uri.TryCreate(metadata.Link, UriKind.Absolute, out Uri uri))
        {
            DataFeedAction modHyperlink = new DataFeedAction();
            modHyperlink.InitBase("Link", path, metadataGroup, "Settings.BepInEx.Plugins.ModPage".AsLocaleKey(), metadata.Link);
            modHyperlink.InitAction(syncDelegate =>
            {
                Slot slot = syncDelegate?.Slot;
                if (slot == null) return;

                slot.AttachComponent<Hyperlink>().URL.Value = uri;
            });
            yield return modHyperlink;
        }
    }

    private static async IAsyncEnumerable<DataFeedItem> EnumerateConfigs(ConfigFile configFile, ModMeta metaData, IReadOnlyList<string> path)
    {
        CategoryHandlers.Clear();

        List<IGrouping<string, ConfigEntryBase>> groupedConfigs = configFile.Values.Where(config => Plugin.ShowHidden.Value || !HiddenConfig.IsHidden(config)).GroupBy(config => config.Definition.Section).ToList();
        foreach (IGrouping<string, ConfigEntryBase> sectionGroup in groupedConfigs)
        {
            string section = sectionGroup.Key;

            if (!sectionGroup.Any()) continue;

            DataFeedResettableGroup configs = new DataFeedResettableGroup();
            configs.InitBase(section, path, null, section);
            configs.InitResetAction(a =>
            {
                Button but = a.Slot.GetComponentInChildren<Button>();
                if (but == null) return;

                but.LocalPressed += (b, _) =>
                {
                    Slot resetBtn = b.Slot.FindParent(x => x.Name == "Reset Button");
                    DataModelValueFieldStore<bool>.Store store = resetBtn?.GetComponentInChildren<DataModelValueFieldStore<bool>.Store>();
                    if (store == null) return;

                    if (!store.Value.Value) return;
                    ResetConfigSection(metaData.ID, section);
                };
            });
            yield return configs;

            List<string> added = new List<string>();
            foreach (ConfigEntryBase config in sectionGroup)
            {
                string initKey = section + "." + config.Definition.Key;
                string key = added.Contains(initKey) ? initKey + added.Count : initKey;
                added.Add(key);

                bool isHidden = HiddenConfig.IsHidden(config);
                Type valueType = config.SettingType;

                LocaleString nameKey = isHidden ? $"<color=hero.yellow>{config.Definition.Key}</color>" : config.Definition.Key;
                LocaleString descKey = config.Description.Description;
                LocaleString defaultKey = $"{config.Definition.Key} : {valueType}";
                LocaleString valueKey = $"{config.Definition.Key} : {config.BoxedValue}";

                bool hasLocale = LocaleLoader.PluginsWithLocales.Any(x => x.Metadata.GUID == metaData.ID);
                if (hasLocale && config.Description.Tags.FirstOrDefault(x => x is ConfigLocale) is ConfigLocale localeString)
                {
                    nameKey = localeString.Name;
                    descKey = localeString.Description;

                    string formatted = localeString.Name.content.GetFormattedLocaleString();
                    defaultKey = $"{formatted} : {valueType}";
                    valueKey = $"{formatted} : {config.BoxedValue}";
                }

                if (isHidden) nameKey = nameKey.SetFormat("<color=hero.yellow>{0}</color>");

                InternalLocale internalLocale = new InternalLocale(nameKey, descKey);


                string[] groupingKeys = [section];

                // Keep your existing valueType logic here (dummy, bool, enum, nullable, etc.)
                // unchanged, just moved inside the sectionGroup loop

                if (valueType == typeof(dummy))
                {
                    DataFeedItem dummyField = null;

                    object firstAction = config.Description.Tags.FirstOrDefault(x => x is ActionConfig or Action);
                    if (firstAction != null)
                    {
                        DataFeedAction actionField = new DataFeedAction();
                        actionField.InitBase(key, path, groupingKeys, nameKey, descKey);
                        actionField.InitAction(syncDelegate =>
                        {
                            Button btn = syncDelegate.Slot.GetComponent<Button>();
                            if (btn == null) return;

                            if (firstAction is ActionConfig actionConfig)
                                btn.LocalPressed += (_, _) => actionConfig.Invoke();
                            else if (firstAction is Action action)
                                btn.LocalPressed += (_, _) => action.Invoke();
                        });

                        dummyField = actionField;
                    }

                    bool customUi = false;
                    if (CustomDataFeed.GetCustomFeedMethod(config) is DataFeedMethod customDataFeed)
                    {
                        customUi = true;
                        IAsyncEnumerable<DataFeedItem> datafeed = customDataFeed(path, groupingKeys);
                        await foreach (DataFeedItem item in datafeed)
                        {
                            yield return item;
                        }
                    }

                    if (dummyField == null && !customUi)
                    {
                        dummyField = new DataFeedValueField<dummy>();
                        dummyField.InitBase(key, path, groupingKeys, nameKey, descKey);
                    }

                    if (!customUi)
                    {
                        yield return dummyField;
                    }
                }
                else if (valueType == typeof(bool))
                {
                    yield return DataFeedHelpers.GenerateToggle(key, path, groupingKeys, internalLocale, config);
                }
                else if (valueType.IsEnum)
                {
                    DataFeedItem enumItem;
                    try
                    {
                        if (valueType.GetCustomAttribute<FlagsAttribute>() != null)
                        {
                            LocaleLoader.AddLocaleString($"Settings.{key}.Breadcrumb", initKey, authors: PluginMetadata.AUTHORS);

                            CategoryHandlers.Add(key, path2 => (IAsyncEnumerable<DataFeedItem>)DataFeedHelpers.HandleFlagsEnumCategory.MakeGenericMethod(valueType).Invoke(null, [path2, config]));
                            enumItem = new DataFeedCategory();
                            enumItem.InitBase(key, path, groupingKeys, valueKey, descKey);
                        }
                        else
                        {
                            enumItem = (DataFeedItem)DataFeedHelpers.GenerateEnumItemsAsync.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, internalLocale, config]);
                        }
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError(e);
                        enumItem = new DataFeedValueField<dummy>();
                        enumItem.InitBase(key, path, groupingKeys, defaultKey, descKey);
                    }

                    yield return enumItem;
                }
                else if (valueType.IsNullable())
                {
                    Type nullableType = valueType.GetGenericArguments()[0];
                    if (nullableType.IsEnum)
                    {
                        IAsyncEnumerable<DataFeedItem> nullableEnumItems;

                        try
                        {
                            nullableEnumItems = (IAsyncEnumerable<DataFeedItem>)DataFeedHelpers.GenerateNullableEnumItemsAsync.MakeGenericMethod(nullableType).Invoke(null, [key, path, groupingKeys, internalLocale, config]);
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.LogError(e);
                            DataFeedValueField<dummy> dummyField = new DataFeedValueField<dummy>();
                            dummyField.InitBase(key, path, groupingKeys, defaultKey, descKey);

                            nullableEnumItems = dummyField.AsAsyncEnumerable();
                        }

                        await foreach (DataFeedItem item in nullableEnumItems)
                        {
                            yield return item;
                        }
                    }
                }
                else
                {
                    DataFeedItem valueItem;
                    try
                    {
                        if (!config.SettingType.IsTypeInjectable() && TomlTypeConverter.CanConvert(config.SettingType))
                        {
                            valueItem = DataFeedHelpers.GenerateProxyField(key, path, groupingKeys, internalLocale, config);
                        }
                        else
                        {
                            valueItem = (DataFeedItem)DataFeedHelpers.GenerateValueField.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, internalLocale, config]);
                        }
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError(e);
                        valueItem = new DataFeedValueField<dummy>();
                        valueItem.InitBase(key, path, groupingKeys, defaultKey, descKey);
                    }

                    yield return valueItem;
                }
            }
        }

        const string groupId = "ActionsGroup";
        DataFeedGroup group = new DataFeedGroup();
        group.InitBase(groupId, path, null, "Settings.BepInEx.Plugins.Actions".AsLocaleKey());
        yield return group;
        string[] groupKeys = [groupId];

        DataFeedAction loadAct = new DataFeedAction();
        loadAct.InitBase("LoadConfig", path, groupKeys, "Settings.BepInEx.Plugins.LoadConfig".AsLocaleKey(), "Settings.BepInEx.Plugins.LoadConfig.Description".AsLocaleKey());
        loadAct.InitAction(syncDelegate =>
        {
            Button btn = syncDelegate.Slot.GetComponent<Button>();
            if (btn == null) return;

            btn.LocalPressed += (_, _) => LoadConfigs(metaData.ID);
        });
        yield return loadAct;

        DataFeedAction saveAct = new DataFeedAction();
        saveAct.InitBase("SaveConfig", path, groupKeys, "Settings.BepInEx.Plugins.SaveConfig".AsLocaleKey(), "Settings.BepInEx.Plugins.SaveConfig.Description".AsLocaleKey());
        saveAct.InitAction(syncDelegate =>
        {
            Button btn = syncDelegate.Slot.GetComponent<Button>();
            if (btn == null) return;

            btn.LocalPressed += (_, _) => SaveConfigs(metaData.ID);
        });
        yield return saveAct;

        DataFeedAction resetAct = new DataFeedAction();
        resetAct.InitBase("ResetConfig", path, groupKeys, "Settings.BepInEx.Plugins.ResetConfig".AsLocaleKey(), "Settings.BepInEx.Plugins.ResetConfig.Description".AsLocaleKey());
        resetAct.InitAction(syncDelegate =>
        {
            Button btn = syncDelegate.Slot?.GetComponent<Button>();
            if (btn == null) return;

            ValueMultiDriver<bool> valueDriver = btn.Slot.GetComponent<ValueMultiDriver<bool>>();
            if (valueDriver != null && valueDriver.Drives.Count > 0)
            {
                SetColor(0, new colorX(0.36f, 0.2f, 0.23f));
                SetColor(1, new colorX(1f, 0.46f, 0.46f));
                SetColor(3, new colorX(0.88f, 0.88f, 0.88f));
            }

            btn.LocalPressed += (b, _) => ResetConfigs(b, metaData.ID, valueDriver);

            return;

            void SetColor(int index, colorX color)
            {
                if (index >= valueDriver.Drives.Count) return;

                FieldDrive<bool> drive = valueDriver.Drives[index];
                BooleanValueDriver<colorX> colorDriver = btn.Slot.GetComponent<BooleanValueDriver<colorX>>(x => x.State == drive.Target);
                if (colorDriver != null)
                {
                    colorDriver.TrueValue.Value = color;
                }
            }
        });
        yield return resetAct;
    }

    private static void LoadConfigs(string pluginId)
    {
        Plugin.Log.LogDebug($"Loading Configs for {pluginId}");

        if (!DataFeedHelpers.TryGetPluginData(pluginId, out ConfigFile configFile, out _)) return;

        configFile.Reload();
    }

    private static void SaveConfigs(string pluginId)
    {
        Plugin.Log.LogDebug($"Saving Configs for {pluginId}");

        if (!DataFeedHelpers.TryGetPluginData(pluginId, out ConfigFile configFile, out _)) return;

        configFile.Save();
    }

    private static bool _resetPressed;
    private static CancellationTokenSource _cts;

    private static void ResetConfigs(IButton btn, string pluginId, ValueMultiDriver<bool> vmd = null)
    {
        try
        {
            if (!_resetPressed)
            {
                btn.LabelTextField.SetLocalized("Settings.BepInEx.Plugins.ResetConfig.Confirm");

                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                CancellationToken token = _cts.Token;

                Task.Run(async () =>
                {
                    await Task.Delay(2000, token);

                    if (!_resetPressed) return;
                    btn.RunSynchronously(() => btn.LabelTextField.SetLocalized("Settings.BepInEx.Plugins.ResetConfig"));
                    _resetPressed = false;
                    if (vmd != null) vmd.Value.Value = _resetPressed;
                }, token);

                _resetPressed = true;
                if (vmd != null) vmd.Value.Value = _resetPressed;
                return;
            }

            if (!DataFeedHelpers.TryGetPluginData(pluginId, out ConfigFile configFile, out _)) return;

            foreach (ConfigEntryBase entry in configFile.Values)
            {
                entry.BoxedValue = entry.DefaultValue;
            }

            btn.LabelTextField.SetLocalized("Settings.BepInEx.Plugins.ResetConfig");
            _resetPressed = false;
            if (vmd != null) vmd.Value.Value = _resetPressed;

            _cts?.Cancel();
            _cts = null;

            Plugin.Log.LogInfo($"Configs for {pluginId} have been reset.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError(e);
        }
    }

    private static void ResetConfigSection(string pluginId, string section)
    {
        try
        {
            if (!DataFeedHelpers.TryGetPluginData(pluginId, out ConfigFile configFile, out _)) return;

            foreach (ConfigEntryBase entry in configFile.Values)
            {
                if (entry.Definition.Section != section) continue;

                entry.BoxedValue = entry.DefaultValue;
            }

            Plugin.Log.LogInfo($"Configs for {pluginId}-{section} have been reset.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError(e);
        }
    }
}