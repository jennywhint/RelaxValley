using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 原版世界地图 + 点击传送。
/// 左键点击有提示框的区域时传送到对应地点，其余行为与原版完全一致。
/// </summary>
public sealed class TeleportMapPage : MapPage
{
    public TeleportMapPage()
        : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
    {
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach (var pair in points)
        {
            if (!pair.Value.bounds.Contains(x, y)) continue;

            // NamespacedId 形如 "Town/Default"，前缀是地图区域 ID
            string areaId = pair.Key.Split('/')[0];

            if (MapTeleport.TryGetWarpTarget(areaId, out string location, out Point tile))
            {
                exitThisMenu(false);
                MapTeleport.WarpTo(location, tile);
                return;
            }

            // 未解锁或不可传送的区域：保持原版行为
            break;
        }

        base.receiveLeftClick(x, y, playSound);
    }
}
