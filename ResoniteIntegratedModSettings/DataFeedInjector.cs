using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;

namespace ResoniteIntegratedModSettings;

public class DataFeedInjector
{
    private static readonly Type _dummyType = typeof(dummy);

    private static IReadOnlyList<string> CurrentPath { get; set; }

    internal static async IAsyncEnumerable<DataFeedItem> CombineEnumerables(IAsyncEnumerable<DataFeedItem> original, IReadOnlyList<string> path)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));

        ResoniteIntegratedModSettings.Log.LogDebug($"Current Path: {string.Join(" -> ", path)}");
        CurrentPath = path;

        if (path.Count == 1)
        {
            LocaleData bepisLocale = new LocaleData
            {
                LocaleCode = "en",
                Authors = new List<string> { "BepinEx" },
                Messages = new Dictionary<string, string>
                {
                    { "Settings.BepinEx.Core.Breadcrumb", "BepinEx Core Config" }
                }
            };
            SettingsLocaleHelper.Update(bepisLocale);

            DataFeedCategory bepisCategory = new DataFeedCategory();
            bepisCategory.InitBase("BepinEx.Core", path, null, "BepinEx Core Config");
            yield return bepisCategory;

            DataFeedGroup bepisGroup = new DataFeedGroup();
            bepisGroup.InitBase("BepinEx", path, null, "BepinEx");
            yield return bepisGroup;

            DataFeedGrid loadedPluginsGrid = new DataFeedGrid();
            loadedPluginsGrid.InitBase("LoadedPluginsGrid", path, ["BepinEx"], "Loaded Plugins Grid");
            yield return loadedPluginsGrid;

            string[] loadedPluginsGroup = new[] { "BepinEx", "LoadedPluginsGrid" };

            if (NetChainloader.Instance.Plugins.Count > 0)
            {
                foreach (PluginInfo plugin in NetChainloader.Instance.Plugins.Values)
                {
                    if (plugin == null) continue;

                    BepInPlugin metaData = plugin.Metadata;
                    string moduleId = metaData.GUID;

                    LocaleData localeData = new LocaleData
                    {
                        LocaleCode = "en",
                        Authors = new List<string> { "BepinEx" },
                        Messages = new Dictionary<string, string>
                        {
                            { $"Settings.{moduleId}.Breadcrumb", metaData.Name }
                        }
                    };
                    SettingsLocaleHelper.Update(localeData);

                    DataFeedCategory loadedModule = new DataFeedCategory();
                    loadedModule.InitBase(moduleId, path, loadedPluginsGroup, metaData.Name, $"{metaData.Name}\n{metaData.GUID}\n({metaData.Version})");
                    yield return loadedModule;
                }
            }
        }
        else if (path.Count == 2 && path[1] != "BepinEx.Core")
        {
            string pluginId = path[1];

            PluginInfo pluginInfo = NetChainloader.Instance.Plugins.Values.FirstOrDefault(x => x.Metadata.GUID == pluginId);

            if (pluginInfo?.Instance is not BasePlugin plugin) yield break;

            // Settings
            // DataFeedGroup settings = new DataFeedGroup();
            // settings.InitBase("Settings", path, null, "Settings");
            // yield return settings;

            // Configs
            DataFeedResettableGroup configs = new DataFeedResettableGroup();
            configs.InitBase("Configs", path, null, "Configs");
            configs.InitResetAction(a => a.Target = ResetConfigs);
            yield return configs;

            string[] configsGroup = new[] { "Configs" };

            if (plugin.Config is not { Count: > 0 })
            {
                DataFeedLabel noConfigs = new DataFeedLabel();
                noConfigs.InitBase("NoConfigs", path, configsGroup, "This Plugin has No Configs");
                yield return noConfigs;
            }

            BepInPlugin metaData = MetadataHelper.GetMetadata(plugin);

            IAsyncEnumerable<DataFeedItem> things = EnumerateConfigs(plugin.Config, metaData.Name, metaData.Version.ToString(), pluginId, configsGroup, path);
            await foreach (DataFeedItem item in things)
            {
                yield return item;
            }
        }
        else if (path.Count == 2 && path[1] == "BepinEx.Core")
        {
            // Settings
            // DataFeedGroup settings = new DataFeedGroup();
            // settings.InitBase("Settings", path, null, "Settings");
            // yield return settings;
            
            // Configs
            DataFeedResettableGroup configs = new DataFeedResettableGroup();
            configs.InitBase("Configs", path, null, "Configs");
            configs.InitResetAction(a => a.Target = ResetConfigs);
            yield return configs;

            string[] configsGroup = new[] { "Configs" };

            ConfigFile config = ConfigFile.CoreConfig;

            IAsyncEnumerable<DataFeedItem> things = EnumerateConfigs(config, "BepinEx Core Config", Utility.BepInExVersion.ToString(), "BepinEx.Core.Config", configsGroup, path);
            await foreach (DataFeedItem item in things)
            {
                yield return item;
            }
        }
        // else if (path.Count >= 3)
        // {
        //     var module = SussyLoader.LoadedModules.FirstOrDefault(a => a.HarmonyId == path[1]);
        //     switch (path.Last())
        //     {
        //         case "SaveConfig":
        //             if (module == null) break;
        // 
        //             string filename = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(module.Name), ".json");
        //             string filePath = System.IO.Path.Combine(SussyConfig.ConfigDir, filename);
        // 
        //             SussyConfig.SaveConfigToFile(module, filePath);
        //             SussyHandler.GoUpOneSetting();
        //             break;
        //         case "Unload":
        //             if (module == null) break;
        //             
        //             SussyHandler.TryUnloadModule(path[1]);
        //             SussyHandler.GoToSettingPath("SussyLoader");
        //             break;
        //     }
        // }
    }

    private static async IAsyncEnumerable<DataFeedItem> EnumerateConfigs(ConfigFile configFile, string name, string vers, string pluginId, IReadOnlyList<string> configsGroup, IReadOnlyList<string> path)
    {
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
                    configs.InitBase(section, path, configsGroup, section);
                    configs.InitResetAction(a =>
                    {
                        a.Target = ResetConfigSection;

                        Comment com = a.Slot.AttachComponent<Comment>();

                        Button but = a.Slot.GetComponentInChildren<Button>();
                        but.LocalPressed += (_, _) => com.Text.Value = $"{section}-Resetting";
                    });
                    yield return configs;
                }

                string initKey = config.Definition.Key + section;
                string key = added.Contains(initKey)
                        ? initKey + added.Count
                        : initKey;

                added.Add(key);

                string[] groupingKeys = configsGroup.Concat([section]).ToArray();

                if (valueType == _dummyType)
                {
                    DataFeedValueField<dummy> dummyField = new DataFeedValueField<dummy>();
                    dummyField.InitBase(key, path, groupingKeys, config.Definition.Key, config.Description.Description);
                    yield return dummyField;
                }
                else if (valueType == typeof(bool))
                {
                    yield return DataFeedHelpers.GenerateToggle(key, path, groupingKeys, config, configFile);
                }
                else if (valueType.IsEnum)
                {
                    DataFeedItem enumItem;

                    try
                    {
                        enumItem = (DataFeedItem)DataFeedHelpers.GenerateEnumItemsAsync.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, config, configFile]);
                    }
                    catch (Exception e)
                    {
                        ResoniteIntegratedModSettings.Log.LogError(e);
                        enumItem = new DataFeedValueField<dummy>();
                        enumItem.InitBase(key, path, groupingKeys, $"{config.Definition.Key} : {valueType}", config.Description.Description);
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
                            nullableEnumItems = (IAsyncEnumerable<DataFeedItem>)DataFeedHelpers.GenerateNullableEnumItemsAsync.MakeGenericMethod(nullableType).Invoke(null, [key, path, groupingKeys, config, configFile]);
                        }
                        catch (Exception e)
                        {
                            ResoniteIntegratedModSettings.Log.LogError(e);
                            DataFeedValueField<dummy> dummyField = new DataFeedValueField<dummy>();
                            dummyField.InitBase(key, path, groupingKeys, $"{config.Definition.Key} : {valueType}", config.Description.Description);

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
                        valueItem = (DataFeedItem)DataFeedHelpers.GenerateValueField.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, config, configFile]);
                    }
                    catch (Exception e)
                    {
                        ResoniteIntegratedModSettings.Log.LogError(e);
                        valueItem = new DataFeedValueField<dummy>();
                        valueItem.InitBase(key, path, groupingKeys, $"{config.Definition.Key} : {valueType}", config.Description.Description);
                    }

                    yield return valueItem;
                }
            }
        }

        DataFeedCategory saveCat = new DataFeedCategory();
        saveCat.InitBase("SaveConfig", path, null, $"Save Config: {name}", "Save the current selected Module's Configs");
        yield return saveCat;

        // Metadata
        DataFeedGroup modGroup = new DataFeedGroup();
        modGroup.InitBase("Metadata", path, null, "Metadata");
        yield return modGroup;

        string[] metadataGroup = new[] { "Metadata" };

        DataFeedIndicator<string> idIndicator = new DataFeedIndicator<string>();
        idIndicator.InitBase("Id", path, metadataGroup, "HarmonyId");
        idIndicator.InitSetupValue(field => field.Value = pluginId);
        yield return idIndicator;

        DataFeedIndicator<string> versionIndicator = new DataFeedIndicator<string>();
        versionIndicator.InitBase("Version", path, metadataGroup, "Version");
        versionIndicator.InitSetupValue(field => field.Value = vers);
        yield return versionIndicator;

        // DataFeedIndicator<string> authorsIndicator = new DataFeedIndicator<string>();
        // authorsIndicator.InitBase("Authors", path, metadataGroup, "Authors");
        // authorsIndicator.InitSetupValue(field => field.Value = module.Author);
        // yield return authorsIndicator;
        // 
        // DataFeedIndicator<string> descriptionIndicator = new DataFeedIndicator<string>();
        // descriptionIndicator.InitBase("Description", path, metadataGroup, "Description");
        // descriptionIndicator.InitSetupValue(field => field.Value = module.Description);
        // yield return descriptionIndicator;
    }

    private static async IAsyncEnumerable<DataFeedItem> GetDummyAsync(DataFeedItem item)
    {
        yield return item;
    }

    [SyncMethod(typeof(Delegate), null)]
    private static void ResetConfigs()
    {
        try
        {
            if (CurrentPath == null || CurrentPath.Count < 2)
            {
                ResoniteIntegratedModSettings.Log.LogWarning("ResetConfigs called with invalid path.");
                return;
            }

            ConfigFile configFile;

            string pluginId = CurrentPath[1];
            if (pluginId != "BepinEx.Core")
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

            ResoniteIntegratedModSettings.Log.LogInfo($"Configs for {pluginId} have been reset.");
        }
        catch (Exception e)
        {
            ResoniteIntegratedModSettings.Log.LogError(e);
        }
    }

    [SyncMethod(typeof(Delegate), null)]
    private static void ResetConfigSection()
    {
        try
        {
            if (CurrentPath == null || CurrentPath.Count < 2)
            {
                ResoniteIntegratedModSettings.Log.LogWarning("ResetConfigSection called with invalid path.");
                return;
            }

            Comment com = Userspace.UserspaceWorld?.RootSlot?.GetComponentInChildren<Comment>(x => x?.Text?.Value?.Contains("-Resetting") ?? false);
            if (com == null) return;

            string section = com.Text.Value.Split("-")[0];
            com.Text.Value = "";

            ConfigFile configFile;
            string pluginId = CurrentPath[1];
            if (pluginId != "BepinEx.Core")
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

            ResoniteIntegratedModSettings.Log.LogInfo($"Configs for {pluginId}-{section} have been reset.");
        }
        catch (Exception e)
        {
            ResoniteIntegratedModSettings.Log.LogError(e);
        }
    }
}