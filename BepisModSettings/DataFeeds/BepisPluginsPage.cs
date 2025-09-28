using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using BepisLocaleLoader;
using BepisModSettings.ConfigAttributes;
using Elements.Core;
using FrooxEngine;

namespace BepisModSettings.DataFeeds;

public static class BepisPluginsPage
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
            Slot slot = field.FindNearestParent<Slot>();
            if (slot == null) return;

            field.Value = SearchString;
            field.Changed += _ => SearchString = field.Value;
            slot.GetComponentInParents<TextEditor>().LocalEditingFinished += _ => DataFeedHelpers.RefreshSettingsScreen(slot.GetComponentInParents<RootCategoryView>());
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
                foreach (PluginInfo pluginInfo in filteredPlugins)
                {
                    bool isEmpty = false;
                    if (pluginInfo.Instance is BasePlugin plugin) isEmpty = plugin.Config.Count == 0 || !plugin.Config.Values.Any(config => Plugin.ShowHidden.Value || !HiddenConfig.IsHidden(config));
                    BepInPlugin pMetadata = MetadataHelper.GetMetadata(pluginInfo.Instance) ?? pluginInfo.Metadata;
                    ResonitePlugin resonitePlugin = pMetadata as ResonitePlugin;

                    ModMeta metaData = new ModMeta(pMetadata.Name, pMetadata.Version.ToString(), pMetadata.GUID, resonitePlugin?.Author, resonitePlugin?.Link);

                    string pluginName = metaData.Name;
                    string pluginGuid = metaData.ID;
                    string pluginAuthor = metaData.Author;

                    LocaleString nameKey = isEmpty ? $"<color=#a8a8a8>{pluginName}</color>" : pluginName;
                    LocaleString description = $"{pluginName} ({metaData.Version}){(!string.IsNullOrEmpty(pluginAuthor) ? $"\nby \"{pluginAuthor}\"" : "")}\n\n{pluginGuid}";

                    if (LocaleLoader.PluginsWithLocales.Contains(pluginInfo))
                    {
                        nameKey = $"Settings.{pluginGuid}".AsLocaleKey();
                        description = $"Settings.{pluginGuid}.Description".AsLocaleKey();
                    }
                    else
                    {
                        LocaleLoader.AddLocaleString($"Settings.{pluginGuid}.Breadcrumb", pluginName, authors: PluginMetadata.AUTHORS);
                    }

                    if (isEmpty) nameKey = nameKey.SetFormat("<color=#a8a8a8>{0}</color>");

                    DataFeedCategory loadedPlugin = new DataFeedCategory();
                    loadedPlugin.InitBase(pluginGuid, path, loadedPluginsGroup, nameKey, description);
                    if (Plugin.SortEmptyPages.Value && isEmpty) loadedPlugin.InitSorting(1);
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
                yield return CreateNoPluginsLabel(path, loadedPluginsGroup);
            }
        }
        else
        {
            yield return CreateNoPluginsLabel(path, loadedPluginsGroup);
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

    private static DataFeedLabel CreateNoPluginsLabel(IReadOnlyList<string> path, string[] loadedPluginsGroup)
    {
        DataFeedLabel noPlugins = new DataFeedLabel();
        noPlugins.InitBase("NoPlugins", path, loadedPluginsGroup, "Settings.BepInEx.Plugins.NoPlugins".AsLocaleKey());
        return noPlugins;
    }

    private static IEnumerable<PluginInfo> FilterPlugins(List<PluginInfo> plugins, string searchString)
    {
        searchString = searchString.Trim();

        return plugins.Where(plugin =>
        {
            if (!Plugin.ShowEmptyPages.Value)
            {
                if (plugin.Instance is BasePlugin plug)
                {
                    if (plug.Config.Count == 0 || !plug.Config.Values.Any(config => Plugin.ShowHidden.Value || !HiddenConfig.IsHidden(config)))
                        return false;
                }
            }

            if (string.IsNullOrWhiteSpace(searchString))
                return true;

            BepInPlugin pMetadata = MetadataHelper.GetMetadata(plugin.Instance) ?? plugin.Metadata;
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