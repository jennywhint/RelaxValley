using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.WorldMaps;
using System;
using System.Collections.Generic;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 地图点击传送的核心逻辑（原版地图与模组地图通用）。
/// 到达点不手写坐标：把点击的地图区域（Data/WorldMap 的 MapArea）展开成它覆盖的地点名，
/// 再用「目标地点」反查全世界 Warp 得到到达格；门禁与原版进入条件一致。
/// </summary>
internal static class MapTeleport
{
    /// <summary>农场建筑内部名 → 显示名 i18n 键（地图标记用）。</summary>
    private static readonly Dictionary<string, string> BuildingNameKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Coop"] = "RelaxValley.UI.Building.Coop",
            ["Barn"] = "RelaxValley.UI.Building.Barn",
            ["Silo"] = "RelaxValley.UI.Building.Silo",
            ["Greenhouse"] = "RelaxValley.UI.Building.Greenhouse",
            ["Shed"] = "RelaxValley.UI.Building.Shed",
            ["Stable"] = "RelaxValley.UI.Building.Stable",
            ["Fish Pond"] = "RelaxValley.UI.Building.FishPond",
            ["Well"] = "RelaxValley.UI.Building.Well",
            ["Mill"] = "RelaxValley.UI.Building.Mill",
            ["Cabin"] = "RelaxValley.UI.Building.Cabin",
            ["Shipping Bin"] = "RelaxValley.UI.Building.ShippingBin",
            ["Gold Clock"] = "RelaxValley.UI.Building.GoldClock",
            ["Junimo Hut"] = "RelaxValley.UI.Building.JunimoHut",
            ["Earth Obelisk"] = "RelaxValley.UI.Building.EarthObelisk",
            ["Water Obelisk"] = "RelaxValley.UI.Building.WaterObelisk",
            ["Desert Obelisk"] = "RelaxValley.UI.Building.DesertObelisk",
            ["Island Obelisk"] = "RelaxValley.UI.Building.IslandObelisk",
            ["Pet Bowl"] = "RelaxValley.UI.Building.PetBowl",
        };

    public static string GetBuildingDisplayName(string buildingType)
    {
        if (!BuildingNameKeys.TryGetValue(buildingType, out string? key)) return buildingType;

        // SMAPI 的 Translation.ToString() 标注为可空（隐式转换同样可能为 null），补 ?? 兜底；
        // 缺失键返回空串时回落到建筑内部名
        string translated = ModEntry.Instance.Helper.Translation.Get(key).ToString() ?? "";
        return string.IsNullOrWhiteSpace(translated) || translated == key ? buildingType : translated;
    }

    /// <summary>收集某个地图区域（按 MapArea.Id）覆盖的所有地点名，原版与模组区域通用。</summary>
    public static HashSet<string> GetAreaLocationNames(string areaId)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (MapRegion region in WorldMapManager.GetMapRegions())
        {
            foreach (MapArea area in region.GetAreas())
            {
                if (!string.Equals(area.Id, areaId, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (MapAreaPosition position in area.GetWorldPositions())
                {
                    if (!string.IsNullOrWhiteSpace(position.Data.LocationName))
                        names.Add(position.Data.LocationName);

                    if (position.Data.LocationNames is null) continue;
                    foreach (string name in position.Data.LocationNames)
                    {
                        if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                    }
                }
            }
        }

        return names;
    }

    /// <summary>门禁与原版进入条件一致：沙漠 = 大巴修好，姜岛 = 船修好，其余默认可传送。</summary>
    public static bool CanTeleportToLocation(string locationName)
    {
        if (string.Equals(locationName, "Desert", StringComparison.OrdinalIgnoreCase))
            return Game1.MasterPlayer.mailReceived.Contains("ccVault");

        if (locationName.StartsWith("Island", StringComparison.Ordinal))
            return Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed");

        return true;
    }

    /// <summary>
    /// 解析地图区域 → 传送目标：区域地点名集合反查全世界 Warp。
    /// 未解锁或找不到入口 Warp 时返回 false（点击保持原版行为）。
    /// </summary>
    public static bool TryGetWarpTarget(string areaId, out string locationName, out Point tile)
        => TryGetWarpTargetAt(areaId, null, out locationName, out tile);

    /// <summary>
    /// 带点击位置的版本：优先匹配点击点所在的**专属**地图位置（WorldPosition 自带像素区），
    /// 用它的地点名反查 Warp——一个区域（area）覆盖多个地点时（如「柳巷」与电影院同区），
    /// 点哪就传哪，不再都落到第一个命中的 Warp 上。无精确命中时回退到整区域集合。
    /// </summary>
    /// <param name="mapPoint4x">点击点换算成的地图像素坐标（×4 尺度，原点为地图左上角）。</param>
    public static bool TryGetWarpTargetAt(string areaId, Point? mapPoint4x, out string locationName, out Point tile)
    {
        locationName = "";
        tile = Point.Zero;

        // 1) 精确匹配：点击点落在某个带专属像素区的世界位置内
        if (mapPoint4x.HasValue)
        {
            foreach (MapRegion region in WorldMapManager.GetMapRegions())
            {
                foreach (MapArea area in region.GetAreas())
                {
                    if (!string.Equals(area.Id, areaId, StringComparison.OrdinalIgnoreCase)) continue;

                    foreach (MapAreaPosition position in area.GetWorldPositions())
                    {
                        if (position.Data.MapPixelArea.IsEmpty) continue; // 无专属像素区的位置不参与精确匹配

                        if (!position.GetPixelArea().Contains(mapPoint4x.Value)) continue;

                        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(position.Data.LocationName))
                            names.Add(position.Data.LocationName);
                        if (position.Data.LocationNames is not null)
                            foreach (string name in position.Data.LocationNames)
                                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);

                        if (TryWarpForNames(names, out locationName, out tile)) return true;
                    }
                }
            }
        }

        // 2) 回退：整个区域的地点名集合反查
        HashSet<string> areaNames = GetAreaLocationNames(areaId);
        if (areaNames.Count == 0) return false;
        return TryWarpForNames(areaNames, out locationName, out tile);
    }

    /// <summary>遍历全世界 Warp，找目标地点名集合中的入口（含门禁判断）。</summary>
    private static bool TryWarpForNames(HashSet<string> names, out string locationName, out Point tile)
    {
        locationName = "";
        tile = Point.Zero;
        if (names.Count == 0) return false;

        foreach (GameLocation loc in Game1.locations)
        {
            foreach (Warp warp in loc.warps)
            {
                if (!names.Contains(warp.TargetName)) continue;
                if (!CanTeleportToLocation(warp.TargetName)) continue;

                locationName = warp.TargetName;
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
