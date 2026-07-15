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
        Config.Bind("Tests", "TestCustomDataFeed", default(dummy), new ConfigDescription("TestCustomDataFeed", null, new CustomDataFeed(CustomDateFeedEnumerate)));
        Config.Bind("Tests", "TestRangeFloat", 0.5f, new ConfigDescription("TestRangeFloat", null, new RangeAttribute(0f, 1f)));

        Config.Bind("Debug", "OpenSettingsInspector", default(dummy), new ConfigDescription("OpenSettingsInspector", null, new HiddenConfig(), new ActionConfig(() => DataFeedHelpers.SettingsDataFeed?.Slot?.OpenInspectorForTarget())));
    }
}