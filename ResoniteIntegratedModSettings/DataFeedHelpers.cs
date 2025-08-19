using BepInEx.Configuration;
using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ResoniteIntegratedModSettings;

public static class DataFeedHelpers
{
    public static readonly MethodInfo GenerateEnumItemsAsync = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateEnumItemsAsyncMethod));
    public static readonly MethodInfo GenerateNullableEnumItemsAsync = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateNullableEnumItemsAsyncMethod));
    public static readonly MethodInfo GenerateValueField = AccessTools.Method(typeof(DataFeedHelpers), nameof(GenerateValueFieldMethod));
    public static readonly MethodInfo HandleFlagsEnumCategory = AccessTools.Method(typeof(DataFeedHelpers), nameof(HandleFlagsEnumCategoryMethod));
    
    public static DataFeedToggle GenerateToggle(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey, ConfigFile module)
    {
        DataFeedToggle toggle = new DataFeedToggle();
        toggle.InitBase($"{key}.Toggle", path, groupKeys, configKey.Definition.Key, configKey.Description.Description);
        toggle.InitSetupValue(field => field.SyncWithConfigKey(configKey, module));

        return toggle;
    }

    public static DataFeedValueField<T> GenerateValueFieldMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey, ConfigFile module)
    {
        DataFeedValueField<T> valueField = new DataFeedValueField<T>();
        valueField.InitBase($"{key}.{configKey.SettingType}", path, groupKeys, configKey.Definition.Key, configKey.Description.Description);
        valueField.InitSetupValue(field => field.SyncWithConfigKey(configKey, module));

        return valueField;
    }

    private static async IAsyncEnumerable<DataFeedItem> GenerateNullableEnumItemsAsyncMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey, ConfigFile module)
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

            method.Invoke(null, new object[] { configKey, module });
        });
        yield return nullableToggle;

        IAsyncEnumerable<DataFeedItem> enumItems = (IAsyncEnumerable<DataFeedItem>)GenerateEnumItemsAsync
            .MakeGenericMethod(typeof(T))
            .Invoke(null, [path, nullableGroupKeys, configKey]);

        await foreach (DataFeedItem item in enumItems)
            yield return item;
    }

    private static void SyncWithConfigKey<T>(this IField<T> field, ConfigEntryBase configKey, ConfigFile module)
    {
        field.Value = (T)(configKey.BoxedValue ?? default(T)!);
        
        field.SetupChangedHandlers(FieldChanged, configKey, KeyChanged);

        return;

        void FieldChanged(IChangeable _)
        {
            configKey.BoxedValue = field.Value;
            field.World.RunSynchronously(() =>
            {
                field.Value = (T)(configKey.BoxedValue ?? default(T)!);
            });
        }

        void KeyChanged(object sender, SettingChangedEventArgs e)
        {
            if (e.ChangedSetting != configKey) return;
            
            if (Equals(field.Value, e.ChangedSetting.BoxedValue))
                return;

            field.World.RunSynchronously(() => field.Value = (T)(e.ChangedSetting.BoxedValue ?? default(T)!));
        }
    }

    private static DataFeedItem GenerateEnumItemsAsyncMethod<T>(string key, IReadOnlyList<string> path, IReadOnlyList<string> groupKeys, ConfigEntryBase configKey, ConfigFile module)
        where T : unmanaged, Enum
    {
        DataFeedEnum<T> enumField = new DataFeedEnum<T>();
        enumField.InitBase($"{key}.Enum", path, groupKeys, configKey.Definition.Key, configKey.Description.Description);
        enumField.InitSetupValue(field => field.SyncWithConfigKey(configKey, module));

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

    internal static void SyncWithConfigKey<T>(this IField<T> field, string harmonyId)
    {
        Comment comment = field.FindNearestParent<Slot>()?.GetComponentOrAttach<Comment>();
        if (comment != null)
        {
            comment.Text.Value = harmonyId;
        }

        field.SetupChangedHandlers(FieldChanged);

        return;

        void FieldChanged(IChangeable _)
        {
            field.World.RunSynchronously(async void () =>
            {
                try
                {
                    if (field is not IField<bool> boolField) return;

                    string harmonyId1 = null;
                    Comment comment1 = field.FindNearestParent<Slot>()?.GetComponentOrAttach<Comment>();
                    if (comment1 != null)
                    {
                        harmonyId1 = comment1.Text.Value;
                    }

                    if (string.IsNullOrEmpty(harmonyId1)) return;

                    // boolField.Value = await SussyHandler.TryToggleAutoLoadModule(harmonyId1, boolField.Value);
                }
                catch (Exception e)
                {
                    ResoniteIntegratedModSettings.Log.LogError(e);
                }
            });
        }
    }

    private static void SetupChangedHandlers(this IField field, Action<IChangeable> fieldChangedHandler)
    {
        Component parent = field.FindNearestParent<Component>();

        field.Changed += fieldChangedHandler;
        parent.Destroyed += ParentDestroyedHandler;

        return;

        void ParentDestroyedHandler(IDestroyable _)
        {
            parent.Destroyed -= ParentDestroyedHandler;
            field.Changed -= fieldChangedHandler;
        }
    }


    internal static async IAsyncEnumerable<DataFeedItem> HandleFlagsEnumCategoryMethod<T>(IReadOnlyList<string> path, ConfigEntryBase key) where T : Enum
    {
        const string groupId = $"FlagsGroup";
        DataFeedGroup group = new DataFeedGroup();
        group.InitBase(groupId, path, null, key.Definition.Section + "." + key.Definition.Key);
        yield return group;
        string[] groupKeys = [groupId];

        var enumType = typeof(T);
        foreach (var val in Enum.GetValues(enumType))
        {
            if(val is not Enum e) continue;
            var intValue = Convert.ToInt64(e);
            if(intValue == 0) continue; // Skip zero value, as it is not a valid flag

            var name = Enum.GetName(enumType, val);
            DataFeedToggle toggle = new DataFeedToggle();
            toggle.InitBase(name, path, groupKeys, name, $"Toggles the '{name}' enum flag.");
            toggle.InitSetupValue(field =>
            {
                bool skipNextChange = false;

                field.Value = ((T) (key.BoxedValue ?? default(T)!)).HasFlag(e);

                field.SetupChangedHandlers(FieldChanged, key, KeyChanged);

                return;

                void FieldChanged(IChangeable _)
                {
                    if(skipNextChange)
                    {
                        skipNextChange = false;
                        return;
                    }
                    var current = Convert.ToInt64(key.BoxedValue ?? default(T));
                    var newValue = field.Value
                        ? current | intValue
                        : current & ~intValue;
                    key.BoxedValue = Enum.ToObject(enumType, newValue);

                    field.World.RunSynchronously(() =>
                    {
                        field.Value = ((T) (key.BoxedValue ?? default(T)!)).HasFlag(e);
                    });
                }

                void KeyChanged(object sender, SettingChangedEventArgs ev)
                {
                    if (ev.ChangedSetting != key) return;

                    var thingy = ((T) (ev.ChangedSetting.BoxedValue ?? default(T)!)).HasFlag(e);

                    if (field.Value == thingy)
                        return;

                    field.World.RunSynchronously(() => {
                        skipNextChange = true;
                        field.Value = thingy;
                    });
                }
            });
            yield return toggle;
        }
    }
}