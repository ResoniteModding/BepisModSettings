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

            slot.GetComponentInParents<TextEditor>().LocalEditingFinished += _ => RefreshSettingsScreen();
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
        if (string.IsNullOrWhiteSpace(searchString))
        {
            return plugins;
        }

        string searchLower = searchString.ToLowerInvariant();

        return plugins.Where(plugin =>
        {
            BepInPlugin pMetadata = MetadataHelper.GetMetadata(plugin) ?? plugin.Metadata;
            ResonitePlugin resonitePlugin = pMetadata as ResonitePlugin;

            ModMeta metadata = new ModMeta(pMetadata.Name, pMetadata.Version.ToString(), pMetadata.GUID, resonitePlugin?.Author, resonitePlugin?.Link);

            if (metadata.Name.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (metadata.ID.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (metadata.Version.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(metadata.Author) && metadata.Author.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return false;
        });
    }

    private static bool _isUpdatingSettings;

    public static bool GoUpOneSetting()
    {
        try
        {
            RootCategoryView rcv = GetRootCategoryView();
            if (rcv == null)
            {
                Plugin.Log.LogWarning("Cannot navigate up: RootCategoryView not found or not in BepInEx settings");
                return false;
            }

            if (rcv.Path.Count <= 1)
            {
                Plugin.Log.LogInfo("Already at the root category, cannot go up further");
                return false;
            }

            string[] newPath = rcv.Path.Take(rcv.Path.Count - 1).ToArray();
            return SetCategoryPathSafe(rcv, newPath);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error navigating up one setting: {e}");
            return false;
        }
    }

    public static bool GoToSettingPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Plugin.Log.LogWarning("Cannot navigate to empty or null path");
            return false;
        }

        return GoToSettingPath(new[] { path });
    }

    public static bool GoToSettingPath(string[] path)
    {
        if (path == null || path.Length == 0)
        {
            Plugin.Log.LogWarning("Cannot navigate to null or empty path array");
            return false;
        }

        try
        {
            RootCategoryView rcv = GetRootCategoryView();
            if (rcv == null)
            {
                Plugin.Log.LogWarning($"Cannot navigate to path [{string.Join("/", path)}]: RootCategoryView not found or not in BepInEx settings");
                return false;
            }

            return SetCategoryPathSafe(rcv, path);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error navigating to path [{string.Join("/", path)}]: {e}");
            return false;
        }
    }

    public static bool RefreshSettingsScreen()
    {
        if (_isUpdatingSettings)
        {
            Plugin.Log.LogInfo("Settings refresh already in progress, skipping");
            return false;
        }

        try
        {
            _isUpdatingSettings = true;

            RootCategoryView rcv = GetRootCategoryView();
            if (rcv == null)
            {
                Plugin.Log.LogWarning("Cannot refresh settings: RootCategoryView not found or not in BepInEx settings");
                return false;
            }

            // Store the current path
            string[] currentPath = rcv.Path.ToArray();

            // Navigate to empty path to trigger refresh
            if (!SetCategoryPathSafe(rcv, new[] { string.Empty }))
            {
                Plugin.Log.LogError("Failed to navigate to empty path during refresh");
                return false;
            }

            rcv.RunInUpdates(3, () =>
            {
                try
                {
                    SetCategoryPathSafe(rcv, currentPath);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Error returning to original path after refresh: {e}");
                }
                finally
                {
                    _isUpdatingSettings = false;
                }
            });

            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error refreshing settings screen: {e}");
            _isUpdatingSettings = false;
            return false;
        }
    }

    private static bool SetCategoryPathSafe(RootCategoryView rcv, string[] path)
    {
        if (rcv == null || path == null)
        {
            Plugin.Log.LogWarning("Cannot set category path: rcv or path is null");
            return false;
        }

        try
        {
            rcv.SetCategoryPath(path);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Failed to set category path [{string.Join("/", path)}]: {e}");
            return false;
        }
    }

    private static RootCategoryView GetRootCategoryView()
    {
        try
        {
            World userspaceWorld = Userspace.UserspaceWorld;
            SettingsDataFeed settingsDataFeed = userspaceWorld?.RootSlot?.GetComponentInChildren<SettingsDataFeed>();
            RootCategoryView rootCategoryView = settingsDataFeed?.Slot?.GetComponent<RootCategoryView>();

            if (rootCategoryView?.Path == null || rootCategoryView.Path.Count == 0)
            {
                return null;
            }

            if (rootCategoryView.Path.All(p => p?.Contains("BepInEx", StringComparison.OrdinalIgnoreCase) != true))
            {
                return null;
            }

            return rootCategoryView;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error getting RootCategoryView: {e}");
            return null;
        }
    }
}