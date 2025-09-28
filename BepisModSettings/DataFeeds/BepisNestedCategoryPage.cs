using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.NET.Common;
using Elements.Core;
using FrooxEngine;

namespace BepisModSettings.DataFeeds;

public static class BepisNestedCategoryPage
{
    public static event Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> CustomNestedCategoryPages;

    internal static async IAsyncEnumerable<DataFeedItem> Enumerate(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        string pluginId = path[1];

        if (BepisConfigsPage.CategoryHandlers.TryGetValue(path[^1], out Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> flagsHandler))
        {
            await foreach (DataFeedItem item in flagsHandler(path))
            {
                yield return item;
            }

            yield break;
        }

        if (NetChainloader.Instance.Plugins.Values.All(x => x.Metadata.GUID != pluginId) && pluginId != "BepInEx.Core.Config")
        {
            if (CustomNestedCategoryPages != null)
            {
                foreach (Delegate del in CustomNestedCategoryPages.GetInvocationList())
                {
                    if (del is not Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> customNestedHandler) continue;

                    await foreach (DataFeedItem item in customNestedHandler(path))
                    {
                        yield return item;
                    }
                }
            }

            yield break;
        }

        DataFeedLabel invalidCategory = new DataFeedLabel();
        invalidCategory.InitBase("InvalidCategory", path, null, "Settings.BepInEx.Plugins.Error".AsLocaleKey());
        yield return invalidCategory;
    }
}