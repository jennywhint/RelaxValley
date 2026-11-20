using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.WorldMaps;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 指定区域的世界地图查看器，并支持点击传送。
/// 底图 / 区域贴图 / 悬浮提示全部来自原版 WorldMaps 数据；
/// 农场区域会额外把农场建筑标记绘制到地图对应位置（农场绘制）。
/// </summary>
public sealed class RegionMapMenu : IClickableMenu
{
    private readonly MapRegion _region;
    private readonly MapArea[] _areas;
    private readonly Rectangle _mapBounds;
    private readonly List<(Rectangle Bounds, string Text, string AreaId)> _tooltips = new();
    private readonly List<(Vector2 Position, string Label)> _buildingMarkers = new();

    private string _hoverText = "";

    public RegionMapMenu(MapRegion region)
        : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: true)
    {
        _region = region;
        _areas = region.GetAreas();
        _mapBounds = region.GetMapPixelBounds();

        foreach (MapArea area in _areas)
        {
            foreach (MapAreaTooltip tooltip in area.GetTooltips())
            {
                Rectangle pixelArea = tooltip.GetPixelArea();
                pixelArea = new Rectangle(
                    _mapBounds.X + pixelArea.X,
                    _mapBounds.Y + pixelArea.Y,
                    pixelArea.Width,
                    pixelArea.Height);

                _tooltips.Add((pixelArea, tooltip.Text, tooltip.NamespacedId));
            }
        }

        CollectFarmBuildingMarkers();
    }

    /// <summary>农场绘制：把农场建筑标记到地图对应位置（仅农场所在区域）。</summary>
    private void CollectFarmBuildingMarkers()
    {
        if (_areas.All(a => !string.Equals(a.Id, "Farm", StringComparison.OrdinalIgnoreCase))) return;

        Farm? farm = Game1.getFarm();
        if (farm is null) return;

        foreach (Building building in farm.buildings)
        {
            Point tile = new(building.tileX.Value, building.tileY.Value);
            MapAreaPositionWithContext? pos = WorldMapManager.GetPositionData(farm, tile);
            if (!pos.HasValue) continue;
            if (!string.Equals(pos.Value.Data.Region.Id, _region.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            Vector2 pixel = pos.Value.GetMapPixelPosition() + new Vector2(_mapBounds.X, _mapBounds.Y);
            _buildingMarkers.Add((pixel, MapTeleport.GetBuildingDisplayName(building.buildingType.Value)));
        }
    }

    public override void draw(SpriteBatch b)
    {
        // 每帧按当前鼠标位置刷新悬浮提示
        performHoverAction(Game1.getMouseX(), Game1.getMouseY());

        // 原版地图的对话框底框
        Game1.drawDialogueBox(
            _mapBounds.X - 32,
            _mapBounds.Y - 96,
            (_mapBounds.Width + 16) * 4,
            (_mapBounds.Height + 32) * 4,
            speaker: false,
            drawOnlyBox: true);

        float sortLayer = 0.86f;

        MapAreaTexture? baseTexture = _region.GetBaseTexture();
        if (baseTexture is not null)
        {
            Rectangle destRect = baseTexture.GetOffsetMapPixelArea(_mapBounds.X, _mapBounds.Y);
            b.Draw(baseTexture.Texture, destRect, baseTexture.SourceRect,
                Color.White, 0f, Vector2.Zero, SpriteEffects.None, sortLayer);
            sortLayer += 0.001f;
        }

        foreach (MapArea area in _areas)
        {
            foreach (MapAreaTexture overlay in area.GetTextures())
            {
                Rectangle destRect = overlay.GetOffsetMapPixelArea(_mapBounds.X, _mapBounds.Y);
                b.Draw(overlay.Texture, destRect, overlay.SourceRect,
                    Color.White, 0f, Vector2.Zero, SpriteEffects.None, sortLayer);
                sortLayer += 0.001f;
            }
        }

        DrawBuildingMarkers(b);
        DrawHoverTooltip(b);

        string hint = I18n.RelaxValley_UI_Map_CloseHint();
        Vector2 hintSize = Game1.smallFont.MeasureString(hint);

        // GetMapPixelBounds() 返回的宽高是“地图单位”（实际像素是 ×4），
        // 提示按 ×4 换算画到地图实际底部。
        float hintY = _mapBounds.Y + _mapBounds.Height * 4 + 40f;
        hintY = Math.Min(hintY, Game1.uiViewport.Height - 56f);

        b.DrawString(
            Game1.smallFont,
            hint,
            new Vector2((Game1.uiViewport.Width - hintSize.X) / 2f, hintY),
            Game1.textColor * 0.8f);

        base.draw(b);
        drawMouse(b);
    }

    private void DrawBuildingMarkers(SpriteBatch b)
    {
        foreach ((Vector2 position, string label) in _buildingMarkers)
        {
            Vector2 size = Game1.smallFont.MeasureString(label);
            var background = new Rectangle(
                (int)position.X - 6,
                (int)position.Y - 6,
                (int)size.X + 12,
                (int)size.Y + 10);

            b.Draw(Game1.fadeToBlackRect, background, Color.Black * 0.55f);
            b.DrawString(Game1.smallFont, label, position, Color.Wheat);
        }
    }

    private void DrawHoverTooltip(SpriteBatch b)
    {
        if (string.IsNullOrWhiteSpace(_hoverText)) return;

        Vector2 position = new(Game1.getMouseX() + 32, Game1.getMouseY() + 32);
        Vector2 size = Game1.smallFont.MeasureString(_hoverText);

        b.Draw(Game1.fadeToBlackRect,
            new Rectangle((int)position.X - 8, (int)position.Y - 8,
                (int)size.X + 16, (int)size.Y + 16),
            Color.Black * 0.65f);

        b.DrawString(Game1.smallFont, _hoverText, position, Game1.textColor);
    }

    public override void performHoverAction(int x, int y)
    {
        _hoverText = "";
        if (!_mapBounds.Contains(x, y)) return;

        foreach ((Rectangle bounds, string text, _) in _tooltips)
        {
            if (!bounds.Contains(x, y)) continue;

            _hoverText = text;
            return;
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach ((Rectangle bounds, _, string areaId) in _tooltips)
        {
            if (!bounds.Contains(x, y)) continue;

            if (MapTeleport.TryGetWarpTarget(areaId, out string location, out Point tile))
            {
                exitThisMenu(false);
                MapTeleport.WarpTo(location, tile);
                return;
            }

            // 区域存在但不可传送（未解锁等）：提示音，保持菜单打开
            Game1.playSound("cancel");
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        exitThisMenu(playSound);
    }
}
