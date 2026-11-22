using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RelaxValley.Framework.UI;

/// <summary>列表中的一行：可点击条目。</summary>
public sealed class MenuRow
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public bool CanClick { get; init; }
    public float Opacity { get; init; } = 1f;
    public string TextColor { get; init; } = "#3B2A1E";

    /// <summary>点击回调。由视图直接调用，避免嵌套重复项里的上下文重定向问题。</summary>
    public Action<MenuRow>? ClickAction { get; init; }

    public Action<MenuRow>? RightClickAction { get; init; }

    public void Click() => ClickAction?.Invoke(this);

    public void RightClick() => RightClickAction?.Invoke(this);
}

/// <summary>一个分类：标题 + 该分类下的条目（由视图排成多列网格）。</summary>
public sealed class MenuSection
{
    public string Header { get; init; } = "";
    public bool HasHeader => Header.Length > 0;
    public List<MenuRow> Entries { get; init; } = new();
}

/// <summary>
/// 主菜单 / 工作室的数据模型。
/// 两者是同一个界面的两页，由本类切换，避免菜单嵌套与关闭回调互相干扰。
/// </summary>
public sealed class MainMenuViewModel : INotifyPropertyChanged, ICloseAware
{
    private const string WorkbenchEntryId = "Workbench";

    /// <summary>主菜单置顶条目存在玩家 modData 里，随存档独立保存。</summary>
    private const string PinnedEntriesKey = "Awassakura.RelaxValley/PinnedEntries";

    private readonly IReadOnlyList<MenuEntry> _mainEntries;
    private readonly Func<IReadOnlyList<MenuEntry>> _workbenchEntriesFactory;
    private readonly ITranslationHelper _translation;
    private readonly IMonitor _monitor;
    private readonly HashSet<string> _pinnedEntries;

    private IReadOnlyList<MenuEntry> _currentEntries;
    private bool _isMainPage;
    private Action? _closeSelf;
    private string _title = "";
    private string _hint = "";

    public MainMenuViewModel(
        IReadOnlyList<MenuEntry> mainEntries,
        Func<IReadOnlyList<MenuEntry>> workbenchEntriesFactory,
        ITranslationHelper translation,
        IMonitor monitor)
    {
        _mainEntries = mainEntries;
        _workbenchEntriesFactory = workbenchEntriesFactory;
        _translation = translation;
        _monitor = monitor;
        _pinnedEntries = LoadPinnedEntries();
        _currentEntries = mainEntries;
        _isMainPage = true;

        Title = translation.Get("RelaxValley.UI.MainMenu.Title");
        Hint = translation.Get("RelaxValley.UI.MainMenu.CloseHint");
        BackText = translation.Get("RelaxValley.UI.MainMenu.BackToMain");
        Sections = BuildSections();
    }

    /// <summary>读取置顶记录（modData，逗号分隔条目 Id）。</summary>
    private static HashSet<string> LoadPinnedEntries()
    {
        string raw = "";
        if (Game1.player is not null
            && Game1.player.modData.TryGetValue(PinnedEntriesKey, out string? value))
        {
            raw = value ?? "";
        }

        return new HashSet<string>(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    private void SavePinnedEntries()
    {
        if (Game1.player is null) return;
        Game1.player.modData[PinnedEntriesKey] = string.Join(",", _pinnedEntries);
    }

    /// <summary>由宿主菜单在控制器创建后注入“真正关闭菜单”的动作。</summary>
    public void AttachClose(Action closeSelf) => _closeSelf = closeSelf;

    // ── 绑定属性 ────────────────────────────────────────────────

    public string Title
    {
        get => _title;
        private set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Hint
    {
        get => _hint;
        private set
        {
            _hint = value;
            OnPropertyChanged(nameof(Hint));
        }
    }

    public string BackText { get; }

    public bool ShowBack => !_isMainPage;

    private List<MenuSection> _sections = new();
    public List<MenuSection> Sections
    {
        get => _sections;
        private set
        {
            _sections = value;
            OnPropertyChanged(nameof(Sections));
        }
    }

    // ── 交互 ────────────────────────────────────────────────────

    public void OnRowClicked(string id)
    {
        MenuEntry? entry = _currentEntries.FirstOrDefault(e => e.Id == id);
        if (entry is null) return;

        if (entry.Id == WorkbenchEntryId)
        {
            GoToWorkbench();
            return;
        }

        MenuEntryStatus status = entry.GetStatus();

        if (status == MenuEntryStatus.NotImplemented)
        {
            Game1.playSound("cancel");
            _monitor.Log($"菜单项 '{entry.Id}' 尚未实现。", LogLevel.Warn);
            return;
        }

        if (status != MenuEntryStatus.Available)
        {
            Game1.playSound("cancel");
            Game1.showGlobalMessage(entry.GetLockReason());
            return;
        }

        Game1.playSound("smallSelect");
        entry.TryExecute(_monitor);
    }

    /// <summary>
    /// 右键条目：「左键原版 / 右键模组」——条目自带更详细页面时执行它；
    /// 否则右键 = 置顶切换（置顶条目提到列表最前的「常用」区）。
    /// </summary>
    public void OnRowRightClicked(string id)
    {
        MenuEntry? entry = _currentEntries.FirstOrDefault(e => e.Id == id);
        if (entry is null) return;

        if (entry.RightExecute is not null)
        {
            entry.RightExecute.Invoke(entry);
            return;
        }

        TogglePin(id);
    }

    private void TogglePin(string id)
    {
        if (!_pinnedEntries.Remove(id))
            _pinnedEntries.Add(id);

        SavePinnedEntries();
        Game1.playSound("smallSelect");
        Sections = BuildSections();
    }

    public void GoToWorkbench()
    {
        _currentEntries = _workbenchEntriesFactory();
        _isMainPage = false;

        Title = _translation.Get("RelaxValley.UI.Workbench.Title");
        Hint = _translation.Get("RelaxValley.UI.Workbench.BackHint");
        Sections = BuildSections();
        NotifyPageChanged();
    }

    public void GoBack()
    {
        if (_isMainPage) return;

        _currentEntries = _mainEntries;
        _isMainPage = true;

        Title = _translation.Get("RelaxValley.UI.MainMenu.Title");
        Hint = _translation.Get("RelaxValley.UI.MainMenu.CloseHint");
        Sections = BuildSections();
        NotifyPageChanged();

        Game1.playSound("bigDeSelect");
    }

    /// <summary>ESC / 右上角关闭请求（当前由 StardewUI 默认行为处理）。</summary>
    public void HandleCloseRequest()
    {
        if (!_isMainPage)
        {
            GoBack();
            return;
        }

        _closeSelf?.Invoke();
    }

    // ── 分类构建 ────────────────────────────────────────────────

    private List<MenuSection> BuildSections()
    {
        var sections = new List<MenuSection>();
        MenuSection? current = null;
        string? lastCategory = null;

        // 常用区：置顶条目提到最前，原位置不再重复显示
        List<MenuEntry> visibleEntries = _currentEntries.Where(e => e.IsVisible).ToList();
        List<MenuEntry> pinnedEntries = visibleEntries.Where(e => _pinnedEntries.Contains(e.Id)).ToList();

        if (pinnedEntries.Count > 0)
        {
            string header = _translation.Get("RelaxValley.UI.MainMenu.PinnedHeader");
            var pinnedSection = new MenuSection { Header = $"--- {header} ---" };
            foreach (MenuEntry entry in pinnedEntries)
                pinnedSection.Entries.Add(CreateRow(entry));
            sections.Add(pinnedSection);
        }

        foreach (MenuEntry entry in visibleEntries.Where(e => !_pinnedEntries.Contains(e.Id)))
        {
            if (!string.IsNullOrEmpty(entry.Category) && entry.Category != lastCategory)
            {
                lastCategory = entry.Category;

                string categoryText = entry.CategoryDisplayName
                    ?? _translation.Get($"RelaxValley.Category.{entry.Category}");

                current = new MenuSection { Header = $"--- {categoryText} ---" };
                sections.Add(current);
            }

            current ??= new MenuSection();
            if (!sections.Contains(current)) sections.Add(current);

            current.Entries.Add(CreateRow(entry));
        }

        return sections;
    }

    private MenuRow CreateRow(MenuEntry entry)
    {
        MenuEntryStatus status = entry.GetStatus();
        bool available = status == MenuEntryStatus.Available;
        string pinnedMark = _pinnedEntries.Contains(entry.Id) ? "★ " : "";

        return new MenuRow
        {
            Id = entry.Id,
            Text = pinnedMark + ResolveName(entry) + ResolveSuffix(entry),
            CanClick = available,
            Opacity = available ? 1f : 0.55f,
            TextColor = available ? "#3B2A1E" : "#8A8580",
            ClickAction = row => OnRowClicked(row.Id),
            RightClickAction = row => OnRowRightClicked(row.Id)
        };
    }

    private string ResolveName(MenuEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DisplayName)) return entry.DisplayName!;

        string raw = _translation.Get(entry.TranslationKey);
        return string.IsNullOrWhiteSpace(raw) || raw == entry.TranslationKey ? entry.Id : raw;
    }

    private string ResolveSuffix(MenuEntry entry)
    {
        string? key = entry.GetStatusSuffixKey();
        return key is null ? "" : _translation.Get(key);
    }

    private void NotifyPageChanged()
    {
        OnPropertyChanged(nameof(ShowBack));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
