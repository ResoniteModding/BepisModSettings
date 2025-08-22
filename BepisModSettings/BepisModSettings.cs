using BepInEx;
using BepInEx.Logging;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.NET.Common;

namespace BepisModSettings;

// TODO: Fix the GUID in the Shim Pls thx <3
// I'm not hardcoding it.

[ResonitePlugin(Guid, Name, Version, Author, Link)]
[BepInDependency(BepInExResoniteShim.BepInExResoniteShim.GUID, BepInDependency.DependencyFlags.HardDependency)]
public class BepisModSettings : BasePlugin
{
    public const string Name = MyPluginInfo.PLUGIN_NAME;
    public const string Guid = MyPluginInfo.PLUGIN_GUID;
    public const string Version = MyPluginInfo.PLUGIN_VERSION;
    public const string Author = MyPluginInfo.PLUGIN_AUTHORS;
    public const string Link = MyPluginInfo.PLUGIN_REPOSITORY_URL;

    internal new static ManualLogSource Log;
    
    // TODO: Add configs for specific things, like internal only etc
    // basically try to get feature parity with ResoniteModSettings
    
    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        
        MethodInfo targetMethod = AccessTools.Method(typeof(SettingsDataFeed), nameof(SettingsDataFeed.Enumerate), new Type[] { typeof(IReadOnlyList<string>), typeof(IReadOnlyList<string>), typeof(string), typeof(object) });
        if (targetMethod == null)
        {
            Log.LogError("Failed to find Enumerate method in SettingsDataFeed.");
            return;
        }
        HarmonyInstance.Patch(targetMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(BepisModSettings), nameof(EnumeratePostfix))));
        
        HarmonyInstance.PatchAll();

        Log.LogInfo($"Plugin {Guid} is loaded!");
    }

    private static IAsyncEnumerable<DataFeedItem> EnumeratePostfix(SettingsDataFeed __instance, IAsyncEnumerable<DataFeedItem> __result, IReadOnlyList<string> path /*, IReadOnlyList<string> groupingKeys, string searchPhrase, object viewData*/)
    {
        try
        {
            if (!__instance.World.IsUserspace()) return __result;
            
            DataFeedHelpers.preset ??= Userspace.UserspaceWorld.RootSlot.GetComponentInChildren<SettingsFacetPreset>();
            
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