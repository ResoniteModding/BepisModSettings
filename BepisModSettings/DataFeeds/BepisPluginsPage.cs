using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.NET.Common;
using BepisLocaleLoader;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables;
using Renderite.Shared;

namespace BepisModSettings.DataFeeds;

public static class BepisPluginsPage
{
    public static event Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> CustomPluginsPages;

    public static string SearchString { get; set; } = "";

    internal static async IAsyncEnumerable<DataFeedItem> Enumerate(IReadOnlyList<string> path)
    {
        await Task.CompletedTask;

        DataFeedGroup searchGroup = new DataFeedGroup();
        searchGroup.InitBase("SearchGroup", path, null, "Settings.BepInEx.Search".AsLocaleKey());
        searchGroup.InitVisible(x =>
        {
            if (!x.TryFindClosestSlot(out Slot slot)) return;

            Slot vert = slot.FindParent(p => p.Name == "Vertical Layout", 2);
            if (vert == null) return;

            vert.GetComponentOrAttach<DynamicVariableSpace>(d => d.SpaceName.Value == "BepInEx").SpaceName.Value = "BepInEx";
        });
        searchGroup.InitSlotName();
        yield return searchGroup;

        string[] searchGroupParam = ["SearchGroup"];

        DataFeedValueField<string> searchField = new DataFeedValueField<string>();
        searchField.InitBase("SearchField", path, searchGroupParam, "Settings.BepInEx.SearchField".AsLocaleKey());
        searchField.InitSlotName();
        searchField.InitSetupValue(field =>
        {
            if (!field.TryFindClosestSlot(out Slot slot)) return;

            field.Value = SearchString;
            field.Changed += _ =>
            {
                SearchString = field.Value;
                Search(slot);
            };

            slot.RunInUpdates(3, () => Search(slot));
            return;

            void Search(Slot initSlot)
            {
                Slot vert = initSlot.FindSpace("BepInEx")?.Slot;
                if (vert == null || vert.ChildrenCount <= 0) return;

                foreach (Slot child1 in vert.Children)
                {
                    Slot pluginsGrid = child1.FindChildInHierarchy("Grid");
                    if (pluginsGrid == null || pluginsGrid.ChildrenCount <= 0) continue;

                    bool noResults = true;
                    foreach (Slot child in pluginsGrid.Children)
                    {
                        if (child.Name.Contains("NoSearchResults")) continue;

                        DynamicVariableSpace space = child.GetComponent<DynamicVariableSpace>();
                        if (space == null) continue;

                        bool visible = ParseThing(space, SearchString);
                        space.TryWriteValue("Visible", visible);
                        if (visible)
                        {
                            noResults = false;
                        }
                    }

                    pluginsGrid.FindChild(x => x.Name.Contains("NoSearchResults"))?.WriteDynamicVariable("Visible", noResults);
                }
                return;

                bool ParseThing(DynamicVariableSpace space, string searchString)
                {
                    if (string.IsNullOrWhiteSpace(searchString))
                        return true;

                    if (space.TryReadValue("Name", out string name) && name.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                        return true;

                    if (space.TryReadValue("ID", out string id) && id.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                        return true;

                    if (space.TryReadValue("Version", out string version) && version.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                        return true;

                    if (space.TryReadValue("Author", out string author) && !string.IsNullOrEmpty(author) && author.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                        return true;

                    return false;
                }
            }
        });
        yield return searchField;

        DataFeedResettableGroup pluginsGroup = new DataFeedResettableGroup();
        pluginsGroup.InitBase("BepInExPlugins", path, null, "Settings.BepInEx.Plugins".AsLocaleKey(), new Uri("https://avatars.githubusercontent.com/u/39589027?s=200&v=4.png"));
        pluginsGroup.InitSlotName();
        pluginsGroup.InitResetAction(x =>
        {
            if (!x.TryFindClosestSlot(out Slot slot)) return;

            BooleanValueDriver<string> bvd = slot.GetComponentInChildren<Text>().Slot.GetComponent<BooleanValueDriver<string>>();
            bvd.FalseValue.Value = "Settings.BepInEx.SaveAll";

            if (bvd.Slot.Parent.Parent.GetComponentInChildren<Image>() is { } img)
            {
                SpriteProvider spr = img.Slot.AttachComponent<SpriteProvider>();
                spr.Texture.Target = img.Slot.AttachTexture(new Uri("resdb:///2f5cc6b6d4249bfdceda48fcd3df6375d47d13614e2100a8ed5a0f511ea9c01e.webp"), wrapMode: TextureWrapMode.Clamp);
                img.Sprite.Target = spr;
            }

            if (slot.GetComponent<Button>() is not { } btn) return;
            btn.LocalPressed += (b, _) =>
            {
                Slot resetBtn = b.Slot.FindParent(x2 => x2.Name == "Reset Button");
                DataModelValueFieldStore<bool>.Store store = resetBtn?.GetComponentInChildren<DataModelValueFieldStore<bool>.Store>();
                if (store == null) return;

                if (!store.Value.Value || NetChainloader.Instance.Plugins.Count == 0) return;

                Plugin.Log.LogDebug($"Saving All Configs");
                NetChainloader.Instance.Plugins.Values.Do(x3 => (x3.Instance as BasePlugin)?.Config?.Save());
            };
        });
        yield return pluginsGroup;

        DataFeedGrid pluginsGrid = new DataFeedGrid();
        pluginsGrid.InitBase("PluginsGrid", path, ["BepInExPlugins"], "Settings.BepInEx.LoadedPlugins".AsLocaleKey());
        pluginsGrid.InitSlotName();
        yield return pluginsGrid;

        string[] loadedPluginsGroup = ["BepInExPlugins", "PluginsGrid"];

        List<(PluginInfo Plugin, bool IsEmpty)> plugins = NetChainloader.Instance.Plugins.Values.Select(plugin => (Plugin: plugin, IsEmpty: DataFeedHelpers.IsEmpty(plugin.Instance))).Where(x => !x.IsEmpty || Plugin.ShowEmptyPages.Value).OrderBy(x => x.Plugin.Metadata.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (plugins.Count == 0)
        {
            DataFeedLabel noPlugins = new DataFeedLabel();
            noPlugins.InitBase("NoPlugins", path, loadedPluginsGroup, "Settings.BepInEx.Plugins.NoPlugins".AsLocaleKey());
            noPlugins.InitSlotName();
            yield return noPlugins;
        }
        else
        {
            foreach ((PluginInfo pluginInfo, bool isEmpty) in plugins)
            {
                ModMeta metaData = pluginInfo.GetMetadata();

                string pluginName = metaData.Name;
                string pluginGuid = metaData.ID;
                string pluginAuthor = metaData.Author;
                string pluginVersion = metaData.Version;

                LocaleString nameKey = pluginName;
                LocaleString descriptionKey = $"{pluginName} ({pluginVersion}){(!string.IsNullOrEmpty(pluginAuthor) ? $"\nby \"{pluginAuthor}\"" : "")}\n\n{pluginGuid}";
                string resolvedDescription = descriptionKey.ToString();

                if (LocaleLoader.PluginsWithLocales.Contains(pluginInfo))
                {
                    nameKey = $"Settings.{pluginGuid}".AsLocaleKey();
                    descriptionKey = $"Settings.{pluginGuid}.Description".AsLocaleKey();
                    resolvedDescription = descriptionKey.content.GetFormattedLocaleString();
                }
                else
                {
                    LocaleLoader.AddLocaleString($"Settings.{pluginGuid}.Breadcrumb", pluginName, authors: PluginMetadata.AUTHORS);
                }

                if (isEmpty)
                {
                    nameKey = nameKey.SetFormat("<color=#a8a8a8>{0}</color>");
                }

                DataFeedCategory loadedPlugin = new DataFeedCategory();
                loadedPlugin.InitBase(pluginGuid, path, loadedPluginsGroup, nameKey, descriptionKey);
                loadedPlugin.InitVisible(x =>
                {
                    if (!x.TryFindClosestSlot(out Slot slot)) return;

                    EnsureSpace(slot, DynamicVariableHelper.ProcessName(pluginGuid));
                    CreateDynField(slot, "Visible", x);

                    CreateValVar(slot, "Name", pluginName);
                    CreateValVar(slot, "Description", resolvedDescription);
                    CreateValVar(slot, "ID", pluginGuid);
                    CreateValVar(slot, "Version", pluginVersion);
                    CreateValVar(slot, "Author", pluginAuthor);
                });

                if (Plugin.SortEmptyPages.Value && isEmpty) loadedPlugin.InitSorting(1);
                yield return loadedPlugin;
            }

            DataFeedLabel noResults = new DataFeedLabel();
            noResults.InitBase("NoSearchResults", path, loadedPluginsGroup, "Settings.BepInEx.Plugins.NoSearchResults".AsLocaleKey());
            noResults.InitVisible(x =>
            {
                x.Value = false;

                if (!x.TryFindClosestSlot(out Slot slot)) return;

                EnsureSpace(slot, DynamicVariableHelper.ProcessName(noResults.ItemKey));
                CreateDynField(slot, "Visible", x);

                CreateValVar(slot, "Name", noResults.ItemKey);
            });
            noResults.InitSlotName();
            yield return noResults;
        }

        if (CustomPluginsPages != null)
        {
            foreach (Delegate del in CustomPluginsPages.GetInvocationList())
            {
                if (del is not Func<IReadOnlyList<string>, IAsyncEnumerable<DataFeedItem>> handler) continue;

                await foreach (DataFeedItem item in handler(path))
                {
                    yield return item;
                }
            }
        }

        DataFeedGroup coreGroup = new DataFeedGroup();
        coreGroup.InitBase("BepInExCore", path, null, "Settings.BepInEx.Core".AsLocaleKey());
        coreGroup.InitSlotName();
        yield return coreGroup;

        string[] coreGroupParam = ["BepInExCore"];

        DataFeedCategory bepisCategory = new DataFeedCategory();
        bepisCategory.InitBase("BepInEx.Core.Config", path, coreGroupParam, "Settings.BepInEx.Core.Config".AsLocaleKey());
        bepisCategory.InitSlotName();
        yield return bepisCategory;

        void EnsureSpace(Slot slot, string spaceName)
        {
            DynamicVariableSpace space = slot.GetComponentOrAttach<DynamicVariableSpace>(d => d.SpaceName.Value == spaceName);
            space.SpaceName.Value = spaceName;
        }

        void CreateDynField<T>(Slot slot, string name, IField<T> value)
        {
            DynamicField<T> dynField = slot.GetComponentOrAttach<DynamicField<T>>(d => d.VariableName.Value == name);
            dynField.VariableName.Value = name;
            dynField.TargetField.Target = value;
        }

        void CreateValVar<T>(Slot slot, string name, T value)
        {
            DynamicValueVariable<T> valueVariable = slot.GetComponentOrAttach<DynamicValueVariable<T>>(d => d.VariableName.Value == name);
            valueVariable.VariableName.Value = name;
            valueVariable.Value.Value = value;
        }
    }
}