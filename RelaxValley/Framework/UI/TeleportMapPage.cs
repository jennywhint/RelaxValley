using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    /// <summary>独立打开时没有 GameMenu 转发悬停事件，自己刷新一次提示文本。</summary>
    public override void draw(SpriteBatch b)
    {
        performHoverAction(Game1.getMouseX(), Game1.getMouseY());
        base.draw(b);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach (ClickableComponent c in points.Values)
        {
            if (!c.bounds.Contains(x, y)) continue;

            // 点击点换算为地图像素坐标（×4，原点为地图左上角），用于精确匹配世界位置
            var mapPoint = new Point(x - mapBounds.X, y - mapBounds.Y);

            if (MapTeleport.TryGetWarpTargetAt(c.name, mapPoint, out string location, out Point tile))
            {
                exitThisMenu(false);
                MapTeleport.WarpTo(location, tile);
                return;
            }

            // 与原版一致的音效彩蛋（贝簋石 / 下水道）
            if (c.name == "Beach/LonelyStone") Game1.playSound("stoneCrack");
            else if (c.name == "Forest/SewerPipe") Game1.playSound("shadowpeep");
            return;
        }

        if (Game1.activeClickableMenu is GameMenu gameMenu)
        {
            gameMenu.changeTab(gameMenu.lastOpenedNonMapTab);
        }
    }
}
