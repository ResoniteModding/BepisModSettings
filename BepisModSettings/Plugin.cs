using BepInEx;
using BepInEx.Logging;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.NET.Common;

namespace BepisModSettings;

[ResonitePlugin(PluginMetadata.GUID, PluginMetadata.NAME, PluginMetadata.VERSION, PluginMetadata.AUTHORS, PluginMetadata.REPOSITORY_URL)]
[BepInDependency(BepInExResoniteShim.PluginMetadata.GUID, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    internal new static ManualLogSource Log;

    // TODO: Add configs for specific things, like internal only etc
    // basically try to get feature parity with ResoniteModSettings

    internal static ConfigEntry<bool> ShowHidden;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;

        ShowHidden = Config.Bind("General", "ShowHidden", false, "Whether to show hidden Configs");

        MethodInfo targetMethod = AccessTools.Method(typeof(SettingsDataFeed), nameof(SettingsDataFeed.Enumerate), [typeof(IReadOnlyList<string>), typeof(IReadOnlyList<string>), typeof(string), typeof(object)]);
        if (targetMethod == null)
        {
            Log.LogError("Failed to find Enumerate method in SettingsDataFeed.");
            return;
        }

        HarmonyInstance.Patch(targetMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(EnumeratePostfix))));
        HarmonyInstance.PatchAll();

        Task.Run(async () =>
        {
            while (Engine.Current == null || Userspace.UserspaceWorld == null)
            {
                await Task.Delay(10);
            }

            await Task.Delay(5000);

            if (NetChainloader.Instance.Plugins.Count <= 0) return;

            NetChainloader.Instance.Plugins.Values.Do(SettingsLocaleHelper.AddLocaleFromPlugin);
        });

        Log.LogInfo($"Plugin {PluginMetadata.GUID} is loaded!");
    }

    private static IAsyncEnumerable<DataFeedItem> EnumeratePostfix(IAsyncEnumerable<DataFeedItem> __result, IReadOnlyList<string> path /*, IReadOnlyList<string> groupingKeys, string searchPhrase, object viewData*/)
    {
        try
        {
            return path.Contains("BepInEx")
                    ? DataFeedInjector.ReplaceEnumerable(path)
                    : __result;
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to generate replacement for {nameof(SettingsDataFeed)}.{nameof(SettingsDataFeed.Enumerate)} - using original result.");
            Log.LogError(ex.Message);
            return __result;
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

    public override bool Unload()
    {
        HarmonyInstance.UnpatchSelf();
        return true;
    }
}