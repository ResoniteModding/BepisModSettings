using System;
using System.Collections.Generic;
using FrooxEngine;

namespace BepisModSettings.ConfigAttributes;

public delegate IAsyncEnumerable<DataFeedItem> DataFeedMethod(IReadOnlyList<string> path, IReadOnlyList<string> groupKeys);

public sealed class CustomDataFeed(DataFeedMethod action)
{
    private readonly DataFeedMethod _action = action ?? throw new ArgumentNullException(nameof(action));

    public IAsyncEnumerable<DataFeedItem> Build(IReadOnlyList<string> path, IReadOnlyList<string> groupKeys)
    {
        ArgumentNullException.ThrowIfNull(path);
        
        return _action(path, groupKeys);
    }
}