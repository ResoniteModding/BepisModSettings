using System.Collections.Generic;
using System.Threading.Tasks;
using FrooxEngine;

namespace BepisModSettings.DataFeeds;

public static class DataFeedInjector
{
    internal static IAsyncEnumerable<DataFeedItem> ReplaceEnumerable(IAsyncEnumerable<DataFeedItem> original, IReadOnlyList<string> path)
    {
        Plugin.Log.LogDebug($"Current Path: {string.Join(" -> ", path)}");

        // Handle root category
        if (path.Count == 1)
        {
            return BepisSettingsPage.Enumerate(path);
        }

        // Handle plugin configs page
        if (path.Count == 2)
        {
            return BepisPluginPage.Enumerate(path);
        }

        // Handle nested category page
        if (path.Count >= 3)
        {
            return BepisNestedCategoryPage.Enumerate(path);
        }

        return original;
    }

    internal static async IAsyncEnumerable<DataFeedItem> NotUserspaceEnumerator(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        DataFeedIndicator<string> notUserspace = new DataFeedIndicator<string>();
        notUserspace.InitBase("NotUserspace", path, null, Userspace.UserspaceWorld.GetLocalized("Settings.BepInEx.Warning"));
        notUserspace.InitSetupValue(field => field.Value = Userspace.UserspaceWorld.GetLocalized("Settings.BepInEx.NotUserspace"));
        yield return notUserspace;
    }
}