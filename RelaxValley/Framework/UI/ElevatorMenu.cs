using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 通用电梯 UI。矿井 / 骷髅矿洞 / 火山地牢共用。
/// 传入楼层列表 + 选中后的回调，UI 只负责显示与点击，传送逻辑由外部决定。
/// 6 列网格布局，支持滚轮滚动。
/// </summary>
public sealed class ElevatorMenu : IClickableMenu
{
    private const int Columns = 6;
    private const int ColGap = 12;
    private const int ButtonHeight = 56;
    private const int SidePadding = 36;
    private const int ContentTopPadding = 96;
    private const int ContentBottomPadding = 88;

    private readonly string _title;
    private readonly List<int> _levels;
    private readonly Action<int> _onSelect;
    private readonly IMonitor _monitor;

    private int _scrollOffset;
    private int _contentHeight;
    private readonly List<(Rectangle Bounds, int Level)> _buttonRects = new();

    private Rectangle ContentBounds => new(
        xPositionOnScreen + SidePadding,
        yPositionOnScreen + ContentTopPadding,
        width - SidePadding * 2,
        height - ContentTopPadding - ContentBottomPadding);

    private int ColumnWidth => (ContentBounds.Width - (Columns - 1) * ColGap) / Columns;

    public ElevatorMenu(string title, List<int> levels, Action<int> onSelect, IMonitor monitor)
        : base(
            x: 30, y: 50,
            width: Game1.uiViewport.Width - 60,
            height: Game1.uiViewport.Height - 100,
            showUpperRightCloseButton: true)
    {
        _title = title;
        _levels = levels;
        _onSelect = onSelect;
        _monitor = monitor;
        _contentHeight = CalculateContentHeight();
    }

    private int CalculateContentHeight()
    {
        int rows = (_levels.Count + Columns - 1) / Columns;
        return rows * (ButtonHeight + ColGap);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
               Game1.graphics.GraphicsDevice.Viewport.Bounds,
               Color.Black * 0.5f);

        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        // 标题
        var titleSize = Game1.dialogueFont.MeasureString(_title);
        b.DrawString(Game1.dialogueFont, _title,
            new Vector2(xPositionOnScreen + (width - titleSize.X) / 2f, yPositionOnScreen + 36),
            Game1.textColor);

        // 楼层按钮
        var content = ContentBounds;
        _buttonRects.Clear();
        _contentHeight = CalculateContentHeight();

        int colW = ColumnWidth;
        for (int i = 0; i < _levels.Count; i++)
        {
            int level = _levels[i];
            int row = i / Columns;
            int col = i % Columns;

            int x = content.X + col * (colW + ColGap);
            int y = content.Y - _scrollOffset + row * (ButtonHeight + ColGap);

            var bounds = new Rectangle(x, y, colW, ButtonHeight);
            if (bounds.Y >= content.Y && bounds.Bottom <= content.Bottom)
            {
                DrawButton(b, bounds, level);
                _buttonRects.Add((bounds, level));
            }
        }

        DrawScrollbar(b, content);

        // 底部提示
        string hint = "点击楼层传送　·　ESC 关闭";
        var hintSize = Game1.dialogueFont.MeasureString(hint);
        b.DrawString(Game1.dialogueFont, hint,
            new Vector2(xPositionOnScreen + (width - hintSize.X) / 2f, yPositionOnScreen + height - 68),
            Game1.textColor * 0.7f);

        base.draw(b);
        drawMouse(b);
    }

    private void DrawButton(SpriteBatch b, Rectangle bounds, int level)
    {
        bool hover = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());

        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            hover ? Color.Wheat : Color.White);

        string label = $"{level} 层";
        var size = Game1.dialogueFont.MeasureString(label);
        b.DrawString(Game1.dialogueFont, label,
            new Vector2(bounds.X + (bounds.Width - size.X) / 2f,
                        bounds.Y + (bounds.Height - size.Y) / 2f),
            Game1.textColor);
    }

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

        foreach (var (bounds, level) in _buttonRects)
        {
            if (!bounds.Contains(x, y)) continue;

            Game1.playSound("smallSelect");
            exitThisMenu(false);
            _onSelect(level);
            return;
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        exitThisMenu(playSound);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        int maxScroll = Math.Max(0, _contentHeight - ContentBounds.Height);
        int step = (ButtonHeight + ColGap) * 2;
        _scrollOffset = Math.Clamp(_scrollOffset + (direction > 0 ? -step : step), 0, maxScroll);
    }
}