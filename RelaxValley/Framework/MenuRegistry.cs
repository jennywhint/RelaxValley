using RelaxValley.Framework.Shops;
using RelaxValley.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewUI.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley.Objects;
using StardewValley.WorldMaps;

namespace RelaxValley.Framework;

public sealed class MenuRegistry
{
    private const int TotalGoldenWalnuts = 130;

    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;
    private readonly ShopRegistry _shops;

    /// <summary>外部模组通过公开 API 注册的条目（显示在工作室「模组支持」分类）。</summary>
    private readonly List<MenuEntry> _externalEntries = new();
    private readonly HashSet<string> _externalIds = new(StringComparer.OrdinalIgnoreCase);

    public MenuRegistry(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        _helper = helper;
        _shops = new ShopRegistry(helper, monitor);
    }

    /// <summary>
    /// 公开 API：注册外部模组的菜单条目。Id 重复时注册失败（返回 false）。
    /// </summary>
    public bool RegisterExternalEntry(string id, string name, Action onOpen)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || onOpen is null)
            return false;

        if (!_externalIds.Add(id))
        {
            _monitor.Log($"外部模组条目 '{id}' 注册失败：Id 已被占用。", LogLevel.Trace);
            return false;
        }

        _externalEntries.Add(new MenuEntry(
            id: id,
            execute: _ =>
            {
                // 与内部条目一致：先关闭 Relax Valley 菜单，再把控制权交给外部动作
                if (TryPrepareMenu())
                    onOpen();
            },
            category: "WorkbenchModSupport",
            displayName: name));

        _monitor.Log($"外部模组条目 '{id}'（{name}）已注册。", LogLevel.Info);
        return true;
    }

    /// <summary>公开 API：注销外部模组的菜单条目。</summary>
    public bool UnregisterExternalEntry(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (!_externalIds.Remove(id)) return false;

        _externalEntries.RemoveAll(e => e.Id == id);
        return true;
    }

    public IReadOnlyList<MenuEntry> GetMainMenuEntries()
    {
        return new List<MenuEntry>
        {
            // ═════════════ A. 信息与统计 ═════════════
            new MenuEntry("Calendar",           OpenCalendar,          category: "Info"),
            new MenuEntry("QuestBoard",         OpenQuestBoard,        category: "Info"),
            new MenuEntry("QuestOverview",      OpenQuestOverview,     category: "Info"),
            new MenuEntry("SpecialOrder",       OpenSpecialOrder,      category: "Info"),
            new MenuEntry("WalnutRoomOrder",    OpenWalnutRoomOrder,   category: "Info",
                status: () => ModEntry.Config.RespectVanillaGates && !IslandWest.IsQiWalnutRoomDoorUnlocked(out _)
                    ? MenuEntryStatus.Locked
                    : MenuEntryStatus.Available,
                lockReason: () => I18n.RelaxValley_UI_WalnutRoom_Locked()),
            new MenuEntry("FarmComputer",       OpenFarmComputer,      category: "Info"),
            new MenuEntry("MonsterKill", OpenMonsterKill, category: "Info",
                rightExecute: OpenExternalPerfectionPage),
            new MenuEntry("CasinoStatistic", OpenCasinoStatistic, category: "Info",
                status: () => ModEntry.Config.RespectVanillaGates && !Game1.player.hasRustyKey
                    ? MenuEntryStatus.Locked
                    : MenuEntryStatus.Available,
                lockReason: () => I18n.RelaxValley_UI_Casino_Locked()),
            new MenuEntry("PerfectionTracker", OpenPerfectionTracker, category: "Info",
                rightExecute: OpenExternalPerfectionPage),
            new MenuEntry("RecipeStats", OpenRecipeStats, category: "Info",
                rightExecute: OpenRecipeStatsMod),
            new MenuEntry("FarmAnimals", OpenFarmAnimals, category: "Info",
                rightExecute: OpenFarmAnimalsMod),
            new MenuEntry("FishingInfo", OpenFishingInfo, category: "Info"),
            new MenuEntry("GoldenWalnutsInfo",  OpenGoldenWalnutsInfo, category: "Info",
                condition: () => Game1.netWorldState.Value.GoldenWalnuts > 0),
            new MenuEntry("Museum",             OpenMuseum,            category: "Info"),
            new MenuEntry("FriendshipData", OpenFriendship, category: "Info"),

            // ═════════════ B. 地图与传送 ═════════════
            // 仅开放「当前地图」；世界地图传送与各区域地图入口暂时隐藏，
            // 功能代码保留在下方（恢复时取消注释）。
            new MenuEntry("LocationMap",        OpenCurrentMap,        category: "Map"),
            /*
            new MenuEntry("WorldMap",           OpenWorldMap,          category: "Map"),
            new MenuEntry("FarmMap",            OpenFarmMap,           category: "Map"),
            new MenuEntry("GingerMap",          OpenGingerMap,         category: "Map"),
            // 模组地图：未安装对应模组时隐藏（发布视角——纯原版玩家不出现装不了的条目）
            new MenuEntry("SVEMap",             OpenSveMap,            category: "Map",
                condition: () => _helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP")),
            new MenuEntry("RidgesideMap",       OpenRidgesideMap,      category: "Map",
                condition: () => _helper.ModRegistry.IsLoaded("Rafseazz.RidgesideVillage")),
            */

            // ═════════════ C. 远程电梯 ═════════════
            new MenuEntry("MineElevator",       OpenMineElevator,      category: "Elevator",
                status: () => ModEntry.Config.ElevatorProgressMode && Game1.player.deepestMineLevel < 5
                    ? MenuEntryStatus.Locked
                    : MenuEntryStatus.Available,
                lockReason: () => I18n.RelaxValley_UI_MineElevator_Locked()),
            new MenuEntry("SkullElevator",      OpenSkullElevator,     category: "Elevator",
                status: () => ModEntry.Config.ElevatorProgressMode && Game1.player.deepestMineLevel <= 120
                    ? MenuEntryStatus.Locked
                    : MenuEntryStatus.Available,
                lockReason: () => I18n.RelaxValley_UI_SkullElevator_Locked()),

            // ═════════════ D. 扩展与入口 ═════════════
            new MenuEntry("Workbench",          OpenWorkbench,         category: "Extended"),
        };
    }

    // ═════════════ 工作室页面 ═════════════

    public IReadOnlyList<MenuEntry> GetWorkbenchEntries()
    {
        var entries = new List<MenuEntry>();

        // ── A. 基础设施 ──
        entries.Add(new MenuEntry("EmilyTailoring", OpenTailoringMenu, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("DyePots", OpenDyeMenu, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("WizardShrine", OpenWizardShrine, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("PrizeMachine", OpenPrizeMachine, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("BobberMachine", OpenBobberMachine, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("UnifyFlowerColor", UnifyFlowerColor, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("Forge", OpenForge, category: "WorkbenchBasic"));

        // ── B. 建造与升级 + 原版商店（保持原版顺序） ──
        List<ShopDefinition> definitions = _shops.GetDefinitions().ToList();

        foreach (ShopDefinition definition in definitions.Where(d => !d.IsFromJson))
            entries.Add(CreateShopEntry(definition));

        // 节日当天：把各个节日摊位（含沙漠节、夜市、模组节日）逐个列出，无需事先发现
        foreach (ShopDefinition festivalShop in _shops.GetFestivalShops())
            entries.Add(CreateShopEntry(festivalShop));

        // ── C. 设施与存取 ──
        entries.Add(new MenuEntry(
            "BuyBackpack",
            OpenBackpack,
            category: "WorkbenchFacility",
            status: () => Game1.player.MaxItems >= 36
                ? MenuEntryStatus.Completed
                : MenuEntryStatus.Available,
            lockReason: () => I18n.RelaxValley_UI_Workbench_BackpackFull()));
        entries.Add(new MenuEntry("JunimoChest", OpenJunimoChest, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("ShippingBin", OpenShippingBin, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("DresserFurniture", OpenDresserFurniture, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("MasterySkill", OpenMasterySkill, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("MonsterKillReward", OpenMonsterKillReward, category: "WorkbenchFacility"));

        // ── D. 模组商店（shops.json）与模组支持，统一沉到最底层 ──
        foreach (ShopDefinition definition in definitions.Where(d => d.IsFromJson))
            entries.Add(CreateShopEntry(definition));

        // 自动发现的模组商店：按来源模组分组，每个模组一个分类标题
        foreach (ShopDefinition modShop in _shops.GetModShops())
            entries.Add(CreateShopEntry(modShop));

        entries.Add(new MenuEntry(
            "RSVQuests",
            OpenRsvQuestBoard,
            category: "WorkbenchModSupport",
            condition: () => _helper.ModRegistry.IsLoaded("Rafseazz.RidgesideVillage")));

        // 外部模组通过公开 API 注册的条目追加在「模组支持」分类末尾
        entries.AddRange(_externalEntries);

        return entries;
    }

    /// <summary>
    /// RSV 任务板：RSV 自带的布告栏与皮埃尔店外的每日任务板同一形态，
    /// 这里打开原版 Billboard 的每日任务模式。
    /// （RSV 若把任务注入每日任务池，这里会同步显示。）
    /// </summary>
    private void OpenRsvQuestBoard(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        Game1.activeClickableMenu = new Billboard(true);
        Game1.playSound("bigSelect");
    }

    private MenuEntry CreateShopEntry(ShopDefinition definition)
    {
        ShopDefinition def = definition;
        return new MenuEntry(
            id: def.Id,
            execute: (entry) => OpenShop(def),
            category: def.Category,
            translationKey: $"RelaxValley.Menu.{def.Id}.Name",
            displayName: def.Name,
            condition: () => _shops.IsVisible(def),
            status: () => _shops.GetStatus(def),
            lockReason: () => _shops.GetLockReason(def),
            categoryDisplayName: def.SourceModName);
    }

    private void OpenShop(ShopDefinition def)
    {
        if (!TryPrepareMenu()) return;
        _shops.Open(def);
    }

    private void OpenBackpack(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 原版皮埃尔柜台的 BuyBackpack 动作：确认问题框、扣款、扩容、举起动画
        // 全部由游戏本体处理，与站在柜台前购买完全一致
        Game1.currentLocation?.performAction(
            new[] { "BuyBackpack" },
            Game1.player,
            new xTile.Dimensions.Location(0, 0));
    }

    // ═════════════ 工作室执行方法 ═════════════

    private void OpenTailoringMenu(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new TailoringMenu();
        Game1.playSound("bigSelect");
    }

    private void OpenDyeMenu(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new DyeMenu();
        Game1.playSound("bigSelect");
    }

    /// <summary>火山锻造台：原版火山锻冶界面（附魔 / 合并 / 宝石镶嵌，流程全部由游戏本体处理）。</summary>
    private void OpenForge(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new ForgeMenu();
        Game1.playSound("bigSelect");
    }

    private void OpenWizardShrine(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new CharacterCustomization(CharacterCustomization.Source.Wizard);
        Game1.playSound("bigSelect");
    }

    private void OpenPrizeMachine(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 原版兑奖机菜单（PrizeTicketMenu），无参构造
        Game1.activeClickableMenu = new PrizeTicketMenu();
        Game1.playSound("bigSelect");
    }

    private void OpenBobberMachine(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 原版威利鱼店浮标机：与柜台 Bobbers 动作一致，
        // 手持鱼竿校验、浮标样式选择与应用全部由游戏本体处理
        Game1.activeClickableMenu = new ChooseFromIconsMenu("bobbers");
        Game1.playSound("bigSelect");
    }

    private void UnifyFlowerColor(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var model = new FlowerUnifyViewModel(_helper.Translation, _monitor);
        ShowView("FlowerUnifyView", model);
    }

    // ═════════════ D. 设施与存取 ═════════════

    /// <summary>祝尼魔箱：遍历所有已加载地图找玩家放置的 Junimo Chest。</summary>
    private void OpenJunimoChest(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        foreach (var location in Game1.locations)
        {
            foreach (var obj in location.objects.Values)
            {
                // 祝尼魔箱
                if (obj is Chest chest && chest.SpecialChestType == Chest.SpecialChestTypes.JunimoChest)
                {
                    chest.ShowMenu(); // 让箱子自己处理打开逻辑
                    Game1.playSound("bigSelect");
                    return;
                }
            }
        }
        _monitor.Log("找不到祝尼魔箱。", LogLevel.Warn);
        Game1.showGlobalMessage(I18n.RelaxValley_UI_Workbench_JunimoChestMissing());
    }

    private void OpenShippingBin(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        var farm = Game1.getFarm();
        if (farm is null) return;

        // 出货箱
        if (farm.getShippingBin(Game1.player) is Chest shippingBin)
        {
            shippingBin.ShowMenu();
            Game1.playSound("bigSelect");
        }
        else
        {
            _monitor.Log("找不到出货箱。", LogLevel.Warn);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Workbench_ShippingBinMissing());
        }
    }

    private void OpenDresserFurniture(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        var house = Game1.getLocationFromName("FarmHouse");
        if (house is null) return;

        foreach (var furniture in house.furniture)
        {
            if (furniture is StorageFurniture storage)
            {
                storage.checkForAction(Game1.player, false); // 第二个参数传 false，表示不是“justCheckingForActivity”
                Game1.playSound("bigSelect");
                return;
            }
        }
        _monitor.Log("农舍里没有梳妆台。", LogLevel.Warn);
        Game1.showGlobalMessage(I18n.RelaxValley_UI_Workbench_DresserMissing());
    }

    /// <summary>精通技能：1.6 的 MasteryTrackerMenu。</summary>
    private void OpenMasterySkill(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new MasteryTrackerMenu();
        Game1.playSound("bigSelect");
    }

    /// <summary>杀怪奖励：打开冒险家公会的领取奖励界面（模拟 Action "Gil"）。</summary>
    private void OpenMonsterKillReward(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var guild = Game1.getLocationFromName("AdventureGuild");
        if (guild is null) { _monitor.Log("找不到冒险家公会。", LogLevel.Warn); return; }

        // 先关掉当前菜单，确保玩家处于“空闲”状态
        Game1.exitActiveMenu();

        Game1.PerformActionWhenPlayerFree(() =>
        {
            guild.performAction("Gil", Game1.player, new xTile.Dimensions.Location(0, 0));
            Game1.playSound("bigSelect");
        });
    }

    // ═════════════ A. 信息与统计 ═════════════

    private void OpenCalendar(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new Billboard();
        Game1.playSound("bigSelect");
    }

    private void OpenQuestBoard(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 皮埃尔商店外的每日任务布告栏（原版 Billboard 的 dailyQuest 模式）
        Game1.activeClickableMenu = new Billboard(true);
        Game1.playSound("bigSelect");
    }

    /// <summary>
    /// 自绘任务一览：只读浏览 Data/Quests，可按模组来源与任务类型筛选。
    /// 不读写原版任务日志，完全不影响原版任务进度。
    /// </summary>
    private void OpenQuestOverview(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，无法打开任务一览。", LogLevel.Warn);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_StardewUIRequired());
            return;
        }

        var model = new QuestBoardViewModel(_helper.Translation, _helper.ModRegistry);
        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/QuestBoardView";
        var menu = viewEngine.CreateMenuFromAsset(assetName, model);

        if (menu is null)
        {
            _monitor.Log($"无法创建 QuestBoardView：{assetName}", LogLevel.Error);
            return;
        }

        Game1.activeClickableMenu = menu;
        Game1.playSound("bigSelect");
    }

    private void OpenSpecialOrder(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new SpecialOrdersBoard();
        Game1.playSound("bigSelect");
    }

    private void OpenWalnutRoomOrder(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new SpecialOrdersBoard("Qi");
        Game1.playSound("bigSelect");
    }

    private void OpenFarmComputer(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 优先背包里的农场电脑；没有再找已放置在世界中的（农场、温室内等任意地点）
        StardewValley.Object? farmComputer = Game1.player.Items
            .OfType<StardewValley.Object>()
            .FirstOrDefault(item => item.QualifiedItemId == "(BC)239");

        bool fromInventory = farmComputer is not null;
        GameLocation? originalLocation = farmComputer?.Location;

        if (!fromInventory)
        {
            foreach (GameLocation location in Game1.locations)
            {
                foreach (StardewValley.Object obj in location.objects.Values)
                {
                    if (obj.QualifiedItemId != "(BC)239") continue;

                    farmComputer = obj;
                    break;
                }

                if (farmComputer is not null) break;
            }
        }

        if (farmComputer == null)
        {
            _monitor.Log("背包里和地图上都没有找到农场电脑。", LogLevel.Warn);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Workbench_FarmComputerMissing());
            return;
        }

        if (Game1.currentLocation is null)
        {
            _monitor.Log("当前地点未知，无法打开农场电脑。", LogLevel.Warn);
            return;
        }

        // 原版 checkForAction 会调用 Location.localSound()，背包里的物品 Location 为 null，
        // 临时挂到当前地点，调用完再还原；已放置的电脑自带 Location，直接用。
        try
        {
            if (fromInventory)
                farmComputer.Location = Game1.currentLocation;

            farmComputer.checkForAction(Game1.player);
        }
        finally
        {
            if (fromInventory)
                farmComputer.Location = originalLocation;
        }
    }

    private void OpenMonsterKill(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，无法打开杀怪统计。", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_StardewUIRequired());
            return;
        }

        var model = new MonsterKillViewModel();
        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/MonsterKillView";
        var menu = viewEngine.CreateMenuFromAsset(assetName, model);

        if (menu is null)
        {
            _monitor.Log($"无法创建 MonsterKillView：{assetName}", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_ViewLoadFailed());
            return;
        }

        Game1.activeClickableMenu = menu;
        Game1.playSound("bigSelect");
    }

    private void OpenCasinoStatistic(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 原版赌场终端的 FarmerFile：统计数据对话由游戏本体显示
        Game1.currentLocation?.farmerFile();

        Game1.playSound("bigSelect");
    }

    private void OpenPerfectionTracker(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 原版核桃房的完美度追踪（ShowQiCat）：
        // 总百分比、12 项分类明细、豁免计数与本地化全部由游戏本体处理
        Game1.currentLocation?.ShowQiCat();
    }

    /// <summary>配方统计：左键查看原版配方（原版制作菜单独立打开，顶部附「已学 X / Y」进度，总数动态含模组配方）。</summary>
    private void OpenRecipeStats(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        Game1.activeClickableMenu = new RecipeStatsPage(
            (Game1.uiViewport.Width - 800) / 2,
            Math.Max(0, (Game1.uiViewport.Height - 600) / 2),
            800,
            600,
            cooking: false,
            standaloneMenu: true);

        Game1.playSound("bigSelect");
    }

    /// <summary>配方统计右键：完美手册的烹饪 / 制作配方进度页（无需道具）。</summary>
    private void OpenRecipeStatsMod(MenuEntry __) => OpenExternalPerfectionPage(__);

    /// <summary>动物页：左键查看农场动物概览（原版数据自绘页面）。</summary>
    private void OpenFarmAnimals(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，无法打开农场动物。", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_StardewUIRequired());
            return;
        }

        var model = new FarmAnimalsViewModel(_helper.Translation);
        var menu = viewEngine.CreateMenuFromAsset(
            $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/FarmAnimalsView",
            model);

        if (menu is null)
        {
            _monitor.Log("无法创建 FarmAnimalsView", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_ViewLoadFailed());
            return;
        }

        Game1.activeClickableMenu = menu;
        Game1.playSound("bigSelect");
    }

    /// <summary>动物页右键：调用更详细的模组页面（GMCM 配置菜单类型，默认空）。</summary>
    private void OpenFarmAnimalsMod(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        foreach (string typeName in ModEntry.Config.StatsMenuTypes)
        {
            if (string.IsNullOrWhiteSpace(typeName)) continue;

            try
            {
                Type? type = Type.GetType(typeName);
                if (type is null) continue;

                if (Activator.CreateInstance(type) is IClickableMenu menu)
                {
                    Game1.activeClickableMenu = menu;
                    Game1.playSound("bigSelect");
                    return;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"打开外接动物页面失败（类型 {typeName}）：{ex.Message}", LogLevel.Warn);
            }
        }

        Game1.showGlobalMessage(I18n.RelaxValley_UI_Animals_NoExternal());
    }

    /// <summary>钓鱼情报：按当前季节 / 天气 / 时间模拟原版垂钓判定，列出当前可钓到的物品与占比。卡片悬浮含原版收藏页同款信息，F1 可查询。</summary>
    private void OpenFishingInfo(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，无法打开钓鱼情报。", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_StardewUIRequired());
            return;
        }

        var location = Game1.currentLocation;
        if (location is null) return;

        // F1 桥接只在 StardewUI fork 上可用；官方版走无桥接模板，避免绑定报错刷屏
        bool bridge = StardewUIF1Bridge.EnsureRegistered();
        string assetName = bridge ? "FishingInfoView" : "FishingInfoViewPlain";
        var model = FishingInfoViewModel.Build(location, Game1.player);
        var menu = viewEngine.CreateMenuFromAsset($"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/{assetName}", model);

        if (menu is null)
        {
            _monitor.Log($"无法创建钓鱼情报界面：{assetName}", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_ViewLoadFailed());
            return;
        }

        Game1.activeClickableMenu = menu;
        Game1.playSound("bigSelect");
    }

    /// <summary>博物馆收藏：逐件列出可捐赠物品（矿物 / 古物）与捐赠状态。可捐判定用原版 IsItemSuitableForDonation，模组物品自动计入。</summary>
    private void OpenMuseum(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // F1 桥接（hovered-subject 绑定）只在 StardewUI fork 上可用；官方版走无桥接模板，避免绑定报错刷屏
        bool bridge = StardewUIF1Bridge.EnsureRegistered();
        ShowView(bridge ? "MuseumView" : "MuseumViewPlain", MuseumViewModel.Build());
    }

    private void OpenFriendship(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，无法打开村民友谊。", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_StardewUIRequired());
            return;
        }

        var model = new FriendshipViewModel(
            _helper.Translation,
            ModEntry.Config.FriendshipProgressMode,
            ModEntry.Config.EnableTalkAll,
            ModEntry.Config.EnableFriendshipTeleport,
            ModEntry.Config.EnableBatchGift);
        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/FriendshipView";
        var menu = viewEngine.CreateMenuFromAsset(assetName, model);

        if (menu is null)
        {
            _monitor.Log($"无法创建 FriendshipView：{assetName}", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_ViewLoadFailed());
            return;
        }

        Game1.activeClickableMenu = menu;
        Game1.playSound("bigSelect");
    }

    private void OpenGoldenWalnutsInfo(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        int found = Game1.netWorldState.Value.GoldenWalnuts;
        int remaining = Math.Max(0, TotalGoldenWalnuts - found);

        var details = new List<string>();

        // 100 个金核桃开启核桃房大门（原版规则）
        if (found < 100)
        {
            details.Add(_helper.Translation
                .Get(I18n.Keys.RelaxValley_Menu_GoldenWalnutsInfo_RoomLocked)
                .Tokens(new { count = (100 - found).ToString() }));
        }
        else
        {
            details.Add(I18n.RelaxValley_Menu_GoldenWalnutsInfo_RoomUnlocked());
        }

        // 齐先生的鹦鹉可按 10000 金 / 个 补齐剩余（原版规则，仅姜岛鹦鹉升级点）
        if (remaining > 0)
        {
            details.Add(_helper.Translation
                .Get(I18n.Keys.RelaxValley_Menu_GoldenWalnutsInfo_Parrot)
                .Tokens(new { price = (remaining * 10000).ToString("N0") }));
        }

        details.Add(I18n.RelaxValley_Menu_GoldenWalnutsInfo_Note());

        int percent = TotalGoldenWalnuts > 0
            ? (int)Math.Round(found * 100.0 / TotalGoldenWalnuts)
            : 0;

        var model = new GoldenWalnutsViewModel(
            I18n.RelaxValley_Menu_GoldenWalnutsInfo_Title(),
            _helper.Translation
              .Get(I18n.Keys.RelaxValley_Menu_GoldenWalnutsInfo_Summary)
              .Tokens(new
              {
                  found = found.ToString(),
                  total = TotalGoldenWalnuts.ToString()
              }),
            $"{found} / {TotalGoldenWalnuts}（{percent}%）",
            details,
            I18n.RelaxValley_Menu_GoldenWalnutsInfo_Hint());

        ShowView("GoldenWalnutsView", model);
    }

    // ═════════════ B. 地图与传送 ═════════════

    /// <summary>
    /// 当前地图：用原版 mapDisplayDevice 把当前地图渲染成纹理（自动裁出内容边界），
    /// 打开可拖拽 / 缩放的查看器（LocationMapMenu），初始铺满内容区。
    /// </summary>
    private void OpenCurrentMap(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        if (Game1.currentLocation is null) return;

        var rendered = LocationMapRenderer.Render(Game1.currentLocation);
        if (rendered is null)
        {
            _monitor.Log($"渲染当前地图失败：{Game1.currentLocation.Name}", LogLevel.Warn);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_LocationMap_Unavailable());
            return;
        }

        Game1.activeClickableMenu = new LocationMapMenu(rendered.Value);
        Game1.playSound("bigSelect");
    }

    /*
    /// <summary>世界地图：原版世界地图 + 点击传送。</summary>
    private void OpenWorldMap(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new TeleportMapPage();
        Game1.playSound("bigSelect");
    }

    /// <summary>按指定地点所在的世界地图区域打开地图。</summary>
    private void OpenRegionMapForLocation(string locationName)
    {
        if (!TryPrepareMenu()) return;

        GameLocation? location = Game1.getLocationFromName(locationName);
        MapRegion? region = location is null
            ? null
            : WorldMapManager.GetPositionData(location, Point.Zero)?.Data.Region;

        if (region is null)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Map_Unavailable());
            return;
        }

        Game1.activeClickableMenu = new RegionMapMenu(region);
        Game1.playSound("bigSelect");
    }

    private void OpenFarmMap(MenuEntry __) =>
        OpenRegionMapForLocation("Farm");

    private void OpenGingerMap(MenuEntry __) =>
        OpenRegionMapForLocation("IslandSouth");

    private void OpenSveMap(MenuEntry __) =>
        OpenModRegionMap(
            "FlashShifter.StardewValleyExpandedCP",
            new[] { "SVE", "Expanded", "Grampleton", "FlashShifter" });

    private void OpenRidgesideMap(MenuEntry __) =>
        OpenModRegionMap(
            "Rafseazz.RidgesideVillage",
            new[] { "RSV", "Ridgeside", "Rafseazz" });

    /// <summary>
    /// 打开模组提供的世界地图区域。
    /// 区域 ID 一般以模组 UniqueID 开头；找不到时用关键字兜底。
    /// 多个区域时让玩家选择。
    /// </summary>
    private void OpenModRegionMap(string modId, string[] keywords)
    {
        if (!TryPrepareMenu()) return;

        if (!_helper.ModRegistry.IsLoaded(modId))
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Map_NotInstalled());
            return;
        }

        var regions = new List<MapRegion>();
        foreach (MapRegion region in WorldMapManager.GetMapRegions())
        {
            bool match = region.Id.Contains(modId, StringComparison.OrdinalIgnoreCase);
            if (!match)
            {
                foreach (string keyword in keywords)
                {
                    if (region.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }
            }

            if (match) regions.Add(region);
        }

        if (regions.Count == 0)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Map_Unavailable());
            return;
        }

        if (regions.Count == 1)
        {
            Game1.activeClickableMenu = new RegionMapMenu(regions[0]);
            Game1.playSound("bigSelect");
            return;
        }

        var location = Game1.currentLocation;
        if (location is null) return;

        location.createQuestionDialogue(
            I18n.RelaxValley_UI_Map_ChooseRegion(),
            regions
                .Select(r => new Response(r.Id, r.Id))
                .Append(new Response("Cancel", I18n.RelaxValley_UI_Friendship_Cancel()))
                .ToArray(),
            (_, answer) =>
            {
                MapRegion? chosen = regions.FirstOrDefault(r => r.Id == answer);
                if (chosen is null) return;

                ModEntry.RunWhenPlayerFree(() =>
                {
                    Game1.activeClickableMenu = new RegionMapMenu(chosen);
                    Game1.playSound("bigSelect");
                });
            });
    }
    */

    // ═════════════ C. 远程电梯 ═════════════

    private void OpenMineElevator(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        int deepest = Game1.player.deepestMineLevel;

        // 进度模式：按最深层数解锁到 5 的倍数；常开模式：全部楼层
        int maxLevel = ModEntry.Config.ElevatorProgressMode
            ? Math.Min(120, deepest / 5 * 5)
            : 120;
        if (maxLevel < 5)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_MineElevator_Locked());
            return;
        }

        // 只开放玩家已经到达过的 5 层检查点，不再直接给到 120 层
        var levels = new List<int>();
        for (int i = 0; i <= maxLevel; i += 5) levels.Add(i);

        var model = new ElevatorViewModel(
            I18n.RelaxValley_UI_Elevator_MineTitle(),
            levels,
            onSelect: level =>
            {
                try
                {
                    Game1.enterMine(level);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"矿井传送失败：{ex.Message}", LogLevel.Error);
                }
            },
            hint: I18n.RelaxValley_UI_Elevator_Hint());

        ShowView("ElevatorView", model);
    }

    private void OpenSkullElevator(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 1. 用 deepestMineLevel 计算骷髅矿洞进度
        int deepestMineLevel = Game1.player.deepestMineLevel;

        // 进度模式：层数 = 内部编号 - 120；常开模式：直接给到常规的 25 层
        int deepestSkullLevel = ModEntry.Config.ElevatorProgressMode
            ? deepestMineLevel - 120
            : 25;

        // 如果玩家尚未解锁或进入过骷髅矿洞，提示并返回
        if (deepestSkullLevel < 1)
        {
            _monitor.Log("尚未解锁骷髅矿洞，无法使用电梯。", LogLevel.Info);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_SkullElevator_Locked());
            return;
        }

        // 2. 根据实际进度动态生成层数列表（每5层一个按钮）
        var levels = new List<int>();
        for (int i = 1; i <= deepestSkullLevel; i += 5)
        {
            levels.Add(i);
        }
        // 确保最深层（如果不是5的倍数）也包含在内
        if (levels.Count == 0 || levels[levels.Count - 1] != deepestSkullLevel)
        {
            levels.Add(deepestSkullLevel);
        }

        // 3. 打开电梯 UI，传送时使用正确的内部编号
        var model = new ElevatorViewModel(
            I18n.RelaxValley_UI_Elevator_SkullTitle(),
            levels,
            onSelect: level =>
            {
                try
                {
                    // 核心：传送时层数 +120，转为内部编号
                    string locationName = MineShaft.GetLevelName(level + 120);
                    Game1.warpFarmer(locationName, 0, 0, false);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"骷髅矿洞传送失败：{ex.Message}", LogLevel.Error);
                }
            },
            hint: I18n.RelaxValley_UI_Elevator_Hint());

        ShowView("ElevatorView", model);
    }

    // ═════════════ D. 扩展与入口 ═════════════

    /// <summary>工作室是主菜单的第二页，由 MainMenuViewModel 内部切换，不需要单独菜单。</summary>
    private static void OpenWorkbench(MenuEntry __)
    {
        // 实际切换逻辑在 MainMenuViewModel.OnRowClicked 里按 Id 拦截
    }

    // ─────────────────────────────────────────────

    // ═════════════ StardewUI 界面入口 ═════════════

    /// <summary>打开主菜单（F3）。主菜单与工作室是同一个 StardewUI 界面的两页。</summary>
    public void OpenMainMenu()
    {
        var model = new MainMenuViewModel(
            GetMainMenuEntries(),
            GetWorkbenchEntries,
            _helper.Translation,
            _monitor);

        IMenuController? controller = CreateView("MainMenuView", model);
        if (controller is null) return;

        // 不拦截默认关闭行为：ESC / 关闭按钮正常关菜单，
        // 工作室页通过顶部的「返回主菜单」行返回，避免影响依赖 exitActiveMenu 的流程。
        model.AttachClose(MakeClose(controller));

        Game1.activeClickableMenu = controller.Menu;
        Game1.playSound("bigSelect");
    }

    /// <summary>
    /// 右键「完美度追踪」：打开完美手册（mushymato.PerfectionHandbook）的页面。
    /// 只走它自己注册的触发器动作（作者的公开入口，无需道具）；
    /// 未安装或调用失败不做其它兜底。
    /// </summary>
    private void OpenExternalPerfectionPage(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        if (Game1.currentLocation is null) return;

        if (!_helper.ModRegistry.IsLoaded("mushymato.PerfectionHandbook"))
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Perfection_NoExternal());
            return;
        }

        try
        {
            if (TriggerActionManager.TryRunAction(
                    "mushymato.PerfectionHandbook_ShowHandbook",
                    out string error,
                    out Exception exception))
            {
                return;
            }

            _monitor.Log($"完美手册触发器未执行：{error} {exception?.Message}", LogLevel.Trace);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Perfection_InvokeFailed());
        }
        catch (Exception ex)
        {
            _monitor.Log($"完美手册触发器调用失败：{ex.Message}", LogLevel.Trace);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Perfection_InvokeFailed());
        }
    }

    /// <summary>
    /// 列出所有条目 Id 与分类，供 config.json 的 MenuShortcutKeys 配置快捷键时对照。
    /// 只读取注册表，不执行任何条目。
    /// </summary>
    public List<(string Id, string Category)> GetAllEntrySummaries()
    {
        var list = new List<(string, string)>();

        foreach (MenuEntry entry in GetMainMenuEntries())
            list.Add((entry.Id, entry.Category));

        foreach (MenuEntry entry in GetWorkbenchEntries())
            list.Add((entry.Id, entry.Category));

        return list;
    }

    /// <summary>快捷键执行：按 Id 在主菜单 + 工作室条目里查找并执行。</summary>
    public bool TryRunEntryById(string id)
    {
        if (!Context.IsWorldReady) return false;

        MenuEntry? entry = GetMainMenuEntries().FirstOrDefault(e => e.Id == id)
            ?? GetWorkbenchEntries().FirstOrDefault(e => e.Id == id);
        if (entry is null) return false;

        if (entry.Id == "Workbench")
        {
            if (Game1.activeClickableMenu is not null)
                Game1.exitActiveMenu();
            OpenMainMenu();
            return true;
        }

        MenuEntryStatus status = entry.GetStatus();
        if (status == MenuEntryStatus.NotImplemented)
        {
            _monitor.Log($"快捷键目标 '{id}' 尚未实现。", LogLevel.Warn);
            return false;
        }

        if (status != MenuEntryStatus.Available)
        {
            Game1.playSound("cancel");
            Game1.showGlobalMessage(entry.GetLockReason());
            return true;
        }

        if (Game1.activeClickableMenu is not null)
            Game1.exitActiveMenu();

        Game1.playSound("smallSelect");
        return entry.TryExecute(_monitor);
    }

    private IMenuController? CreateView(string viewName, object model)    {
        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，无法打开界面。", LogLevel.Error);
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Common_StardewUIRequired());
            return null;
        }

        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/{viewName}";
        IMenuController? controller = viewEngine.CreateMenuControllerFromAsset(assetName, model);

        if (controller is null)
        {
            _monitor.Log($"无法创建界面：{assetName}", LogLevel.Error);
            return null;
        }

        controller.EnableCloseButton();
        return controller;
    }

    private static Action MakeClose(IMenuController controller) => () =>
    {
        // 先恢复默认关闭行为，再执行真正的关闭
        controller.CloseAction = null;
        controller.Close();
    };

    private void ShowView(string viewName, ICloseAware model)
    {
        IMenuController? controller = CreateView(viewName, model);
        if (controller is null) return;

        Action close = MakeClose(controller);
        model.AttachClose(close);
        controller.CloseAction = close;

        Game1.activeClickableMenu = controller.Menu;
        Game1.playSound("bigSelect");
    }

    private void ShowView(string viewName, object model)
    {
        IMenuController? controller = CreateView(viewName, model);
        if (controller is null) return;

        controller.CloseAction = MakeClose(controller);

        Game1.activeClickableMenu = controller.Menu;
        Game1.playSound("bigSelect");
    }

    // ─────────────────────────────────────────────

    private bool TryPrepareMenu()
    {
        if (!Context.IsWorldReady)
        {
            _monitor.Log("世界尚未就绪，已忽略菜单打开请求。", LogLevel.Warn);
            return false;
        }
        if (Game1.activeClickableMenu is not null)
            Game1.exitActiveMenu();
        return true;
    }
}