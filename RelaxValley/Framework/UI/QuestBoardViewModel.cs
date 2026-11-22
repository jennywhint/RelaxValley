using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using StardewValley.TokenizableStrings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RelaxValley.Framework.UI;

/// <summary>
/// 自绘任务一览的数据模型。
/// 只读取 Data/Quests 与原版任务日志做展示，不接取、不完成、不改写任何任务，
/// 因此不会影响原版任务进度和其它模组的任务。
/// 可按“来源”（原版 / 各模组）与“任务类型”（杀怪、交付、收获、社交……）筛选。
/// </summary>
public class QuestBoardViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// 原版 Quest.getQuestFromId 能解析的类型。
    /// 其它类型（如 Fishing、Resource 或模组自定义类型）直接调用会让游戏打印
    /// “doesn't match a known type” 的解析错误，因此这些类型改用原始字段展示。
    /// </summary>
    private static readonly HashSet<string> ParsableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Basic",
        "Crafting",
        "ItemDelivery",
        "Monster",
        "Social",
        "Location",
        "Building",
        "ItemHarvest",
        "LostItem",
        "SecretLostItem"
    };

    /// <summary>Data/Quests 里已知的类型字符串 → i18n 键后缀。</summary>
    private static readonly Dictionary<string, string> KnownTypeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Basic"] = "Basic",
        ["Crafting"] = "Crafting",
        ["ItemDelivery"] = "ItemDelivery",
        ["Monster"] = "Monster",
        ["Social"] = "Social",
        ["Location"] = "Location",
        ["Building"] = "Building",
        ["ItemHarvest"] = "ItemHarvest",
        ["LostItem"] = "LostItem",
        ["SecretLostItem"] = "SecretLostItem",
        ["Fishing"] = "Fishing",
        ["Resource"] = "Resource"
    };

    private readonly ITranslationHelper _translations;
    private readonly IModRegistry _modRegistry;
    private readonly List<QuestItem> _allQuests;

    /// <summary>与下拉选项一一对应的内部值；第 0 项固定是“全部”。</summary>
    private readonly List<string> _sourceValues = new() { "" };
    private readonly List<string> _typeValues = new() { "" };

    public string Title => I18n.RelaxValley_UI_QuestBoard_Title();

    public string SearchPlaceholder => I18n.RelaxValley_UI_QuestBoard_SearchPlaceholder();

    public string Hint => I18n.RelaxValley_UI_QuestBoard_Hint();

    public List<string> SourceOptions { get; } = new() { I18n.RelaxValley_UI_QuestBoard_AllSources() };

    public List<string> TypeOptions { get; } = new() { I18n.RelaxValley_UI_QuestBoard_AllTypes() };

    public List<string> StatusOptions { get; } = new()
    {
        I18n.RelaxValley_UI_QuestBoard_AllStatuses(),
        I18n.RelaxValley_UI_QuestBoard_StatusNotAccepted(),
        I18n.RelaxValley_UI_QuestBoard_StatusAccepted(),
        I18n.RelaxValley_UI_QuestBoard_StatusCompleted()
    };

    /// <summary>与状态下拉一一对应的内部值，空串表示全部。</summary>
    private static readonly string[] StatusValues = { "", "NotAccepted", "Accepted", "Completed" };

    /// <summary>可选的任务 ID 前缀白名单（用于 RSV 任务板这类子集视图）。</summary>
    private readonly string[] _idPrefixes = Array.Empty<string>();

    private List<QuestItem> _quests = new();
    public List<QuestItem> Quests
    {
        get => _quests;
        private set
        {
            if (ReferenceEquals(_quests, value)) return;
            _quests = value;
            OnPropertyChanged(nameof(Quests));
            OnPropertyChanged(nameof(CountText));
        }
    }

    /// <summary>前缀过滤后的任务总数（不受下拉筛选影响）。</summary>
    public int TotalCount => _allQuests.Count;

    public string CountText => _translations
        .Get(I18n.Keys.RelaxValley_UI_QuestBoard_Count)
        .Tokens(new { count = _quests.Count });

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            ApplyFilters();
        }
    }

    private int _selectedSourceIndex;
    public int SelectedSourceIndex
    {
        get => _selectedSourceIndex;
        set
        {
            if (_selectedSourceIndex == value) return;
            _selectedSourceIndex = value;
            OnPropertyChanged(nameof(SelectedSourceIndex));
            ApplyFilters();
        }
    }

    private int _selectedTypeIndex;
    public int SelectedTypeIndex
    {
        get => _selectedTypeIndex;
        set
        {
            if (_selectedTypeIndex == value) return;
            _selectedTypeIndex = value;
            OnPropertyChanged(nameof(SelectedTypeIndex));
            ApplyFilters();
        }
    }

    private int _selectedStatusIndex;
    public int SelectedStatusIndex
    {
        get => _selectedStatusIndex;
        set
        {
            if (_selectedStatusIndex == value) return;
            _selectedStatusIndex = value;
            OnPropertyChanged(nameof(SelectedStatusIndex));
            ApplyFilters();
        }
    }

    public QuestBoardViewModel(
        ITranslationHelper translations,
        IModRegistry modRegistry,
        string[]? idPrefixes = null)
    {
        _translations = translations;
        _modRegistry = modRegistry;
        _idPrefixes = idPrefixes ?? Array.Empty<string>();
        _allQuests = BuildQuests();
        BuildOptions();
        ApplyFilters();
    }

    // ── 构建 ────────────────────────────────────────────────────

    private List<QuestItem> BuildQuests()
    {
        var result = new List<QuestItem>();

        Dictionary<string, string> questData;
        try
        {
            questData = Game1.content.Load<Dictionary<string, string>>("Data/Quests");
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"读取 Data/Quests 失败：{ex.Message}", LogLevel.Error);
            return result;
        }

        foreach (var pair in questData)
        {
            string[] fields = pair.Value.Split('/');
            if (fields.Length < 3 || string.IsNullOrWhiteSpace(fields[0])) continue;

            // 子集视图（例如 RSV 任务板）：只保留指定 ID 前缀的任务
            if (_idPrefixes.Length > 0 && !_idPrefixes.Any(p =>
                    pair.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string rawType = fields[0];
            Quest? quest = null;

            // 只让原版解析器处理它支持的类型，避免解析错误刷日志
            if (ParsableTypes.Contains(rawType))
            {
                try
                {
                    quest = Quest.getQuestFromId(pair.Key);
                }
                catch
                {
                    // 数据损坏时用原始字段兜底展示
                }
            }

            // 原版解析成功时用完整数据；自定义类型的模组任务用原始字段，
            // 保证不会因为解析失败而从列表里消失。
            string title = quest is not null
                ? ParseText(quest.questTitle)
                : ParseText(fields[1]);
            if (string.IsNullOrWhiteSpace(title)) title = pair.Key;

            string objective = quest is not null
                ? ParseText(quest.currentObjective)
                : (fields.Length > 3 ? ParseText(fields[3]) : "");

            string description = quest is not null
                ? ParseText(quest.questDescription)
                : ParseText(fields[2]);

            string sourceValue = GetSourceValue(pair.Key);
            string statusText = GetStatusText(pair.Key, out string statusColor, out string statusValue);

            result.Add(new QuestItem
            {
                Id = pair.Key,
                Title = title,
                TypeValue = rawType,
                TypeLabel = GetTypeLabel(rawType),
                SourceValue = sourceValue,
                SourceLabel = GetSourceLabel(sourceValue),
                Objective = objective,
                Description = description,
                RewardText = quest is not null ? BuildRewardText(quest) : "",
                StatusText = statusText,
                StatusColor = statusColor,
                StatusValue = statusValue
            });
        }

        return result
            .OrderBy(q => q.SourceLabel)
            .ThenBy(q => q.TypeLabel)
            .ThenBy(q => q.Title)
            .ToList();
    }

    private void BuildOptions()
    {
        foreach (string type in _allQuests
                     .Select(q => q.TypeValue)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            _typeValues.Add(type);
            TypeOptions.Add(GetTypeLabel(type));
        }

        foreach (string source in _allQuests
                     .Select(q => q.SourceValue)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            _sourceValues.Add(source);
            SourceOptions.Add(GetSourceLabel(source));
        }
    }

    // ── 来源 / 类型 ─────────────────────────────────────────────

    /// <summary>
    /// 判断任务来源：纯数字 ID 视为原版；
    /// 其余按“已加载模组的 UniqueID / 名称前缀”匹配，匹配不到再退回 ID 前缀分组。
    /// </summary>
    private string GetSourceValue(string questId)
    {
        if (questId.Length > 0 && questId.All(char.IsDigit))
            return "Vanilla";

        foreach (IModInfo mod in _modRegistry.GetAll())
        {
            string[] keys =
            {
                mod.Manifest.UniqueID,
                mod.Manifest.Name
            };

            foreach (string key in keys)
            {
                if (key.Length < 3) continue;
                if (questId.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    return $"Mod:{mod.Manifest.Name}";
            }
        }

        int separator = questId.IndexOfAny(new[] { '.', '_', '-' });
        return separator > 0
            ? $"Prefix:{questId[..separator]}"
            : "Other";
    }

    private string GetSourceLabel(string sourceValue)
    {
        if (sourceValue == "Vanilla")
            return I18n.RelaxValley_UI_QuestBoard_SourceVanilla();

        if (sourceValue.StartsWith("Mod:", StringComparison.Ordinal))
            return sourceValue["Mod:".Length..];

        if (sourceValue.StartsWith("Prefix:", StringComparison.Ordinal))
            return sourceValue["Prefix:".Length..];

        return I18n.RelaxValley_UI_QuestBoard_SourceOther();
    }

    private string GetTypeLabel(string rawType)
    {
        if (KnownTypeKeys.TryGetValue(rawType, out string? key))
        {
            string translated = _translations.Get($"RelaxValley.UI.QuestBoard.Type.{key}");
            if (!string.IsNullOrWhiteSpace(translated)
                && translated != $"RelaxValley.UI.QuestBoard.Type.{key}")
            {
                return translated;
            }
        }

        // 模组自定义类型：直接显示原始类型名
        return rawType;
    }

    // ── 状态 / 奖励 ─────────────────────────────────────────────

    private string GetStatusText(string questId, out string color, out string statusValue)
    {
        Quest? accepted = Game1.player?.questLog
            .FirstOrDefault(q => q is not null && q.id.Value == questId);

        if (accepted is not null)
        {
            if (accepted.completed.Value)
            {
                color = "#2E7D32";
                statusValue = "Completed";
                return I18n.RelaxValley_UI_QuestBoard_StatusCompleted();
            }

            color = "#B06A00";
            statusValue = "Accepted";
            return I18n.RelaxValley_UI_QuestBoard_StatusAccepted();
        }

        color = "#6B6B6B";
        statusValue = "NotAccepted";
        return I18n.RelaxValley_UI_QuestBoard_StatusNotAccepted();
    }

    private string BuildRewardText(Quest quest)
    {
        int money = quest.moneyReward.Value;
        if (money > 0)
        {
            return _translations
                .Get(I18n.Keys.RelaxValley_UI_QuestBoard_RewardMoney)
                .Tokens(new { money });
        }

        string description = ParseText(quest.rewardDescription.Value);
        return string.IsNullOrWhiteSpace(description) ? "" : description;
    }

    private static string ParseText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        try
        {
            return TokenParser.ParseText(raw, null, null, Game1.player) ?? raw;
        }
        catch
        {
            return raw;
        }
    }

    /// <summary>重建任务列表（纯只读浏览，不提供接取——接取请到游戏内的对应 NPC / 布告栏）。</summary>
    private void RefreshQuests()
    {
        _allQuests.Clear();
        _allQuests.AddRange(BuildQuests());
        ApplyFilters();
    }

    // ── 筛选 ────────────────────────────────────────────────────
    private void ApplyFilters()
    {
        IEnumerable<QuestItem> filtered = _allQuests;

        string source = _selectedSourceIndex >= 0 && _selectedSourceIndex < _sourceValues.Count
            ? _sourceValues[_selectedSourceIndex]
            : "";
        if (!string.IsNullOrEmpty(source))
            filtered = filtered.Where(q => q.SourceValue == source);

        string type = _selectedTypeIndex >= 0 && _selectedTypeIndex < _typeValues.Count
            ? _typeValues[_selectedTypeIndex]
            : "";
        if (!string.IsNullOrEmpty(type))
            filtered = filtered.Where(q => string.Equals(q.TypeValue, type, StringComparison.OrdinalIgnoreCase));

        string status = _selectedStatusIndex >= 0 && _selectedStatusIndex < StatusValues.Length
            ? StatusValues[_selectedStatusIndex]
            : "";
        if (!string.IsNullOrEmpty(status))
            filtered = filtered.Where(q => q.StatusValue == status);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string query = SearchText.Trim().ToLowerInvariant();
            filtered = filtered.Where(q =>
                q.Title.ToLowerInvariant().Contains(query)
                || q.Objective.ToLowerInvariant().Contains(query));
        }

        Quests = filtered.ToList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class QuestItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string TypeValue { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public string SourceValue { get; set; } = "";
    public string SourceLabel { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Description { get; set; } = "";
    public string RewardText { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string StatusColor { get; set; } = "#6B6B6B";

    /// <summary>NotAccepted / Accepted / Completed，用于状态筛选。</summary>
    public string StatusValue { get; set; } = "NotAccepted";
}
