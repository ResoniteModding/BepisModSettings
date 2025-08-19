using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using FrooxEngine;
using HarmonyLib;

namespace ResoniteIntegratedModSettings;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(BepInExResoniteShim.BepInExResoniteShim.GUID, BepInDependency.DependencyFlags.HardDependency)]
public class ResoniteIntegratedModSettings : BaseResonitePlugin
{
    public const string PluginName = "Resonite Integrated Mod Settings";
    public const string PluginGuid = "com.NepuShiro.ResoniteIntegratedModSettings";
    public const string PluginVersion = "1.0.0";
    public override string Author => "NepuShiro";
    public override string Link => "https://github.com/NepuShiro/ResoniteIntegratedModSettings";

    internal new static ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        
        MethodInfo targetMethod = AccessTools.Method(typeof(SettingsDataFeed), nameof(SettingsDataFeed.Enumerate), new Type[] { typeof(IReadOnlyList<string>), typeof(IReadOnlyList<string>), typeof(string), typeof(object) });
        MethodInfo postfixMethod = AccessTools.Method(typeof(ResoniteIntegratedModSettings), nameof(EnumeratePostfix));
        HarmonyInstance.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
        
        Log.LogInfo($"Plugin {PluginGuid} is loaded!");
    }
    
    private static IAsyncEnumerable<DataFeedItem> EnumeratePostfix(IAsyncEnumerable<DataFeedItem> __result, IReadOnlyList<string> path, IReadOnlyList<string> groupingKeys, string searchPhrase, object viewData)
    {
        try
        {
            return path.Contains("BepInEx")
                    ? DataFeedInjector.CombineEnumerables(__result, path)
                    : __result;
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to generate replacement for {nameof(SettingsDataFeed)}.{nameof(SettingsDataFeed.Enumerate)} - using original result.");
            Log.LogError(ex.Message);
            return __result;
        }
    }
}
