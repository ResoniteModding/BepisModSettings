using System;
using System.Collections.Generic;
using Elements.Assets;
using FrooxEngine;

namespace ResoniteIntegratedModSettings;

[AutoRegisterSetting]
[SettingCategory("BepInEx")]
public class BepisSettings : SettingComponent<BepisSettings>
{
    public override bool UserspaceOnly => true;

    protected override void OnStart()
    {
        SettingsLocaleHelper.AddLocaleString("Settings.Category.BepInEx", "BepInEx");
        SettingsLocaleHelper.AddLocaleString("Settings.BepInEx", "BepInEx");
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