/*
 * This file is based on code from:
 * https://github.com/ResoniteModdingGroup/MonkeyLoader.GamePacks.Resonite/blob/master/MonkeyLoader.Resonite.Integration/DataFeeds/Settings/ConfigSectionSettingsItems.cs
 *
 * Original code licensed under the GNU Lesser General Public License v3.0.
 * In accordance with the LGPL v3.0, this file is redistributed under
 * the terms of the GNU General Public License v3.0, as permitted by LGPL v3.0.
 *
 * Modifications: Edited by NepuShiro and ResoniteModding contributors.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using BepisModSettings.ConfigAttributes;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;

namespace BepisModSettings.DataFeeds;

public static class DataFeedHelpers
{
    internal static SettingsDataFeed SettingsDataFeed { get; set; }

    private static RootCategoryView _rootCategoryView;

    private static RootCategoryView RootCategoryView
    {
        get
        {
            _rootCategoryView = _rootCategoryView?.FilterWorldElement() ?? SettingsDataFeed.Slot.GetComponent<RootCategoryView>();
            return _rootCategoryView;
        }
    }

    private static DataFeedItemMapper Mapper => RootCategoryView?.ItemsManager.TemplateMapper.Target.FilterWorldElement();

    internal static readonly MethodInfo GenerateEnumItemsAsync = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateEnumItemsAsyncMethod));
    internal static readonly MethodInfo GenerateNullableEnumItemsAsync = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateNullableEnumItemsAsyncMethod));
    internal static readonly MethodInfo GenerateValueField = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateValueFieldMethod));
    internal static readonly MethodInfo HandleFlagsEnumCategory = AccessTools.Method(typeof(DataFeedHelpers), nameof(HandleFlagsEnumCategoryMethod));

    internal static DataFeedToggle GenerateToggle(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, InternalLocale internalLocale, ConfigEntryBase configKey)
    {
        DataFeedToggle toggle = new DataFeedToggle();
        toggle.InitBase($"{key}.Toggle", path, groupKeys, internalLocale.Key, internalLocale.Description);
        toggle.InitSetupValue(field => field.SyncWithConfigKey(configKey));

        return toggle;
    }

    internal static DataFeedValueField<T> GenerateValueFieldMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, InternalLocale internalLocale, ConfigEntryBase configKey)
    {
        DataFeedValueField<T> valueField = new DataFeedValueField<T>();
        valueField.InitBase($"{key}.{configKey.SettingType}", path, groupKeys, internalLocale.Key, internalLocale.Description);
        valueField.InitSetupValue(field =>
        {
            if (!Plugin.ShowProtected.Value && ProtectedConfig.GetMask(configKey) is string mask)
            {
                TextField textField = field.FindNearestParent<Slot>().FindParent(x => x.Name == "DataFeedValueField<string>", 5).GetComponentInChildren<TextField>();
                textField.Text.ParseRichText.Value = false;
                textField.Editor.Target.Undo.Value = false;
                textField.Text.MaskPattern.Value = mask;
            }

            field.SyncWithConfigKey(configKey);
        });

        TryInjectNewTemplateType(configKey.SettingType);

        return valueField;
    }

    internal static DataFeedValueField<string> GenerateProxyField(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, InternalLocale internalLocale, ConfigEntryBase configKey)
    {
        DataFeedValueField<string> valueField = new DataFeedValueField<string>();
        valueField.InitBase($"{key}.{configKey.SettingType}", path, groupKeys, internalLocale.Key, internalLocale.Description);
        valueField.InitSetupValue(field => field.SyncProxyWithConfigKey(configKey));

        if (TomlTypeConverter.CanConvert(configKey.SettingType) && SettingsDataFeed != null)
        {
            SettingsDataFeed.Slot.RunSynchronously(() => InjectNewTemplateType(typeof(string)));
        }

        return valueField;
    }

    private static async IAsyncEnumerable<DataFeedItem> GenerateNullableEnumItemsAsyncMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, InternalLocale internalLocale, ConfigEntryBase configKey)
            where T : unmanaged, Enum
    {
        await Task.CompletedTask;

        DataFeedGroup nullableEnumGroup = new DataFeedGroup();
        nullableEnumGroup.InitBase($"{key}.NullableGroup", path, groupKeys, configKey.Definition.Key);
        yield return nullableEnumGroup;

        string[] nullableGroupKeys = groupKeys.Concat([$"{key}..NullableGroup"]).ToArray();

        DataFeedToggle nullableToggle = new DataFeedToggle();
        nullableToggle.InitBase($"{key}.HasValue", path, nullableGroupKeys, "?");
        nullableToggle.InitSetupValue(field =>
        {
            Slot slot = field.FindNearestParent<Slot>();

            if (slot.GetComponentInParents<FeedItemInterface>() is { } feedItemInterface)
            {
                feedItemInterface.Slot.AttachComponent<Comment>().Text.Value = configKey.Definition.Key;
            }

            MethodInfo method = AccessTools.Method(typeof(DataFeedHelpers), nameof(SyncWithNullableConfigKeyHasValue)).MakeGenericMethod(configKey.SettingType);
            method.Invoke(null, new object[] { configKey });
        });
        yield return nullableToggle;

        IAsyncEnumerable<DataFeedItem> enumItems = (IAsyncEnumerable<DataFeedItem>)GenerateEnumItemsAsync.MakeGenericMethod(typeof(T)).Invoke(null, [path, nullableGroupKeys, internalLocale, configKey]);
        if (enumItems != null)
        {
            await foreach (DataFeedItem item in enumItems)
            {
                yield return item;
            }
        }
    }

    private static void SyncWithConfigKey<T>(this IField<T> field, ConfigEntryBase configKey)
    {
        field.Value = (T)(configKey.BoxedValue ?? default(T)!);

        field.SetupChangedHandlers(FieldChanged, configKey, KeyChanged);

        return;

        void FieldChanged(IChangeable _)
        {
            configKey.BoxedValue = field.Value;
            field.World.RunSynchronously(() => { field.Value = (T)(configKey.BoxedValue ?? default(T)!); });
        }

        void KeyChanged(object sender, SettingChangedEventArgs e)
        {
            if (e.ChangedSetting != configKey) return;

            if (Equals(field.Value, e.ChangedSetting.BoxedValue))
                return;

            field.World.RunSynchronously(() => field.Value = (T)(e.ChangedSetting.BoxedValue ?? default(T)!));
        }
    }

    private static void SyncProxyWithConfigKey(this IField<string> field, ConfigEntryBase configKey)
    {
        field.Value = TomlTypeConverter.ConvertToString(configKey.BoxedValue ?? configKey.SettingType.GetDefault(), configKey.SettingType);

        field.SetupChangedHandlers(FieldChanged, configKey, KeyChanged);

        return;

        void FieldChanged(IChangeable _)
        {
            try
            {
                configKey.BoxedValue = TomlTypeConverter.ConvertToValue(field.Value, configKey.SettingType);
            }
            catch
            {
                return;
            }

            field.World.RunSynchronously(() => { field.Value = TomlTypeConverter.ConvertToString(configKey.BoxedValue ?? configKey.SettingType.GetDefault(), configKey.SettingType); });
        }

        void KeyChanged(object sender, SettingChangedEventArgs e)
        {
            if (e.ChangedSetting != configKey) return;
            string valAsStr = TomlTypeConverter.ConvertToString(e.ChangedSetting.BoxedValue ?? configKey.SettingType.GetDefault(), configKey.SettingType);
            if (Equals(field.Value, valAsStr))
                return;

            field.World.RunSynchronously(() => field.Value = valAsStr);
        }
    }

    private static DataFeedEnum<T> GenerateEnumItemsAsyncMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, InternalLocale internalLocale, ConfigEntryBase configKey)
            where T : unmanaged, Enum
    {
        DataFeedEnum<T> enumField = new DataFeedEnum<T>();
        enumField.InitBase($"{key}.Enum", path, groupKeys, internalLocale.Key, internalLocale.Description);
        enumField.InitSetupValue(field => field.SyncWithConfigKey(configKey));

        return enumField;
    }

    private static void SyncWithNullableConfigKeyHasValue<T>(this IField<bool> field, ConfigEntryBase configKey)
            where T : struct
    {
        object value = configKey.BoxedValue;
        field.Value = ((T?)value).HasValue;

        SetupChangedHandlers(field, FieldChanged, configKey, KeyChanged);
        return;

        void FieldChanged(IChangeable _)
        {
            T? newValue = field.Value ? default(T) : null;

            if (field.Value == ((T?)value).HasValue)
            {
                configKey.BoxedValue = newValue;
                return;
            }

            field.World.RunSynchronously(() => field.SetWithoutChangedHandler(((T?)value).HasValue, FieldChanged));
        }

        void KeyChanged(object sender, SettingChangedEventArgs settingChangedEventArgs)
        {
            if (settingChangedEventArgs.ChangedSetting != configKey)
                return;

            if (field.Value == ((T?)value).HasValue)
                return;

            field.World.RunSynchronously(() => field.SetWithoutChangedHandler(((T?)value).HasValue, FieldChanged));
        }
    }

    private static void SetWithoutChangedHandler<T>(this IField<T> field, T value, Action<IChangeable> changedHandler)
    {
        field.Changed -= changedHandler;
        field.Value = value;
        field.Changed += changedHandler;
    }

    private static void SetupChangedHandlers(this IField field, Action<IChangeable> fieldChangedHandler, ConfigEntryBase configKey, EventHandler<SettingChangedEventArgs> keyChangedHandler)
    {
        Component parent = field.FindNearestParent<Component>();

        field.Changed += fieldChangedHandler;
        configKey.ConfigFile.SettingChanged += keyChangedHandler;
        parent.Destroyed += ParentDestroyedHandler;
        return;

        void ParentDestroyedHandler(IDestroyable _)
        {
            parent.Destroyed -= ParentDestroyedHandler;
            configKey.ConfigFile.SettingChanged -= keyChangedHandler;
            field.Changed -= fieldChangedHandler;
        }
    }

    internal static async IAsyncEnumerable<DataFeedItem> HandleFlagsEnumCategoryMethod<T>(IReadOnlyList<string> path, ConfigEntryBase configKey) where T : Enum
    {
        await Task.CompletedTask;

        const string groupId = "FlagsGroup";
        DataFeedGroup group = new DataFeedGroup();
        group.InitBase(groupId, path, null, configKey.Definition.Section + "." + configKey.Definition.Key);
        yield return group;
        string[] groupKeys = [groupId];

        Type enumType = typeof(T);
        foreach (object val in Enum.GetValues(enumType))
        {
            if (val is not Enum e) continue;
            long intValue = Convert.ToInt64(e);
            if (intValue == 0) continue; // Skip zero value, as it is not a valid flag

            string name = Enum.GetName(enumType, val);
            DataFeedToggle toggle = new DataFeedToggle();
            toggle.InitBase(name, path, groupKeys, name, "Settings.BepInEx.Plugins.Config.FlagsEnum.Description".AsLocaleKey(("name", name)));
            toggle.InitSetupValue(field =>
            {
                bool skipNextChange = false;

                field.Value = ((T)(configKey.BoxedValue ?? default(T)!)).HasFlag(e);

                field.SetupChangedHandlers(FieldChanged, configKey, KeyChanged);

                return;

                void FieldChanged(IChangeable _)
                {
                    if (skipNextChange)
                    {
                        skipNextChange = false;
                        return;
                    }

                    long current = Convert.ToInt64(configKey.BoxedValue ?? default(T));
                    long newValue = field.Value
                            ? current | intValue
                            : current & ~intValue;
                    configKey.BoxedValue = Enum.ToObject(enumType, newValue);

                    field.World.RunSynchronously(() => { field.Value = ((T)(configKey.BoxedValue ?? default(T)!)).HasFlag(e); });
                }

                void KeyChanged(object sender, SettingChangedEventArgs ev)
                {
                    if (ev.ChangedSetting != configKey) return;

                    bool thingy = ((T)(ev.ChangedSetting.BoxedValue ?? default(T)!)).HasFlag(e);

                    if (field.Value == thingy)
                        return;

                    field.World.RunSynchronously(() =>
                    {
                        skipNextChange = true;
                        field.Value = thingy;
                    });
                }
            });
            yield return toggle;
        }
    }

    // TODO: find a better way of doing this non-destructively
    /*internal static void EnsureBetterInnerContainerItem()
    {
        try
        {
            Slot templatesRoot = Mapper.Slot.Parent?.FindChild("Templates");
            if (templatesRoot == null) return;

            Slot betterInnterInterfaceSlot = templatesRoot.FindChild("Injected - InnerContainerItem");
            if (betterInnterInterfaceSlot == null)
            {
                templatesRoot.RunSynchronously(() =>
                {
                    Slot innerInterfaceSlot = templatesRoot.FindChild("InnerContainerItem");
                    if (innerInterfaceSlot != null)
                    {
                        betterInnterInterfaceSlot = innerInterfaceSlot.Duplicate();
                        betterInnterInterfaceSlot.Name = "Injected - InnerContainerItem";
                        betterInnterInterfaceSlot.ActiveSelf = false;
                        betterInnterInterfaceSlot.PersistentSelf = false;

                        betterInnterInterfaceSlot.FindChildInHierarchy("Button")?.Parent.Destroy();

                        FeedItemInterface injectedInnerContainerItem = betterInnterInterfaceSlot.GetComponent<FeedItemInterface>();
                        templatesRoot.ForeachComponentInChildren<FeedItemInterface>(interface2 =>
                        {
                            if (interface2 == null) return;
                            if (interface2.ParentContainer.Target != null) return;

                            interface2.ParentContainer.Target = injectedInnerContainerItem;
                        });
                    }
                    else
                    {
                        Plugin.Log.LogError("InnerContainerItem slot is null in EnsureBetterInnerContainerItem!");
                    }
                });
            }

            while (templatesRoot.FindChild("Injected - InnerContainerItem") == null)
            {
                Task.Delay(10).Wait();
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error in EnsureBetterInnerContainerItem: {e}");
        }
    }*/

    public static bool IsEmpty(object instance) => instance is BasePlugin plug && IsEmpty(plug);
    public static bool IsEmpty(BasePlugin plug) => plug.Config != null && IsEmpty(plug.Config);
    public static bool IsEmpty(ConfigFile config) => config.Count == 0 || config.Values.All(configIn => !Plugin.ShowHidden.Value && HiddenConfig.IsHidden(configIn));
    
    public static bool DoesPluginExist(string pluginId) => NetChainloader.Instance.Plugins.ContainsKey(pluginId) || pluginId == "BepInEx.Core.Config";

    public static ModMeta GetMetadata(this PluginInfo pluginInfo)
    {
        BepInPlugin metadata = MetadataHelper.GetMetadata(pluginInfo.Instance) ?? pluginInfo.Metadata;
        ResonitePlugin resoPlugin = metadata as ResonitePlugin;

        string assCo = resoPlugin?.Author;
        string assUrl = resoPlugin?.Link;

        if (pluginInfo.Instance is BasePlugin plug)
        {
            assCo ??= plug.GetType().Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
            assUrl ??= plug.GetType().Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key.Contains("url", StringComparison.CurrentCultureIgnoreCase))?.Value;
        }

        return new ModMeta(metadata.Name, metadata.Version.ToString(), metadata.GUID, assCo, assUrl);
    }

    public static bool TryGetPluginData(string pluginId, out ConfigFile config, out ModMeta meta)
    {
        config = null;
        meta = default;

        if (pluginId == "BepInEx.Core.Config")
        {
            config = ConfigFile.CoreConfig;
            meta = new ModMeta("BepInEx Core Config", Utility.BepInExVersion.ToString(), "BepInEx.Core", "Bepis", "https://github.com/ResoniteModding/BepisLoader");
        }
        else if (NetChainloader.Instance.Plugins.TryGetValue(pluginId, out PluginInfo pluginInfo) && pluginInfo.Instance is BasePlugin plugin)
        {
            config = plugin.Config;
            meta = pluginInfo.GetMetadata();
        }

        return config != null;
    }

    public static async IAsyncEnumerable<DataFeedItem> AsAsyncEnumerable(this DataFeedItem item)
    {
        await Task.CompletedTask;

        yield return item;
    }

    public static bool IsTypeInjectable(this Type type) => type.Name != nameof(dummy) && (type.IsEnginePrimitive() || type == typeof(Type));

    public static bool TryInjectNewTemplateType(Type typeToInject)
    {
        if (!typeToInject.IsTypeInjectable() || SettingsDataFeed == null) return false;

        SettingsDataFeed.Slot.RunSynchronously(() => InjectNewTemplateType(typeToInject));
        return true;
    }

    private static void InjectNewTemplateType(Type typeToInject)
    {
        if (Mapper == null) return;

        if (!typeToInject.IsTypeInjectable())
        {
            Plugin.Log.LogError($"Attempted to inject unsupported editor type {typeToInject.GetNiceName()} in DoInject!");
            return;
        }

        Type dataFeedValueFieldType = typeof(DataFeedValueField<>).MakeGenericType(typeToInject);
        if (Mapper.Mappings.Any(mapping => mapping.MatchingType == dataFeedValueFieldType && mapping.Template.Target != null)) return;

        Slot templatesRoot = Mapper.Slot.Parent?.FindChild("Templates");
        if (templatesRoot != null)
        {
            bool changeIndex = false;
            DataFeedItemMapper.ItemMapping mapping = Mapper.Mappings.FirstOrDefault(mapping => mapping.MatchingType == dataFeedValueFieldType && mapping.Template.Target == null);

            if (mapping == null)
            {
                mapping = Mapper.Mappings.Add();
                mapping.MatchingType.Value = dataFeedValueFieldType;
                changeIndex = true;
            }

            Slot template = templatesRoot.AddSlot($"Injected DataFeedValueField<{typeToInject.GetNiceName()}>", false);
            template.ActiveSelf = false;
            template.AttachComponent<LayoutElement>();

            UIBuilder ui = new UIBuilder(template);
            RadiantUI_Constants.SetupEditorStyle(ui);

            ui.ForceNext = template.AttachComponent<RectTransform>();
            HorizontalLayout hori = ui.HorizontalLayout(11.78908f, 11.78908f);
            hori.ForceExpandWidth.Value = true;
            if (typeToInject != typeof(colorX))
            {
                hori.Slot.GetComponent<LayoutElement>().MinHeight.Value = 64;
            }

            ui.PushStyle();
            ui.Style.FlexibleWidth = 1f;
            Text text = ui.Text("Label");
            ui.PopStyle();

            text.Size.Value = 24f;
            text.HorizontalAlign.Value = TextHorizontalAlignment.Left;

            Component component;
            ISyncMember member;
            FieldInfo fieldInfo;

            if (typeToInject == typeof(Type))
            {
                component = template.AttachComponent<TypeField>();
                member = ((TypeField)component).Type;
                fieldInfo = component.GetSyncMemberFieldInfo("Type");
            }
            else
            {
                component = template.AttachComponent(typeof(ValueField<>).MakeGenericType(typeToInject));
                member = component.GetSyncMember("Value");

                if (member == null)
                {
                    Plugin.Log.LogError($"Could not get Value sync member from attached ValueField<{typeToInject.Name}> component!");
                    return;
                }

                fieldInfo = component.GetSyncMemberFieldInfo("Value");
            }

            ui.PushStyle();
            ui.Style.MinWidth = 521.36f;
            SyncMemberEditorBuilder.Build(member, null, fieldInfo, ui, 0f);
            ui.PopStyle();

            Slot memberActions = ui.Root?.GetComponentInChildren<InspectorMemberActions>()?.Slot;
            if (memberActions != null)
                memberActions.ActiveSelf = false;

            Component feedValueFieldInterface = template.AttachComponent(typeof(FeedValueFieldInterface<>).MakeGenericType(typeToInject));

            ((FeedItemInterface)feedValueFieldInterface).ItemName.Target = text.Content;

            if (feedValueFieldInterface.GetSyncMember("Value") is not ISyncRef valueField)
                Plugin.Log.LogError("Could not get Value sync member from attached FeedValueFieldInterface component!");
            else
                valueField.Target = member;

            Slot innerInterfaceSlot = templatesRoot.FindChild("InnerContainerItem");
            if (innerInterfaceSlot != null)
            {
                FeedItemInterface innerInterface = innerInterfaceSlot.GetComponent<FeedItemInterface>();

                ((FeedItemInterface)feedValueFieldInterface).ParentContainer.Target = innerInterface;
            }
            else
            {
                Plugin.Log.LogError("InnerContainerItem slot is null in DoInject!");
            }

            mapping.Template.Target = (FeedItemInterface)feedValueFieldInterface;

            if (changeIndex)
            {
                // Move the new mapping above the previous last element (default DataFeedItem mapping) in the list
                Mapper.Mappings.MoveToIndex(Mapper.Mappings.Count - 1, Mapper.Mappings.Count - 2);
            }
        }
        else
        {
            Plugin.Log.LogError("Could not find Templates slot in DoInject!");
        }
    }

    private static bool _isUpdatingSettings;

    public static bool GoUpOneSetting()
    {
        try
        {
            RootCategoryView rcv = GetRootCategoryView();
            if (rcv == null || rcv.Path.Count <= 1) return false;

            rcv.MoveUpInCategory();
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error navigating up one setting: {e}");
            return false;
        }
    }

    public static bool GoToSettingPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return GoToSettingPath(new[] { path });
    }

    public static bool GoToSettingPath(string[] path)
    {
        if (path == null || path.Length == 0) return false;

        try
        {
            RootCategoryView rcv = GetRootCategoryView();
            return rcv != null && SetCategoryPathSafe(rcv, path);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error navigating to path [{string.Join("/", path)}]: {e}");
            return false;
        }
    }

    public static bool RefreshSettingsScreen(RootCategoryView rcv = null)
    {
        if (_isUpdatingSettings) return false;

        try
        {
            _isUpdatingSettings = true;
            rcv ??= GetRootCategoryView();
            if (rcv == null)
            {
                _isUpdatingSettings = false;
                return false;
            }

            // Store the current path
            string[] currentPath = rcv.Path.ToArray();

            // Navigate to empty path to trigger refresh
            if (!SetCategoryPathSafe(rcv, new[] { string.Empty }))
            {
                Plugin.Log.LogError("Failed to navigate to empty path during refresh");
                _isUpdatingSettings = false;
                return false;
            }

            rcv.RunInUpdates(3, () =>
            {
                try
                {
                    // Go back to the original path
                    SetCategoryPathSafe(rcv, currentPath);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Error returning to original path after refresh: {e}");
                }
                finally
                {
                    _isUpdatingSettings = false;
                }
            });

            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error refreshing settings screen: {e}");
            _isUpdatingSettings = false;
            return false;
        }
    }

    private static bool SetCategoryPathSafe(RootCategoryView rcv, string[] path)
    {
        if (rcv == null || path == null) return false;

        try
        {
            rcv.SetCategoryPath(path);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"SetCategoryPathSafe [{string.Join("/", path)}] failed: {e}");
            return false;
        }
    }

    private static RootCategoryView GetRootCategoryView()
    {
        try
        {
            RootCategoryView rcv = RootCategoryView ?? Userspace.UserspaceWorld?.RootSlot?.GetComponentInChildren<SettingsDataFeed>()?.Slot?.GetComponent<RootCategoryView>(); // <- just for safety :)

            if (rcv?.Path == null) throw new Exception("Path is null or empty");
            return rcv;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Error getting RootCategoryView: {e}");
            return null;
        }
    }
}