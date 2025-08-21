using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using FrooxEngine;

namespace BepisModSettings;

// Edited Locale Code is from - https://github.com/Xlinka/Project-Obsidian/blob/main/ProjectObsidian/Settings/LocaleHelper.cs
public static class SettingsLocaleHelper
{
    private static StaticLocaleProvider _localeProvider;
    private static string _lastOverrideLocale;
    private const string OverrideLocaleString = "somethingRandomJustToMakeItChange";
    
    // TODO: Figure out how to load locale strings from a file - maybe put into BepInExResoniteShim?
    public static void AddLocaleString(string rawString, string localeString, bool force = false)
    {
        LocaleData localeData = new LocaleData
        {
            LocaleCode = "en",
            Authors = new List<string> { "BepInEx" },
            Messages = new Dictionary<string, string>
            {
                { rawString, localeString }
            }
        };

        Update(localeData, force);
    }
    
    private static void Update(LocaleData localeData, bool force)
    {
        UpdateDelayed(localeData, force);
        Settings.RegisterValueChanges<LocaleSettings>(_ => UpdateDelayed(localeData, force));
    }

    private static void UpdateDelayed(LocaleData localeData, bool force)
    {
        Userspace.UserspaceWorld.RunInUpdates(15, () => UpdateIntern(localeData, force));
    }

    private static void UpdateIntern(LocaleData localeData, bool force)
    {
        _localeProvider = Userspace.UserspaceWorld?.GetCoreLocale();
        if (_localeProvider?.Asset?.Data is null)
        {
            Userspace.UserspaceWorld?.RunSynchronously(() => UpdateIntern(localeData, force));
        }
        else
        {
            UpdateLocale(localeData, force);
        }
    }

    private static void UpdateLocale(LocaleData localeData, bool force)
    {
        if (_localeProvider?.Asset?.Data != null)
        {
            if (!force)
            {
                string firstKey = localeData.Messages.Keys.FirstOrDefault();
                
                bool alreadyExists = _localeProvider.Asset.Data.Messages.Any(ld => ld.Key == firstKey);
                if (alreadyExists) return;
            }
            
            _localeProvider.Asset.Data.LoadDataAdditively(localeData);

            // force asset update for locale provider
            if (_localeProvider.OverrideLocale.Value != null && _localeProvider.OverrideLocale.Value != OverrideLocaleString)
            {
                _lastOverrideLocale = _localeProvider.OverrideLocale.Value;
            }

            _localeProvider.OverrideLocale.Value = OverrideLocaleString;
            Userspace.UserspaceWorld.RunInUpdates(1, () => { _localeProvider.OverrideLocale.Value = _lastOverrideLocale; });
        }
        else if (_localeProvider?.Asset?.Data == null)
        {
            BepisModSettings.Log.LogError("Locale data is null when it shouldn't be!");
        }
    }
}