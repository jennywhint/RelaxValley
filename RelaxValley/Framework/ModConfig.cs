using StardewModdingAPI;
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
    /// 村民友谊页的「一键对话」按钮。默认关闭，避免误触一次性消耗全部交谈机会。
    /// </summary>
    public bool EnableTalkAll { get; set; } = false;

    /// <summary>
    /// 村民友谊页左键卡片互动菜单里的「传送」选项。默认关闭，保持页面为纯信息用途。
    /// </summary>
    public bool EnableFriendshipTeleport { get; set; } = false;

    /// <summary>
    /// 批量送礼按钮。开启后，友谊页可把手持物品按原版送礼规则各送一份给今日未送礼的村民。
    /// 会消耗每日/每周送礼机会，默认关闭。
    /// </summary>
    public bool EnableBatchGift { get; set; } = false;

    /// <summary>
    /// 原版门禁。开启 = 赌场统计需要锈钥匙、核桃房订单需要核桃房开启（与原版进入条件一致）；
    /// 关闭 = 这两项始终可用。默认开启。
    /// </summary>
    public bool RespectVanillaGates { get; set; } = true;

    /// <summary>
    /// 远程电梯进度模式。开启 = 电梯按玩家当前最深层数解锁；
    /// 关闭 = 常开模式，矿井全部楼层与骷髅矿洞常规楼层直接可用。
    /// </summary>
    public bool ElevatorProgressMode { get; set; } = true;

    /// <summary>
    /// 右键「农场动物」时尝试实例化的菜单类型全名
    /// （格式：命名空间.类型名, 程序集名）。留空表示不使用。
    /// </summary>
    public List<string> StatsMenuTypes { get; set; } = new();
}
