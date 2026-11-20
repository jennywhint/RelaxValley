using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace RelaxValley.Framework;

/// <summary>模组配置（config.json，也可通过 GMCM 修改）。</summary>
public sealed class ModConfig
{
    /// <summary>打开主菜单的按键（GMCM 可改），默认 F3。</summary>
    public SButton OpenMenuKey { get; set; } = SButton.F3;

    /// <summary>
    /// 菜单条目快捷键：键 = 条目 Id（如 Calendar / QuestOverview / ShopPierre），
    /// 值 = 绑定的按键组合列表（如 ["LeftControl + H", "J"]）。默认为空 = 不绑定。
    /// 组合键写法："修饰键 + 主键"，按下主键且修饰键处于按住状态时触发；
    /// 同一条目可绑定多个组合。
    /// </summary>
    public Dictionary<string, List<string>> MenuShortcutKeys { get; set; } = new();

    /// <summary>
    /// 商店进度模式。开启 = 需先在游戏里打开过一次商店才能远程购买；
    /// 关闭 = 常开模式，所有已安装的商店始终可交互（营业时间等条件仍然生效）。
    /// </summary>
    public bool ShopProgressMode { get; set; } = true;

    /// <summary>
    /// 村民友谊进度模式。开启 = 未认识的村民不可互动；
    /// 关闭 = 常开模式，允许一键对话时结识新村民、点击卡片直接“认识”并获得好感度。
    /// </summary>
    public bool FriendshipProgressMode { get; set; } = true;

    /// <summary>
    /// 远程电梯进度模式。开启 = 电梯按玩家当前最深层数解锁；
    /// 关闭 = 常开模式，矿井全部楼层与骷髅矿洞常规楼层直接可用。
    /// </summary>
    public bool ElevatorProgressMode { get; set; } = true;

    /// <summary>
    /// 右键「完美度追踪」时要尝试使用的物品名（中英文均可，逗号分隔）。
    /// 例如完美手册的物品名；找到后会像使用该物品一样打开它的界面。
    /// </summary>
    public List<string> PerfectionItems { get; set; } = new()
    {
        "Perfect Handbook",
        "完美手册"
    };

    /// <summary>
    /// 右键「完美度追踪」时要尝试实例化的菜单类型全名
    /// （格式：命名空间.类型名, 程序集名）。留空表示不使用。
    /// </summary>
    public List<string> PerfectionMenuTypes { get; set; } = new();
}
