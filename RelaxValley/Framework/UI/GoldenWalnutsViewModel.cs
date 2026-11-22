using System.Collections.Generic;
using System.Linq;

namespace RelaxValley.Framework.UI;

/// <summary>金核桃信息中的一行明细。</summary>
public sealed class DetailLine
{
    public string Text { get; init; } = "";
}

/// <summary>
/// 通用「收集进度」小页的视图模型（金核桃信息 / 博物馆收藏共用）。
/// 纯展示，一次性绑定。
/// </summary>
public sealed class GoldenWalnutsViewModel
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
}
