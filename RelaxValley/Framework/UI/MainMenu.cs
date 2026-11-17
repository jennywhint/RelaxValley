using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RelaxValley.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 主面板。所有已实现 / 未实现的菜单项统一从这点击触发。
/// 4 列网格布局，木质原生背景，支持滚轮滚动。
/// </summary>
public sealed class MainMenu : IClickableMenu
{
    private const int Columns = 4;
    private const int ColGap = 12;
    private const int ItemHeight = 52;
    private const int CategoryHeight = 46;
    private const int SidePadding = 36;
    private const int ContentTopPadding = 96;
    private const int ContentBottomPadding = 88;

    private readonly IReadOnlyList<MenuEntry> _entries;
    private readonly ITranslationHelper _translation;
    private readonly IMonitor _monitor;

    private int _scrollOffset;
    private int _contentHeight;
    private readonly List<(Rectangle Bounds, MenuEntry Entry)> _itemRects = new();

    // ── 对外暴露的属性（供 WorkbenchMenu 等子页面返回时使用） ──
    public ITranslationHelper Translation => _translation;
    public IMonitor Monitor => _monitor;
    public IReadOnlyList<MenuEntry> AllEntries => _entries;

    private Rectangle ContentBounds => new(
        xPositionOnScreen + SidePadding,
        yPositionOnScreen + ContentTopPadding,
        width - SidePadding * 2,
        height - ContentTopPadding - ContentBottomPadding);

    private int ColumnWidth => (ContentBounds.Width - (Columns - 1) * ColGap) / Columns;

    public MainMenu(IReadOnlyList<MenuEntry> entries, ITranslationHelper translation, IMonitor monitor)
        : base(
            x: 30,
            y: 50,
            width: Game1.uiViewport.Width - 60,
            height: Game1.uiViewport.Height - 100,
            showUpperRightCloseButton: true)
    {
        _entries = entries;
        _translation = translation;
        _monitor = monitor;
        _contentHeight = CalculateContentHeight();
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
               Game1.graphics.GraphicsDevice.Viewport.Bounds,
               Color.Black * 0.5f);

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen,
            width, height,
            Color.White);

        // 标题
        string title = _translation.Get("RelaxValley.UI.MainMenu.Title");
        var titleSize = Game1.dialogueFont.MeasureString(title);
        b.DrawString(Game1.dialogueFont, title,
            new Vector2(xPositionOnScreen + (width - titleSize.X) / 2f, yPositionOnScreen + 36),
            Game1.textColor);

        // 内容区
        var content = ContentBounds;
        _itemRects.Clear();
        _contentHeight = CalculateContentHeight();

        var visible = _entries.Where(e => e.IsVisible).ToList();
        int colW = ColumnWidth;

        int cursorY = content.Y - _scrollOffset;
        int col = 0;
        string? lastCategory = null;

        foreach (var entry in visible)
        {
            if (!string.IsNullOrEmpty(entry.Category) && entry.Category != lastCategory)
            {
                if (col > 0) { cursorY += ItemHeight; col = 0; }
                lastCategory = entry.Category;

                string catName = _translation.Get($"RelaxValley.Category.{entry.Category}");
                if (cursorY + CategoryHeight > content.Y && cursorY < content.Bottom)
                {
                    var catSize = Game1.dialogueFont.MeasureString(catName);
                    b.DrawString(Game1.dialogueFont, catName,
                        new Vector2(content.X + (content.Width - catSize.X) / 2f, cursorY + 14),
                        new Color(120, 60, 30));
                }
                cursorY += CategoryHeight;
            }

            int entryX = content.X + col * (colW + ColGap);
            var bounds = new Rectangle(entryX, cursorY, colW, ItemHeight - 6);

            if (bounds.Y >= content.Y && bounds.Bottom <= content.Bottom)
            {
                DrawItem(b, bounds, entry);
                _itemRects.Add((bounds, entry));
            }

            col++;
            if (col >= Columns) { col = 0; cursorY += ItemHeight; }
        }

        DrawScrollbar(b, content);

        string hint = _translation.Get("RelaxValley.UI.MainMenu.CloseHint");
        var hintSize = Game1.dialogueFont.MeasureString(hint);
        b.DrawString(Game1.dialogueFont, hint,
            new Vector2(xPositionOnScreen + (width - hintSize.X) / 2f, yPositionOnScreen + height - 68),
            Game1.textColor * 0.7f);

        base.draw(b);
        drawMouse(b);
    }

    private void DrawItem(SpriteBatch b, Rectangle bounds, MenuEntry entry)
    {
        bool implemented = entry.IsImplemented;
        bool hover = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());

        if (hover && implemented)
            b.Draw(Game1.staminaRect, bounds, Color.Wheat * 0.55f);

        string name = ResolveName(entry);
        if (!implemented)
            name += _translation.Get("RelaxValley.UI.MainMenu.NotImplementedSuffix");

        Vector2 textPos = new(
            bounds.X + 16,
            bounds.Y + (bounds.Height - Game1.dialogueFont.LineSpacing) / 2f + 2);

        Color color = implemented ? Game1.textColor : Game1.textColor * 0.4f;
        b.DrawString(Game1.dialogueFont, name, textPos, color);
    }

    private void DrawScrollbar(SpriteBatch b, Rectangle content)
    {
        int maxScroll = Math.Max(0, _contentHeight - content.Height);
        if (maxScroll <= 0) return;

        int barX = xPositionOnScreen + width - 22;
        int barWidth = 10;
        int barHeight = content.Height;

        b.Draw(Game1.staminaRect, new Rectangle(barX, content.Y, barWidth, barHeight), Color.Black * 0.3f);

        float ratio = (float)content.Height / _contentHeight;
        int thumbHeight = Math.Max(30, (int)(barHeight * ratio));
        int thumbY = content.Y + (int)((barHeight - thumbHeight) * ((float)_scrollOffset / maxScroll));

        b.Draw(Game1.staminaRect, new Rectangle(barX, thumbY, barWidth, thumbHeight), Color.Wheat);
    }

    private string ResolveName(MenuEntry entry)
    {
        string raw = _translation.Get(entry.TranslationKey);
        return string.IsNullOrWhiteSpace(raw) || raw == entry.TranslationKey ? entry.Id : raw;
    }

    private int CalculateContentHeight()
    {
        int total = 0, col = 0;
        string? lastCategory = null;
        foreach (var entry in _entries.Where(e => e.IsVisible))
        {
            if (!string.IsNullOrEmpty(entry.Category) && entry.Category != lastCategory)
            {
                if (col > 0) { total += ItemHeight; col = 0; }
                lastCategory = entry.Category;
                total += CategoryHeight;
            }
            col++;
            if (col >= Columns) { col = 0; total += ItemHeight; }
        }
        if (col > 0) total += ItemHeight;
        return total;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        foreach (var (bounds, entry) in _itemRects)
        {
            if (!bounds.Contains(x, y)) continue;

            if (!entry.IsImplemented)
            {
                Game1.playSound("cancel");
                _monitor.Log($"菜单项 '{entry.Id}' 尚未实现。", LogLevel.Warn);
                return;
            }

            Game1.playSound("smallSelect");
            entry.TryExecute(this, _monitor);
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
        int step = ItemHeight * 2;
        _scrollOffset = Math.Clamp(
            _scrollOffset + (direction > 0 ? -step : step),
            0,
            maxScroll);
    }
}