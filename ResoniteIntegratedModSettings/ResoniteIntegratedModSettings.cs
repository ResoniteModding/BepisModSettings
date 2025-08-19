using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using FrooxEngine;
using HarmonyLib;

namespace ResoniteIntegratedModSettings;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class ResoniteIntegratedModSettings : BasePlugin
{
    public const string PluginName = "ResoniteIntegratedModSettings";
    public const string PluginGuid = "com.NepuShiro." + PluginName;
    public const string PluginVersion = "1.0.0";
    
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
            return path.Contains("BepinEx")
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
