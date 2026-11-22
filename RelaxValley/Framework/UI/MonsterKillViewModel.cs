using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace RelaxValley.Framework.UI;

/// <summary>纯展示模型，全部一次性绑定，无需 INPC。</summary>
public class MonsterKillViewModel
{
    public string Title => I18n.RelaxValley_Menu_MonsterKill_Name();

    public List<GoalItem> Goals { get; }

    public MonsterKillViewModel()
    {
        Goals = BuildGoals();
    }

    private List<GoalItem> BuildGoals()
    {
        var result = new List<GoalItem>();
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

            bool completed = totalKilled >= quest.Count;
            result.Add(new GoalItem
            {
                DisplayName = TokenParser.ParseText(quest.DisplayName, null, null, Game1.player),
                ProgressText = $"{totalKilled} / {quest.Count}",
                IsComplete = completed,
                TextColor = completed ? "#50B450" : "#FF6060"
            });
        }
        return result.OrderBy(g => g.IsComplete).ThenBy(g => g.DisplayName).ToList();
    }

    private static Dictionary<string, int>? GetSpecificMonstersKilled()
    {
        var field = typeof(StardewValley.Stats).GetField(
            "specificMonstersKilled",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        return field?.GetValue(Game1.player.stats) as Dictionary<string, int>;
    }
}

public class GoalItem
{
    public string DisplayName { get; set; } = "";
    public string ProgressText { get; set; } = "";
    public bool IsComplete { get; set; }
    public string TextColor { get; set; } = "#FFFFFF";
}