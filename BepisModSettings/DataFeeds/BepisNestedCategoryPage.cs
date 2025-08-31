using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;

namespace BepisModSettings.DataFeeds;

public static class BepisNestedCategoryPage
{
    internal static async IAsyncEnumerable<DataFeedItem> Enumerate(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        if (BepisPluginPage.CategoryHandlers.TryGetValue(path[^1], out Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> handler))
        {
            await foreach (DataFeedItem item in handler(path))
            {
                yield return item;
            }
        }
        else
        {
            DataFeedLabel noConfigs = new DataFeedLabel();
            noConfigs.InitBase("InvalidCategory", path, null, "Settings.BepInEx.Plugins.Error".AsLocaleKey());
            yield return noConfigs;
        }
    }
}