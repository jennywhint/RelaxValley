using System.Collections.Generic;

namespace RelaxValley.Framework.Shops;

/// <summary>工作台商店条目的打开方式。</summary>
public enum ShopKind
{
    /// <summary>普通 Data/Shops 商店，用 Utility.TryOpenShopMenu 打开。</summary>
    Shop,

    /// <summary>罗宾的建造 / 升级菜单（CarpenterMenu）。</summary>
    Carpenter,

    /// <summary>法师的建造菜单（CarpenterMenu）。</summary>
    WizardBuild,

    /// <summary>玛妮的购买动物菜单。</summary>
    AnimalShop,

    /// <summary>铁匠铺的砸晶石菜单（GeodeMenu）。</summary>
    Geode,

    /// <summary>节日当天的节日商店（从 Data/Shops 动态查找）。</summary>
    Festival,

    /// <summary>大树桩浣熊商店（从 Data/Shops 动态查找）。</summary>
    Raccoon
}

/// <summary>
/// 一个工作台商店/功能条目。内置条目在 ShopRegistry 里构造；
/// 模组商店由 assets/data/shops.json 提供。
/// </summary>
public sealed class ShopDefinition
{
    /// <summary>唯一 ID，同时用于默认 i18n 键 RelaxValley.Menu.{Id}.Name。</summary>
    public string Id { get; set; } = "";

    /// <summary>字面显示名。为空时使用 i18n 键。</summary>
    public string? Name { get; set; }

    /// <summary>分类，对应 RelaxValley.Category.{Category}。</summary>
    public string Category { get; set; } = "WorkbenchShopMod";

    /// <summary>打开方式。</summary>
    public ShopKind Kind { get; set; } = ShopKind.Shop;

    /// <summary>Data/Shops 里的商店 ID。普通商店必填。</summary>
    public string? ShopId { get; set; }

    /// <summary>可选：需要安装的模组 UniqueID；未安装时该条目隐藏。</summary>
    public string? RequiredMod { get; set; }

    /// <summary>可选：额外的 GameStateQuery 可用条件（为空表示不额外限制）。</summary>
    public string? Condition { get; set; }

    /// <summary>可选：不可用时的提示文本；为空时使用商店自身的 ClosedMessage 或通用提示。</summary>
    public string? LockReason { get; set; }

    /// <summary>是否来自 assets/data/shops.json（模组商店），用于把模组条目排在最后。</summary>
    public bool IsFromJson { get; set; }

    /// <summary>自动发现的模组商店所属模组显示名；原版商店为 null。</summary>
    public string? SourceModName { get; set; }

    /// <summary>自动发现的模组商店用于分组的分类 ID，例如 Mod:FlashShifter.StardewValleyExpandedCP。</summary>
    public string? SourceModId { get; set; }
}

/// <summary>assets/data/shops.json 的根对象。</summary>
public sealed class ShopConfig
{
    public string? Readme { get; set; }

    public List<ShopDefinition> Shops { get; set; } = new();
}

/// <summary>assets/data/shop-names.json 的根对象（手动中文名覆盖）。</summary>
public sealed class ShopNameConfig
{
    public string? Readme { get; set; }

    public Dictionary<string, string> ShopNames { get; set; } = new();
}
