using System;
using System.Collections.Generic;
using Elements.Assets;
using FrooxEngine;

namespace ResoniteIntegratedModSettings;

[AutoRegisterSetting]
[SettingCategory("BepinEx")]
public class BepisSettings : SettingComponent<BepisSettings>
{
    private LocaleData _localeData;

    public override bool UserspaceOnly => true;

    protected override void OnStart()
    {
        _localeData = new LocaleData
        {
            LocaleCode = "en",
            Authors = new List<string> { "BepinEx" },
            Messages = new Dictionary<string, string>
            {
                { "Settings.Category.BepinEx", "BepinEx" },
                { "Settings.BepinEx", "BepinEx" }
            }
        };
        SettingsLocaleHelper.Update(_localeData);
    }

    protected override void InitializeSyncMembers()
    {
        base.InitializeSyncMembers();
    }

    public override ISyncMember GetSyncMember(int index)
    {
        return index switch
        {
            0 => persistent, 
            1 => updateOrder, 
            2 => EnabledField, 
            _ => throw new ArgumentOutOfRangeException(), 
        };
    }

    public static BepisSettings __New()
    {
        return new BepisSettings();
    }
}