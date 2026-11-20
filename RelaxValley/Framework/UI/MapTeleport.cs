using Microsoft.Xna.Framework;
using StardewValley;
using System;
using System.Collections.Generic;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 地图点击传送的核心逻辑。
/// 到达点不手写坐标：收集全世界所有 Warp，用「目标地点」反查到达格，
/// 因此自然跟随游戏进度（大巴 / 船未修好时对应区域不可传送）。
/// </summary>
internal static class MapTeleport
{
    /// <summary>世界地图区域 ID → 地点名（原版区域与地点同名）。</summary>
    private static readonly Dictionary<string, string> AreaLocations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Farm"] = "Farm",
            ["Town"] = "Town",
            ["Beach"] = "Beach",
            ["Mountain"] = "Mountain",
            ["Forest"] = "Forest",
            ["BusStop"] = "BusStop",
            ["Desert"] = "Desert",
            ["Railroad"] = "Railroad",
            ["IslandSouth"] = "IslandSouth",
            ["IslandNorth"] = "IslandNorth",
            ["IslandWest"] = "IslandWest",
            ["IslandEast"] = "IslandEast",
            ["IslandSouthEast"] = "IslandSouthEast",
        };

    /// <summary>农场建筑内部名 → 中文显示名（地图标记用）。</summary>
    private static readonly Dictionary<string, string> BuildingNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Coop"] = "鸡舍",
            ["Barn"] = "畜棚",
            ["Silo"] = "筒仓",
            ["Greenhouse"] = "温室",
            ["Shed"] = "小屋",
            ["Stable"] = "马厩",
            ["Fish Pond"] = "鱼塘",
            ["Well"] = "水井",
            ["Mill"] = "磨坊",
            ["Cabin"] = "木屋",
            ["Shipping Bin"] = "出货箱",
            ["Gold Clock"] = "黄金时钟",
            ["Junimo Hut"] = "祝尼魔小屋",
            ["Earth Obelisk"] = "大地图腾柱",
            ["Water Obelisk"] = "水地图腾柱",
            ["Desert Obelisk"] = "沙漠图腾柱",
            ["Island Obelisk"] = "姜岛图腾柱",
            ["Pet Bowl"] = "宠物碗",
        };

    public static string GetBuildingDisplayName(string buildingType) =>
        BuildingNames.TryGetValue(buildingType, out string? name) ? name : buildingType;

    /// <summary>区域是否当前可传送（沙漠需大巴修好、姜岛需船修好，与原版门禁一致）。</summary>
    public static bool CanTeleport(string areaId)
    {
        if (!AreaLocations.TryGetValue(areaId, out string? location)) return false;

        if (location == "Desert")
            return Game1.MasterPlayer.mailReceived.Contains("ccVault");

        if (location.StartsWith("Island", StringComparison.Ordinal))
            return Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed");

        return true;
    }

    /// <summary>
    /// 解析区域 → 传送目标。到达点取自全世界 Warp 的目标格；
    /// 未解锁或找不到入口时返回 false（点击保持原版行为）。
    /// </summary>
    public static bool TryGetWarpTarget(string areaId, out string locationName, out Point tile)
    {
        locationName = "";
        tile = Point.Zero;

        if (!AreaLocations.TryGetValue(areaId, out string? location)) return false;
        if (!CanTeleport(areaId)) return false;

        foreach (GameLocation loc in Game1.locations)
        {
            foreach (Warp warp in loc.warps)
            {
                if (!string.Equals(warp.TargetName, location, StringComparison.OrdinalIgnoreCase))
                    continue;

                locationName = location;
                tile = new Point(warp.TargetX, warp.TargetY);
                return true;
            }
        }

        return false;
    }

    public static void WarpTo(string locationName, Point tile)
    {
        Game1.playSound("wand");
        Game1.warpFarmer(locationName, tile.X, tile.Y, false);
    }
}
