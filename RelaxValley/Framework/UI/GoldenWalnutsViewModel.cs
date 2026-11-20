using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RelaxValley.Framework.UI;

/// <summary>金核桃信息中的一行明细。</summary>
public sealed class DetailLine
{
    public string Text { get; init; } = "";
}

/// <summary>金核桃收集信息的 StardewUI 视图模型。</summary>
public sealed class GoldenWalnutsViewModel : INotifyPropertyChanged
{
    public GoldenWalnutsViewModel(
        string title,
        string summary,
        string progressText,
        IEnumerable<string> details,
        string hint)
    {
        Title = title;
        Summary = summary;
        ProgressText = progressText;
        Details = details.Select(line => new DetailLine { Text = line }).ToList();
        Hint = hint;
    }

    public string Title { get; }

    public string Summary { get; }

    /// <summary>例如 “42%”。</summary>
    public string ProgressText { get; }

    public List<DetailLine> Details { get; }

    public string Hint { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
