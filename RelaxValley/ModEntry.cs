using RelaxValley.Framework;
using RelaxValley.Framework.Shops;
using RelaxValley.Framework.UI;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewUI.Framework;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Menus;

namespace RelaxValley;

public class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static IViewEngine? ViewEngine { get; private set; }
    internal static ModConfig Config { get; set; } = new();

    private MenuRegistry _menuRegistry = null!;

    /// <summary>
    /// 需要等玩家空闲（没有菜单、对话、动画）后再执行的动作。
    /// 从菜单/问题对话框的回调里不能立刻打开新对话，否则会被旧菜单的收尾流程关掉。
    /// </summary>
    private static readonly Queue<Action> PendingActions = new();

    /// <summary>把动作排到玩家空闲后的下一帧执行。</summary>
    internal static void RunWhenPlayerFree(Action action) => PendingActions.Enqueue(action);

    /// <summary>公开 API：其它模组可通过 ModRegistry.GetApi&lt;IRelaxValleyApi&gt; 获取并注册菜单条目。</summary>
    public override object? GetApi() => new RelaxValleyApi(_menuRegistry);

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        I18n.Init(helper.Translation);
        Config = helper.ReadConfig<ModConfig>();
        _menuRegistry = new MenuRegistry(Monitor, helper);

        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Display.MenuChanged += OnMenuChanged;

        Monitor.Log("Relax Valley 已加载。", LogLevel.Info);

        // 临时调试：列出 Stats 里所有疑似赌场的字段
        helper.ConsoleCommands.Add("debug_casino_stats", "列出赌场相关统计字段", (cmd, args) =>
        {
            var statsType = typeof(StardewValley.Stats);
            var props = statsType.GetProperties(System.Reflection.BindingFlags.Instance |
                                                System.Reflection.BindingFlags.Public |
                                                System.Reflection.BindingFlags.NonPublic);
            foreach (var p in props)
            {
                if (p.Name.Contains("casino", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("club", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("qiCoin", StringComparison.OrdinalIgnoreCase))
                {
                    var value = p.GetValue(Game1.player.stats);
                    Monitor.Log($"[CasinoDebug] {p.Name} = {value} ({p.PropertyType.Name})", LogLevel.Info);
                }
            }
            Monitor.Log($"[CasinoDebug] Game1.player.clubCoins = {Game1.player.clubCoins}", LogLevel.Info);
        });

        // 供 config.json 的 MenuShortcutKeys 使用：列出所有可绑定快捷键的条目 Id
        helper.ConsoleCommands.Add(
            "rv_list_entries",
            "列出所有菜单条目 Id 与分类，供 config.json 的 MenuShortcutKeys 使用",
            (cmd, args) =>
            {
                if (!Context.IsWorldReady)
                {
                    Monitor.Log("请先进入存档再执行该命令。", LogLevel.Warn);
                    return;
                }

                foreach ((string id, string category) in _menuRegistry.GetAllEntrySummaries())
                    Monitor.Log($"{id} [{category}]", LogLevel.Info);
            });

        // 供诊断 F1 桥接（Lookup Anything 兼容）是否可用；发布前随其它 debug 命令一并删除
        helper.ConsoleCommands.Add("rv_f1diag", "诊断 StardewUI 的 F1 桥接状态", (cmd, args) =>
        {
            bool registered = StardewUIF1Bridge.EnsureRegistered();
            Monitor.Log($"F1 桥接诊断:\n{StardewUIF1Bridge.GetDiagnosis()}\n注册结果: {registered}", LogLevel.Info);
        });

        // 供 mod 商店 JSON 使用：列出 Data/Shops 的所有商店 ID 与拥有者条件
        helper.ConsoleCommands.Add(
            "rv_list_shops",
            "列出 Data/Shops 的商店 ID 与拥有者条件，供 assets/data/shops.json 使用",
            (cmd, args) =>
            {
                try
                {
                    var shops = Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops");
                    foreach (var pair in shops.OrderBy(p => p.Key))
                    {
                        string owners = pair.Value.Owners is null
                            ? ""
                            : string.Join(", ", pair.Value.Owners.Select(o => $"{o.Name} [{o.Condition}]"));
                        Monitor.Log($"{pair.Key} | {owners}", LogLevel.Info);
                    }

                    Monitor.Log($"共 {shops.Count} 个商店。", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"读取 Data/Shops 失败：{ex}", LogLevel.Error);
                }
            });
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        // 玩家在原版里打开过某个商店/菜单后，才允许工作室远程打开它。
        switch (e.NewMenu)
        {
            case ShopMenu shopMenu:
                ShopDiscovery.MarkVisited(shopMenu.ShopId);
                break;

            case CarpenterMenu carpenterMenu:
                ShopDiscovery.MarkVisited(
                    carpenterMenu.Builder == Game1.builder_wizard ? "WizardBuild" : "RobinBuild");
                break;

            case PurchaseAnimalsMenu:
                ShopDiscovery.MarkVisited("AnimalPurchase");
                break;

            case GeodeMenu:
                ShopDiscovery.MarkVisited("Geode");
                break;
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        // 老存档兼容：按存档进度补记“必然已经打开过”的原版商店
        ShopDiscovery.InitializeFromSave();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        // 配置与工具图标不依赖 StardewUI，先注册，保证界面 API 异常时仍可改设置
        RegisterToolbarIcon();
        RegisterGenericModConfigMenu();

        // 赋值给静态字段，MenuRegistry 读的就是这个
        ViewEngine = Helper.ModRegistry.GetApi<IViewEngine>("focustense.StardewUI");
        if (ViewEngine is null)
        {
            Monitor.Log("StardewUI API 未找到，UI 功能不可用。", LogLevel.Warn);
            return;
        }

        ViewEngine.RegisterViews($"Mods/{ModManifest.UniqueID}/Views", "assets/views");
        ViewEngine.EnableHotReloading();

        Monitor.Log("StardewUI API 已就绪。", LogLevel.Info);
    }

    /// <summary>Iconic Framework 兼容：注册一个打开本模组主菜单的工具图标（等效按主菜单键）。</summary>
    private void RegisterToolbarIcon()
    {
        var api = Helper.ModRegistry.GetApi<IIconicFrameworkApi>("furyx639.ToolbarIcons");
        if (api is null) return;

        api.AddToolbarIcon(
            ModManifest.UniqueID,
            "Maps/springobjects",
            new Rectangle(16, 48, 16, 16),   // 金核桃（(O)73），与完美手册的聊天表情区分开
            () => I18n.RelaxValley_UI_Iconic_Title(),
            () => I18n.RelaxValley_UI_Iconic_Desc(),
            () =>
            {
                if (!Context.IsPlayerFree) return;
                _menuRegistry.OpenMainMenu();
            });

        Monitor.Log("已向 Iconic Framework 注册工具图标。", LogLevel.Info);
    }

    /// <summary>可选集成：装了 GMCM 时注册配置项。</summary>
    private void RegisterGenericModConfigMenu()
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu");

        if (api is null) return;

        api.Register(
            ModManifest,
            reset: () => Config = new ModConfig(),
            save: () => Helper.WriteConfig(Config));

        api.AddKeybind(
            ModManifest,
            getValue: () => Config.OpenMenuKey,
            setValue: value => Config.OpenMenuKey = value,
            name: () => I18n.RelaxValley_Config_OpenMenu_Name(),
            tooltip: () => I18n.RelaxValley_Config_OpenMenu_Tooltip());

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.ShopProgressMode,
            setValue: value => Config.ShopProgressMode = value,
            name: () => I18n.RelaxValley_Config_ShopProgress_Name(),
            tooltip: () => I18n.RelaxValley_Config_ShopProgress_Tooltip());

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.FriendshipProgressMode,
            setValue: value => Config.FriendshipProgressMode = value,
            name: () => I18n.RelaxValley_Config_FriendshipProgress_Name(),
            tooltip: () => I18n.RelaxValley_Config_FriendshipProgress_Tooltip());

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.ElevatorProgressMode,
            setValue: value => Config.ElevatorProgressMode = value,
            name: () => I18n.RelaxValley_Config_ElevatorProgress_Name(),
            tooltip: () => I18n.RelaxValley_Config_ElevatorProgress_Tooltip());

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.EnableTalkAll,
            setValue: value => Config.EnableTalkAll = value,
            name: () => I18n.RelaxValley_Config_TalkAll_Name(),
            tooltip: () => I18n.RelaxValley_Config_TalkAll_Tooltip());

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.EnableFriendshipTeleport,
            setValue: value => Config.EnableFriendshipTeleport = value,
            name: () => I18n.RelaxValley_Config_FriendshipTeleport_Name(),
            tooltip: () => I18n.RelaxValley_Config_FriendshipTeleport_Tooltip());

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.EnableBatchGift,
            setValue: value => Config.EnableBatchGift = value,
            name: () => I18n.RelaxValley_Config_BatchGift_Name(),
            tooltip: () => I18n.RelaxValley_Config_BatchGift_Tooltip());

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.RespectVanillaGates,
            setValue: value => Config.RespectVanillaGates = value,
            name: () => I18n.RelaxValley_Config_VanillaGates_Name(),
            tooltip: () => I18n.RelaxValley_Config_VanillaGates_Tooltip());

        api.AddTextOption(
            ModManifest,
            getValue: () => string.Join("; ", Config.StatsMenuTypes),
            setValue: value => Config.StatsMenuTypes = SplitOption(value, ';'),
            name: () => I18n.RelaxValley_Config_StatsMenuTypes_Name(),
            tooltip: () => I18n.RelaxValley_Config_StatsMenuTypes_Tooltip());

        Monitor.Log("已注册 GMCM 配置项。", LogLevel.Info);
    }

    private static List<string> SplitOption(string value, char separator = ',')
    {
        var result = new List<string>();

        foreach (string part in (value ?? "").Split(separator))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0) result.Add(trimmed);
        }

        return result;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            // 读档/回标题时丢弃上一局遗留的延迟动作
            PendingActions.Clear();
            return;
        }

        if (PendingActions.Count == 0 || !Context.IsPlayerFree) return;

        while (PendingActions.Count > 0 && Context.IsPlayerFree)
        {
            Action action = PendingActions.Dequeue();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Monitor.Log($"延迟动作执行失败：{ex}", LogLevel.Error);
            }
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady) return;

        // 搜索框等文本输入控件正在接收输入时，不拦截按键
        if (Game1.keyboardDispatcher.Subscriber is not null) return;

        if (e.Button == Config.OpenMenuKey)
        {
            // 没有菜单时，过场 / 对话等非自由状态不打开，避免打断剧情
            if (Game1.activeClickableMenu is null && !Context.IsPlayerFree) return;

            if (Game1.activeClickableMenu is not null)
                Game1.exitActiveMenu();

            _menuRegistry.OpenMainMenu();
            return;
        }

        TryRunMenuShortcut(e.Button);
    }

    /// <summary>菜单条目快捷键：支持组合键，同一条目可绑定多个按键 / 组合。</summary>
    private void TryRunMenuShortcut(SButton pressed)
    {
        foreach (var (entryId, combos) in Config.MenuShortcutKeys)
        {
            foreach (string combo in combos)
            {
                if (!MatchesCombo(combo, pressed)) continue;

                if (_menuRegistry.TryRunEntryById(entryId)) return;
            }
        }
    }

    /// <summary>
    /// 组合键匹配：按下的是组合中的主键，且其余修饰键都处于按住状态。
    /// 形如 "LeftControl + H"；解析失败的配置直接忽略。
    /// </summary>
    private bool MatchesCombo(string combo, SButton pressed)
    {
        var buttons = new List<SButton>();

        foreach (string part in combo.Split('+'))
        {
            string name = part.Trim();
            if (name.Length == 0) return false;
            if (!Enum.TryParse(name, ignoreCase: true, out SButton button)) return false;
            buttons.Add(button);
        }

        if (buttons.Count == 0 || !buttons.Contains(pressed)) return false;

        foreach (SButton button in buttons)
        {
            if (button != pressed && !Helper.Input.IsDown(button)) return false;
        }

        return true;
    }
}