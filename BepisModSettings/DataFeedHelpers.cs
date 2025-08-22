using BepInEx.Configuration;
using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine.UIX;

namespace BepisModSettings;

// Edited Helper code is from - https://github.com/ResoniteModdingGroup/MonkeyLoader.GamePacks.Resonite/blob/master/MonkeyLoader.Resonite.Integration/DataFeeds/Settings/ConfigSectionSettingsItems.cs
public static class DataFeedHelpers
{
    public static SettingsFacetPreset preset;

    public static readonly MethodInfo GenerateEnumItemsAsync = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateEnumItemsAsyncMethod));
    public static readonly MethodInfo GenerateNullableEnumItemsAsync = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateNullableEnumItemsAsyncMethod));
    public static readonly MethodInfo GenerateValueField = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateValueFieldMethod));
    public static readonly MethodInfo HandleFlagsEnumCategory = AccessTools.Method(typeof(DataFeedHelpers), nameof(HandleFlagsEnumCategoryMethod));

    public static DataFeedToggle GenerateToggle(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey)
    {
        DataFeedToggle toggle = new DataFeedToggle();
        toggle.InitBase($"{key}.Toggle", path, groupKeys, configKey.Definition.Key, configKey.Description.Description);
        toggle.InitSetupValue(field => field.SyncWithConfigKey(configKey));

        return toggle;
    }

    public static DataFeedValueField<T> GenerateValueFieldMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey)
    {
        DataFeedValueField<T> valueField = new DataFeedValueField<T>();
        valueField.InitBase($"{key}.{configKey.SettingType}", path, groupKeys, configKey.Definition.Key, configKey.Description.Description);
        valueField.InitSetupValue(field => field.SyncWithConfigKey(configKey));

        // TODO: See below
        // if (configKey.SettingType.IsGoodInject())
        // {
        //     preset.Slot.RunSynchronously(() => { DoInject(configKey.SettingType); });
        // }

        return valueField;
    }

    private static async IAsyncEnumerable<DataFeedItem> GenerateNullableEnumItemsAsyncMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey)
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
            if (method == null) return;

            method.Invoke(null, new object[] { configKey });
        });
        yield return nullableToggle;

        IAsyncEnumerable<DataFeedItem> enumItems = (IAsyncEnumerable<DataFeedItem>)GenerateEnumItemsAsync.MakeGenericMethod(typeof(T)).Invoke(null, [path, nullableGroupKeys, configKey]);
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

    private static DataFeedItem GenerateEnumItemsAsyncMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey)
            where T : unmanaged, Enum
    {
        DataFeedEnum<T> enumField = new DataFeedEnum<T>();
        enumField.InitBase($"{key}.Enum", path, groupKeys, configKey.Definition.Key, configKey.Description.Description);
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

    internal static async IAsyncEnumerable<DataFeedItem> HandleFlagsEnumCategoryMethod<T>(IReadOnlyList<string> path, ConfigEntryBase key) where T : Enum
    {
        await Task.CompletedTask;

        const string groupId = "FlagsGroup";
        DataFeedGroup group = new DataFeedGroup();
        group.InitBase(groupId, path, null, key.Definition.Section + "." + key.Definition.Key);
        yield return group;
        string[] groupKeys = [groupId];

        var enumType = typeof(T);
        foreach (object val in Enum.GetValues(enumType))
        {
            if (val is not Enum e) continue;
            long intValue = Convert.ToInt64(e);
            if (intValue == 0) continue; // Skip zero value, as it is not a valid flag

            string name = Enum.GetName(enumType, val);
            DataFeedToggle toggle = new DataFeedToggle();
            toggle.InitBase(name, path, groupKeys, name, $"Toggles the '{name}' enum flag.");
            toggle.InitSetupValue(field =>
            {
                bool skipNextChange = false;

                field.Value = ((T)(key.BoxedValue ?? default(T)!)).HasFlag(e);

                field.SetupChangedHandlers(FieldChanged, key, KeyChanged);

                return;

                void FieldChanged(IChangeable _)
                {
                    if (skipNextChange)
                    {
                        skipNextChange = false;
                        return;
                    }

                    long current = Convert.ToInt64(key.BoxedValue ?? default(T));
                    long newValue = field.Value
                            ? current | intValue
                            : current & ~intValue;
                    key.BoxedValue = Enum.ToObject(enumType, newValue);

                    field.World.RunSynchronously(() => { field.Value = ((T)(key.BoxedValue ?? default(T)!)).HasFlag(e); });
                }

                void KeyChanged(object sender, SettingChangedEventArgs ev)
                {
                    if (ev.ChangedSetting != key) return;

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

    // TODO: Figure out how to do this without needing a goofy component <3
    // internal static bool IsGoodInject(this Type type) => type.Name != nameof(dummy) && (type.IsEnginePrimitive() || type == typeof(Type));
    //
    // internal static void DoInject(Type theType)
    // {
    //     var templatesRoot = preset.Slot[0].FindChild("Templates");
    //     if (templatesRoot != null)
    //     {
    //         if (templatesRoot.FindChild($"Injected DataFeedValueField<{theType.Name}>") != null)
    //         {
    //             BepisModSettings.Log.LogInfo($"DataFeedValueField<{theType.Name}> already injected!");
    //             return;
    //         }
    //
    //         var template = templatesRoot.AddSlot($"Injected DataFeedValueField<{theType.Name}>", false);
    //         template.ActiveSelf = false;
    //         template.AttachComponent<LayoutElement>();
    //
    //         var ui = new UIBuilder(template);
    //         RadiantUI_Constants.SetupEditorStyle(ui);
    //
    //         ui.ForceNext = template.AttachComponent<RectTransform>();
    //         ui.HorizontalLayout(11.78908f, 11.78908f).ForceExpandWidth.Value = false;
    //
    //         ui.PushStyle();
    //         ui.Style.FlexibleWidth = 1f;
    //         var text = ui.Text("Label");
    //         ui.PopStyle();
    //
    //         text.Size.Value = 24f;
    //         text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
    //
    //         Component component;
    //         ISyncMember member;
    //         FieldInfo fieldInfo;
    //
    //         if (theType == typeof(Type))
    //         {
    //             component = template.AttachComponent<TypeField>();
    //             member = ((TypeField)component).Type;
    //             fieldInfo = component.GetSyncMemberFieldInfo("Type");
    //         }
    //         else
    //         {
    //             component = template.AttachComponent(typeof(ValueField<>).MakeGenericType(theType));
    //             member = component.GetSyncMember("Value");
    //
    //             if (member == null)
    //             {
    //                 BepisModSettings.Log.LogError($"Could not get Value sync member from attached ValueField<{theType.Name}> component!");
    //                 return;
    //             }
    //
    //             fieldInfo = component.GetSyncMemberFieldInfo("Value");
    //         }
    //
    //         ui.PushStyle();
    //         ui.Style.MinWidth = 521.36f;
    //         SyncMemberEditorBuilder.Build(member, null!, fieldInfo, ui, 0f);
    //         ui.PopStyle();
    //
    //         var memberActions = ui.Root?.GetComponentInChildren<InspectorMemberActions>()?.Slot;
    //         if (memberActions != null)
    //             memberActions.ActiveSelf = false;
    //
    //         var feedValueFieldInterface = template.AttachComponent(typeof(FeedValueFieldInterface<>).MakeGenericType(theType));
    //
    //         ((FeedItemInterface)feedValueFieldInterface).ItemName.Target = text.Content;
    //
    //         if (feedValueFieldInterface.GetSyncMember("Value") is not ISyncRef valueField)
    //             BepisModSettings.Log.LogError("Could not get Value sync member from attached FeedValueFieldInterface component!");
    //         else
    //             valueField.Target = member;
    //
    //         var innerInterfaceSlot = templatesRoot.FindChild("InnerContainerItem");
    //         if (innerInterfaceSlot != null)
    //         {
    //             var innerInterface = innerInterfaceSlot.GetComponent<FeedItemInterface>();
    //
    //             ((FeedItemInterface)feedValueFieldInterface).ParentContainer.Target = innerInterface;
    //         }
    //         else
    //         {
    //             BepisModSettings.Log.LogError("InnerContainerItem slot is null in EnsureDataFeedValueFieldTemplate!");
    //         }
    //
    //         BepisModSettings.Log.LogInfo($"Injected DataFeedValueField<{theType.Name}> template");
    //     }
    //     else
    //     {
    //         BepisModSettings.Log.LogError("Could not find Templates slot in EnsureDataFeedValueFieldTemplate!");
    //     }
    // }
}