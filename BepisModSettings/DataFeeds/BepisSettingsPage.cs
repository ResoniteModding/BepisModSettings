using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using BepisLocaleLoader;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;

namespace BepisModSettings.DataFeeds;

public static class BepisSettingsPage
{
    public static event Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> CustomPluginsPages;

    public static string SearchString { get; set; } = "";

    internal static async IAsyncEnumerable<DataFeedItem> Enumerate(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        DataFeedGroup searchGroup = new DataFeedGroup();
        searchGroup.InitBase("SearchGroup", path, null, "Settings.BepInEx.Search".AsLocaleKey());
        yield return searchGroup;

        string[] searchGroupParam = ["SearchGroup"];

        DataFeedValueField<string> searchField = new DataFeedValueField<string>();
        searchField.InitBase("SearchField", path, searchGroupParam, "Settings.BepInEx.SearchField".AsLocaleKey());
        searchField.InitSetupValue(field =>
        {
            field.Value = SearchString;
            field.Changed += FieldChanged;

            Slot slot = field.FindNearestParent<Slot>();
            if (slot == null) return;

            slot.GetComponentInParents<TextEditor>().LocalEditingFinished += _ => DataFeedHelpers.RefreshSettingsScreen();
            return;

            void FieldChanged(IChangeable _)
            {
                SearchString = field.Value;
            }
        });
        yield return searchField;

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

            List<PluginInfo> filteredPlugins = FilterPlugins(sortedPlugins, SearchString).ToList();
            if (filteredPlugins.Count > 0)
            {
                foreach (PluginInfo plugin in filteredPlugins)
                {
                    BepInPlugin metaData = plugin.Metadata;

                    string pluginname = metaData.Name;
                    string pluginGuid = metaData.GUID;

                    LocaleString nameKey = pluginname;
                    LocaleString description = $"{pluginname}\n{pluginGuid}\n({metaData.Version})";

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
            else if (!string.IsNullOrWhiteSpace(SearchString))
            {
                DataFeedLabel noResults = new DataFeedLabel();
                noResults.InitBase("NoSearchResults", path, loadedPluginsGroup, "Settings.BepInEx.Plugins.NoSearchResults".AsLocaleKey());
                yield return noResults;
            }
            else
            {
                DataFeedLabel noPlugins = new DataFeedLabel();
                noPlugins.InitBase("NoPlugins", path, loadedPluginsGroup, "Settings.BepInEx.Plugins.NoPlugins".AsLocaleKey());
                yield return noPlugins;
            }
        }
        else
        {
            DataFeedLabel noPlugins = new DataFeedLabel();
            noPlugins.InitBase("NoPlugins", path, loadedPluginsGroup, "Settings.BepInEx.Plugins.NoPlugins".AsLocaleKey());
            yield return noPlugins;
        }

        if (CustomPluginsPages != null)
        {
            foreach (Delegate del in CustomPluginsPages.GetInvocationList())
            {
                if (del is not Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> handler) continue;

                await foreach (DataFeedItem item in handler(path))
                {
                    yield return item;
                }
            }
        }

        DataFeedGroup coreGroup = new DataFeedGroup();
        coreGroup.InitBase("BepInExCore", path, null, "Settings.BepInEx.Core".AsLocaleKey());
        yield return coreGroup;

        string[] coreGroupParam = ["BepInExCore"];

        DataFeedCategory bepisCategory = new DataFeedCategory();
        bepisCategory.InitBase("BepInEx.Core.Config", path, coreGroupParam, "Settings.BepInEx.Core.Config".AsLocaleKey());
        yield return bepisCategory;
    }

    private static IEnumerable<PluginInfo> FilterPlugins(List<PluginInfo> plugins, string searchString)
    {
        searchString = searchString.Trim();

        return plugins.Where(plugin =>
        {
            if (!Plugin.ShowEmptyPages.Value && plugin.Instance is BasePlugin plug && plug.Config.Count == 0)
                return false;

            if (string.IsNullOrWhiteSpace(searchString))
                return true;
            
            BepInPlugin pMetadata = MetadataHelper.GetMetadata(plugin) ?? plugin.Metadata;
            ResonitePlugin resonitePlugin = pMetadata as ResonitePlugin;

            ModMeta metadata = new ModMeta(pMetadata.Name, pMetadata.Version.ToString(), pMetadata.GUID, resonitePlugin?.Author, resonitePlugin?.Link);

            if (metadata.Name.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (metadata.ID.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (metadata.Version.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(metadata.Author) && metadata.Author.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return false;
        });
    }
}