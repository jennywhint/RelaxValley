using RelaxValley.Framework.UI;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley.Objects;

namespace RelaxValley.Framework;

public sealed class MenuRegistry
{
    private const int TotalGoldenWalnuts = 130;

    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;

    public MenuRegistry(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        _helper = helper;
    }

    public IReadOnlyList<MenuEntry> GetMainMenuEntries()
    {
        return new List<MenuEntry>
        {
            // ═════════════ A. 信息与统计 ═════════════
            new MenuEntry("Calendar",           OpenCalendar,          category: "Info"),
            new MenuEntry("QuestBoard",         OpenQuestBoard,        category: "Info"),
            new MenuEntry("SpecialOrder",       OpenSpecialOrder,      category: "Info"),
            new MenuEntry("WalnutRoomOrder",    OpenWalnutRoomOrder,   category: "Info"),
            new MenuEntry("RSVQuests",          null,                  category: "Info"),
            new MenuEntry("FarmComputer",       OpenFarmComputer,      category: "Info"),
            new MenuEntry("MonsterKill", OpenMonsterKill, category: "Info"),
            new MenuEntry("CasinoStatistic", OpenCasinoStatistic, category: "Info"),
            new MenuEntry("PerfectionTracker",  null,                  category: "Info"),
            new MenuEntry("GoldenWalnutsInfo",  OpenGoldenWalnutsInfo, category: "Info",
                condition: () => Game1.netWorldState.Value.GoldenWalnuts > 0),
            new MenuEntry("FriendshipData", OpenFriendship, category: "Info"),

            // ═════════════ B. 地图与传送 ═════════════
            new MenuEntry("LocationMap",        OpenMap,               category: "Map"),
            new MenuEntry("FarmMap",            OpenMap,               category: "Map"),
            new MenuEntry("GingerMap",          OpenMap,               category: "Map"),
            new MenuEntry("SVEMap",             null,                  category: "Map"),
            new MenuEntry("RidgesideMap",       null,                  category: "Map"),

            // ═════════════ C. 远程电梯 ═════════════
            new MenuEntry("MineElevator",       OpenMineElevator,      category: "Elevator"),
            new MenuEntry("SkullElevator",      OpenSkullElevator,     category: "Elevator"),
            new MenuEntry("VolcanoElevator",    OpenVolcanoElevator,   category: "Elevator"),

            // ═════════════ D. 扩展与入口 ═════════════
            new MenuEntry("Workbench",          OpenWorkbench,         category: "Extended"),
        };
    }

    // ═════════════ 工作室页面 ═════════════

    public IReadOnlyList<MenuEntry> GetWorkbenchEntries()
    {
        var entries = new List<MenuEntry>();

        entries.Add(new MenuEntry("EmilyTailoring", OpenTailoringMenu, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("DyePots", OpenDyeMenu, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("WizardShrine", OpenWizardShrine, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("PrizeMachine", OpenPrizeMachine, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("BobberMachine", OpenBobberMachine, category: "WorkbenchBasic"));
        entries.Add(new MenuEntry("UnifyFlowerColor", UnifyFlowerColor, category: "WorkbenchBasic"));
        // ── D. 设施与存取 ──
        entries.Add(new MenuEntry("BuyBackpack", OpenBuyBackpack, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("JunimoChest", OpenJunimoChest, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("ShippingBin", OpenShippingBin, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("DresserFurniture", OpenDresserFurniture, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("MasterySkill", OpenMasterySkill, category: "WorkbenchFacility"));
        entries.Add(new MenuEntry("MonsterKillReward", OpenMonsterKillReward, category: "WorkbenchFacility"));

        var vanillaShops = new (string Id, string ShopId)[]
        {
            ("ShopPierre",       "SeedShop"),
            ("ShopCarpenter",    "Carpenter"),
            ("ShopAnimal",       "AnimalShop"),
            ("ShopFish",         "FishShop"),
            ("ShopHospital",     "Hospital"),
            ("ShopBlacksmith",   "Blacksmith"),
            ("ShopSaloon",       "Saloon"),
            ("ShopJoja",         "Joja"),
            ("ShopDwarf",        "Dwarf"),
            ("ShopSandy",        "Sandy"),
            ("ShopTraveler",     "Traveler"),
            ("ShopCasino",       "Casino"),
            ("ShopAdventure",    "AdventureShop"),
            ("ShopDesertTrade",  "DesertTrade"),
            ("ShopIslandTrade",  "IslandTrade"),
            ("ShopShadow",       "ShadowShop"),
            ("ShopIceCream",     "IceCreamStand"),
            ("ShopHatMouse",     "HatMouse"),
            ("ShopClintUpgrade", "ClintUpgrade"),
            ("ShopRecovery",     "AdventureGuildRecovery"),
                        ("ShopBookseller",   "Bookseller"),
        };

        foreach (var (id, shopId) in vanillaShops)
        {
            entries.Add(new MenuEntry(id, execute: OpenRemoteShop, category: "WorkbenchShop", argument: shopId));
        }

        entries.Add(new MenuEntry(
            "ShopSVE", execute: OpenRemoteShop, category: "WorkbenchCompatShop",
            condition: () => _helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP"),
            argument: "SVE_ShopId_Placeholder"));

        entries.Add(new MenuEntry(
            "ShopRSV", execute: OpenRemoteShop, category: "WorkbenchCompatShop",
            condition: () => _helper.ModRegistry.IsLoaded("Rafseazz.RidgesideVillage"),
            argument: "RSV_ShopId_Placeholder"));

        return entries;
    }

    // ═════════════ 工作室执行方法 ═════════════

    private void OpenTailoringMenu(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new TailoringMenu();
        Game1.playSound("bigSelect");
    }

    private void OpenDyeMenu(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new DyeMenu();
        Game1.playSound("bigSelect");
    }

    private void OpenWizardShrine(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new CharacterCustomization(CharacterCustomization.Source.Wizard);
        Game1.playSound("bigSelect");
    }

    private void OpenPrizeMachine(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var menuType = Type.GetType("StardewValley.Menus.PrizeTicketMenu, Stardew Valley");
        if (menuType == null)
        {
            _monitor.Log("未找到兑奖机菜单，请前往刘易斯家使用。", LogLevel.Warn);
            Game1.showGlobalMessage("兑奖机位于刘易斯家。");
            return;
        }
        try
        {
            Game1.activeClickableMenu = (IClickableMenu?)Activator.CreateInstance(menuType);
            Game1.playSound("bigSelect");
        }
        catch (Exception ex)
        {
            _monitor.Log($"打开兑奖机失败：{ex.Message}", LogLevel.Warn);
        }
    }

    private void OpenBobberMachine(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        _monitor.Log("浮标机请前往威利的鱼店使用。", LogLevel.Info);
        Game1.showGlobalMessage("浮标机位于威利的鱼店内。");
    }

    private void UnifyFlowerColor(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        _monitor.Log("花色统一功能将在后续版本中完善。", LogLevel.Info);
        Game1.showGlobalMessage("花色统一功能将在后续版本中完善。");
    }

    private void OpenRemoteShop(MainMenu _, MenuEntry entry)
    {
        if (!TryPrepareMenu()) return;
        string? shopId = entry.Argument;
        if (string.IsNullOrWhiteSpace(shopId))
        {
            _monitor.Log($"商店条目 '{entry.Id}' 未提供有效的 shopId。", LogLevel.Warn);
            return;
        }
        Utility.TryOpenShopMenu(shopId, (string?)null);
        Game1.playSound("bigSelect");
    }

    // ═════════════ D. 设施与存取 ═════════════

    /// <summary>买背包：1.6 无独立 UI，背包升级是 Pierre 商店里的特殊商品。这里打开 Pierre 商店。</summary>
    private void OpenBuyBackpack(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        _monitor.Log("背包升级在 Pierre 商店的背包标签，已打开 Pierre 商店。", LogLevel.Info);
        Utility.TryOpenShopMenu("SeedShop", (string?)null);
        Game1.playSound("bigSelect");
    }

    /// <summary>祝尼魔箱：遍历所有已加载地图找玩家放置的 Junimo Chest。</summary>
    private void OpenJunimoChest(MainMenu _, MenuEntry __)
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

    private void OpenShippingBin(MainMenu _, MenuEntry __)
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

    private void OpenDresserFurniture(MainMenu _, MenuEntry __)
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
    private void OpenMasterySkill(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new MasteryTrackerMenu();
        Game1.playSound("bigSelect");
    }

    /// <summary>杀怪奖励：打开冒险家公会的领取奖励界面（模拟 Action "Gil"）。</summary>
    private void OpenMonsterKillReward(MainMenu _, MenuEntry __)
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

    private void OpenCalendar(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new Billboard();
        Game1.playSound("bigSelect");
    }

    private void OpenQuestBoard(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new QuestLog();
        Game1.playSound("bigSelect");
    }

    private void OpenSpecialOrder(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new SpecialOrdersBoard();
        Game1.playSound("bigSelect");
    }

    private void OpenWalnutRoomOrder(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new SpecialOrdersBoard("Qi");
        Game1.playSound("bigSelect");
    }

    private void OpenFarmComputer(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var farmComputer = Game1.player.Items
            .OfType<StardewValley.Object>()
            .FirstOrDefault(item => item.Name == "Farm Computer");

        if (farmComputer == null)
        {
            _monitor.Log("背包里没有找到农场电脑。", LogLevel.Warn);
            Game1.showGlobalMessage("背包里没有农场电脑。");
            return;
        }
        farmComputer.checkForAction(Game1.player);
    }

    private void OpenMonsterKill(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，使用自绘版本。", LogLevel.Warn);
            Game1.activeClickableMenu = new MonsterStatsMenu();
            Game1.playSound("bigSelect");
            return;
        }

        var model = new MonsterKillViewModel();
        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/MonsterKillView";
        var menu = viewEngine.CreateMenuFromAsset(assetName, model);

        if (menu is null)
        {
            _monitor.Log($"无法创建 MonsterKillView：{assetName}，降级到自绘版。", LogLevel.Error);
            Game1.activeClickableMenu = new MonsterStatsMenu();
            Game1.playSound("bigSelect");
            return;
        }

        Game1.activeClickableMenu = menu;
        Game1.playSound("bigSelect");
    }

    private void OpenCasinoStatistic(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 直接调用游戏内置的 FarmerFile 动作，打开赌场终端同款的统计数据对话
        Game1.currentLocation.performAction("FarmerFile", Game1.player, new xTile.Dimensions.Location(0, 0));

        Game1.playSound("bigSelect");
    }

    private void OpenFriendship(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        var viewEngine = ModEntry.ViewEngine;
        if (viewEngine is null)
        {
            _monitor.Log("StardewUI API 不可用，使用自绘版本。", LogLevel.Warn);
            Game1.activeClickableMenu = new FriendshipMenu();
            Game1.playSound("bigSelect");
            return;
        }

        var model = new FriendshipViewModel(_helper.Translation);
        string assetName = $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/Views/FriendshipView";
        var menu = viewEngine.CreateMenuFromAsset(assetName, model);

        if (menu is null)
        {
            _monitor.Log($"无法创建 FriendshipView：{assetName}，降级到自绘版。", LogLevel.Error);
            Game1.activeClickableMenu = new FriendshipMenu();
            Game1.playSound("bigSelect");
            return;
        }

        Game1.activeClickableMenu = menu;
        Game1.playSound("bigSelect");
    }

    private void OpenGoldenWalnutsInfo(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        int found = Game1.netWorldState.Value.GoldenWalnuts;

        Game1.activeClickableMenu = new GoldenWalnutsMenu(
            title: I18n.RelaxValley_Menu_GoldenWalnutsInfo_Title(),
            summary: _helper.Translation
              .Get(I18n.Keys.RelaxValley_Menu_GoldenWalnutsInfo_Summary)
              .Tokens(new
              {
                  found = found.ToString(),
                  total = TotalGoldenWalnuts.ToString()
              })
              .ToString(),
            hint: I18n.RelaxValley_Menu_GoldenWalnutsInfo_Hint(),
            found: found,
            total: TotalGoldenWalnuts);

        Game1.playSound("bigSelect");
    }

    // ═════════════ B. 地图与传送 ═════════════

    private void OpenMap(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;
        Game1.activeClickableMenu = new MapPage(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        Game1.playSound("bigSelect");
    }

    // ═════════════ C. 远程电梯 ═════════════

    private void OpenMineElevator(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 矿井电梯标准层：0, 5, 10, ..., 120
        var levels = new List<int>();
        for (int i = 0; i <= 120; i += 5) levels.Add(i);

        Game1.activeClickableMenu = new ElevatorMenu(
            title: "矿井电梯",
            levels: levels,
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
            monitor: _monitor);

        Game1.playSound("bigSelect");
    }

    private void OpenSkullElevator(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 1. 用 deepestMineLevel 计算骷髅矿洞进度
        int deepestMineLevel = Game1.player.deepestMineLevel;
        int deepestSkullLevel = deepestMineLevel - 120; // 骷髅矿洞实际层数 = 内部编号 - 120

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
        Game1.activeClickableMenu = new ElevatorMenu(
            title: "骷髅矿洞电梯",
            levels: levels,
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
            monitor: _monitor);

        Game1.playSound("bigSelect");
    }

    private void OpenVolcanoElevator(MainMenu _, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        // 1.6 火山地牢无公开 API 直接传送到指定层，先占位
        _monitor.Log("火山电梯暂未完全支持，请前往火山地牢。", LogLevel.Info);
        Game1.showGlobalMessage("火山电梯暂未支持，请前往火山地牢。");
    }

    // ═════════════ D. 扩展与入口 ═════════════

    private void OpenWorkbench(MainMenu source, MenuEntry __)
    {
        if (!TryPrepareMenu()) return;

        Game1.activeClickableMenu = new WorkbenchMenu(
            source.Translation,
            source.Monitor,
            source.AllEntries,
            GetWorkbenchEntries());

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