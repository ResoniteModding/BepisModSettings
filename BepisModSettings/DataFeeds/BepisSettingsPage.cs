using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.NET.Common;
using BepisLocaleLoader;
using Elements.Core;
using FrooxEngine;

namespace BepisModSettings.DataFeeds;

public static class BepisSettingsPage
{
    internal static async IAsyncEnumerable<DataFeedItem> Enumerate(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        DataFeedGroup plguinsGroup = new DataFeedGroup();
        plguinsGroup.InitBase("BepInExPlugins", path, null, "Settings.BepInEx.Plugins".AsLocaleKey());
        yield return plguinsGroup;

        DataFeedGrid pluginsGrid = new DataFeedGrid();
        pluginsGrid.InitBase("PluginsGrid", path, ["BepInExPlugins"], "Settings.BepInEx.LoadedPlugins".AsLocaleKey());
        yield return pluginsGrid;

        string[] loadedPluginsGroup = ["BepInExPlugins", "PluginsGrid"];

        if (NetChainloader.Instance.Plugins.Count > 0)
        {
            List<PluginInfo> sortedPlugins = new List<PluginInfo>(NetChainloader.Instance.Plugins.Values);
            sortedPlugins.Sort((a, b) => string.Compare(a.Metadata.Name, b.Metadata.Name, StringComparison.OrdinalIgnoreCase));
            foreach (PluginInfo plugin in sortedPlugins)
            {
                BepInPlugin metaData = plugin.Metadata;

                string pluginname = metaData.Name;
                string pluginGuid = metaData.GUID;

                LocaleString nameKey = pluginname;
                LocaleString description = $"{pluginname}\n{pluginGuid}\n({metaData.Version})"; // "Settings.BepInEx.Plugin.Description".AsLocaleKey(("name", pluginname), ("guid", metaData.GUID), ("version", metaData.Version));

                if (LocaleLoader.PluginsWithLocales.Contains(plugin))
                {
                    nameKey = $"Settings.{pluginGuid}".AsLocaleKey();
                    description = $"Settings.{pluginGuid}.Description".AsLocaleKey();
                }
                else
                {
                    LocaleLoader.AddLocaleString($"Settings.{pluginGuid}.Breadcrumb", pluginname, authors: PluginMetadata.AUTHORS);
                }

                DataFeedCategory loadedPlugin = new DataFeedCategory();
                loadedPlugin.InitBase(pluginGuid, path, loadedPluginsGroup, nameKey, description);
                yield return loadedPlugin;
            }
        }
        else
        {
            DataFeedLabel noPlugins = new DataFeedLabel();
            noPlugins.InitBase("NoPlugins", path, loadedPluginsGroup, "Settings.BepInEx.Plugins.NoPlugins".AsLocaleKey());
            yield return noPlugins;
        }

        DataFeedGroup coreGroup = new DataFeedGroup();
        coreGroup.InitBase("BepInExCore", path, null, "Settings.BepInEx.Core".AsLocaleKey());
        yield return coreGroup;

        string[] coreGroupParam = ["BepInExCore"];

        DataFeedCategory bepisCategory = new DataFeedCategory();
        bepisCategory.InitBase("BepInEx.Core.Config", path, coreGroupParam, "Settings.BepInEx.Core.Config".AsLocaleKey());
        yield return bepisCategory;
    }
}