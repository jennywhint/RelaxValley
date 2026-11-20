using StardewValley;
using System;
using System.Collections.Generic;

namespace RelaxValley.Framework.Shops;

/// <summary>
/// 记录玩家在原版里“打开过一次”的商店。
/// 工作室只有在商店被原版打开过之后才允许远程打开，
/// 这样皮埃尔、矮人、姜岛、火山、赌场等都会自然跟随存档进度，
/// 不需要给每个商店手写解锁条件。
/// </summary>
public static class ShopDiscovery
{
    private const string Key = "Awassakura.RelaxValley/VisitedShops";

    /// <summary>发现记录的数据版本。旧版本曾无条件补记商店，升级后需要清空重算一次。</summary>
    private const string VersionKey = "Awassakura.RelaxValley/VisitedShopsVersion";
    private const int SchemaVersion = 2;

    /// <summary>
    /// 商店家族归一化。
    /// 旅行商人 / 书商这类“定期出现、且不同地区可能有不同 ShopId”的商店
    /// （原版森林、沙漠节、SVE、RSV 等模组各有自己的 ID），
    /// 只要在任何地方打开过一次，就永久记住整个家族，
    /// 之后到了出现日期直接可远程打开，不用再跑一次。
    /// </summary>
    private static string NormalizeKey(string shopId)
    {
        string lower = shopId.ToLowerInvariant();

        if (lower.Contains("travel"))
            return "Family:TravelingCart";

        if (lower.Contains("bookseller") || lower.Contains("book_seller"))
            return "Family:Bookseller";

        return shopId;
    }

    public static bool IsVisited(string? shopId)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return false;
        return Load().Contains(NormalizeKey(shopId));
    }

    public static void MarkVisited(string? shopId)
    {
        if (string.IsNullOrWhiteSpace(shopId) || Game1.player is null) return;

        var visited = Load();
        if (visited.Add(NormalizeKey(shopId)))
            Game1.player.modData[Key] = string.Join(",", visited);
    }

    /// <summary>
    /// 读档时调用：按存档进度把老存档里“必然已经打开过”的原版商店补记为已发现，
    /// 这样已经玩了很久的存档不用再挨个跑一趟。
    /// 新存档不做任何补记，避免一进游戏所有商店就全部点亮。
    /// </summary>
    public static void InitializeFromSave()
    {
        if (Game1.player is null) return;
        Farmer who = Game1.player;

        // 旧版本（v1）会无条件把常驻商店补记为已发现，导致新存档一进去就全亮。
        // 这里做一次性迁移：清空旧记录，再按下面的新规则重新计算。
        string rawVersion = who.modData.TryGetValue(VersionKey, out string? value) ? value ?? "" : "";
        if (!int.TryParse(rawVersion, out int storedVersion) || storedVersion < SchemaVersion)
        {
            // 用空串清空，避免依赖不同版本 modData 的 Remove 重载
            who.modData[Key] = "";
            who.modData[VersionKey] = SchemaVersion.ToString();
        }

        // 常驻柜台：只有已经玩满一个季节（28 天）的存档才补记，
        // 这种进度的存档正常游玩必然已在皮埃尔 / 玛妮 / 铁匠 / 罗宾等处买过东西。
        // 新存档（1~27 天）保持“必须亲自打开一次”的规则。
        if (who.stats.DaysPlayed >= 28)
        {
            foreach (string shopId in new[]
            {
                Game1.shop_generalStore,       // 皮埃尔杂货铺
                Game1.shop_jojaMart,           // Joja 超市
                Game1.shop_carpenter,          // 罗宾木匠商店
                Game1.shop_blacksmith,         // 铁匠铺
                Game1.shop_blacksmithUpgrades, // 铁匠铺工具升级
                Game1.shop_animalSupplies,     // 玛妮牧场
                Game1.shop_fish,               // 威利鱼店
                Game1.shop_hospital,           // 医院药店
                Game1.shop_saloon,             // 格斯酒吧
            })
            {
                MarkVisited(shopId);
            }
        }

        // 进度型商店：下面的存档标记本身就证明玩家已经去过对应地点 / 完成了对应前置，
        // 任何存档都适用，不会把没去过的地方点亮。
        if (who.canUnderstandDwarves) MarkVisited(Game1.shop_dwarf);
        if (who.hasClubCard) MarkVisited(Game1.shop_casino);
        if (who.hasRustyKey) MarkVisited(Game1.shop_krobus);

        // 沙漠：社区中心金库完成 → 大巴修好，沙漠商店必然能去
        if (Game1.MasterPlayer.mailReceived.Contains("ccVault"))
            MarkVisited(Game1.shop_desertTrader);

        // 姜岛：拿到过金核桃说明已经上岛
        if (Game1.netWorldState.Value.GoldenWalnutsFound > 0)
        {
            MarkVisited(Game1.shop_islandTrader);
            MarkVisited(Game1.shop_volcanoShop);
        }

        // 核桃房：100 金核桃开门（与原版存档迁移同款判断）
        if (StardewValley.Locations.IslandWest.IsQiWalnutRoomDoorUnlocked(out _))
            MarkVisited(Game1.shop_qiGemShop);
    }

    private static HashSet<string> Load()
    {
        if (Game1.player is null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string raw = Game1.player.modData.ContainsKey(Key)
            ? Game1.player.modData[Key]
            : "";

        return new HashSet<string>(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}
