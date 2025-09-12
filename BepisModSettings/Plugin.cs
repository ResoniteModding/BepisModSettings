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

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;

        ShowHidden = Config.Bind("General", "ShowHidden", false, "Whether to show hidden Configs - Not Implemented");
        ShowProtected = Config.Bind("General", "ShowProtected", false, "Whether to show protected Configs");

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
}