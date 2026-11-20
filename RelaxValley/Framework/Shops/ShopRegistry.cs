using RelaxValley.Framework.UI;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Shops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RelaxValley.Framework.Shops;

/// <summary>
/// 工作台商店注册表。
/// 内置条目使用 Game1.shop_* 常量，避免手写商店 ID 出错；
/// 模组商店从 assets/data/shops.json 读取。
/// 可用性优先用 Data/Shops 里每个 owner 的 Condition（营业时间、季节、节日、解锁条件等），
/// 这样旅行商人、冰淇淋摊、节日商店等不用在代码里硬编码日期规则。
/// </summary>
public sealed class ShopRegistry
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private Dictionary<string, ShopData>? _shopData;

    public ShopRegistry(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    // ── 条目 ────────────────────────────────────────────────────

    public IReadOnlyList<ShopDefinition> GetDefinitions()
    {
        var definitions = CreateBuiltIns();
        definitions.AddRange(LoadJson());
        definitions.RemoveAll(d => string.IsNullOrWhiteSpace(d.Id));
        return definitions;
    }

    // ── 自动发现：模组商店 / 节日商店 ─────────────────────────────

    /// <summary>
    /// 关键字前缀别名：某些模组给商店 ID 用的前缀不是自己的 UniqueID。
    /// 例如 RSV 的商店都叫 RSVxxxShop。
    /// </summary>
    private static readonly Dictionary<string, string> ShopPrefixAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RSV"] = "Rafseazz.RidgesideVillage",
        };

    /// <summary>
    /// 从 Data/Shops 自动发现所有“由已安装模组添加”的商店，
    /// 按来源模组分组。这样不需要把所有模组商店硬编码进 shops.json，
    /// 装了任何新模组都会自动出现在工作台的“模组商店”区域。
    /// </summary>
    public IReadOnlyList<ShopDefinition> GetModShops()
    {
        var result = new List<ShopDefinition>();

        // 已内置 / 已写进 shops.json 的商店（按 Id 与 ShopId 双重去重）
        List<ShopDefinition> known = GetDefinitions().ToList();
        var knownIds = new HashSet<string>(known.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
        var knownShopIds = new HashSet<string>(
            known.Where(d => !string.IsNullOrWhiteSpace(d.ShopId)).Select(d => d.ShopId!),
            StringComparer.OrdinalIgnoreCase);

        // 预先把每个模组的匹配前缀算好，避免逐个商店重复拼接字符串
        var modIndex = new List<(IModInfo mod, string[] prefixes)>();
        foreach (IModInfo mod in _helper.ModRegistry.GetAll())
        {
            string uniqueId = mod.Manifest.UniqueID;
            if (uniqueId.Length < 3) continue;

            string underscoreId = uniqueId.Replace('.', '_');
            modIndex.Add((mod, underscoreId == uniqueId
                ? new[] { uniqueId }
                : new[] { uniqueId, underscoreId }));
        }

        foreach (var pair in GetShopData())
        {
            if (knownIds.Contains(pair.Key)) continue;
            if (knownShopIds.Contains(pair.Key)) continue;

            (IModInfo mod, string prefix)? source = FindSourceMod(modIndex, pair.Key);
            if (source is null) continue;

            result.Add(new ShopDefinition
            {
                Id = pair.Key,
                Name = ResolveLocalizedShopName(pair.Key, source.Value.prefix),
                Category = $"Mod:{source.Value.mod.Manifest.UniqueID}",
                Kind = ShopKind.Shop,
                ShopId = pair.Key,
                RequiredMod = source.Value.mod.Manifest.UniqueID,
                SourceModName = source.Value.mod.Manifest.Name,
                SourceModId = source.Value.mod.Manifest.UniqueID
            });
        }

        return result
            .OrderBy(d => d.SourceModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>节日当天动态生成该节日的所有摊位商店（原版节日、夜市、沙漠节、模组节日）。</summary>
    public IReadOnlyList<ShopDefinition> GetFestivalShops()
    {
        var result = new List<ShopDefinition>();

        // 当前激活的“被动节日”（夜市、沙漠节等），由游戏按日期写入世界状态
        var activePassive = new List<string>();
        var worldState = Game1.netWorldState?.Value;
        if (worldState is not null)
        {
            foreach (string id in worldState.ActivePassiveFestivals)
                activePassive.Add(id);
        }

        // 普通节日（有事件过场的那种）：用 festival_{assetName} → 商店名片段 的映射精确匹配
        string? eventSegment = ResolveCurrentEventFestivalSegment();

        foreach (var pair in GetShopData())
        {
            string key = pair.Key;
            if (!key.Contains("Festival", StringComparison.OrdinalIgnoreCase)) continue;

            bool isPassiveShop =
                key.Contains("NightMarket", StringComparison.OrdinalIgnoreCase)
                || key.Contains("DesertFestival", StringComparison.OrdinalIgnoreCase);

            bool include = false;

            if (isPassiveShop)
            {
                foreach (string id in activePassive)
                {
                    if (key.Contains(id, StringComparison.OrdinalIgnoreCase))
                    {
                        include = true;
                        break;
                    }
                }
            }
            else if (eventSegment is not null)
            {
                include = key.Contains(eventSegment, StringComparison.OrdinalIgnoreCase);
            }
            else if (Game1.isFestival())
            {
                // 模组节日：无法映射名称时退回“当天是节日就全部列出”
                include = true;
            }

            if (!include) continue;
            if (!IsShopDataAvailable(key)) continue;

            result.Add(new ShopDefinition
            {
                Id = key,
                Name = ResolveFestivalShopName(key),
                Category = "WorkbenchShopFestival",
                Kind = ShopKind.Festival,
                ShopId = key
            });
        }

        return result
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>原版节日事件 ID（festival_spring13 等）→ Data/Shops 里的商店名片段。</summary>
    private static readonly Dictionary<string, string> FestivalShopSegments =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["spring13"] = "EggFestival",
            ["spring24"] = "FlowerDance",
            ["summer11"] = "Luau",
            ["summer28"] = "DanceOfTheMoonlightJellies",
            ["fall16"] = "StardewValleyFair",
            ["fall27"] = "SpiritsEve",
            ["winter8"] = "FestivalOfIce",
            ["winter25"] = "FeastOfTheWinterStar",
        };

    private static string? ResolveCurrentEventFestivalSegment()
    {
        string? eventId = Game1.CurrentEvent?.id;
        if (string.IsNullOrWhiteSpace(eventId)) return null;
        if (!eventId.StartsWith("festival_", StringComparison.OrdinalIgnoreCase)) return null;

        string festivalKey = eventId["festival_".Length..];
        return FestivalShopSegments.TryGetValue(festivalKey, out string? segment) ? segment : null;
    }

    /// <summary>找出添加该商店的模组：先按 UniqueID 前缀，再按已知别名。</summary>
    private (IModInfo mod, string prefix)? FindSourceMod(
        List<(IModInfo mod, string[] prefixes)> modIndex, string shopId)
    {
        foreach (var entry in modIndex)
        {
            foreach (string prefix in entry.prefixes)
            {
                if (MatchesShopPrefix(shopId, prefix))
                    return (entry.mod, prefix);
            }
        }

        foreach (var alias in ShopPrefixAliases)
        {
            if (!MatchesShopPrefix(shopId, alias.Key)) continue;

            IModInfo? mod = _helper.ModRegistry.Get(alias.Value);
            if (mod is not null)
                return (mod, alias.Key);
        }

        return null;
    }

    /// <summary>商店 ID 是否以指定前缀开头，且前缀后面是分隔符（避免 RSV 误匹配 RSVillage 之类）。</summary>
    private static bool MatchesShopPrefix(string shopId, string prefix)
    {
        if (!shopId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        if (shopId.Length == prefix.Length) return true;

        return shopId[prefix.Length] is '_' or '.' or '-' or ' ';
    }

    /// <summary>把 FlashShifter.StardewValleyExpandedCP_AlesiaVendor 这类 ID 变成 “Alesia Vendor”。</summary>
    private static string PrettyShopName(string shopId, string prefix)
    {
        string name = StripShopPrefix(shopId, prefix);

        name = name.Replace('_', ' ').Trim();
        if (name.Length == 0) return shopId;

        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) && name[i - 1] != ' ')
                builder.Append(' ');
            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string StripShopPrefix(string shopId, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return shopId;
        if (!shopId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return shopId;

        return shopId[prefix.Length..].TrimStart('_', '.', ' ');
    }

    // ── 中文店名 ────────────────────────────────────────────────

    /// <summary>Data/Characters 的本地化显示名，用于把商店 ID 翻成中文。</summary>
    private Dictionary<string, string>? _characterDisplayNames;

    /// <summary>assets/data/shop-names.json 的手动中文名覆盖。</summary>
    private Dictionary<string, string>? _nameOverrides;

    /// <summary>商店 ID 里常见英文单词 → 中文，用于翻译没有对应 NPC 的店铺。</summary>
    private static readonly Dictionary<string, string> WordTranslations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["shop"] = "商店", ["store"] = "商店", ["vendor"] = "商店", ["market"] = "市场",
            ["grocery"] = "杂货", ["general"] = "杂货", ["convenience"] = "便利店",
            ["clothes"] = "服装", ["clothing"] = "服装", ["tailor"] = "裁缝",
            ["blacksmith"] = "铁匠铺", ["smith"] = "铁匠铺", ["forge"] = "铁匠铺",
            ["alchemist"] = "炼金", ["familiars"] = "魔宠", ["magic"] = "魔法",
            ["catalogue"] = "目录", ["catalog"] = "目录", ["furniture"] = "家具",
            ["ledger"] = "账本", ["cafe"] = "咖啡馆", ["restaurant"] = "餐厅",
            ["bar"] = "酒吧", ["lodge"] = "旅店", ["inn"] = "旅店",
            ["bakery"] = "面包店", ["baker"] = "面包店", ["flower"] = "花店",
            ["flowers"] = "花店", ["fish"] = "鱼", ["seeds"] = "种子",
            ["seed"] = "种子", ["tools"] = "工具", ["weapon"] = "武器",
            ["weapons"] = "武器", ["armor"] = "护甲", ["jeweler"] = "珠宝",
            ["jewelry"] = "珠宝", ["potion"] = "药水", ["potions"] = "药水",
            ["gift"] = "礼物", ["gifts"] = "礼物", ["food"] = "食物",
            ["drinks"] = "饮品", ["candy"] = "糖果", ["icecream"] = "冰淇淋",
            ["garden"] = "花园", ["farm"] = "农场", ["forest"] = "森林",
            ["hidden"] = "隐藏", ["liquor"] = "酒", ["wine"] = "酒",
            ["cheese"] = "奶酪", ["artisan"] = "工匠", ["record"] = "唱片",
            ["book"] = "书籍", ["books"] = "书籍", ["toy"] = "玩具",
            ["toys"] = "玩具", ["card"] = "卡牌", ["cards"] = "卡牌",
            ["vending"] = "贩卖机", ["twilight"] = "暮光", ["festival"] = "节日",
            ["supply"] = "补给", ["supplies"] = "补给", ["goods"] = "杂货"
        };

    /// <summary>商店类型后缀 → 中文。用于组合“某人的某店”。</summary>
    private static readonly (string Keyword, string Chinese)[] ShopKindWords =
    {
        ("Vendor", "商店"),
        ("Shop", "商店"),
        ("Store", "商店"),
        ("Market", "市场"),
        ("Blacksmith", "铁匠铺"),
        ("Smith", "铁匠铺"),
        ("Ledger", "账本"),
        ("Catalogue", "目录"),
        ("Cafe", "咖啡馆"),
        ("Recipes", "食谱"),
        ("Box", "售票处"),
        ("Ledgers", "账本"),
    };

    /// <summary>节日 ID 片段 → 中文名。</summary>
    private static readonly (string Keyword, string Chinese)[] FestivalWords =
    {
        ("FeastOfTheWinterStar", "冬日星盛宴"),
        ("DanceOfTheMoonlightJellies", "月光水母之舞"),
        ("StardewValleyFair", "星露谷集市"),
        ("FestivalOfIce", "冰雪节"),
        ("EggFestival", "蛋蛋节"),
        ("FlowerDance", "花之舞"),
        ("SpiritsEve", "灵魂之夜"),
        ("DesertFestival", "沙漠节"),
        ("NightMarket", "夜市"),
        ("Luau", "夏威夷宴会"),
    };

    private Dictionary<string, string> GetCharacterDisplayNames()
    {
        if (_characterDisplayNames is not null) return _characterDisplayNames;
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var data = Game1.content.Load<Dictionary<string, CharacterData>>("Data/Characters");
            foreach (var pair in data)
            {
                string? displayName = pair.Value.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName)) continue;

                names[pair.Key] = TokenParser.ParseText(displayName, null, null, Game1.player)
                                  ?? displayName;
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"读取 Data/Characters 失败：{ex.Message}", LogLevel.Warn);
        }

        _characterDisplayNames = names;
        return names;
    }

    /// <summary>取商店 NamedNpc 拥有者的本地化名字。</summary>
    private string? GetNamedOwnerDisplayName(string shopId)
    {
        if (!GetShopData().TryGetValue(shopId, out ShopData data) || data.Owners is null)
            return null;

        foreach (ShopOwnerData owner in data.Owners)
        {
            if (owner.Type != ShopOwnerType.NamedNpc) continue;
            if (string.IsNullOrWhiteSpace(owner.Name)) continue;

            return GetCharacterDisplayNames().TryGetValue(owner.Name, out string? localized)
                ? localized
                : owner.Name;
        }

        return null;
    }

    /// <summary>在商店 ID 文本里找 NPC 名字（取最长的匹配，避免 Sam 命中 Samantha 之类）。</summary>
    private string? FindNpcNameInText(string text)
    {
        Dictionary<string, string> names = GetCharacterDisplayNames();
        string? bestKey = null;

        foreach (string key in names.Keys)
        {
            if (key.Length < 3) continue;
            if (!text.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;

            // 名字后面必须是结尾 / 大写字母 / 分隔符，避免误匹配
            int next = key.Length;
            if (text.Length > next)
            {
                char c = text[next];
                if (!char.IsUpper(c) && c != '_' && c != ' ' && !char.IsDigit(c))
                    continue;
            }

            if (bestKey is null || key.Length > bestKey.Length)
                bestKey = key;
        }

        return bestKey is null ? null : names[bestKey];
    }

    /// <summary>读取 assets/data/shop-names.json 的手动中文名。</summary>
    private Dictionary<string, string> GetNameOverrides()
    {
        if (_nameOverrides is not null) return _nameOverrides;

        try
        {
            ShopNameConfig? config = _helper.Data.ReadJsonFile<ShopNameConfig>("assets/data/shop-names.json");
            _nameOverrides = new Dictionary<string, string>(
                config?.ShopNames ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _monitor.Log($"读取 shop-names.json 失败：{ex.Message}", LogLevel.Warn);
            _nameOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return _nameOverrides;
    }

    /// <summary>把英文商店名逐词翻译成中文；一个词都翻不出来时返回空串。</summary>
    private static string TranslateShopWords(string text)
    {
        var builder = new System.Text.StringBuilder();
        bool translatedAny = false;

        foreach (string word in SplitShopWords(text))
        {
            if (WordTranslations.TryGetValue(word, out string? chinese))
            {
                builder.Append(chinese);
                translatedAny = true;
            }
            else
            {
                builder.Append(word);
            }
        }

        return translatedAny ? builder.ToString() : "";
    }

    /// <summary>按 CamelCase / 下划线 / 空格拆分英文标识符。</summary>
    private static List<string> SplitShopWords(string text)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        char previous = '\0';

        foreach (char c in text)
        {
            if (c is '_' or ' ' or '-' or '.')
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }

                previous = c;
                continue;
            }

            if (current.Length > 0
                && char.IsUpper(c)
                && !char.IsUpper(previous)
                && !char.IsDigit(previous))
            {
                words.Add(current.ToString());
                current.Clear();
            }

            current.Append(c);
            previous = c;
        }

        if (current.Length > 0) words.Add(current.ToString());
        return words;
    }

    private static string ResolveKindWord(string text)
    {
        foreach (var (keyword, chinese) in ShopKindWords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return chinese;
        }

        return "商店";
    }

    /// <summary>
    /// 模组商店的中文名：优先用拥有者 NPC 的名字，其次从 ID 里找 NPC 名字，
    /// 例如 AlesiaVendor → “阿莱西亚的商店”。都找不到时退回英文 ID。
    /// </summary>
    private string ResolveLocalizedShopName(string shopId, string prefix)
    {
        // 1) 手动覆盖表优先
        if (GetNameOverrides().TryGetValue(shopId, out string? custom)
            && !string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        string remainder = StripShopPrefix(shopId, prefix);

        // 2) NPC 拥有者 / ID 里的 NPC 名字
        string npcName = GetNamedOwnerDisplayName(shopId)
                         ?? FindNpcNameInText(remainder)
                         ?? "";

        if (!string.IsNullOrWhiteSpace(npcName))
            return $"{npcName}的{ResolveKindWord(remainder)}";

        // 3) 常见英文词翻译
        string translated = TranslateShopWords(remainder);
        if (!string.IsNullOrWhiteSpace(translated)) return translated;

        // 4) 兜底：美化后的英文 ID
        return PrettyShopName(shopId, prefix);
    }

    /// <summary>节日商店的中文名：节日名 · NPC 名，例如 “沙漠节·阿比盖尔”。</summary>
    private string ResolveFestivalShopName(string shopId)
    {
        // 手动覆盖优先
        if (GetNameOverrides().TryGetValue(shopId, out string? custom)
            && !string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        string label = "";
        string matchedKeyword = "";

        foreach (var (keyword, chinese) in FestivalWords)
        {
            if (shopId.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                label = chinese;
                matchedKeyword = keyword;
                break;
            }
        }

        string npcName = GetNamedOwnerDisplayName(shopId)
                         ?? FindNpcNameInText(shopId)
                         ?? "";

        if (!string.IsNullOrWhiteSpace(npcName))
            return string.IsNullOrEmpty(label) ? npcName : $"{label}·{npcName}";

        // 没有 NPC 的摊位（夜市小船、代币摊等）：先尝试逐词翻译
        string remainder = StripShopPrefix(shopId, "");
        string translated = TranslateShopWords(remainder);

        if (!string.IsNullOrWhiteSpace(translated))
        {
            translated = translated
                .Replace("节日", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (translated.Length > 0)
                return string.IsNullOrEmpty(label) ? translated : $"{label}·{translated}";
        }

        // 最后退回美化后的英文
        string pretty = PrettyShopName(shopId, "")
            .Replace("Festival", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (matchedKeyword.Length > 0)
        {
            pretty = pretty
                .Replace(matchedKeyword, "", StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        pretty = pretty.TrimStart('·', '-', '_', ' ').Trim();
        if (pretty.Length == 0) pretty = shopId;

        return string.IsNullOrEmpty(label) ? pretty : $"{label}·{pretty}";
    }

    /// <summary>未安装 RequiredMod 的条目直接隐藏。</summary>
    public bool IsVisible(ShopDefinition def) =>
        string.IsNullOrWhiteSpace(def.RequiredMod)
        || _helper.ModRegistry.IsLoaded(def.RequiredMod);

    public bool IsAvailable(ShopDefinition def) => GetStatus(def) == MenuEntryStatus.Available;

    /// <summary>
    /// 细分状态：
    /// 没发现过 → 未发现；发现过但今天不出现 → 今日不开放；
    /// 节日商店在节日当天直接可用，不需要先打开过。
    /// </summary>
    public MenuEntryStatus GetStatus(ShopDefinition def)
    {
        if (!IsVisible(def)) return MenuEntryStatus.Locked;

        if (!string.IsNullOrWhiteSpace(def.Condition)
            && !GameStateQuery.CheckConditions(def.Condition, null, null))
        {
            return MenuEntryStatus.Locked;
        }

        return def.Kind switch
        {
            ShopKind.Shop => GetShopStatus(def.ShopId),

            // 建造 / 动物 / 砸晶石是功能菜单，游戏自身已有条件限制
            ShopKind.Carpenter or ShopKind.WizardBuild
                or ShopKind.AnimalShop or ShopKind.Geode => MenuEntryStatus.Available,

            // 节日商店：节日当天自动可用，不需要“发现过一次”
            ShopKind.Festival => def.ShopId is { Length: > 0 } festivalId
                ? (IsShopDataAvailable(festivalId)
                    ? MenuEntryStatus.Available
                    : MenuEntryStatus.NotToday)
                : (ResolveFestivalShop() is not null
                    ? MenuEntryStatus.Available
                    : MenuEntryStatus.NotToday),

            ShopKind.Raccoon => GetRaccoonStatus(),

            _ => MenuEntryStatus.Available
        };
    }

    /// <summary>浣熊商店：进度模式下必须先在原版打开过一次，之后再按营业条件判断。</summary>
    private MenuEntryStatus GetRaccoonStatus()
    {
        string? raccoonShop = FindShopKeyByKeyword("Raccoon");
        if (raccoonShop is null) return MenuEntryStatus.NotToday;
        if (ModEntry.Config.ShopProgressMode && !ShopDiscovery.IsVisited(raccoonShop))
            return MenuEntryStatus.NotDiscovered;

        return IsShopDataAvailable(raccoonShop)
            ? MenuEntryStatus.Available
            : MenuEntryStatus.NotToday;
    }

    public string GetLockReason(ShopDefinition def)
    {
        if (!string.IsNullOrWhiteSpace(def.Condition)
            && !GameStateQuery.CheckConditions(def.Condition, null, null))
        {
            return def.LockReason ?? I18n.RelaxValley_UI_Workbench_Closed();
        }

        return def.Kind switch
        {
            ShopKind.Shop => GetShopLockReason(def.ShopId, def.LockReason),
            ShopKind.Carpenter or ShopKind.WizardBuild
                or ShopKind.AnimalShop or ShopKind.Geode =>
                def.LockReason ?? I18n.RelaxValley_UI_Workbench_Closed(),
            ShopKind.Festival => def.LockReason ?? I18n.RelaxValley_UI_Workbench_NoFestival(),
            ShopKind.Raccoon => ResolveRaccoonShop() is { } raccoonShop
                ? GetShopLockReason(raccoonShop, def.LockReason)
                : def.LockReason ?? I18n.RelaxValley_UI_Workbench_NoRaccoon(),
            _ => def.LockReason ?? I18n.RelaxValley_UI_Workbench_Closed()
        };
    }

    // ── 打开 ────────────────────────────────────────────────────

    public void Open(ShopDefinition def)
    {
        switch (def.Kind)
        {
            case ShopKind.Shop:
                OpenShop(def.ShopId);
                break;

            case ShopKind.Carpenter:
                OpenCarpenterOptions();
                break;

            case ShopKind.WizardBuild:
                Game1.activeClickableMenu = new CarpenterMenu(Game1.builder_wizard, null);
                Game1.playSound("bigSelect");
                break;

            case ShopKind.AnimalShop:
                OpenAnimalShop();
                break;

            case ShopKind.Geode:
                Game1.activeClickableMenu = new GeodeMenu();
                Game1.playSound("bigSelect");
                break;

            case ShopKind.Festival:
                OpenShop(def.ShopId ?? ResolveFestivalShop());
                break;

            case ShopKind.Raccoon:
                OpenShop(ResolveRaccoonShop());
                break;
        }
    }

    /// <summary>
    /// 罗宾：复刻原版 ScienceHouse 柜台的问题框（GameLocation.carpenters）。
    /// 选项与解锁条件与原版逐行一致；问题框使用原版 "carpenter" 键，
    /// 玩家的选择会由游戏本体路由到 carpenter_Shop / carpenter_Upgrade /
    /// carpenter_CommunityUpgrade / carpenter_Renovate / carpenter_Construct，
    /// 升级报价、社区升级、翻新菜单等全部走原版逻辑，随游戏进度自然解锁。
    /// </summary>
    private void OpenCarpenterOptions()
    {
        var location = Game1.currentLocation;
        if (location is null) return;

        // 与原版一致：房屋施工中 / 农场建筑在建时，柜台只开放商店
        if (Game1.player.daysUntilHouseUpgrade.Value >= 0 || Game1.IsThereABuildingUnderConstruction())
        {
            Utility.TryOpenShopMenu(Game1.shop_carpenter, "Robin");
            Game1.playSound("bigSelect");
            return;
        }

        var options = new List<Response>
        {
            new("Shop", Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_Shop"))
        };

        if (Game1.IsMasterGame)
        {
            if (Game1.player.HouseUpgradeLevel < 3)
            {
                options.Add(new("Upgrade",
                    Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_UpgradeHouse")));
            }
            else if ((Game1.MasterPlayer.mailReceived.Contains("ccIsComplete")
                      || Game1.MasterPlayer.mailReceived.Contains("JojaMember")
                      || Game1.MasterPlayer.hasCompletedCommunityCenter())
                     && Game1.RequireLocation<Town>("Town").daysUntilCommunityUpgrade.Value <= 0)
            {
                if (!Game1.MasterPlayer.mailReceived.Contains("pamHouseUpgrade"))
                    options.Add(new("CommunityUpgrade",
                        Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_CommunityUpgrade")));
                else if (!Game1.MasterPlayer.mailReceived.Contains("communityUpgradeShortcuts"))
                    options.Add(new("CommunityUpgrade",
                        Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_CommunityUpgrade")));
            }
        }
        else if (Game1.player.HouseUpgradeLevel < 3)
        {
            // 农夫协助升级小屋
            options.Add(new("Upgrade",
                Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_UpgradeCabin")));
        }

        if (Game1.player.HouseUpgradeLevel >= 2)
        {
            options.Add(new("Renovate", Game1.content.LoadString(Game1.IsMasterGame
                ? "Strings\\Locations:ScienceHouse_CarpenterMenu_RenovateHouse"
                : "Strings\\Locations:ScienceHouse_CarpenterMenu_RenovateCabin")));
        }

        options.Add(new("Construct",
            Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_Construct")));
        options.Add(new("Leave",
            Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_Leave")));

        location.createQuestionDialogue(
            Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu"),
            options.ToArray(),
            "carpenter");
    }

    private void OpenAnimalShop()
    {
        // 原版 Marnie_Adopt 的调用方式：在 currentLocation 上打开，
        // 地点选择与 PurchaseAnimalsMenu 都由游戏本体处理。
        // 必须等当前菜单收尾完成后再打开，否则对话框会被旧菜单的收尾流程吞掉。
        ModEntry.RunWhenPlayerFree(() => Game1.currentLocation?.ShowAnimalShopMenu());
        Game1.playSound("bigSelect");
    }

    private void OpenShop(string? shopId)
    {
        if (string.IsNullOrWhiteSpace(shopId))
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Workbench_MissingShop());
            return;
        }

        Utility.TryOpenShopMenu(shopId, GetFirstNamedOwner(shopId));
        Game1.playSound("bigSelect");
    }

    /// <summary>
    /// 远程打开商店时没有 NPC 在附近，需要显式把商店 owner 的名字传给游戏，
    /// 否则 Pierre / Robin / Marnie 这类 NamedNpc 商店会静默失败。
    /// </summary>
    private string? GetFirstNamedOwner(string shopId)
    {
        if (!GetShopData().TryGetValue(shopId, out ShopData data) || data.Owners is null)
            return null;

        foreach (ShopOwnerData owner in data.Owners)
        {
            if (owner.Type != ShopOwnerType.NamedNpc) continue;
            if (string.IsNullOrWhiteSpace(owner.Name)) continue;

            if (!string.IsNullOrWhiteSpace(owner.Condition)
                && !GameStateQuery.CheckConditions(owner.Condition, null, null))
            {
                continue;
            }

            return owner.Name;
        }

        return null;
    }

    // ── Data/Shops 可用性 ───────────────────────────────────────

    private Dictionary<string, ShopData> GetShopData()
    {
        if (_shopData is not null) return _shopData;

        try
        {
            _shopData = Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops");
        }
        catch (Exception ex)
        {
            _monitor.Log($"读取 Data/Shops 失败：{ex.Message}", LogLevel.Error);
            _shopData = new Dictionary<string, ShopData>();
        }

        return _shopData;
    }

    private MenuEntryStatus GetShopStatus(string? shopId)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return MenuEntryStatus.Locked;

        // 进度模式：原版里能打开一次才可远程打开；
        // 常开模式：跳过发现检查，只要商店存在且今天营业即可交互。
        if (ModEntry.Config.ShopProgressMode && !ShopDiscovery.IsVisited(shopId))
            return MenuEntryStatus.NotDiscovered;
        if (!GetShopData().ContainsKey(shopId)) return MenuEntryStatus.Locked;

        // 出现日期 / 季节：原版固定商店用游戏自身规则判断；
        // 模组商店（ID 不在原版常量里）交给 Data/Shops 里 owner 的条件判断，
        // 所以 SVE / RSV / 沙漠节等新增的旅行商人也能按各自数据生效。
        if (!IsShopTimeAvailable(shopId)) return MenuEntryStatus.NotToday;
        if (!IsShopDataAvailable(shopId)) return MenuEntryStatus.NotToday;

        return MenuEntryStatus.Available;
    }

    /// <summary>旅行商人 / 书商 / 冰淇淋摊的“今天是否出现”。</summary>
    private static bool IsShopTimeAvailable(string shopId)
    {
        if (shopId == Game1.shop_travelingCart)
        {
            return Game1.Date.DayOfWeek == DayOfWeek.Friday
                || Game1.Date.DayOfWeek == DayOfWeek.Sunday;
        }

        if (shopId == Game1.shop_bookseller || shopId == Game1.shop_bookseller_trade)
            return IsBooksellerDay();

        if (shopId == Game1.shop_iceCreamStand)
            return Game1.currentSeason == "summer";

        return true;
    }

    /// <summary>
    /// 书商每季随机来两天，直接用游戏本体的 Utility.getDaysOfBooksellerThisSeason() 判断。
    /// </summary>
    private static bool IsBooksellerDay()
    {
        try
        {
            return Utility.getDaysOfBooksellerThisSeason().Contains(Game1.dayOfMonth);
        }
        catch
        {
            return false;
        }
    }

    private bool IsShopDataAvailable(string shopId)
    {
        if (!GetShopData().TryGetValue(shopId, out ShopData data)) return false;
        if (data.Owners is null || data.Owners.Count == 0) return true;

        foreach (ShopOwnerData owner in data.Owners)
        {
            if (string.IsNullOrWhiteSpace(owner.Condition)) return true;
            if (GameStateQuery.CheckConditions(owner.Condition, null, null)) return true;
        }

        return false;
    }

    private string GetShopLockReason(string? shopId, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(shopId))
            return fallback ?? I18n.RelaxValley_UI_Workbench_MissingShop();

        if (ModEntry.Config.ShopProgressMode && !ShopDiscovery.IsVisited(shopId))
            return I18n.RelaxValley_UI_Workbench_NotDiscovered();

        if (!GetShopData().TryGetValue(shopId, out ShopData data))
            return fallback ?? I18n.RelaxValley_UI_Workbench_MissingShop();

        if (!IsShopTimeAvailable(shopId) || !IsShopDataAvailable(shopId))
            return I18n.RelaxValley_UI_Workbench_NotToday();

        if (data.Owners is not null)
        {
            foreach (ShopOwnerData owner in data.Owners)
            {
                if (!string.IsNullOrWhiteSpace(owner.ClosedMessage))
                {
                    string message = TokenParser.ParseText(owner.ClosedMessage, null, null, Game1.player);
                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                }
            }
        }

        return fallback ?? I18n.RelaxValley_UI_Workbench_Closed();
    }

    // ── 节日 / 浣熊的动态商店 ID ────────────────────────────────

    private string? ResolveFestivalShop()
    {
        if (!Game1.isFestival()) return null;
        return FindShopByKeyword("Festival");
    }

    private string? ResolveRaccoonShop() => FindShopByKeyword("Raccoon");

    /// <summary>按关键字找商店 ID，不检查当天是否可用（用于判断“是否发现过”）。</summary>
    private string? FindShopKeyByKeyword(string keyword)
    {
        foreach (var pair in GetShopData())
        {
            if (pair.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return pair.Key;
        }

        return null;
    }

    private string? FindShopByKeyword(string keyword)
    {
        foreach (var pair in GetShopData())
        {
            if (!pair.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;
            if (IsShopDataAvailable(pair.Key)) return pair.Key;
        }

        return null;
    }

    // ── 模组商店 JSON ───────────────────────────────────────────

    private List<ShopDefinition> LoadJson()
    {
        try
        {
            ShopConfig? config = _helper.Data.ReadJsonFile<ShopConfig>("assets/data/shops.json");
            List<ShopDefinition> shops = config?.Shops ?? new List<ShopDefinition>();

            foreach (ShopDefinition shop in shops)
                shop.IsFromJson = true;

            return shops;
        }
        catch (Exception ex)
        {
            _monitor.Log($"读取 assets/data/shops.json 失败：{ex.Message}", LogLevel.Warn);
            return new List<ShopDefinition>();
        }
    }

    // ── 内置条目 ────────────────────────────────────────────────

    private static List<ShopDefinition> CreateBuiltIns()
    {
        return new List<ShopDefinition>
        {
            // 建造与升级
            new() { Id = "RobinBuild",   Category = "WorkbenchBuild", Kind = ShopKind.Carpenter },
            new() { Id = "WizardBuild",  Category = "WorkbenchBuild", Kind = ShopKind.WizardBuild },
            new() { Id = "AnimalPurchase", Category = "WorkbenchBuild", Kind = ShopKind.AnimalShop },
            new() { Id = "Geode",        Category = "WorkbenchBuild", Kind = ShopKind.Geode },

            // 杂货商店
            new() { Id = "ShopPierre",    Category = "WorkbenchShopGeneral", Kind = ShopKind.Shop, ShopId = Game1.shop_generalStore },
            new() { Id = "ShopJoja",      Category = "WorkbenchShopGeneral", Kind = ShopKind.Shop, ShopId = Game1.shop_jojaMart },
            new() { Id = "ShopSandy",     Category = "WorkbenchShopGeneral", Kind = ShopKind.Shop, ShopId = Game1.shop_sandy },
            new() { Id = "ShopTraveler",  Category = "WorkbenchShopGeneral", Kind = ShopKind.Shop, ShopId = Game1.shop_travelingCart },
            new() { Id = "ShopBookseller", Category = "WorkbenchShopGeneral", Kind = ShopKind.Shop, ShopId = Game1.shop_bookseller },
            new() { Id = "ShopBooksellerTrade", Category = "WorkbenchShopGeneral", Kind = ShopKind.Shop, ShopId = Game1.shop_bookseller_trade },

            // 生活服务
            new() { Id = "ShopCarpenter", Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_carpenter },
            new() { Id = "ShopBlacksmith", Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_blacksmith },
            new() { Id = "ShopClintUpgrade", Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_blacksmithUpgrades },
            new() { Id = "ShopAnimal",    Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_animalSupplies },
            new() { Id = "ShopFish",      Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_fish },
            new() { Id = "ShopHospital",  Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_hospital },
            new() { Id = "ShopSaloon",    Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_saloon },
            new() { Id = "ShopIceCream",  Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_iceCreamStand },
            new() { Id = "ShopBoxOffice", Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_boxOffice },
            new() { Id = "ShopCatalogue", Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_furnitureCatalogue },
            new() { Id = "ShopPetAdoption", Category = "WorkbenchShopService", Kind = ShopKind.Shop, ShopId = Game1.shop_petAdoption },

            // 特殊商店
            new() { Id = "ShopAdventure", Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_adventurersGuild },
            new() { Id = "ShopRecovery",  Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_adventurersGuildItemRecovery },
            new() { Id = "ShopDesertTrade", Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_desertTrader },
            new() { Id = "ShopIslandTrade", Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_islandTrader },
            new() { Id = "ShopShadow",    Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_krobus },
            new() { Id = "ShopDwarf",     Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_dwarf },
            new() { Id = "ShopVolcano",   Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_volcanoShop },
            new() { Id = "ShopCasino",    Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_casino },
            new() { Id = "ShopQiGem",     Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_qiGemShop },
            new() { Id = "ShopHatMouse",  Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_hatMouse },
            new() { Id = "ShopResortBar", Category = "WorkbenchShopSpecial", Kind = ShopKind.Shop, ShopId = Game1.shop_resortBar },
            new() { Id = "ShopRaccoon",   Category = "WorkbenchShopSpecial", Kind = ShopKind.Raccoon }
        };
    }
}
