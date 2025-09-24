using BepInEx.Configuration;
using System.Linq;

namespace BepisModSettings.ConfigAttributes;

public class HiddenConfig 
{
    public static bool IsHidden(ConfigEntryBase config)
    {
        return config?.Description?.Tags.Any(tag => tag is HiddenConfig || (tag as string) == "Hidden") ?? false;
    }
}