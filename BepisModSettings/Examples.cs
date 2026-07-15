using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepisModSettings.ConfigAttributes;
using BepisModSettings.DataFeeds;
using Elements.Core;
using FrooxEngine;

namespace BepisModSettings;

public partial class Plugin
{
    public void ExampleConfigs()
    {
        Config.Bind("Tests", "TestAction", default(dummy), new ConfigDescription("TestAction", null, new ActionConfig(() => Log.LogError("OneOfThem"))));
        Config.Bind("Tests", "TestProtected", "AWAWAWAWA THIS IS A TEST MESSAGE", new ConfigDescription("TestProtected", null, new ProtectedConfig()));
        Config.Bind("Tests", "TestHidden", "AWAWAWAWA THIS IS A TEST MESSAGE", new ConfigDescription("TestHidden", null, new HiddenConfig()));
        Config.Bind("Tests", "TestCustomDataFeed", default(dummy), new ConfigDescription("TestCustomDataFeed", null, new CustomDataFeed(CustomDataFeedEnumerate)));
        Config.Bind("Tests", "TestRangeFloat", 0.5f, new ConfigDescription("TestRangeFloat", null, new RangeAttribute(0f, 1f)));

        Config.Bind("Debug", "OpenSettingsInspector", default(dummy), new ConfigDescription("OpenSettingsInspector", null, new HiddenConfig(), new ActionConfig(() => DataFeedHelpers.SettingsDataFeed?.Slot?.OpenInspectorForTarget())));
    }

    private static async IAsyncEnumerable<DataFeedItem> CustomDataFeedEnumerate(IReadOnlyList<string> path, IReadOnlyList<string> groupingKeys)
    {
        await Task.CompletedTask;

        DataFeedGroup group = DataFeedHelpers.DataFeedCollapseGroup("TestCustomDataFeed", path, groupingKeys, "TestCustomDataFeed");
        yield return group;

        string[] groupingKeysArray = groupingKeys.Concat(["TestCustomDataFeed"]).ToArray();

        DataFeedIndicator<string> indicator = new DataFeedIndicator<string>();
        indicator.InitBase("Test1", path, groupingKeysArray, "Test1");
        indicator.InitSetupValue(field => field.Value = "Test1");
        yield return indicator;

        DataFeedIndicator<string> indicator2 = new DataFeedIndicator<string>();
        indicator2.InitBase("Test2", path, groupingKeysArray, "Test2");
        indicator2.InitSetupValue(field => field.Value = "Test2");
        yield return indicator2;

        DataFeedIndicator<string> indicator3 = new DataFeedIndicator<string>();
        indicator3.InitBase("Test3", path, groupingKeysArray, "Test3");
        indicator3.InitSetupValue(field => field.Value = "Test3");
        yield return indicator3;

        DataFeedIndicator<string> indicator4 = new DataFeedIndicator<string>();
        indicator4.InitBase("Test4", path, groupingKeysArray, "Test4");
        indicator4.InitSetupValue(field => field.Value = "Test4");
        yield return indicator4;
    }
}