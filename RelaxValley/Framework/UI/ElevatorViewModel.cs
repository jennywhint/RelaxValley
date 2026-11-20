using StardewValley;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace RelaxValley.Framework.UI;

/// <summary>电梯楼层按钮。</summary>
public sealed class ElevatorLevel
{
    public string Value { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>通用电梯（矿井 / 骷髅矿洞）的 StardewUI 视图模型。</summary>
public sealed class ElevatorViewModel : INotifyPropertyChanged, ICloseAware
{
    private readonly Action<int> _onSelect;
    private Action? _closeSelf;

    public ElevatorViewModel(string title, IEnumerable<int> levels, Action<int> onSelect, string hint)
    {
        Title = title;
        Hint = hint;
        _onSelect = onSelect;

        var items = new List<ElevatorLevel>();
        foreach (int level in levels)
        {
            items.Add(new ElevatorLevel
            {
                Value = level.ToString(),
                Text = $"{level} 层"
            });
        }

        Levels = items;
    }

    public string Title { get; }

    public string Hint { get; }

    public List<ElevatorLevel> Levels { get; }

    public void AttachClose(Action closeSelf) => _closeSelf = closeSelf;

    public void OnLevelClicked(string value)
    {
        if (!int.TryParse(value, out int level)) return;

        Game1.playSound("smallSelect");
        _closeSelf?.Invoke();
        _onSelect(level);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
