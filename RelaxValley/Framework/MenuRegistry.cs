using RelaxValley.Framework.Shops;
using RelaxValley.Framework.UI;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewUI.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewValley.Objects;
using StardewValley.WorldMaps;

namespace RelaxValley.Framework;

public sealed class MenuRegistry
{
    private const int TotalGoldenWalnuts = 130;

    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;
    private readonly ShopRegistry _shops;

    public MenuRegistry(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        _helper = helper;
        _shops = new ShopRegistry(helper, monitor);
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
            new MenuEntry("WalnutRoomOrder",    OpenWalnutRoomOrder,   category: "Info"),
            new MenuEntry("FarmComputer",       OpenFarmComputer,      category: "Info"),
            new MenuEntry("MonsterKill", OpenMonsterKill, category: "Info"),
            new MenuEntry("CasinoStatistic", OpenCasinoStatistic, category: "Info"),
            new MenuEntry("PerfectionTracker", OpenPerfectionTracker, category: "Info"),
            new MenuEntry("GoldenWalnutsInfo",  OpenGoldenWalnutsInfo, category: "Info",
                condition: () => Game1.netWorldState.Value.GoldenWalnuts > 0),
            new MenuEntry("FriendshipData", OpenFriendship, category: "Info"),

            // ═════════════ B. 地图与传送 ═════════════
            new MenuEntry("LocationMap",        OpenCurrentMap,        category: "Map"),
            new MenuEntry("FarmMap",            OpenFarmMap,           category: "Map"),
            new MenuEntry("GingerMap",          OpenGingerMap,         category: "Map"),
            new MenuEntry("SVEMap",             OpenSveMap,            category: "Map"),
            new MenuEntry("RidgesideMap",       OpenRidgesideMap,      category: "Map"),

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
        Game1.showGlobalMessage("找不到祝尼魔箱，请先放置一个。");
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
            Game1.showGlobalMessage("找不到出货箱。");
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
        Game1.showGlobalMessage("农舍里没有梳妆台。");
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
            Game1.showGlobalMessage("任务一览需要 StardewUI。");
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
            Game1.showGlobalMessage("没有找到农场电脑（背包里或已放置在某个地点）。");
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
            Game1.showGlobalMessage("杀怪统计需要 StardewUI。");
            return;
        }

        var model = new MonsterKillViewModel();
        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/MonsterKillView";
        var menu = viewEngine.CreateMenuFromAsset(assetName, model);

        if (menu is null)
        {
            _monitor.Log($"无法创建 MonsterKillView：{assetName}", LogLevel.Error);
            Game1.showGlobalMessage("杀怪统计界面加载失败。");
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

    private void OpenFriendship(MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，无法打开村民友谊。", LogLevel.Error);
            Game1.showGlobalMessage("村民友谊需要 StardewUI。");
            return;
        }

        var model = new FriendshipViewModel(_helper.Translation, ModEntry.Config.FriendshipProgressMode);
        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/FriendshipView";
        var menu = viewEngine.CreateMenuFromAsset(assetName, model);

        if (menu is null)
        {
            _monitor.Log($"无法创建 FriendshipView：{assetName}", LogLevel.Error);
            Game1.showGlobalMessage("村民友谊界面加载失败。");
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
                .Tokens(new { count = (100 - found).ToString() })
                .ToString());
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
                .Tokens(new { price = (remaining * 10000).ToString("N0") })
                .ToString());
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
              })
              .ToString(),
            $"{found} / {TotalGoldenWalnuts}（{percent}%）",
            details,
            I18n.RelaxValley_Menu_GoldenWalnutsInfo_Hint());

        ShowView("GoldenWalnutsView", model);
    }

    // ═════════════ B. 地图与传送 ═════════════

    /// <summary>当前位置：原版世界地图 + 点击传送。</summary>
    private void OpenCurrentMap(MenuEntry __)
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
            Game1.showGlobalMessage("尚未解锁骷髅矿洞，请先获得头骨钥匙。");
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
            _monitor,
            onRowRightClick: HandleRowRightClick);

        IMenuController? controller = CreateView("MainMenuView", model);
        if (controller is null) return;

        // 不拦截默认关闭行为：ESC / 关闭按钮正常关菜单，
        // 工作室页通过顶部的「返回主菜单」行返回，避免影响依赖 exitActiveMenu 的流程。
        model.AttachClose(MakeClose(controller));

        Game1.activeClickableMenu = controller.Menu;
        Game1.playSound("bigSelect");
    }

    /// <summary>主菜单右键：目前只有「完美度追踪」有外接页面。</summary>
    private void HandleRowRightClick(string id)
    {
        if (id == "PerfectionTracker")
            OpenExternalPerfectionPage();
    }

    /// <summary>
    /// 右键「完美度追踪」：打开其它完美度模组的页面。
    /// 依次尝试：完美手册的触发器动作（作者公开入口，无需道具）→
    /// 反射其公开方法 → GMCM 配置的菜单类型 → GMCM 配置的物品。
    /// </summary>
    private void OpenExternalPerfectionPage()
    {
        if (!TryPrepareMenu()) return;

        // 1) 完美手册（mushymato.PerfectionHandbook）注册的触发器动作
        if (_helper.ModRegistry.IsLoaded("mushymato.PerfectionHandbook"))
        {
            try
            {
                if (TriggerActionManager.TryRunAction(
                        "mushymato.PerfectionHandbook_ShowHandbook",
                        out string error,
                        out Exception exception))
                {
                    Game1.playSound("bigSelect");
                    return;
                }

                _monitor.Log($"完美手册触发器未执行：{error} {exception?.Message}", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"完美手册触发器调用失败：{ex.Message}", LogLevel.Trace);
            }
        }

        // 2) 反射调用其公开入口 PerfectionHandbook.MenuHandler.ShowHandbook()
        try
        {
            Type? handler = Type.GetType("PerfectionHandbook.MenuHandler, PerfectionHandbook");
            MethodInfo? show = handler?.GetMethod(
                "ShowHandbook",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);

            if (handler is not null && show is not null)
            {
                show.Invoke(null, null);
                Game1.playSound("bigSelect");
                return;
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"反射调用完美手册失败：{ex.Message}", LogLevel.Trace);
        }

        // 3) GMCM 配置的菜单类型
        foreach (string typeName in ModEntry.Config.PerfectionMenuTypes)
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
                _monitor.Log($"打开外接完美度页面失败（类型 {typeName}）：{ex.Message}", LogLevel.Warn);
            }
        }

        // 4) GMCM 配置的物品（例如完美手册实体）
        foreach (string configuredName in ModEntry.Config.PerfectionItems)
        {
            if (string.IsNullOrWhiteSpace(configuredName)) continue;

            StardewValley.Object? item = Game1.player.Items
                .OfType<StardewValley.Object>()
                .FirstOrDefault(i =>
                    string.Equals(i.ItemId, configuredName, StringComparison.OrdinalIgnoreCase)
                    || i.Name.Contains(configuredName, StringComparison.OrdinalIgnoreCase)
                    || i.DisplayName.Contains(configuredName, StringComparison.OrdinalIgnoreCase));

            if (item is null) continue;

            GameLocation? originalLocation = item.Location;
            item.Location = Game1.currentLocation;

            try
            {
                Game1.playSound("bigSelect");
                item.checkForAction(Game1.player);
            }
            catch (Exception ex)
            {
                _monitor.Log($"打开外接完美度页面失败（物品）：{ex}", LogLevel.Error);
            }
            finally
            {
                item.Location = originalLocation;
            }

            return;
        }

        Game1.showGlobalMessage(I18n.RelaxValley_UI_Perfection_NoExternal());
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
            Game1.showGlobalMessage("该界面需要 StardewUI。");
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