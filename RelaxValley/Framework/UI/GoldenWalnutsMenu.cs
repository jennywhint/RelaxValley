using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 金核桃收集详情 UI（自绘）。
/// 原版没有独立界面，本类提供：
///   - 已收集 / 总数 的汇总文字
///   - 横向进度条
///   - ESC / 右键 / 右上角 X 关闭
/// 本类不依赖 IModHelper / i18n，文案由调用方传入，方便复用与测试。
/// </summary>
public sealed class GoldenWalnutsMenu : IClickableMenu
{
    private readonly string _title;
    private readonly string _summary;
    private readonly string _hint;
    private readonly int _found;
    private readonly int _total;

    public GoldenWalnutsMenu(string title, string summary, string hint, int found, int total)
        : base(
              x: (Game1.uiViewport.Width - 640) / 2,
              y: (Game1.uiViewport.Height - 340) / 2,
              width: 640,
              height: 340,
              showUpperRightCloseButton: true)
    {
        _title = title;
        _summary = summary;
        _hint = hint;
        _found = found;
        _total = total;
    }

    public override void draw(SpriteBatch b)
    {
        // 半透明遮罩
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen,
            width, height,
            Color.White);

        // 标题（居中）
        var titleSize = Game1.dialogueFont.MeasureString(_title);
        b.DrawString(
            Game1.dialogueFont,
            _title,
            new Vector2(xPositionOnScreen + (width - titleSize.X) / 2f, yPositionOnScreen + 70),
            Game1.textColor);

        // 汇总文字（居中）
        var summarySize = Game1.dialogueFont.MeasureString(_summary);
        b.DrawString(
            Game1.dialogueFont,
            _summary,
            new Vector2(xPositionOnScreen + (width - summarySize.X) / 2f, yPositionOnScreen + 145),
            Game1.textColor);

        // 进度条
        float ratio = _total > 0 ? MathHelper.Clamp((float)_found / _total, 0f, 1f) : 0f;
        int barX = xPositionOnScreen + 60;
        int barY = yPositionOnScreen + 210;
        int barW = width - 120;
        int barH = 32;

        b.Draw(Game1.staminaRect, new Rectangle(barX - 2, barY - 2, barW + 4, barH + 4), Color.Black);
        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, barW, barH), new Color(60, 60, 60));
        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, (int)(barW * ratio), barH),
               new Color(200, 90, 90));

        // 提示文字（居中、略淡）
        var hintSize = Game1.dialogueFont.MeasureString(_hint);
        b.DrawString(
            Game1.dialogueFont,
            _hint,
            new Vector2(xPositionOnScreen + (width - hintSize.X) / 2f, yPositionOnScreen + height - 75),
            Game1.textColor * 0.7f);

        base.draw(b);   // 右上角关闭按钮
        drawMouse(b);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        exitThisMenu(playSound);
    }
}