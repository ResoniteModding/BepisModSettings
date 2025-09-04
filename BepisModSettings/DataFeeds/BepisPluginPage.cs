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
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables;

namespace BepisModSettings.DataFeeds;

public static class BepisPluginPage
{
    internal static readonly Dictionary<string, Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>>> CategoryHandlers = new Dictionary<string, Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>>>();

    internal static async IAsyncEnumerable<DataFeedItem> Enumerate(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        string pluginId = path[1];

        ConfigFile configFile;
        ModMeta metadata;

        if (pluginId == "BepInEx.Core.Config")
        {
            configFile = ConfigFile.CoreConfig;
            metadata = new ModMeta("BepInEx Core Config", Utility.BepInExVersion.ToString(), "BepInEx.Core", null, null);
        }
        else
        {
            PluginInfo pluginInfo = NetChainloader.Instance.Plugins.Values.FirstOrDefault(x => x.Metadata.GUID == pluginId);
            if (pluginInfo?.Instance is not BasePlugin plugin) yield break;

            BepInPlugin pMetadata = MetadataHelper.GetMetadata(plugin);
            ResonitePlugin resonitePlugin = pMetadata as ResonitePlugin;

            configFile = plugin.Config;
            metadata = new ModMeta(pMetadata.Name, pMetadata.Version.ToString(), pluginId, resonitePlugin?.Author, resonitePlugin?.Link);
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
            metadata.Name = "<i>Unknown</i>";
        if (string.IsNullOrWhiteSpace(metadata.Version))
            metadata.Version = "<i>Unknown</i>";
        if (string.IsNullOrWhiteSpace(metadata.ID))
            metadata.ID = "<i>Unknown</i>";

        if (configFile.Count == 0)
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

        string[] metadataGroup = new[] { "Metadata" };

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

        if (!string.IsNullOrWhiteSpace(metadata.Link) && Uri.TryCreate(metadata.Link, UriKind.Absolute, out var uri))
        {
            var modHyperlink = new DataFeedAction();
            modHyperlink.InitBase("Link", path, metadataGroup, "Settings.BepInEx.Plugins.ModPage".AsLocaleKey());
            modHyperlink.InitAction(syncDelegate =>
            {
                var slot = syncDelegate?.Slot;
                if (slot == null) return;

                slot.AttachComponent<Hyperlink>().URL.Value = uri;
            });
            yield return modHyperlink;
        }
    }

    private static async IAsyncEnumerable<DataFeedItem> EnumerateConfigs(ConfigFile configFile, ModMeta metaData, IReadOnlyList<string> path)
    {
        // Used for enum config keys. basically you can define a function which will display a subcategory of this category.
        CategoryHandlers.Clear();

        if (configFile.Count > 0)
        {
            HashSet<string> sections = new HashSet<string>();
            List<string> added = new List<string>();
            foreach (ConfigEntryBase config in configFile.Values)
            {
                Type valueType = config.SettingType;

                string section = config.Definition.Section;
                if (sections.Add(section))
                {
                    DataFeedResettableGroup configs = new DataFeedResettableGroup();
                    configs.InitBase(section, path, null, section);
                    configs.InitResetAction(a =>
                    {
                        Button but = a.Slot.GetComponentInChildren<Button>();
                        if (but == null) return;

                        but.LocalPressed += (b, _) =>
                        {
                            Slot resetBtn = b.Slot.FindParent(x => x.Name == "Reset Button");
                            var store = resetBtn?.GetComponentInChildren<DataModelValueFieldStore<bool>.Store>();
                            if (store == null) return;

                            if (!store.Value.Value) return;
                            ResetConfigSection(metaData.ID, section);
                        };
                    });
                    yield return configs;
                }

                string initKey = section + "." + config.Definition.Key;
                string key = added.Contains(initKey) ? initKey + added.Count : initKey;

                // TODO: Figure out how to actually Localize config keys.
                // TODO: Add Key for SubCategories

                string defaultKey = $"{config.Definition.Key} : {valueType}";       // "Settings.BepInEx.Plugins.Configs.Default".AsLocaleKey(("name", config.Definition.Key), ("type", valueType));
                string valueKey = $"{config.Definition.Key} : {config.BoxedValue}"; // "Settings.BepInEx.Plugins.Configs.Value".AsLocaleKey(("name", config.Definition.Key), ("value", config.BoxedValue));
                string nameKey = config.Definition.Key;                             // "Settings.BepInEx.Plugins.Configs.Name".AsLocaleKey(("name", config.Definition.Key));
                string descKey = config.Description.Description;                    // "Settings.BepInEx.Plugins.Configs.Description".AsLocaleKey(("description", config.Description.Description));

                // if (SettingsLocaleHelper.PluginsWithLocales.Any(x => x.Metadata.GUID == metaData.ID))
                // {
                //     nameKey = config.Definition.Key.AsLocaleKey();
                //     descKey = config.Description.Description.AsLocaleKey();
                //     
                //     defaultKey = $"Settings.{metaData.ID}.Configs.Default".AsLocaleKey(("name", nameKey.content), ("type", valueType));
                //     valueKey = $"Settings.{metaData.ID}.Configs.Value".AsLocaleKey(("name", nameKey.content), ("value", config.BoxedValue));
                // }

                added.Add(key);

                string[] groupingKeys = [section];

                if (valueType == typeof(dummy))
                {
                    DataFeedItem dummyField = null;

                    if (config.Description.Tags.Contains("Action") && config.Description.Tags.FirstOrDefault(x => x is Delegate) is Delegate del)
                    {
                        DataFeedAction actionField = new DataFeedAction();
                        actionField.InitBase(key, path, groupingKeys, nameKey, descKey);
                        actionField.InitAction(syncDelegate =>
                        {
                            Button btn = syncDelegate.Slot.GetComponent<Button>();
                            if (btn == null) return;

                            btn.LocalPressed += (_, _) => del.DynamicInvoke();
                        });

                        dummyField = actionField;
                    }

                    if (dummyField == null)
                    {
                        dummyField = new DataFeedValueField<dummy>();
                        dummyField.InitBase(key, path, groupingKeys, nameKey, descKey);
                    }

                    yield return dummyField;
                }
                else if (valueType == typeof(bool))
                {
                    yield return DataFeedHelpers.GenerateToggle(key, path, groupingKeys, config);
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
                            enumItem = (DataFeedItem)DataFeedHelpers.GenerateEnumItemsAsync.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, config]);
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
                            nullableEnumItems = (IAsyncEnumerable<DataFeedItem>)DataFeedHelpers.GenerateNullableEnumItemsAsync.MakeGenericMethod(nullableType).Invoke(null, [key, path, groupingKeys, config]);
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.LogError(e);
                            DataFeedValueField<dummy> dummyField = new DataFeedValueField<dummy>();
                            dummyField.InitBase(key, path, groupingKeys, defaultKey, descKey);

                            nullableEnumItems = GetDummyAsync(dummyField);
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
                        valueItem = (DataFeedItem)DataFeedHelpers.GenerateValueField.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, config]);
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

    private static async IAsyncEnumerable<DataFeedItem> GetDummyAsync(DataFeedItem item)
    {
        await Task.CompletedTask;

        yield return item;
    }
    private static void LoadConfigs(string pluginId)
    {
        Plugin.Log.LogDebug($"Loading Configs for {pluginId}");
        if (pluginId == "BepInEx.Core")
        {
            ConfigFile.CoreConfig.Reload();
        }
        else if (NetChainloader.Instance.Plugins.TryGetValue(pluginId, out var pluginInfo) && pluginInfo.Instance is BasePlugin plugin)
        {
            plugin.Config?.Reload();
        }
    }

    private static void SaveConfigs(string pluginId)
    {
        Plugin.Log.LogDebug($"Saving Configs for {pluginId}");
        if (pluginId == "BepInEx.Core")
        {
            ConfigFile.CoreConfig.Save();
        }
        else if (NetChainloader.Instance.Plugins.TryGetValue(pluginId, out var pluginInfo) && pluginInfo.Instance is BasePlugin plugin)
        {
            plugin.Config?.Save();
        }
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

            ConfigFile configFile;

            if (pluginId != "BepInEx.Core.Config")
            {
                PluginInfo pluginInfo = NetChainloader.Instance.Plugins.Values.FirstOrDefault(x => x.Metadata.GUID == pluginId);

                if (pluginInfo?.Instance is not BasePlugin plugin) return;

                configFile = plugin.Config;
            }
            else
            {
                configFile = ConfigFile.CoreConfig;
            }

            if (configFile == null) return;

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
            ConfigFile configFile;

            if (pluginId != "BepInEx.Core.Config")
            {
                PluginInfo pluginInfo = NetChainloader.Instance.Plugins.Values.FirstOrDefault(x => x.Metadata.GUID == pluginId);

                if (pluginInfo?.Instance is not BasePlugin plugin) return;

                configFile = plugin.Config;
            }
            else
            {
                configFile = ConfigFile.CoreConfig;
            }

            if (configFile == null) return;

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