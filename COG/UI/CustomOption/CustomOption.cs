using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using AmongUs.GameOptions;
using COG.Config.Impl;
using COG.Listener;
using COG.Rpc;
using COG.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static COG.UI.CustomOption.CustomOption;
using Object = UnityEngine.Object;
using Mode = COG.Utils.WinAPI.OpenFileDialogue.OpenFileMode;
using COG.States;
using COG.Utils.WinAPI;

namespace COG.UI.CustomOption;

// Code base from
// https://github.com/TheOtherRolesAU/TheOtherRoles/blob/main/TheOtherRoles/Modules/CustomOptions.cs
[DataContract]
public sealed class CustomOption
{
    [Serializable]
    public enum CustomOptionType
    {
        General = 0,
        Impostor = 1,
        Neutral = 2,
        Crewmate = 3,
        Addons = 4
    }

    internal static bool FirstOpen = true;

    public static readonly List<CustomOption?> Options = new();

    public int Selection;

    public OptionBehaviour? OptionBehaviour;

    public readonly int DefaultSelection;

    public readonly int ID;

    public readonly bool IsHeader;

    public readonly string Name;

    public readonly CustomOption? Parent;

    public readonly object[] Selections;

    public readonly CustomOptionType Type;

    public readonly int CharacteristicCode;

    public bool Ignore;

    private static int _typeId;

    public static CustomOption? GetCustomOptionByCharacteristicCode(int characteristicCode)
        => Options.FirstOrDefault(customOption => customOption != null && customOption.CharacteristicCode == characteristicCode);

    // Option creation
    public CustomOption(bool ignore, CustomOptionType type, string name, object[] selections,
        object defaultValue, CustomOption? parent, bool isHeader)
    {
        Ignore = ignore;
        ID = _typeId;
        _typeId++;
        Name = parent == null ? name : ColorUtils.ToColorString(Color.gray, "→ ") + name;
        Selections = selections;
        var index = Array.IndexOf(selections, defaultValue);
        DefaultSelection = index >= 0 ? index : 0;
        Selection = DefaultSelection;
        Parent = parent;
        IsHeader = isHeader;
        Type = type;
        Selection = 0;
        Options.Add(this);

        CharacteristicCode = GetHashCode();
    }

    public static CustomOption Create(bool ignore, CustomOptionType type, string name, string[] selections,
        CustomOption? parent = null, bool isHeader = false)
    {
        return new CustomOption(ignore, type, name, selections, "", parent, isHeader);
    }

    public static CustomOption Create(bool ignore, CustomOptionType type, string name, float defaultValue, float min,
        float max, float step, CustomOption? parent = null, bool isHeader = false)
    {
        List<object> selections = new();
        for (var s = min; s <= max; s += step) selections.Add(s);
        return new CustomOption(ignore, type, name, selections.ToArray(), defaultValue, parent, isHeader);
    }

    public static CustomOption Create(bool ignore, CustomOptionType type, string name, bool defaultValue,
        CustomOption? parent = null, bool isHeader = false)
    {
        return new CustomOption(ignore, type, name,
            new object[] { LanguageConfig.Instance.Disable, LanguageConfig.Instance.Enable },
            defaultValue ? LanguageConfig.Instance.Enable : LanguageConfig.Instance.Disable, parent, isHeader);
    }
    
    public static void ShareConfigs(PlayerControl target)
    {
        if (PlayerUtils.GetAllPlayers().Count <= 0 || !AmongUsClient.Instance.AmHost) return;

        // 当游戏选项更改的时候调用

        var localPlayer = PlayerControl.LocalPlayer;

        // 新建写入器
        var writer = AmongUsClient.Instance.StartRpcImmediately(localPlayer.NetId, (byte)KnownRpc.ShareOptions, SendOption.Reliable, target.GetClientID());

        var sb = new StringBuilder();

        foreach (var option in from option in Options where option != null where !option.Ignore where option.Selection != option.DefaultSelection select option)
        {
            sb.Append(option.ID + "|" + option.Selection);
            sb.Append(',');
        }
        
        writer.Write(sb.ToString().RemoveLast());
        
        // id|selection,id|selection

        // OK 现在进行一个结束
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void LoadOptionFromPreset(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            using StreamReader reader = new(path, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                var optionInfo = line.Split(" ");
                var optionID = optionInfo[0];
                var optionSelection = optionInfo[1];

                var option = Options.FirstOrDefault(o => o?.ID.ToString() == optionID);
                if (option == null) continue;
                option.UpdateSelection(int.Parse(optionSelection));
            }
        }
        catch (System.Exception e)
        {
            Main.Logger.LogError("Error loading options: " + e);
        }
    }

    public static void SaveCurrentOption(string path)
    {
        try
        {
            var realPath = path.EndsWith(".cog") ? path : path + ".cog";
            using StreamWriter writer = new(realPath, false, Encoding.UTF8);
            foreach (var option in Options.Where(o => o is { Ignore: false }).OrderBy(o => o!.ID))
                writer.WriteLine(option!.ID + " " + option.Selection);
        }
        catch (System.Exception e)
        {
            Main.Logger.LogError("Error saving options: " + e);
        }
    }

    public static void SaveOptionWithDialogue()
    {
        var file = OpenFileDialogue.Open(Mode.Save, "Preset File(*.cog)\0*.cog\0\0");
        if (file.FilePath is null or "") return;
        SaveCurrentOption(file.FilePath);
    }

    public static void OpenPresetWithDialogue()
    {
        var file = OpenFileDialogue.Open(Mode.Open, "Preset File(*.cog)\0*.cog\0\0");
        if (file.FilePath is null or "") return;
        LoadOptionFromPreset(file.FilePath);
    }

    public int GetSelection()
    {
        return Selection;
    }

    public bool GetBool()
    {
        return Selection > 0;
    }

    public float GetFloat()
    {
        return (float)Selections[Selection];
    }

    public int GetQuantity()
    {
        return Selection + 1;
    }

    // Option changes
    public void UpdateSelection(int newSelection)
    {
        Selection = Mathf.Clamp((newSelection + Selections.Length) % Selections.Length, 0, Selections.Length - 1);
        if (OptionBehaviour != null && OptionBehaviour is StringOption stringOption)
        {
            stringOption.oldValue = stringOption.Value = Selection;
            stringOption.ValueText.text = Selections[Selection].ToString();

            ShareOptionChange(newSelection);
        }
    }

    public void ShareOptionChange(int newSelection)
    {
        var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId,
            (byte)KnownRpc.UpdateOption, SendOption.Reliable);
        writer.Write(ID + "|" + newSelection);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Start))]
    private class GameOptionsMenuStartPatch
    {
        public static void Postfix(GameOptionsMenu __instance)
        {
            CreateClassicTabs(__instance);
        }

        private static void CreateClassicTabs(GameOptionsMenu __instance)
        {
            var isReturn = SetNames(
                new Dictionary<string, string>
                {
                    ["COGSettings"] = LanguageConfig.Instance.GeneralSetting,
                    ["ImpostorSettings"] = LanguageConfig.Instance.ImpostorRolesSetting,
                    ["NeutralSettings"] = LanguageConfig.Instance.NeutralRolesSetting,
                    ["CrewmateSettings"] = LanguageConfig.Instance.CrewmateRolesSetting,
                    ["AddonsSettings"] = LanguageConfig.Instance.AddonsSetting
                });

            if (isReturn) return;

            // Setup COG tab
            var template = Object.FindObjectsOfType<StringOption>().FirstOrDefault();
            if (template == null) return;
            var gameSettings = GameObject.Find("Game Settings");
            var gameSettingMenu = Object.FindObjectsOfType<GameSettingMenu>().FirstOrDefault();

            var parent = gameSettings.transform.parent;
            var torSettings = Object.Instantiate(gameSettings, parent);
            var torMenu = GetMenu(torSettings, "COGSettings");

            var impostorSettings = Object.Instantiate(gameSettings, parent);
            var impostorMenu = GetMenu(impostorSettings, "ImpostorSettings");

            var neutralSettings = Object.Instantiate(gameSettings, parent);
            var neutralMenu = GetMenu(neutralSettings, "NeutralSettings");

            var crewmateSettings = Object.Instantiate(gameSettings, parent);
            var crewmateMenu = GetMenu(crewmateSettings, "CrewmateSettings");

            var addonsSettings = Object.Instantiate(gameSettings, parent);
            var modifierMenu = GetMenu(addonsSettings, "AddonsSettings");

            var roleTab = GameObject.Find("RoleTab");
            var gameTab = GameObject.Find("GameTab");

            var cogTab = Object.Instantiate(roleTab, roleTab.transform.parent);
            var cogTabHighlight = GetTabHighlight(cogTab, "COGTab", "COG.Resources.InDLL.Images.Setting.COG.png");

            var impostorTab = Object.Instantiate(roleTab, cogTab.transform);
            var impostorTabHighlight = GetTabHighlight(impostorTab, "ImpostorTab",
                "COG.Resources.InDLL.Images.Setting.Imposter.png");

            var neutralTab = Object.Instantiate(roleTab, impostorTab.transform);
            var neutralTabHighlight =
                GetTabHighlight(neutralTab, "NeutralTab", "COG.Resources.InDLL.Images.Setting.Neutral.png");

            var crewmateTab = Object.Instantiate(roleTab, neutralTab.transform);
            var crewmateTabHighlight = GetTabHighlight(crewmateTab, "CrewmateTab",
                "COG.Resources.InDLL.Images.Setting.Crewmate.png");

            var modifierTab = Object.Instantiate(roleTab, crewmateTab.transform);
            var modifierTabHighlight = GetTabHighlight(modifierTab, "ModifierTab",
                "COG.Resources.InDLL.Images.Setting.SubRole.png");

            // Position of Tab Icons
            gameTab.transform.position += Vector3.left * 3f;
            roleTab.transform.position += Vector3.left * 3f;
            cogTab.transform.position += Vector3.left * 2f;
            impostorTab.transform.localPosition = Vector3.right * 1f;
            neutralTab.transform.localPosition = Vector3.right * 1f;
            crewmateTab.transform.localPosition = Vector3.right * 1f;
            modifierTab.transform.localPosition = Vector3.right * 1f;

            var tabs = new[] { gameTab, roleTab, cogTab, impostorTab, neutralTab, crewmateTab, modifierTab };
            if (gameSettingMenu != null)
            {
                var settingsHighlightMap = new Dictionary<GameObject, SpriteRenderer>
                {
                    [gameSettingMenu.RegularGameSettings] = gameSettingMenu.GameSettingsHightlight,
                    [gameSettingMenu.RolesSettings.gameObject] = gameSettingMenu.RolesSettingsHightlight,
                    [torSettings.gameObject] = cogTabHighlight,
                    [impostorSettings.gameObject] = impostorTabHighlight,
                    [neutralSettings.gameObject] = neutralTabHighlight,
                    [crewmateSettings.gameObject] = crewmateTabHighlight,
                    [addonsSettings.gameObject] = modifierTabHighlight
                };
                for (var i = 0; i < tabs.Length; i++)
                {
                    var button = tabs[i].GetComponentInChildren<PassiveButton>();
                    if (button == null) continue;
                    var copiedIndex = i;
                    button.OnClick = new Button.ButtonClickedEvent();
                    button.OnClick.AddListener((Action)(() =>
                    {
                        if (settingsHighlightMap == null!) return;
                        SetListener(settingsHighlightMap, copiedIndex);
                    }));
                }
            }

            DestroyOptions(new List<List<OptionBehaviour>>
            {
                torMenu.GetComponentsInChildren<OptionBehaviour>().ToList(),
                impostorMenu.GetComponentsInChildren<OptionBehaviour>().ToList(),
                neutralMenu.GetComponentsInChildren<OptionBehaviour>().ToList(),
                crewmateMenu.GetComponentsInChildren<OptionBehaviour>().ToList(),
                modifierMenu.GetComponentsInChildren<OptionBehaviour>().ToList()
            });

            var torOptions = new List<OptionBehaviour>();
            var impostorOptions = new List<OptionBehaviour>();
            var neutralOptions = new List<OptionBehaviour>();
            var crewmateOptions = new List<OptionBehaviour>();
            var modifierOptions = new List<OptionBehaviour>();

            var menus = new List<Transform>
            {
                torMenu.transform, impostorMenu.transform, neutralMenu.transform, crewmateMenu.transform,
                modifierMenu.transform
            };
            var optionBehaviours = new List<List<OptionBehaviour>>
                { torOptions, impostorOptions, neutralOptions, crewmateOptions, modifierOptions };

            foreach (var option in Options.Where(option => option == null || (int)option.Type <= 4))
            {
                if (option?.OptionBehaviour == null && option != null)
                {
                    if (!option.Ignore)
                    {
                        var stringOption = Object.Instantiate(template, menus[(int)option.Type]);
                        optionBehaviours[(int)option.Type].Add(stringOption);
                        stringOption.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                        stringOption.TitleText.text = stringOption.name = option.Name;
                        if (FirstOpen)
                            stringOption.Value = stringOption.oldValue = option.Selection = option.DefaultSelection;
                        else
                            stringOption.Value = stringOption.oldValue = option.Selection;

                        stringOption.ValueText.text = option.Selections[option.Selection].ToString();

                        option.OptionBehaviour = stringOption;
                    }
                    else // 对预设用选项处理
                    {
                        var templateToggle = GameObject.Find("ResetToDefault")?.GetComponent<ToggleOption>();
                        if (!templateToggle) return;

                        var strOpt = Object.Instantiate(templateToggle, menus[(int)option.Type]);
                        strOpt!.transform.Find("CheckBox")?.gameObject.SetActive(false);
                        strOpt.TitleText.transform.localPosition = Vector3.zero;
                        strOpt.name = option.Name;

                        option.OptionBehaviour = strOpt;
                    }
                }

                if (option?.OptionBehaviour != null) option.OptionBehaviour.gameObject.SetActive(true);
            }

            SetOptions(
                new List<GameOptionsMenu> { torMenu, impostorMenu, neutralMenu, crewmateMenu, modifierMenu },
                new List<List<OptionBehaviour>>
                    { torOptions, impostorOptions, neutralOptions, crewmateOptions, modifierOptions },
                new List<GameObject>
                    { torSettings, impostorSettings, neutralSettings, crewmateSettings, addonsSettings }
            );

            AdaptTaskCount(__instance);
        }

        private static void SetListener(Dictionary<GameObject, SpriteRenderer> settingsHighlightMap, int index)
        {
            foreach (var entry in settingsHighlightMap)
            {
                if (entry.Key == null || entry.Value == null) continue;
                entry.Key.SetActive(false);
                entry.Value.enabled = false;
            }

            settingsHighlightMap.ElementAt(index).Key.SetActive(true);
            settingsHighlightMap.ElementAt(index).Value.enabled = true;
        }

        private static void DestroyOptions(List<List<OptionBehaviour>> optionBehavioursList)
        {
            foreach (var option in optionBehavioursList.SelectMany(optionBehaviours => optionBehaviours))
                Object.Destroy(option.gameObject);
        }

        private static bool SetNames(Dictionary<string, string> gameObjectNameDisplayNameMap)
        {
            foreach (var entry in gameObjectNameDisplayNameMap)
                if (GameObject.Find(entry.Key) != null)
                {
                    // Settings setup has already been performed, fixing the title of the tab and returning
                    GameObject.Find(entry.Key).transform.FindChild("GameGroup").FindChild("Text")
                        .GetComponent<TextMeshPro>().SetText(entry.Value);
                    return true;
                }

            return false;
        }

        private static GameOptionsMenu GetMenu(GameObject setting, string settingName)
        {
            var menu = setting.transform.FindChild("GameGroup").FindChild("SliderInner")
                .GetComponent<GameOptionsMenu>();
            setting.name = settingName;

            return menu;
        }

        private static SpriteRenderer GetTabHighlight(GameObject tab, string tabName, string tabSpritePath)
        {
            var tabHighlight = tab.transform.FindChild("Hat Button").FindChild("Tab Background")
                .GetComponent<SpriteRenderer>();
            tab.transform.FindChild("Hat Button").FindChild("Icon").GetComponent<SpriteRenderer>().sprite =
                ResourceUtils.LoadSprite(tabSpritePath, 100f);
            tab.name = "tabName";

            return tabHighlight;
        }

        private static SpriteRenderer GetTabHighlight(GameObject tab, string tabName, Sprite tabSprite)
        {
            var tabHighlight = tab.transform.FindChild("Hat Button").FindChild("Tab Background")
                .GetComponent<SpriteRenderer>();
            tab.transform.FindChild("Hat Button").FindChild("Icon").GetComponent<SpriteRenderer>().sprite = tabSprite;
            tab.name = "tabName";

            return tabHighlight;
        }

        private static void SetOptions(List<GameOptionsMenu> menus, List<List<OptionBehaviour>> options,
            List<GameObject> settings)
        {
            if (!(menus.Count == options.Count && options.Count == settings.Count))
            {
                Main.Logger.LogError("List counts are not equal");
                return;
            }

            for (var i = 0; i < menus.Count; i++)
            {
                menus[i].Children = options[i].ToArray();
                settings[i].gameObject.SetActive(false);
            }
        }

        private static void AdaptTaskCount(GameOptionsMenu __instance)
        {
            // Adapt task count for main options
            var commonTasksOption = __instance.Children.FirstOrDefault(x => x.name == "NumCommonTasks")
                ?.TryCast<NumberOption>();
            if (commonTasksOption != null) commonTasksOption.ValidRange = new FloatRange(0f, 4f);

            var shortTasksOption = __instance.Children.FirstOrDefault(x => x.name == "NumShortTasks")
                ?.TryCast<NumberOption>();
            if (shortTasksOption != null) shortTasksOption.ValidRange = new FloatRange(0f, 23f);

            var longTasksOption = __instance.Children.FirstOrDefault(x => x.name == "NumLongTasks")
                ?.TryCast<NumberOption>();
            if (longTasksOption != null) longTasksOption.ValidRange = new FloatRange(0f, 15f);
        }
    }
}

[HarmonyPatch(typeof(StringOption))]
public class StringOptionPatch
{
    [HarmonyPatch(nameof(StringOption.Increase))]
    [HarmonyPrefix]
    public static bool IncreasePatch(StringOption __instance)
    {
        var option = Options.FirstOrDefault(option => option?.OptionBehaviour == __instance);
        if (option == null) return true;

        option.UpdateSelection(option.Selection + 1);
        return false;
    }

    [HarmonyPatch(nameof(StringOption.Decrease))]
    [HarmonyPrefix]
    public static bool DecreasePatch(StringOption __instance)
    {
        var option = Options.FirstOrDefault(option => option?.OptionBehaviour == __instance);
        if (option == null) return true;

        option.UpdateSelection(option.Selection - 1);
        return false;
    }

    [HarmonyPatch(nameof(StringOption.OnEnable))]
    [HarmonyPrefix]
    public static bool OnEnablePatch(StringOption __instance)
    {
        var option = Options.FirstOrDefault(option =>
            option?.OptionBehaviour == __instance && !option.Ignore);
        if (option == null) return true;

        __instance.OnValueChanged = new Action<OptionBehaviour>(_ => { });
        __instance.TitleText.text = option.Name;

        //if (FirstOpen)
        //    __instance.Value = __instance.oldValue = option.Selection = option.DefaultSelection;
        //else
            __instance.Value = __instance.oldValue = option.Selection;

        __instance.ValueText.text = option.Selections[option.Selection].ToString();

        return false;
    }
}

[HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
internal class GameOptionsMenuUpdatePatch
{
    private static float _timer = 1f;
    private const float TimerForBugFix = 1f;

    public static void Postfix(GameOptionsMenu __instance)
    {
        // Return Menu Update if in normal among us settings 
        var gameSettingMenu = Object.FindObjectsOfType<GameSettingMenu>().FirstOrDefault();
        if (gameSettingMenu != null && (gameSettingMenu.RegularGameSettings.active ||
                                        gameSettingMenu.RolesSettings.gameObject.active)) return;

        __instance.GetComponentInParent<Scroller>().ContentYBounds.max = -0.5F + __instance.Children.Length * 0.55F;
        _timer += Time.deltaTime;
        _timer += Time.deltaTime;
        if (_timer < 0.1f) return;

        _timer = 0f;

        if (TimerForBugFix < 3.0f) FirstOpen = false;

        var offset = 2.75f;
        foreach (var option in Options.Where(o => o != null))
        {
            if (option != null && GameObject.Find("COGSettings") && option.Type != CustomOptionType.General)
                continue;
            if (option != null && GameObject.Find("ImpostorSettings") && option.Type != CustomOptionType.Impostor)
                continue;
            if (option != null && GameObject.Find("NeutralSettings") && option.Type != CustomOptionType.Neutral)
                continue;
            if (option != null && GameObject.Find("CrewmateSettings") && option.Type != CustomOptionType.Crewmate)
                continue;
            if (option != null && GameObject.Find("AddonsSettings") && option.Type != CustomOptionType.Addons)
                continue;
            if (option?.OptionBehaviour != null && option.OptionBehaviour.gameObject != null)
            {
                var enabled = true;
                var parent = option.Parent;
                while (enabled)
                    if (parent != null)
                    {
                        enabled = parent.Selection != 0;
                        parent = parent.Parent;
                    }
                    else
                    {
                        break;
                    }

                option.OptionBehaviour.gameObject.SetActive(enabled);
                if (enabled)
                {
                    offset -= option.IsHeader ? 0.75f : 0.5f;
                    var transform = option.OptionBehaviour.transform;
                    var localPosition = transform.localPosition;
                    localPosition = new Vector3(localPosition.x, offset, localPosition.z);
                    transform.localPosition = localPosition;
                }
            }
        }


        //每帧更新预设选项名称与按下按钮操作
        var load = (ToggleOption)GlobalCustomOption.LoadPreset.OptionBehaviour!;
        var save = (ToggleOption)GlobalCustomOption.SavePreset.OptionBehaviour!;

        load!.TitleText.text = GlobalCustomOption.LoadPreset.Name;
        save!.TitleText.text = GlobalCustomOption.SavePreset.Name;

        load.OnValueChanged = new Action<OptionBehaviour>((_) => OpenPresetWithDialogue());
        save.OnValueChanged = new Action<OptionBehaviour>((_) => SaveOptionWithDialogue());
    }
}

[HarmonyPatch]
internal class HudStringPatch
{
    [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.ToHudString))]
    private static void Postfix(ref string __result)
    {
        if (GameOptionsManager.Instance.currentGameOptions.GameMode == GameModes.HideNSeek)
            return; // Allow Vanilla Hide N Seek

        foreach (var listener in ListenerManager.GetManager().GetListeners())
            listener.OnIGameOptionsExtensionsDisplay(ref __result);
    }

    public static string GetOptByType(CustomOptionType type)
    {
        var txt = "";
        List<CustomOption> opt = new();
        foreach (var option in Options)
            if (option != null && option.Type == type && !option.Ignore)
                opt.Add(option);
        foreach (var option in opt)
        {
            // 临时解决方案
            try
            {
                txt += option.Name + ": " + option.Selections[option.Selection] + Environment.NewLine;
            }
            catch (IndexOutOfRangeException)
            {
            }
        }
        return txt;
        /*
         * FIXME
         * 房主设置更新，然后如果将已经选择打开的职业关闭，那么成员客户端就会抛这个错误
         * [Error  :Il2CppInterop] During invoking native->managed trampoline
         * Exception: System.IndexOutOfRangeException: Index was outside the bounds of the array.
         * at COG.UI.CustomOption.HudStringPatch.GetOptByType(CustomOptionType type) in D:\RiderProjects\ClashOfGods\COG\UI\CustomOption\CustomOption.cs:line 652
         * at COG.UI.SidebarText.Impl.CrewmateSettings.ForResult(String& result) in D:\RiderProjects\ClashOfGods\COG\UI\SidebarText\Impl\CrewmateSettings.cs:line 15
         * at COG.Listener.Impl.OptionListener.OnIGameOptionsExtensionsDisplay(String& result) in D:\RiderProjects\ClashOfGods\COG\Listener\Impl\OptionListener.cs:line 30
         * at COG.UI.CustomOption.HudStringPatch.Postfix(String& __result) in D:\RiderProjects\ClashOfGods\COG\UI\CustomOption\CustomOption.cs:line 641
         * at DMD<IGameOptionsExtensions::ToHudString>(IGameOptions gameOptions, Int32 numPlayers)
         * at (il2cpp -> managed) ToHudString(IntPtr , Int32 , Il2CppMethodInfo* )
         */
    }
}

[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
public static class GameOptionsNextPagePatch
{
    public static void Postfix(KeyboardJoystick __instance)
    {
        foreach (var listener in ListenerManager.GetManager().GetListeners())
            listener.OnKeyboardJoystickUpdate(__instance);
    }
}