using BepInEx.Configuration;
using System.Linq;

namespace BepisModSettings.ConfigAttributes;

public class ProtectedConfig(string maskString)
{
    public ProtectedConfig() : this("*") { }

    public string MaskString { get; } = maskString;

    public static string GetMask(ConfigEntryBase config)
    {
        if (config?.Description?.Tags == null) return null;
        foreach (var tag in config.Description.Tags)
        {
            if(tag is ProtectedConfig protectedConfig)
            {
                return protectedConfig.MaskString;
            }
            else if(tag as string == "Protected")
            {
                return "*";
            }
        }
        return null;
    }
}