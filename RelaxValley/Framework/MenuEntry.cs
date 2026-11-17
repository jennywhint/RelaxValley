using System;
using RelaxValley.Framework.UI;
using StardewModdingAPI;

namespace RelaxValley.Framework;

public sealed class MenuEntry
{
    public string Id { get; }
    public string TranslationKey { get; }
    public string? DescriptionKey { get; }
    public string Category { get; }

    /// <summary>执行动作。参数：(发起菜单, 本条目自身)。通过 entry.Argument 可获取商店ID等参数。</summary>
    public Action<MainMenu, MenuEntry>? Execute { get; }

    public Func<bool>? Condition { get; }

    /// <summary>可选参数。例如远程商店的 shopId、电梯的目标层数等。</summary>
    public string? Argument { get; }

    public bool IsVisible => Condition?.Invoke() ?? true;
    public bool IsImplemented => Execute is not null;

    public MenuEntry(
        string id,
        Action<MainMenu, MenuEntry>? execute,
        string? category = null,
        string? translationKey = null,
        string? descriptionKey = null,
        Func<bool>? condition = null,
        string? argument = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("id 不能为空", nameof(id));

        Id = id;
        Execute = execute;
        Category = category ?? "Misc";
        TranslationKey = translationKey ?? $"RelaxValley.Menu.{id}.Name";
        DescriptionKey = descriptionKey;
        Condition = condition;
        Argument = argument;
    }

    public bool TryExecute(MainMenu source, IMonitor monitor)
    {
        if (Execute is null)
        {
            monitor.Log($"菜单项 '{Id}' 尚未实现。", LogLevel.Warn);
            return false;
        }
        try
        {
            Execute(source, this);
            return true;
        }
        catch (Exception ex)
        {
            monitor.Log($"执行菜单项 '{Id}' 时发生异常：{ex}", LogLevel.Error);
            return false;
        }
    }

    public override string ToString() => $"MenuEntry({Id})";
}