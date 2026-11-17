using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using System.Collections.Generic;
using System.Linq;


namespace RelaxValley.Framework.UI;

public sealed class MonsterStatsMenu : ScrollableGridMenu
{
    private class GoalEntry
    {
        public string DisplayName = "";
        public int Killed;
        public int TargetCount;
    }

    private readonly List<GoalEntry> _goals;

    protected override int ItemCount => _goals.Count;

    public MonsterStatsMenu() : base(columns: 3, cellWidth: 0, cellHeight: 80, cellGap: 16)
    {
        _goals = BuildGoals();
    }

    private List<GoalEntry> BuildGoals()
    {
        var result = new List<GoalEntry>();
        var monsterData = Game1.content.Load<Dictionary<string, StardewValley.GameData.MonsterSlayerQuestData>>("Data/MonsterSlayerQuests");
        var killsDict = GetSpecificMonstersKilled();

        foreach (var pair in monsterData)
        {
            var quest = pair.Value;
            int totalKilled = 0;

            foreach (string monsterName in quest.Targets)
            {
                int killed = 0;
                if (killsDict != null && killsDict.TryGetValue(monsterName, out killed))
                    totalKilled += killed;
            }

            result.Add(new GoalEntry
            {
                DisplayName = TokenParser.ParseText(quest.DisplayName, null, null, Game1.player),
                Killed = totalKilled,
                TargetCount = quest.Count
            });
        }
        return result.OrderBy(g => g.DisplayName).ToList();
    }

    private static Dictionary<string, int>? GetSpecificMonstersKilled()
    {
        // 1.6 中这是私有字段，名字是 camelCase：specificMonstersKilled
        var field = typeof(StardewValley.Stats).GetField(
            "specificMonstersKilled",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (field == null)
        {
            // 保底：遍历所有字段，找类型是 Dictionary<string,int> 且名字含 "Monster" 的
            foreach (var f in typeof(StardewValley.Stats).GetFields(
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                if (f.FieldType == typeof(Dictionary<string, int>) &&
                    f.Name.Contains("Monster", StringComparison.OrdinalIgnoreCase))
                {
                    return f.GetValue(Game1.player.stats) as Dictionary<string, int>;
                }
            }
            return null;
        }

        return field.GetValue(Game1.player.stats) as Dictionary<string, int>;
    }

    protected override void DrawCell(SpriteBatch b, Rectangle bounds, int dataIndex)
    {
        var goal = _goals[dataIndex];
        bool hover = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            hover ? Color.Wheat : Color.White);

        b.DrawString(Game1.smallFont, goal.DisplayName,
            new Vector2(bounds.X + 12, bounds.Y + 8), Game1.textColor);

        float ratio = goal.TargetCount > 0 ? (float)goal.Killed / goal.TargetCount : 0f;
        ratio = MathHelper.Clamp(ratio, 0f, 1f);
        string progress = $"{goal.Killed} / {goal.TargetCount}";
        b.DrawString(Game1.smallFont, progress,
            new Vector2(bounds.X + 12, bounds.Y + 36), Game1.textColor * 0.8f);

        int barX = bounds.X + 12, barY = bounds.Y + 58, barW = bounds.Width - 24, barH = 8;
        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, barW, barH), new Color(60, 60, 60));

        // 已完成显示绿色，未完成显示红色
        bool completed = goal.Killed >= goal.TargetCount;
        Color barColor = completed ? new Color(80, 180, 80) : new Color(200, 90, 90);

        b.Draw(Game1.staminaRect, new Rectangle(barX, barY, (int)(barW * ratio), barH), barColor);
    }

    protected override void OnCellClicked(int dataIndex) { /* 纯展示，无需操作 */ }
}