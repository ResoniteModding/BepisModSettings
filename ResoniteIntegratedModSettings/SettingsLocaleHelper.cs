using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using FrooxEngine;

namespace ResoniteIntegratedModSettings;

public static class SettingsLocaleHelper
{
    private static StaticLocaleProvider _localeProvider;
    private static string _lastOverrideLocale;
    private const string OverrideLocaleString = "somethingRandomJustToMakeItChange";
    
    public static void AddLocaleString(string rawString, string localeString)
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

        Update(localeData);
    }

    private static void Update(LocaleData localeData)
    {
        UpdateDelayed(localeData);
        Settings.RegisterValueChanges<LocaleSettings>(_ => UpdateDelayed(localeData));
    }

    private static void UpdateDelayed(LocaleData localeData)
    {
        Userspace.UserspaceWorld.RunInUpdates(15, () => UpdateIntern(localeData));
    }

    private static void UpdateIntern(LocaleData localeData)
    {
        _localeProvider = Userspace.UserspaceWorld?.GetCoreLocale();
        if (_localeProvider?.Asset?.Data is null)
        {
            Userspace.UserspaceWorld?.RunSynchronously(() => UpdateIntern(localeData));
        }
        else
        {
            UpdateLocale(localeData);
        }
    }

    private static void UpdateLocale(LocaleData localeData)
    {
        string firstKey = localeData.Messages.Keys.FirstOrDefault();
        if (firstKey == null) return;

        if (_localeProvider?.Asset?.Data != null)
        {
            bool alreadyExists = _localeProvider.Asset.Data.Messages.Any(ld => ld.Key == firstKey);
            if (alreadyExists) return;
            
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
            ResoniteIntegratedModSettings.Log.LogError("Locale data is null when it shouldn't be!");
        }
    }
}