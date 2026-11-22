using System;
using RelaxValley.Framework.UI;
using StardewModdingAPI;

namespace RelaxValley.Framework;

/// <summary>
/// 菜单条目的显示状态。用于把“没实现 / 已完成 / 没发现过 / 今天不出现 / 已锁”区分开，
/// 避免所有不可点击的条目都显示成“未实现”。
/// </summary>
public enum MenuEntryStatus
{
    /// <summary>可点击。</summary>
    Available,

    /// <summary>功能还没做。</summary>
    NotImplemented,

    /// <summary>功能已经全部完成，无需再点（例如背包已满级）。</summary>
    Completed,

    /// <summary>还没在游戏里遇到过 / 打开过（商店发现机制）。</summary>
    NotDiscovered,

    /// <summary>已经发现过，但今天不出现或不在营业时间。</summary>
    NotToday,

    /// <summary>其它条件未满足。</summary>
    Locked
}

public sealed class MenuEntry
{
    public string Id { get; }
    public string TranslationKey { get; }
    public string? DescriptionKey { get; }
    public string Category { get; }

    /// <summary>
    /// 分类标题的字面显示名。为 null 时使用 i18n 键 RelaxValley.Category.{Category}。
    /// 用于“按模组分组”这类动态分类。
    /// </summary>
    public string? CategoryDisplayName { get; }

    /// <summary>
    /// 可选的字面显示名。用于 JSON / 动态商店等没有 i18n 键的条目；
    /// 为 null 时使用 <see cref="TranslationKey"/>。
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>执行动作。通过 entry.Argument 可获取商店ID等参数。</summary>
    public Action<MenuEntry>? Execute { get; }

    /// <summary>
    /// 右键动作。用于“左键原版内容 / 右键更详细的模组页面”模式
    /// （例如完美度追踪：左键原版 ShowQiCat，右键打开完美手册）。
    /// </summary>
    public Action<MenuEntry>? RightExecute { get; }

    public Func<bool>? Condition { get; }

    /// <summary>
    /// 当前是否可用（例如商店今天是否营业、背包是否还能升级）。
    /// 与 <see cref="IsImplemented"/> 分开：未实现 = 永远灰色；不可用 = 临时灰色。
    /// </summary>
    public Func<bool>? Available { get; }

    /// <summary>不可用时的原因，点击时通过 HUD 提示给玩家。</summary>
    public Func<string>? LockReason { get; }

    /// <summary>可选参数。例如远程商店的 shopId、电梯的目标层数等。</summary>
    public string? Argument { get; }

    /// <summary>
    /// 更细的状态来源。返回 <see cref="MenuEntryStatus"/>，用于区分
    /// “已完成 / 未发现 / 今日不开放”等等。为 null 时回退到 Available 布尔判断。
    /// </summary>
    public Func<MenuEntryStatus>? StatusProvider { get; }

    public bool IsVisible => Condition?.Invoke() ?? true;
    public bool IsImplemented => Execute is not null;

    public MenuEntryStatus GetStatus()
    {
        if (!IsImplemented) return MenuEntryStatus.NotImplemented;
        if (StatusProvider is not null) return StatusProvider();
        return (Available?.Invoke() ?? true) ? MenuEntryStatus.Available : MenuEntryStatus.Locked;
    }

    public bool IsAvailable => GetStatus() == MenuEntryStatus.Available;

    /// <summary>条目名称后缀对应的 i18n 键；可点击时返回 null。</summary>
    public string? GetStatusSuffixKey() => GetStatus() switch
    {
        MenuEntryStatus.NotImplemented => "RelaxValley.UI.Status.NotImplemented",
        MenuEntryStatus.Completed => "RelaxValley.UI.Status.Completed",
        MenuEntryStatus.NotDiscovered => "RelaxValley.UI.Status.NotDiscovered",
        MenuEntryStatus.NotToday => "RelaxValley.UI.Status.NotToday",
        MenuEntryStatus.Locked => "RelaxValley.UI.Status.Locked",
        _ => null
    };

    public MenuEntry(
        string id,
        Action<MenuEntry>? execute,
        string? category = null,
        string? translationKey = null,
        string? descriptionKey = null,
        Func<bool>? condition = null,
        string? argument = null,
        string? displayName = null,
        Func<bool>? available = null,
        Func<string>? lockReason = null,
        Func<MenuEntryStatus>? status = null,
        string? categoryDisplayName = null,
        Action<MenuEntry>? rightExecute = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("id 不能为空", nameof(id));

        Id = id;
        Execute = execute;
        RightExecute = rightExecute;
        Category = category ?? "Misc";
        CategoryDisplayName = categoryDisplayName;
        TranslationKey = translationKey ?? $"RelaxValley.Menu.{id}.Name";
        DescriptionKey = descriptionKey;
        Condition = condition;
        Argument = argument;
        DisplayName = displayName;
        Available = available;
        LockReason = lockReason;
        StatusProvider = status;
    }

    public string GetLockReason() => LockReason?.Invoke() ?? I18n.RelaxValley_UI_Common_Locked();

    public bool TryExecute(IMonitor monitor)
    {
        if (Execute is null)
        {
            monitor.Log($"菜单项 '{Id}' 尚未实现。", LogLevel.Warn);
            return false;
        }
        try
        {
            Execute(this);
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
