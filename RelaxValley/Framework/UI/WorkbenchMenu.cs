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
/// 工作台子页面骨架。结构与 MainMenu 一致，条目来源独立。
/// 关闭时返回 MainMenu，而非退出菜单。
/// </summary>
public sealed class WorkbenchMenu : IClickableMenu
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
    private readonly IReadOnlyList<MenuEntry> _allEntries;

    private int _scrollOffset;
    private int _contentHeight;
    private readonly List<(Rectangle Bounds, MenuEntry Entry)> _itemRects = new();

    private Rectangle ContentBounds => new(
        xPositionOnScreen + SidePadding,
        yPositionOnScreen + ContentTopPadding,
        width - SidePadding * 2,
        height - ContentTopPadding - ContentBottomPadding);

    private int ColumnWidth => (ContentBounds.Width - (Columns - 1) * ColGap) / Columns;

    public WorkbenchMenu(
        ITranslationHelper translation,
        IMonitor monitor,
        IReadOnlyList<MenuEntry> allEntries,
        IReadOnlyList<MenuEntry> workbenchEntries)
        : base(
            x: 30, y: 50,
            width: Game1.uiViewport.Width - 60,
            height: Game1.uiViewport.Height - 100,
            showUpperRightCloseButton: true)
    {
        _translation = translation;
        _monitor = monitor;
        _allEntries = allEntries;
        _entries = workbenchEntries;
        _contentHeight = CalculateContentHeight();
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
               Game1.graphics.GraphicsDevice.Viewport.Bounds,
               Color.Black * 0.5f);

        IClickableMenu.drawTextureBox(
            b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        string title = _translation.Get("RelaxValley.UI.Workbench.Title");
        var titleSize = Game1.dialogueFont.MeasureString(title);
        b.DrawString(Game1.dialogueFont, title,
            new Vector2(xPositionOnScreen + (width - titleSize.X) / 2f, yPositionOnScreen + 36),
            Game1.textColor);

        var content = ContentBounds;
        _itemRects.Clear();
        _contentHeight = CalculateContentHeight();

        int cursorY = content.Y - _scrollOffset;
        int col = 0;
        string? lastCategory = null;
        int colW = ColumnWidth;

        foreach (var entry in _entries.Where(e => e.IsVisible))
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

            var bounds = new Rectangle(content.X + col * (colW + ColGap), cursorY, colW, ItemHeight - 6);
            if (bounds.Y >= content.Y && bounds.Bottom <= content.Bottom)
            {
                DrawItem(b, bounds, entry);
                _itemRects.Add((bounds, entry));
            }
            col++;
            if (col >= Columns) { col = 0; cursorY += ItemHeight; }
        }

        DrawScrollbar(b, content);

        string hint = _translation.Get("RelaxValley.UI.Workbench.BackHint");
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

        Vector2 textPos = new(bounds.X + 16,
            bounds.Y + (bounds.Height - Game1.dialogueFont.LineSpacing) / 2f + 2);
        Color color = implemented ? Game1.textColor : Game1.textColor * 0.4f;
        b.DrawString(Game1.dialogueFont, name, textPos, color);
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

    // ── 返回主菜单 ──

    private void ReturnToMainMenu()
    {
        Game1.activeClickableMenu = new MainMenu(_allEntries, _translation, _monitor);
        Game1.playSound("bigDeSelect");
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
                _monitor.Log($"工作台条目 '{entry.Id}' 尚未实现。", LogLevel.Warn);
                return;
            }
            Game1.playSound("smallSelect");
            entry.TryExecute(new MainMenu(_allEntries, _translation, _monitor), _monitor);
            return;
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        ReturnToMainMenu();
    }

    public override void receiveScrollWheelAction(int direction)
    {
        int maxScroll = Math.Max(0, _contentHeight - ContentBounds.Height);
        int step = ItemHeight * 2;
        _scrollOffset = Math.Clamp(_scrollOffset + (direction > 0 ? -step : step), 0, maxScroll);
    }
}