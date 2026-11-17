using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 可滚动的网格菜单基类。负责滚动条、鼠标滚轮、布局计算。
/// 子类只需实现 DrawCell 和 OnCellClicked。
/// </summary>
public abstract class ScrollableGridMenu : IClickableMenu
{
    protected readonly int Columns;
    protected readonly int CellWidth;
    protected readonly int CellHeight;
    protected readonly int CellGap;

    private int _scrollOffset;
    private int _contentHeight;
    private readonly List<(Rectangle Bounds, int DataIndex)> _cellRects = new();

    protected abstract int ItemCount { get; }
    protected virtual int TopPadding => 96;
    protected virtual int BottomPadding => 88;
    protected virtual int SidePadding => 36;

    protected Rectangle ContentBounds => new(
        xPositionOnScreen + SidePadding,
        yPositionOnScreen + TopPadding,
        width - SidePadding * 2,
        height - TopPadding - BottomPadding);

    protected ScrollableGridMenu(int columns, int cellWidth, int cellHeight, int cellGap)
        : base(
            x: 30, y: 50,
            width: Game1.uiViewport.Width - 60,
            height: Game1.uiViewport.Height - 100,
            showUpperRightCloseButton: true)
    {
        Columns = columns;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        CellGap = cellGap;
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        var content = ContentBounds;
        _cellRects.Clear();
        _contentHeight = CalculateContentHeight();

        int colW = (content.Width - (Columns - 1) * CellGap) / Columns;
        int startRow = Math.Max(0, _scrollOffset / (CellHeight + CellGap));
        int endRow = Math.Min(TotalRows() - 1, (_scrollOffset + content.Height) / (CellHeight + CellGap));

        for (int i = startRow * Columns; i < ItemCount && i < (endRow + 1) * Columns; i++)
        {
            int row = i / Columns;
            int col = i % Columns;
            int x = content.X + col * (colW + CellGap);
            int y = content.Y - _scrollOffset + row * (CellHeight + CellGap);

            var bounds = new Rectangle(x, y, colW, CellHeight);
            if (bounds.Y >= content.Y && bounds.Bottom <= content.Bottom)
            {
                DrawCell(b, bounds, i);
                _cellRects.Add((bounds, i));
            }
        }

        DrawScrollbar(b, content);
        base.draw(b);
        drawMouse(b);
    }

    protected abstract void DrawCell(SpriteBatch b, Rectangle bounds, int dataIndex);
    protected abstract void OnCellClicked(int dataIndex);

    private int TotalRows() => (ItemCount + Columns - 1) / Columns;
    private int CalculateContentHeight() => TotalRows() * (CellHeight + CellGap);

    private void DrawScrollbar(SpriteBatch b, Rectangle content)
    {
        int maxScroll = Math.Max(0, _contentHeight - content.Height);
        if (maxScroll <= 0) return;
        int barX = xPositionOnScreen + width - 22;
        b.Draw(Game1.staminaRect, new Rectangle(barX, content.Y, 10, content.Height), Color.Black * 0.3f);
        float ratio = (float)content.Height / _contentHeight;
        int thumbH = Math.Max(30, (int)(content.Height * ratio));
        int thumbY = content.Y + (int)((content.Height - thumbH) * ((float)_scrollOffset / maxScroll));
        b.Draw(Game1.staminaRect, new Rectangle(barX, thumbY, 10, thumbH), Color.Wheat);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        foreach (var (bounds, index) in _cellRects)
        {
            if (!bounds.Contains(x, y)) continue;
            OnCellClicked(index);
            return;
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true) => exitThisMenu(playSound);

    public override void receiveScrollWheelAction(int direction)
    {
        int maxScroll = Math.Max(0, _contentHeight - ContentBounds.Height);
        _scrollOffset = Math.Clamp(_scrollOffset + (direction > 0 ? -60 : 60), 0, maxScroll);
    }
}