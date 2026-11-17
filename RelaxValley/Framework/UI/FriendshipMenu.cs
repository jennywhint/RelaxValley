using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 村民友谊统计页。复用 ScrollableGridMenu，每格显示一个已结交村民。
/// </summary>
public sealed class FriendshipMenu : ScrollableGridMenu
{
    private class FriendEntry
    {
        public string DisplayName = "";
        public int Hearts;
        public int MaxHearts;
        public int GiftsThisWeek;
        public bool GiftAvailableToday;
    }

    private readonly List<FriendEntry> _friends;

    protected override int ItemCount => _friends.Count;

    public FriendshipMenu()
        : base(columns: 3, cellWidth: 0, cellHeight: 100, cellGap: 16)
    {
        _friends = BuildFriends();
    }

    private List<FriendEntry> BuildFriends()
    {
        var result = new List<FriendEntry>();

        foreach (var pair in Game1.player.friendshipData.Pairs)
        {
            string npcName = pair.Key;
            var friendship = pair.Value;

            NPC? npc = Game1.getCharacterFromName(npcName);
            if (npc == null) continue;

            int points = friendship.Points;
            int hearts = points / 250;

            // 从 Data/Characters 读取最大心数
            int maxHearts = npc.isMarried() ? 14 : 10;

            result.Add(new FriendEntry
            {
                DisplayName = string.IsNullOrEmpty(npc.displayName) ? npcName : npc.displayName,
                Hearts = Math.Min(hearts, maxHearts),
                MaxHearts = maxHearts,
                GiftsThisWeek = friendship.GiftsThisWeek,
                GiftAvailableToday = friendship.GiftsToday == 0   // 调用方法
            });
        }

        return result
            .OrderByDescending(f => f.Hearts)
            .ThenBy(f => f.DisplayName)
            .ToList();
    }

    protected override void DrawCell(SpriteBatch b, Rectangle bounds, int dataIndex)
    {
        var friend = _friends[dataIndex];
        bool hover = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());

        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            hover ? Color.Wheat : Color.White);

        // 村民名
        b.DrawString(Game1.smallFont, friend.DisplayName,
            new Vector2(bounds.X + 12, bounds.Y + 8), Game1.textColor);

        // 心数
        string heartText = $"{friend.Hearts} / {friend.MaxHearts} 心";
        b.DrawString(Game1.smallFont, heartText,
            new Vector2(bounds.X + 12, bounds.Y + 34), Game1.textColor * 0.85f);

        // 礼物状态（右上角）
        string giftText = friend.GiftAvailableToday ? "今日可送礼" : "今日已送礼";
        Color giftColor = friend.GiftAvailableToday
            ? new Color(80, 180, 80)
            : new Color(150, 150, 150);
        var giftSize = Game1.smallFont.MeasureString(giftText);
        b.DrawString(Game1.smallFont, giftText,
            new Vector2(bounds.Right - giftSize.X - 12, bounds.Y + 8), giftColor);

        // 心形进度条
        float ratio = friend.MaxHearts > 0 ? (float)friend.Hearts / friend.MaxHearts : 0f;
        ratio = MathHelper.Clamp(ratio, 0f, 1f);
        bool full = friend.Hearts >= friend.MaxHearts;
        Color barColor = full ? new Color(80, 180, 80) : new Color(220, 100, 120);

        int barX = bounds.X + 12, barY = bounds.Y + 70;
        int barW = bounds.Width - 24, barH = 10;
        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, barW, barH), new Color(60, 60, 60));
        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, (int)(barW * ratio), barH), barColor);
    }

    protected override void OnCellClicked(int dataIndex) { /* 纯展示 */ }
}