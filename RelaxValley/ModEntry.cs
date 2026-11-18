using RelaxValley.Framework;
using RelaxValley.Framework.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewUI.Framework;
using StardewValley;

namespace RelaxValley;

public class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal static IViewEngine? ViewEngine { get; private set; }

    private MenuRegistry _menuRegistry = null!;

    /// <summary>
    /// 需要等玩家空闲（没有菜单、对话、动画）后再执行的动作。
    /// 从菜单/问题对话框的回调里不能立刻打开新对话，否则会被旧菜单的收尾流程关掉。
    /// </summary>
    private static readonly Queue<Action> PendingActions = new();

    /// <summary>把动作排到玩家空闲后的下一帧执行。</summary>
    internal static void RunWhenPlayerFree(Action action) => PendingActions.Enqueue(action);

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        I18n.Init(helper.Translation);
        _menuRegistry = new MenuRegistry(Monitor, helper);

        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

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
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
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
        if (e.Button != SButton.F3) return;

        if (Game1.activeClickableMenu is not null)
            Game1.exitActiveMenu();

        var entries = _menuRegistry.GetMainMenuEntries();
        Game1.activeClickableMenu = new MainMenu(entries, Helper.Translation, Monitor);
        Game1.playSound("bigSelect");
    }
}