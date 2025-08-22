using BepInEx;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BepisModSettings;

public class DataFeedInjector
{
    private static readonly Type DummyType = typeof(dummy);

    private static IReadOnlyList<string> CurrentPath { get; set; }

    internal static async IAsyncEnumerable<DataFeedItem> ReplaceEnumerable(IReadOnlyList<string> path)
    {
        BepisModSettings.Log.LogDebug($"Current Path: {string.Join(" -> ", path)}");
        CurrentPath = path;

        // Handle root category
        if (path.Count == 1)
        {
            SettingsLocaleHelper.AddLocaleString("Settings.BepInEx.Core.Breadcrumb", "BepInEx Core Config");

            DataFeedCategory bepisCategory = new DataFeedCategory();
            bepisCategory.InitBase("BepInEx.Core", path, null, "BepInEx Core Config");
            yield return bepisCategory;

            DataFeedGroup bepisGroup = new DataFeedGroup();
            bepisGroup.InitBase("BepInEx", path, null, "BepInEx");
            yield return bepisGroup;

            DataFeedGrid loadedPluginsGrid = new DataFeedGrid();
            loadedPluginsGrid.InitBase("LoadedPluginsGrid", path, ["BepInEx"], "Loaded Plugins Grid");
            yield return loadedPluginsGrid;

            string[] loadedPluginsGroup = new[] { "BepInEx", "LoadedPluginsGrid" };

            if (NetChainloader.Instance.Plugins.Count > 0)
            {
                foreach (PluginInfo plugin in NetChainloader.Instance.Plugins.Values)
                {
                    if (plugin == null) continue;

                    BepInPlugin metaData = plugin.Metadata;
                    string moduleId = metaData.GUID;

                    SettingsLocaleHelper.AddLocaleString($"Settings.{moduleId}.Breadcrumb", metaData.Name);

                    DataFeedCategory loadedModule = new DataFeedCategory();
                    loadedModule.InitBase(moduleId, path, loadedPluginsGroup, metaData.Name, $"{metaData.Name}\n{metaData.GUID}\n({metaData.Version})");
                    yield return loadedModule;
                }
            }
        }
        // Handle plugin configs page
        else if (path.Count == 2)
        {
            string pluginId = path[1];

            ConfigFile file;
            ModMeta meta;

            if (pluginId == "BepInEx.Core")
            {
                file = ConfigFile.CoreConfig;
                meta = new ModMeta("BepInEx Core Config", Utility.BepInExVersion.ToString(), pluginId, null, null);
            }
            else
            {
                PluginInfo pluginInfo = NetChainloader.Instance.Plugins.Values.FirstOrDefault(x => x.Metadata.GUID == pluginId);
                if (pluginInfo?.Instance is not BasePlugin plugin) yield break;

                BepInPlugin metaData = MetadataHelper.GetMetadata(plugin);
                ResonitePlugin resonitePlugin = metaData as ResonitePlugin;

                file = plugin.Config;
                meta = new ModMeta(metaData.Name, metaData.Version.ToString(), pluginId, resonitePlugin?.Author, resonitePlugin?.Link);
            }
            
            if (string.IsNullOrWhiteSpace(meta.Name))
                meta.Name = "<i>Unknown</i>";
            if (string.IsNullOrWhiteSpace(meta.Version))
                meta.Version = "<i>Unknown</i>";
            if (string.IsNullOrWhiteSpace(meta.ID))
                meta.ID = "<i>Unknown</i>";

            // Settings
            // DataFeedGroup settings = new DataFeedGroup();
            // settings.InitBase("Settings", path, null, "Settings");
            // yield return settings;

            // Configs
            // DataFeedResettableGroup configs = new DataFeedResettableGroup();
            // configs.InitBase("Configs", path, null, "Configs");
            // configs.InitResetAction(a => a.Target = ResetConfigs);
            // yield return configs;

            // string[] configsGroup = new[] { "Configs" };

            if (file.Count == 0)
            {
                DataFeedLabel noConfigs = new DataFeedLabel();
                noConfigs.InitBase("NoConfigs", path, null, "This Plugin has No Configs");
                yield return noConfigs;
            }
            else
            {
                IAsyncEnumerable<DataFeedItem> things = EnumerateConfigs(file, meta, path);
                await foreach (DataFeedItem item in things)
                {
                    yield return item;
                }
            }

            // Metadata
            DataFeedGroup modGroup = new DataFeedGroup();
            modGroup.InitBase("Metadata", path, null, "Metadata");
            yield return modGroup;

            string[] metadataGroup = new[] { "Metadata" };

            DataFeedIndicator<string> idIndicator = new DataFeedIndicator<string>();
            idIndicator.InitBase("Id", path, metadataGroup, "Mod ID");
            idIndicator.InitSetupValue(field => field.Value = meta.ID);
            yield return idIndicator;

            if (!string.IsNullOrWhiteSpace(meta.Author))
            {
                DataFeedIndicator<string> authorsIndicator = new DataFeedIndicator<string>();
                authorsIndicator.InitBase("Author", path, metadataGroup, "Author(s)");
                authorsIndicator.InitSetupValue(field => field.Value = meta.Author);
                yield return authorsIndicator;
            }

            DataFeedIndicator<string> versionIndicator = new DataFeedIndicator<string>();
            versionIndicator.InitBase("Version", path, metadataGroup, "Version");
            versionIndicator.InitSetupValue(field => field.Value = meta.Version);
            yield return versionIndicator;

            if (!string.IsNullOrWhiteSpace(meta.Link) && Uri.TryCreate(meta.Link, UriKind.Absolute, out var uri))
            {
                var modHyperlink = new DataFeedAction();
                modHyperlink.InitBase("Link", path, metadataGroup, "Open Mod Page");
                modHyperlink.InitAction(syncDelegate =>
                {
                    var slot = syncDelegate?.Slot;
                    if (slot == null) return;

                    slot.AttachComponent<Hyperlink>().URL.Value = uri;
                });
                yield return modHyperlink;
            }
        }
        else if (path.Count >= 3)
        {
            if (CategoryHandlers.TryGetValue(path[^1], out Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> handler))
            {
                await foreach (DataFeedItem item in handler(path))
                {
                    yield return item;
                }
            }
            else
            {
                DataFeedLabel noConfigs = new DataFeedLabel();
                noConfigs.InitBase("InvalidCategory", path, null, "An error has occurred.");
                yield return noConfigs;
            }
        }
    }

    private static readonly Dictionary<string, Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>>> CategoryHandlers = new Dictionary<string, Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>>>();

    private record struct ModMeta(string Name, string Version, string ID, string Author, string Link);

    private static async IAsyncEnumerable<DataFeedItem> EnumerateConfigs(ConfigFile configFile, ModMeta meta, IReadOnlyList<string> path)
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
                        a.Target = ResetConfigSection;

                        Comment com = a.Slot.AttachComponent<Comment>();

                        Button but = a.Slot.GetComponentInChildren<Button>();
                        but.LocalPressed += (_, _) => com.Text.Value = $"{section}-Resetting";
                    });
                    yield return configs;
                }

                string initKey = section + "." + config.Definition.Key;
                string key = added.Contains(initKey)
                        ? initKey + added.Count
                        : initKey;

                added.Add(key);

                string[] groupingKeys = [section];

                if (valueType == DummyType)
                {
                    DataFeedValueField<dummy> dummyField = new DataFeedValueField<dummy>();
                    dummyField.InitBase(key, path, groupingKeys, config.Definition.Key, config.Description.Description);
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
                            SettingsLocaleHelper.AddLocaleString($"Settings.{key}.Breadcrumb", initKey);

                            CategoryHandlers.Add(key, path2 => (IAsyncEnumerable<DataFeedItem>)DataFeedHelpers.HandleFlagsEnumCategory.MakeGenericMethod(valueType).Invoke(null, [path2, config]));
                            enumItem = new DataFeedCategory();
                            enumItem.InitBase(key, path, groupingKeys, $"{config.Definition.Key} : {config.BoxedValue}", config.Description.Description);
                        }
                        else
                        {
                            enumItem = (DataFeedItem)DataFeedHelpers.GenerateEnumItemsAsync.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, config]);
                        }
                    }
                    catch (Exception e)
                    {
                        BepisModSettings.Log.LogError(e);
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
                            nullableEnumItems = (IAsyncEnumerable<DataFeedItem>)DataFeedHelpers.GenerateNullableEnumItemsAsync.MakeGenericMethod(nullableType).Invoke(null, [key, path, groupingKeys, config]);
                        }
                        catch (Exception e)
                        {
                            BepisModSettings.Log.LogError(e);
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
                    // TODO: See TODO in DataFeedHelpers.cs
                    DataFeedItem valueItem;

                    try
                    {
                        valueItem = (DataFeedItem)DataFeedHelpers.GenerateValueField.MakeGenericMethod(valueType).Invoke(null, [key, path, groupingKeys, config]);
                    }
                    catch (Exception e)
                    {
                        BepisModSettings.Log.LogError(e);
                        valueItem = new DataFeedValueField<dummy>();
                        valueItem.InitBase(key, path, groupingKeys, $"{config.Definition.Key} : {valueType}", config.Description.Description);
                    }

                    yield return valueItem;
                }
            }
        }

        const string groupId = "ActionsGroup";
        DataFeedGroup group = new DataFeedGroup();
        group.InitBase(groupId, path, null, "Actions");
        yield return group;
        string[] groupKeys = [groupId];

        DataFeedValueAction<string> saveAct = new DataFeedValueAction<string>();
        saveAct.InitBase("SaveConfig", path, groupKeys, "Save Config File", "Save the currently selected Plugin's Configs");
        saveAct.InitAction(syncDelegate => syncDelegate.Target = SaveConfigs, meta.ID);
        yield return saveAct;

        DataFeedValueAction<string> resetAct = new DataFeedValueAction<string>();
        resetAct.InitBase("ResetConfig", path, groupKeys, "Reset ALL Config Categories", "Reset all categories from the currently selected Plugin's Configs");
        resetAct.InitAction(syncDelegate => syncDelegate.Target = ResetConfigs, meta.ID);
        yield return resetAct;
    }

    private static async IAsyncEnumerable<DataFeedItem> GetDummyAsync(DataFeedItem item)
    {
        await Task.CompletedTask;
        
        yield return item;
    }

    [SyncMethod(typeof(Delegate), null)]
    private static void SaveConfigs(string pluginId)
    {
        BepisModSettings.Log.LogDebug($"Saving Configs for {pluginId}");
        if (pluginId == "BepInEx.Core")
        {
            ConfigFile.CoreConfig.Save();
        }
        else if (
            NetChainloader.Instance.Plugins.TryGetValue(pluginId, out var pluginInfo) &&
            pluginInfo.Instance is BasePlugin plugin
        )
        {
            plugin.Config?.Save();
        }
    }

    [SyncMethod(typeof(Delegate), null)]
    private static void ResetConfigs(string pluginId)
    {
        try
        {
            if (CurrentPath == null || CurrentPath.Count < 2)
            {
                BepisModSettings.Log.LogWarning("ResetConfigs called with invalid path.");
                return;
            }

            ConfigFile configFile;

            if (pluginId != "BepInEx.Core")
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

            BepisModSettings.Log.LogInfo($"Configs for {pluginId} have been reset.");
        }
        catch (Exception e)
        {
            BepisModSettings.Log.LogError(e);
        }
    }

    [SyncMethod(typeof(Delegate), null)]
    private static void ResetConfigSection()
    {
        try
        {
            if (CurrentPath == null || CurrentPath.Count < 2)
            {
                BepisModSettings.Log.LogWarning("ResetConfigSection called with invalid path.");
                return;
            }

            Comment com = Userspace.UserspaceWorld?.RootSlot?.GetComponentInChildren<Comment>(x => x?.Text?.Value?.Contains("-Resetting") ?? false);
            if (com == null) return;

            string section = com.Text.Value.Split("-")[0];
            com.Text.Value = "";

            ConfigFile configFile;
            string pluginId = CurrentPath[1];
            if (pluginId != "BepInEx.Core")
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

            BepisModSettings.Log.LogInfo($"Configs for {pluginId}-{section} have been reset.");
        }
        catch (Exception e)
        {
            BepisModSettings.Log.LogError(e);
        }
    }
}