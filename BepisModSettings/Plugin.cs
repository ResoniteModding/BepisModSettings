using BepInEx;
using BepInEx.Logging;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using BepisLocaleLoader;
using BepisModSettings.ConfigAttributes;
using BepisModSettings.DataFeeds;
using BepisResoniteWrapper;

namespace BepisModSettings;

[ResonitePlugin(PluginMetadata.GUID, PluginMetadata.NAME, PluginMetadata.VERSION, PluginMetadata.AUTHORS, PluginMetadata.REPOSITORY_URL)]
[BepInDependency(BepInExResoniteShim.PluginMetadata.GUID, BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(BepisLocaleLoader.PluginMetadata.GUID, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    internal new static ManualLogSource Log;

    // TODO: Add configs for specific things, like internal only etc
    // basically try to get feature parity with ResoniteModSettings

    internal static ConfigEntry<bool> ShowHidden;
    internal static ConfigEntry<bool> ShowProtected;
    internal static ConfigEntry<bool> ShowEmptyPages;

    internal static ConfigEntry<dummy> TestAction;
    internal static ConfigEntry<string> TestProtected;
    internal static ConfigEntry<string> TestHidden;
    internal static ConfigEntry<dummy> TestCustomDataFeed;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;

        ShowHidden = Config.Bind("General", "ShowHidden", false, new ConfigDescription("Whether to show hidden Configs", null, new ConfigLocale("Settings.ResoniteModding.BepisModSettings.Configs.ShowHidden", "Settings.ResoniteModding.BepisModSettings.Configs.ShowHidden.Description")));
        ShowProtected = Config.Bind("General", "ShowProtected", false, "Whether to show protected Configs");
        ShowEmptyPages = Config.Bind("General", "ShowEmptyPages", true, "Whether to show category buttons for pages which would have no content");

        TestAction = Config.Bind("Tests", "TestAction", default(dummy), new ConfigDescription("TestAction", null, new ActionConfig(() => Log.LogError("OneOfThem"))));
        TestProtected = Config.Bind("Tests", "TestProtected", "AWAWAWAWA THIS IS A TEST MESSAGE", new ConfigDescription("TestProtected", null, new ProtectedConfig()));
        TestHidden = Config.Bind("Tests", "TestHidden", "AWAWAWAWA THIS IS A TEST MESSAGE", new ConfigDescription("TestHidden", null, new HiddenConfig()));
        TestCustomDataFeed = Config.Bind("Tests", "TestCustomDataFeed", default(dummy), new ConfigDescription("TestCustomDataFeed", null, new CustomDataFeed(CustomDateFeedEnumerate)));

        HarmonyInstance.PatchAll();

        ResoniteHooks.OnEngineReady += () =>
        {
            FieldInfo categoryField = AccessTools.Field(typeof(Settings), "_categoryInfos");
            if (categoryField != null && categoryField.GetValue(null) is Dictionary<string, SettingCategoryInfo> categoryInfos)
            {
                SettingCategoryInfo bepInExCategory = new SettingCategoryInfo(new Uri("https://avatars.githubusercontent.com/u/39589027?s=200&v=4.png"), 99L);
                bepInExCategory.InitKey("BepInEx");

                categoryInfos.Add(bepInExCategory.Key, bepInExCategory);
            }
            else
            {
                Log.LogError("Failed to find _categoryInfos field in Settings.");
            }

            Engine.Current.OnShutdown += () =>
            {
                Plugin.Log.LogInfo("Running shutdown, saving configs...");

                Plugin.Log.LogDebug("Saving Config for BepInEx.Core");
                ConfigFile.CoreConfig?.Save();

                if (NetChainloader.Instance.Plugins.Count <= 0) return;
                NetChainloader.Instance.Plugins.Values.Do(x =>
                {
                    if (x.Instance is not BasePlugin plugin) return;

                    Plugin.Log.LogDebug($"Saving Config for {x.Metadata.GUID}");
                    plugin.Config?.Save();
                });
            };
        };

        Log.LogInfo($"Plugin {PluginMetadata.GUID} is loaded!");
    }

    [HarmonyPatch(typeof(SettingsDataFeed), nameof(SettingsDataFeed.Enumerate), new Type[] { typeof(IReadOnlyList<string>), typeof(IReadOnlyList<string>), typeof(string), typeof(object) })]
    private static class EnumeratorPostfix
    {
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private static IAsyncEnumerable<DataFeedItem> Postfix(IAsyncEnumerable<DataFeedItem> __result, SettingsDataFeed __instance, IReadOnlyList<string> path /*, IReadOnlyList<string> groupingKeys, string searchPhrase, object viewData*/)
        {
            try
            {
                if (!path.Contains("BepInEx")) return __result;

                DataFeedHelpers.SettingsDataFeed = __instance;

                return __instance.World.IsUserspace() ? DataFeedInjector.ReplaceEnumerable(__result, path) : DataFeedInjector.NotUserspaceEnumerator(path);
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to generate replacement for {nameof(SettingsDataFeed)}.{nameof(SettingsDataFeed.Enumerate)} - using original result.");
                Log.LogError(ex.Message);
                return __result;
            }
        }
    }

    [HarmonyPatch(typeof(SettingsDataFeed), nameof(SettingsDataFeed.PathSegmentName))]
    private static class PathSegmentNamePatch
    {
        private static bool Prefix(string pathSegment, int depth, ref LocaleString __result)
        {
            __result = depth switch
            {
                1 => $"Settings.Category.{pathSegment}".AsLocaleKey(),
                _ => $"Settings.{pathSegment}.Breadcrumb".AsLocaleKey()
            };

            return false;
        }
    }

    private static async IAsyncEnumerable<DataFeedItem> CustomDateFeedEnumerate(IReadOnlyList<string> path, IReadOnlyList<string> groupingKeys)
    {
        await Task.CompletedTask;

        DataFeedGroup group = new DataFeedGroup();
        group.InitBase("Test", path, groupingKeys, "Test");
        yield return group;

        string[] groupingKeysArray = groupingKeys.Concat(["Test"]).ToArray();

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