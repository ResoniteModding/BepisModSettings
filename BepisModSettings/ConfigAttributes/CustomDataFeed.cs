using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using FrooxEngine;

namespace BepisModSettings.ConfigAttributes;

public delegate IAsyncEnumerable<DataFeedItem> DataFeedMethod(IReadOnlyList<string> path, IReadOnlyList<string> groupKeys);

public sealed class CustomDataFeed(DataFeedMethod action)
{
    private readonly DataFeedMethod _action = action ?? throw new ArgumentNullException(nameof(action));

    public static DataFeedMethod GetCustomFeedMethod(ConfigEntryBase config)
    {
        if (config?.Description?.Tags == null) return null;
        foreach (object tag in config.Description.Tags)
        {
            if (tag is CustomDataFeed customDataFeed)
            {
                return customDataFeed._action;
            }

            if (tag is Func<IReadOnlyList<string>, IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> func)
            {
                return (DataFeedMethod)Delegate.CreateDelegate(typeof(DataFeedMethod), func.Target, func.Method);
            }
        }

        return null;
    }
}