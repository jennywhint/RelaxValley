using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RelaxValley.Framework.UI;

public class FriendshipViewModel : INotifyPropertyChanged
{
    /// <summary>置顶村民列表存在玩家 modData 里，换存档独立、跟随存档保存。</summary>
    private const string PinnedKey = "Awassakura.RelaxValley/PinnedFriends";

    /// <summary>普通村民名字颜色（深棕黑，贴合原版羊皮纸背景）。</summary>
    private const string NormalNameColor = "#3B2A1E";

    /// <summary>可结婚对象的名字颜色（粉色）。</summary>
    private const string MarriageNameColor = "#D955A0";

    /// <summary>未认识村民的灰显颜色。</summary>
    private const string UnmetColor = "#8A8580";

    private readonly ITranslationHelper _translations;
    private readonly List<FriendItem> _allFriends;
    private readonly HashSet<string> _todaysBirthdays;
    private readonly HashSet<string> _pinned;

    /// <summary>
    /// 进度模式 = 未认识的村民不可互动；
    /// 常开模式（false）= 允许一键对话结识新村民、点击卡片直接“认识”。
    /// </summary>
    private readonly bool _progressMode;

    /// <summary>「一键对话」按钮是否可用（GMCM 开关，默认关闭）。</summary>
    private readonly bool _talkAllEnabled;

    /// <summary>左键互动菜单里是否提供「传送」选项（GMCM 开关，默认关闭）。</summary>
    private readonly bool _teleportEnabled;

    /// <summary>「批量送礼」按钮是否可用（GMCM 开关，默认关闭）。</summary>
    private readonly bool _batchGiftEnabled;

    /// <summary>生日定位模式：今日生日村民浮到列表顶部（紧跟置顶之后）。</summary>
    private bool _birthdayFirst;

    public string Title => I18n.RelaxValley_UI_Friendship_Title();

    public string SearchPlaceholder => I18n.RelaxValley_UI_Friendship_SearchPlaceholder();

    public string InteractionHint => _teleportEnabled
        ? I18n.RelaxValley_UI_Friendship_InteractionHintTeleport()
        : I18n.RelaxValley_UI_Friendship_InteractionHint();

    public string PreviewHint => I18n.RelaxValley_UI_Friendship_PreviewHint();

    public string TalkAllText => I18n.RelaxValley_UI_Friendship_TalkAll();

    /// <summary>「一键对话」按钮只在 GMCM 开关打开时显示（默认关闭）。</summary>
    public bool ShowTalkAll => _talkAllEnabled;

    public string LocateBirthdayText => I18n.RelaxValley_UI_Friendship_LocateBirthday();

    /// <summary>「批量送礼」按钮只在 GMCM 开关打开时显示（默认关闭）。</summary>
    public bool ShowBatchGift => _batchGiftEnabled;

    public string BatchGiftText => I18n.RelaxValley_UI_Friendship_BatchGift();

    public string PinHint => I18n.RelaxValley_UI_Friendship_PinHint();

    // ── 左侧悬停预览 ─────────────────────────────────────────────

    private bool _hasPreview;
    public bool HasPreview
    {
        get => _hasPreview;
        private set => Set(ref _hasPreview, value, nameof(HasPreview));
    }

    private string _previewName = "";
    public string PreviewName
    {
        get => _previewName;
        private set => Set(ref _previewName, value, nameof(PreviewName));
    }

    private string _previewPointsText = "";
    public string PreviewPointsText
    {
        get => _previewPointsText;
        private set => Set(ref _previewPointsText, value, nameof(PreviewPointsText));
    }

    private string _previewLocationText = "";
    public string PreviewLocationText
    {
        get => _previewLocationText;
        private set => Set(ref _previewLocationText, value, nameof(PreviewLocationText));
    }

    private string _previewScheduleText = "";
    public string PreviewScheduleText
    {
        get => _previewScheduleText;
        private set => Set(ref _previewScheduleText, value, nameof(PreviewScheduleText));
    }

    private Tuple<Texture2D, Rectangle>? _previewPortrait;
    public Tuple<Texture2D, Rectangle>? PreviewPortrait
    {
        get => _previewPortrait;
        private set => Set(ref _previewPortrait, value, nameof(PreviewPortrait));
    }

    private Tuple<Texture2D, Rectangle>? _previewSprite;
    public Tuple<Texture2D, Rectangle>? PreviewSprite
    {
        get => _previewSprite;
        private set => Set(ref _previewSprite, value, nameof(PreviewSprite));
    }

    private bool _previewHasPortrait;
    public bool PreviewHasPortrait
    {
        get => _previewHasPortrait;
        private set => Set(ref _previewHasPortrait, value, nameof(PreviewHasPortrait));
    }

    private bool _previewHasSprite;
    public bool PreviewHasSprite
    {
        get => _previewHasSprite;
        private set => Set(ref _previewHasSprite, value, nameof(PreviewHasSprite));
    }

    // ── 送礼口味推荐（悬停预览） ────────────────────────────────

    /// <summary>口味推荐的单个物品图标。</summary>
    public sealed class GiftIcon
    {
        public Tuple<Texture2D, Rectangle>? Sprite { get; init; }

        public bool HasSprite => Sprite is not null;
    }

    private List<GiftIcon> _individualLovedIcons = new();
    public List<GiftIcon> IndividualLovedIcons
    {
        get => _individualLovedIcons;
        private set
        {
            if (ReferenceEquals(_individualLovedIcons, value)) return;
            _individualLovedIcons = value;
            OnPropertyChanged(nameof(IndividualLovedIcons));
            OnPropertyChanged(nameof(HasIndividualLoved));
        }
    }

    private List<GiftIcon> _individualLikedIcons = new();
    public List<GiftIcon> IndividualLikedIcons
    {
        get => _individualLikedIcons;
        private set
        {
            if (ReferenceEquals(_individualLikedIcons, value)) return;
            _individualLikedIcons = value;
            OnPropertyChanged(nameof(IndividualLikedIcons));
            OnPropertyChanged(nameof(HasIndividualLiked));
        }
    }

    private List<GiftIcon> _universalLovedIcons = new();
    public List<GiftIcon> UniversalLovedIcons
    {
        get => _universalLovedIcons;
        private set
        {
            if (ReferenceEquals(_universalLovedIcons, value)) return;
            _universalLovedIcons = value;
            OnPropertyChanged(nameof(UniversalLovedIcons));
            OnPropertyChanged(nameof(HasUniversalLoved));
        }
    }

    private List<GiftIcon> _universalLikedIcons = new();
    public List<GiftIcon> UniversalLikedIcons
    {
        get => _universalLikedIcons;
        private set
        {
            if (ReferenceEquals(_universalLikedIcons, value)) return;
            _universalLikedIcons = value;
            OnPropertyChanged(nameof(UniversalLikedIcons));
            OnPropertyChanged(nameof(HasUniversalLiked));
        }
    }

    public string FavoriteLabel => I18n.RelaxValley_UI_Friendship_FavoriteLabel();

    public string LikeLabel => I18n.RelaxValley_UI_Friendship_LikeLabel();

    public string UniversalFavoriteLabel => I18n.RelaxValley_UI_Friendship_UniversalFavoriteLabel();

    public string UniversalLikeLabel => I18n.RelaxValley_UI_Friendship_UniversalLikeLabel();

    public bool HasIndividualLoved => IndividualLovedIcons.Count > 0;

    public bool HasIndividualLiked => IndividualLikedIcons.Count > 0;

    public bool HasUniversalLoved => UniversalLovedIcons.Count > 0;

    public bool HasUniversalLiked => UniversalLikedIcons.Count > 0;

    // ── 列表与筛选 ───────────────────────────────────────────────

    private List<FriendItem> _friends = new();
    public List<FriendItem> Friends
    {
        get => _friends;
        private set
        {
            if (ReferenceEquals(_friends, value)) return;
            _friends = value;
            OnPropertyChanged(nameof(Friends));
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            ApplyFilters();
        }
    }

    public List<string> SortOptions { get; } = new()
    {
        "心数（高→低）",
        "心数（低→高）",
        "名字（A→Z）",
        "本周送礼次数（高→低）",
        "今日未送礼优先"
    };

    private int _selectedSortIndex = 0;
    public int SelectedSortIndex
    {
        get => _selectedSortIndex;
        set
        {
            if (_selectedSortIndex == value) return;
            _selectedSortIndex = value;
            OnPropertyChanged(nameof(SelectedSortIndex));
            ApplyFilters();
        }
    }

    public FriendshipViewModel(
        ITranslationHelper translations,
        bool progressMode = true,
        bool talkAllEnabled = false,
        bool teleportEnabled = false,
        bool batchGiftEnabled = false)
    {
        _translations = translations;
        _progressMode = progressMode;
        _talkAllEnabled = talkAllEnabled;
        _teleportEnabled = teleportEnabled;
        _batchGiftEnabled = batchGiftEnabled;
        _todaysBirthdays = BuildTodaysBirthdays();
        _pinned = LoadPinned();
        _allFriends = BuildFriends();
        ApplyFilters();
    }

    /// <summary>今天过生日的村民名字。用原版 NPC.isBirthday() 判断，和日历一致。</summary>
    private static HashSet<string> BuildTodaysBirthdays()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Utility.ForEachVillager(npc =>
            {
                if (npc.isBirthday()) names.Add(npc.Name);
                return true;
            });
        }
        catch
        {
            // 日历 / 生日数据异常时不影响页面其它功能
        }

        return names;
    }

    // ── 置顶 ─────────────────────────────────────────────────────

    private HashSet<string> LoadPinned()
    {
        string raw = "";
        if (Game1.player is not null
            && Game1.player.modData.TryGetValue(PinnedKey, out string? value))
        {
            raw = value ?? "";
        }

        return new HashSet<string>(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    private void SavePinned()
    {
        if (Game1.player is null) return;
        Game1.player.modData[PinnedKey] = string.Join(",", _pinned);
    }

    /// <summary>右键村民卡片：置顶 / 取消置顶（可同时置顶多位）。</summary>
    public void OnFriendRightClicked(string npcName)
    {
        if (string.IsNullOrWhiteSpace(npcName)) return;
        TogglePin(npcName);
    }

    private void TogglePin(string npcName)
    {
        if (!_pinned.Remove(npcName))
            _pinned.Add(npcName);

        SavePinned();
        RefreshFriendFlags();
        Game1.playSound("smallSelect");
    }

    /// <summary>刷新卡片上的置顶 / 生日标记，不重建整个列表。</summary>
    private void RefreshFriendFlags()
    {
        foreach (FriendItem friend in _allFriends)
        {
            friend.IsPinned = _pinned.Contains(friend.NpcName);
            friend.PinText = friend.IsPinned
                ? I18n.RelaxValley_UI_Friendship_PinnedBadge()
                : "";
            friend.IsBirthday = _todaysBirthdays.Contains(friend.NpcName);
            friend.BirthdayText = friend.IsBirthday
                ? I18n.RelaxValley_UI_Friendship_BirthdayBadge()
                : "";
        }

        ApplyFilters();
    }

    // ── 数据构建 ─────────────────────────────────────────────────

    /// <summary>
    /// 从 Data/Characters 取所有社交 NPC（CanSocialize=true），
    /// 再补上已认识但不在数据里的 NPC；未认识的不从列表中移除，只灰显。
    /// </summary>
    private List<FriendItem> BuildFriends()
    {
        var result = new List<FriendItem>();
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var characterData = Game1.content.Load<Dictionary<string, CharacterData>>("Data/Characters");

        // Pairs 转成字典，避免依赖 FriendshipData 的索引器/ContainsKey 细节
        var friendships = Game1.player.friendshipData.Pairs
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in characterData)
        {
            NPC? npc = Game1.getCharacterFromName(pair.Key);
            bool canSocialize = npc?.CanSocialize
                ?? (string.IsNullOrWhiteSpace(pair.Value.CanSocialize)
                    || GameStateQuery.CheckConditions(pair.Value.CanSocialize, null, null));

            if (!canSocialize) continue;
            if (!added.Add(pair.Key)) continue;
            result.Add(BuildFriend(pair.Key, pair.Value, friendships));
        }

        // 已认识但 Data/Characters 里没有（或 CanSocialize=false）的 NPC
        foreach (var pair in friendships)
        {
            if (!added.Add(pair.Key)) continue;
            result.Add(BuildFriend(pair.Key, null, friendships));
        }

        return result;
    }

    private FriendItem BuildFriend(
        string npcName,
        CharacterData? data,
        IReadOnlyDictionary<string, Friendship> friendships)
    {
        NPC? npc = Game1.getCharacterFromName(npcName);
        bool isMet = friendships.TryGetValue(npcName, out Friendship? foundFriendship);
        Friendship? friendship = isMet ? foundFriendship : null;

        int maxHearts = npc?.isMarried() == true ? 14 : 10;
        int points = friendship?.Points ?? 0;
        int hearts = Math.Min(points / 250, maxHearts);
        int maxPoints = maxHearts * 250;
        bool giftedToday = (friendship?.GiftsToday ?? 0) != 0;
        int giftsThisWeek = friendship?.GiftsThisWeek ?? 0;

        string displayName = ResolveDisplayName(npcName, npc, data);
        string? locationName = npc?.currentLocation?.DisplayName;
        if (string.IsNullOrWhiteSpace(locationName))
            locationName = I18n.RelaxValley_UI_Friendship_Unknown();

        var schedule = GetNextSchedule(npc);

        string pointsLabel = _translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_PreviewPoints)
            .Tokens(new { points, max = maxPoints });

        string locationLabel = _translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_PreviewLocation)
            .Tokens(new { location = locationName });

        string scheduleLabel = schedule is { } s
            ? _translations
                .Get(I18n.Keys.RelaxValley_UI_Friendship_PreviewSchedule)
                .Tokens(new { time = s.Time, location = s.Location })
            : I18n.RelaxValley_UI_Friendship_NoSchedule();

        string tooltip = pointsLabel + "\n" + locationLabel + "\n" + scheduleLabel;

        string textColor = isMet ? NormalNameColor : UnmetColor;

        // 可结婚对象用粉色名字，其余普通村民用黑色（未认识的灰显）
        bool isDatable = npc?.datable.Value ?? data?.CanBeRomanced ?? false;
        string nameColor = !isMet
            ? UnmetColor
            : isDatable ? MarriageNameColor : NormalNameColor;

        bool isBirthday = _todaysBirthdays.Contains(npcName);

        string giftStatus;
        string giftColor;

        if (isMet)
        {
            giftStatus = giftedToday ? "今日已送礼" : "今日可送礼";
            giftColor = giftedToday ? "#8A8580" : "#2E7D32";
        }
        else
        {
            giftStatus = I18n.RelaxValley_UI_Friendship_Unmet();
            giftColor = UnmetColor;
            tooltip += "\n" + I18n.RelaxValley_UI_Friendship_Unmet();
        }

        bool isPinned = _pinned.Contains(npcName);

        return new FriendItem
        {
            NpcName = npcName,
            DisplayName = displayName,
            IsMet = isMet,
            IsPinned = isPinned,
            IsBirthday = isBirthday,
            PinText = isPinned ? I18n.RelaxValley_UI_Friendship_PinnedBadge() : "",
            BirthdayText = isBirthday ? I18n.RelaxValley_UI_Friendship_BirthdayBadge() : "",
            NameColor = nameColor,
            TextColor = textColor,
            CardOpacity = isMet ? 1f : 0.55f,
            Points = points,
            HeartValue = hearts,
            HeartText = $"{hearts} / {maxHearts} 心",
            GiftsThisWeek = giftsThisWeek,
            GiftsThisWeekText = $"本周送礼：{giftsThisWeek} 次",
            GiftStatus = giftStatus,
            GiftColor = giftColor,
            GiftedToday = giftedToday,
            PointsLabel = pointsLabel,
            LocationLabel = locationLabel,
            ScheduleLabel = scheduleLabel,
            TooltipText = tooltip
        };
    }

    private static string ResolveDisplayName(string npcName, NPC? npc, CharacterData? data)
    {
        string? name = npc?.displayName;
        if (string.IsNullOrWhiteSpace(name) && data is not null && !string.IsNullOrWhiteSpace(data.DisplayName))
            name = TokenParser.ParseText(data.DisplayName, null, null, Game1.player);

        return string.IsNullOrWhiteSpace(name) ? npcName : name;
    }

    /// <summary>
    /// 取当前时间之后的下一段日程，例如 (17:00, 星之酒吧)。
    /// 没有日程、或者 NPC 不在世界中被实例化时返回 null。
    /// </summary>
    private static (string Time, string Location)? GetNextSchedule(NPC? npc)
    {
        if (npc is null) return null;

        // 1.6 的日程按需加载：社交面板直接读 Schedule 会一直是空——
        // 这正是之前所有村民都显示「日程：无」的原因，必须先走原版加载入口
        try
        {
            npc.TryLoadSchedule();
        }
        catch
        {
            // 个别自定义 NPC 日程数据异常时不影响页面其它功能
        }

        if (npc.Schedule is null || npc.Schedule.Count == 0) return null;

        int now = Game1.timeOfDay;
        var next = npc.Schedule.OrderBy(p => p.Key).FirstOrDefault(p => p.Key > now);
        if (next.Value is null)
        {
            // 今日行程已全部走完：显示最后一站（即当前所在处）
            next = npc.Schedule.OrderBy(p => p.Key).Last();
        }

        string? target = next.Value.targetLocationName;
        if (string.IsNullOrWhiteSpace(target)) return null;

        string time = $"{next.Key / 100:00}:{next.Key % 100:00}";
        string location = Game1.getLocationFromName(target)?.DisplayName ?? target;
        return (time, location);
    }

    // ── 悬停预览 / 左键交互 ─────────────────────────────────────

    /// <summary>鼠标进入 NPC 卡片：左侧显示头像 + 行走图 + 详细数据。</summary>
    public void OnFriendHovered(string npcName)
    {
        var friend = _allFriends.FirstOrDefault(f => f.NpcName == npcName);
        if (friend == null) return;

        PreviewName = friend.DisplayName;
        PreviewPointsText = friend.PointsLabel;
        PreviewLocationText = friend.LocationLabel;
        PreviewScheduleText = friend.ScheduleLabel;

        var portrait = LoadPortrait(npcName);
        if (portrait != null)
        {
            PreviewPortrait = portrait;
            PreviewHasPortrait = true;
        }
        else
        {
            PreviewHasPortrait = false;
        }

        var sprite = LoadWalkSprite(npcName);
        if (sprite != null)
        {
            PreviewSprite = sprite;
            PreviewHasSprite = true;
        }
        else
        {
            PreviewHasSprite = false;
        }

        // 送礼口味推荐：个人口味与通用口味分开显示（原版 Data/NPCGiftTastes，模组改的数据自动生效）
        var prefs = GetGiftPreferences(friend.NpcName);
        IndividualLovedIcons = BuildGiftIcons(prefs.IndividualLoved);
        IndividualLikedIcons = BuildGiftIcons(prefs.IndividualLiked);
        UniversalLovedIcons = BuildGiftIcons(prefs.UniversalLoved);
        UniversalLikedIcons = BuildGiftIcons(prefs.UniversalLiked);

        HasPreview = true;
    }

    /// <summary>鼠标离开 NPC 卡片：隐藏左侧预览。</summary>
    public void OnFriendUnhovered()
    {
        HasPreview = false;
        IndividualLovedIcons = new List<GiftIcon>();
        IndividualLikedIcons = new List<GiftIcon>();
        UniversalLovedIcons = new List<GiftIcon>();
        UniversalLikedIcons = new List<GiftIcon>();
    }

    // ── 送礼口味解析 ────────────────────────────────────────────

    /// <summary>口味解析结果缓存（会话内数据固定，无需重复解析）。</summary>
    private static readonly Dictionary<string,
        (List<string> IndividualLoved, List<string> IndividualLiked, List<string> UniversalLoved, List<string> UniversalLiked)> GiftTasteCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>悬停预览里最多显示多少个图标。</summary>
    private const int MaxGiftIcons = 8;

    /// <summary>
    /// 从原版 Data/NPCGiftTastes 解析村民最爱 / 喜欢的礼物清单。
    /// 口味 = 个人条目（字段 5/7/9/11）与通用条目（Universal_Love/Like/Dislike/Hate 的同位字段）合并，
    /// 优先级与原版 <see cref="NPC.getGiftTasteForThisItem"/> 一致：
    /// 个人最爱 → 个人喜欢 → 个人不喜欢 → 个人讨厌 → 通用最爱 → 通用喜欢 → 通用不喜欢 → 通用讨厌。
    /// 模组（CP / 数据编辑）改过的内容在 Game1.NPCGiftTastes 里即为最终值，自动生效。
    /// </summary>
    private static (List<string> IndividualLoved, List<string> IndividualLiked, List<string> UniversalLoved, List<string> UniversalLiked) GetGiftPreferences(string npcName)
    {
        if (GiftTasteCache.TryGetValue(npcName, out var cached))
            return cached;

        var result = (new List<string>(), new List<string>(), new List<string>(), new List<string>());

        try
        {
            var tastes = Game1.NPCGiftTastes;
            if (tastes is not null && tastes.ContainsKey(npcName))            {
                // 有序桶：第一个命中某物品的桶决定其口味（个人覆盖通用，讨厌/不喜欢压过喜爱）
                var buckets = new (List<string> Items, bool Loved, bool Personal)[]
                {
                    (ParseGiftIds(GetTasteField(tastes, npcName, 5)), true, true),
                    (ParseGiftIds(GetTasteField(tastes, npcName, 7)), false, true),
                    (ParseGiftIds(GetTasteField(tastes, npcName, 9)), false, true),
                    (ParseGiftIds(GetTasteField(tastes, npcName, 11)), false, true),
                    (ParseGiftIds(GetTasteField(tastes, "Universal_Love", 5)), true, false),
                    (ParseGiftIds(GetTasteField(tastes, "Universal_Like", 7)), false, false),
                    (ParseGiftIds(GetTasteField(tastes, "Universal_Dislike", 9)), false, false),
                    (ParseGiftIds(GetTasteField(tastes, "Universal_Hate", 11)), false, false),
                };

                var indLoved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var indLiked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var uniLoved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var uniLiked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach ((List<string> items, bool isLoved, bool isPersonal) in buckets)
                {
                    foreach (string id in items)
                    {
                        if (!assigned.Add(id)) continue;

                        if (isPersonal)
                        {
                            if (isLoved) indLoved.Add(id); else indLiked.Add(id);
                        }
                        else
                        {
                            if (isLoved) uniLoved.Add(id); else uniLiked.Add(id);
                        }
                    }
                }

                result = (indLoved.ToList(), indLiked.ToList(), uniLoved.ToList(), uniLiked.ToList());
            }
        }
        catch
        {
            // 数据异常时只是没有推荐，不影响页面其它功能
        }

        GiftTasteCache[npcName] = result;
        return result;
    }

    /// <summary>取某条目指定下标的口味字段（条目串以 '/' 分隔，越界或缺失返回空串）。</summary>
    private static string GetTasteField(IDictionary<string, string> tastes, string key, int index)
    {
        if (!tastes.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw)) return "";

        string[] fields = raw.Split('/');
        return fields.Length > index ? fields[index] : "";
    }

    /// <summary>类别 ID → 该类别下的物品 ID（qualified）。首次使用时从 objectData 构建，模组物品自动包含。</summary>
    private static Dictionary<int, List<string>>? _categoryItems;

    private static List<string> GetCategoryItems(int category)
    {
        if (_categoryItems is null)
        {
            var map = new Dictionary<int, List<string>>();
            foreach (var pair in Game1.objectData)
            {
                if (pair.Value.Category is int cat)
                {
                    if (!map.TryGetValue(cat, out List<string> list))
                        map[cat] = list = new List<string>();
                    list.Add("(O)" + pair.Key);
                }
            }
            _categoryItems = map;
        }

        return _categoryItems.TryGetValue(category, out List<string>? items)
            ? items
            : new List<string>();
    }

    /// <summary>
    /// 规范化口味字段里的物品 ID：兼容裸数字（"80"）、旧格式（"O 80"）
    /// 与 1.6 的限定格式（"(O)80"）。
    /// 负数是类别引用（如 -2 = 所有宝石、-80 = 所有花）：原版通用口味大量使用类别，
    /// 直接跳过会让「宝石是通用喜欢」这类口味完全消失——这里把类别展开成
    /// 该类别下的具体物品（按数据顺序，模组加的新物品自动包含），再参与口味判定。
    /// </summary>
    private static List<string> ParseGiftIds(string raw)
    {
        var ids = new List<string>();

        foreach (string part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string id = part.Trim();
            if (id.Length == 0) continue;

            if (id.StartsWith('-') && int.TryParse(id, out int category))
            {
                ids.AddRange(GetCategoryItems(-category));
                continue;
            }

            if (id.StartsWith("(O)", StringComparison.Ordinal))
            {
                ids.Add(id);
            }
            else if (id.StartsWith("O ", StringComparison.OrdinalIgnoreCase))
            {
                ids.Add("(O)" + id.Substring(2).Trim());
            }
            else if (id.All(char.IsDigit))
            {
                ids.Add("(O)" + id);
            }
        }

        return ids;
    }

    /// <summary>把物品 ID 转成预览图标；未知物品（模组未加载等）直接跳过。</summary>
    private static List<GiftIcon> BuildGiftIcons(List<string> ids)
    {
        var icons = new List<GiftIcon>();

        foreach (string id in ids)
        {
            if (icons.Count >= MaxGiftIcons) break;

            var data = ItemRegistry.GetData(id);
            if (data is null) continue;

            icons.Add(new GiftIcon { Sprite = Tuple.Create(data.GetTexture(), data.GetSourceRect()) });
        }

        return icons;
    }

    /// <summary>
    /// 左键卡片：已认识 → 打开互动菜单（聊天 / 送礼 / 传送）；
    /// 未认识 → 只提示，不打开菜单。
    /// </summary>
    public void OnFriendClicked(string npcName)
    {
        var friend = _allFriends.FirstOrDefault(f => f.NpcName == npcName);
        if (friend == null) return;

        if (!friend.IsMet)
        {
            // 进度模式：未认识不可互动；常开模式：点击即“认识”
            if (_progressMode)
            {
                Game1.showGlobalMessage(_translations
                    .Get(I18n.Keys.RelaxValley_UI_Friendship_NotMet)
                    .Tokens(new { name = friend.DisplayName }));
                return;
            }

            MeetNpc(friend);
            return;
        }

        ShowInteractionMenu(npcName);
    }

    /// <summary>常开模式：结识村民并按原版规则获得当日交谈好感度。</summary>
    private void MeetNpc(FriendItem friend)
    {
        NPC? npc = Game1.getCharacterFromName(friend.NpcName);
        if (npc == null)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_NotFound());
            return;
        }

        if (!npc.CanSocialize)
        {
            Game1.showGlobalMessage(_translations
                .Get(I18n.Keys.RelaxValley_UI_Friendship_CannotSocialize)
                .Tokens(new { name = friend.DisplayName }));
            return;
        }

        if (!Game1.player.friendshipData.ContainsKey(friend.NpcName))
            Game1.player.friendshipData[friend.NpcName] = new Friendship();

        // 原版 grantConversationFriendship：当日交谈标记 + 好感度（雕像祝福等加成由游戏处理）
        npc.grantConversationFriendship(Game1.player);
        Game1.playSound("bigSelect");

        Game1.showGlobalMessage(_translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_Met)
            .Tokens(new { name = friend.DisplayName }));

        _allFriends.Clear();
        _allFriends.AddRange(BuildFriends());
        ApplyFilters();
    }

    private void ShowInteractionMenu(string npcName)
    {
        var npc = Game1.getCharacterFromName(npcName);
        var location = Game1.currentLocation;
        if (npc == null)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_NotFound());
            return;
        }
        if (location == null) return;

        string question = _translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_InteractionTitle)
            .Tokens(new { name = npc.displayName });

        // 「传送」是 GMCM 可关的附加功能（默认关闭），关闭时不出现在选项里
        var responses = new List<Response>
        {
            new("Talk", I18n.RelaxValley_UI_Friendship_Talk()),
            new("Gift", I18n.RelaxValley_UI_Friendship_Gift())
        };
        if (_teleportEnabled)
            responses.Add(new Response("Teleport", I18n.RelaxValley_UI_Friendship_Teleport()));
        responses.Add(new Response("Cancel", I18n.RelaxValley_UI_Friendship_Cancel()));

        location.createQuestionDialogue(
            question,
            responses.ToArray(),
            (_, answer) =>
            {
                switch (answer)
                {
                    case "Talk":
                        TalkTo(npc);
                        break;
                    case "Gift":
                        GiftTo(npc);
                        break;
                    case "Teleport":
                        TeleportTo(npcName, waitForMenuClose: true);
                        break;
                }
            });
    }

    /// <summary>
    /// 聊天：模拟玩家空手在 NPC 面前按互动键，交给原版 <see cref="NPC.checkAction"/> 处理。
    /// 原版规则下手中拿着物品时互动会变成送礼，所以这里要求空手。
    /// </summary>
    private void TalkTo(NPC npc)
    {
        if (Game1.player.ActiveObject != null)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_TalkNeedsEmptyHands());
            return;
        }

        RunVanillaInteraction(npc);
    }

    /// <summary>
    /// 送礼：同样走原版 <see cref="NPC.checkAction"/>，由游戏本体处理礼物喜好、
    /// 好感度、每周/每日送礼限制、对话反应与物品扣除。
    /// </summary>
    private void GiftTo(NPC npc)
    {
        if (Game1.player.ActiveObject == null)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_GiftNeedsItem());
            return;
        }

        RunVanillaInteraction(npc);
    }

    private static void RunVanillaInteraction(NPC npc)
    {
        var location = npc.currentLocation ?? Game1.currentLocation;
        if (location == null) return;

        // 问题对话框的点击回调还在执行时不能直接打开新对话，
        // 否则会被旧对话框的收尾流程覆盖；排到玩家空闲的下一帧再执行。
        ModEntry.RunWhenPlayerFree(() =>
        {
            try
            {
                npc.checkAction(Game1.player, location);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"村民互动失败：{ex}", LogLevel.Error);
                Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_InteractionFailed());
            }
        });
    }

    private void TeleportTo(string npcName, bool waitForMenuClose = false)
    {
        var friend = _allFriends.FirstOrDefault(f => f.NpcName == npcName);
        if (friend == null) return;

        if (waitForMenuClose)
        {
            // 从问题对话框里选“传送”时，等对话框完全关闭后再传送
            ModEntry.RunWhenPlayerFree(friend.Teleport);
        }
        else
        {
            friend.Teleport();
        }
    }

    // ── 头像 / 行走图 ────────────────────────────────────────────

    private static Tuple<Texture2D, Rectangle>? LoadPortrait(string npcName)
    {
        Texture2D? texture = null;
        try
        {
            var npc = Game1.getCharacterFromName(npcName);

            // 立绘只会在 NPC 创建时加载一次；换季 / 节日 / 岛装等外观模组
            // 需要重新走一遍 ChooseAppearance 才能拿到当前实际显示的样子。
            npc?.ChooseAppearance();

            texture = npc?.Portrait;
        }
        catch
        {
            // 自定义 NPC 的肖像路径加载失败时走下面的默认路径
        }

        texture ??= TryLoadTexture($"Portraits/{npcName}");
        return texture is null
            ? null
            : Tuple.Create(texture, new Rectangle(0, 0, 64, 64));
    }

    private static Tuple<Texture2D, Rectangle>? LoadWalkSprite(string npcName)
    {
        Texture2D? texture = null;
        Rectangle sourceRect = new(0, 0, 16, 32);

        try
        {
            NPC? npc = Game1.getCharacterFromName(npcName);
            if (npc is not null && npc.Sprite is not null)
            {
                // 与立绘同理：刷新外观后再读取行走图
                npc.ChooseAppearance();

                texture = npc.Sprite.Texture;
                sourceRect = npc.Sprite.SourceRect;
            }
        }
        catch
        {
            // 自定义 NPC 的行走图加载失败时走下面的默认路径
        }

        if (texture is null)
        {
            texture = TryLoadTexture($"Characters/{npcName}");
            sourceRect = new Rectangle(0, 0, 16, 32);
        }

        return texture is null
            ? null
            : Tuple.Create(texture, sourceRect);
    }

    private static Texture2D? TryLoadTexture(string assetName)
    {
        try
        {
            return Game1.content.Load<Texture2D>(assetName);
        }
        catch
        {
            return null;
        }
    }

    // ── 筛选 / 排序 ─────────────────────────────────────────────

    private void ApplyFilters()
    {
        IEnumerable<FriendItem> filtered = _allFriends;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string q = SearchText.Trim().ToLower();
            filtered = filtered.Where(f => f.DisplayName.ToLower().Contains(q));
        }

        filtered = SelectedSortIndex switch
        {
            // 好感度排序使用精确点数，而不是整数心数
            0 => filtered.OrderByDescending(f => f.Points),
            1 => filtered.OrderBy(f => f.Points),
            2 => filtered.OrderBy(f => f.DisplayName),
            3 => filtered.OrderByDescending(f => f.GiftsThisWeek),
            4 => filtered.OrderBy(f => f.GiftedToday).ThenByDescending(f => f.Points),
            _ => filtered.OrderByDescending(f => f.Points)
        };

        // 置顶村民永远排在最前；生日定位模式下今日生日者紧随其后，其余保持所选排序
        IOrderedEnumerable<FriendItem> ordered = filtered.OrderByDescending(f => f.IsPinned);
        if (_birthdayFirst)
            ordered = ordered.ThenByDescending(f => f.IsBirthday);

        Friends = ordered.ToList();
    }

    // ── 一键对话 / 定位今日生日 ─────────────────────────────────

    /// <summary>
    /// 一键对话：对所有已认识、且今天还没聊过的村民调用原版
    /// <see cref="NPC.grantConversationFriendship"/>，
    /// 好感度、任务通知、雕像祝福加成等全部走游戏本体规则，不弹对话。
    /// </summary>
    public void TalkAll()
    {
        int count = 0;

        foreach (FriendItem friend in _allFriends)
        {
            // 进度模式：只对已认识的村民生效；常开模式：顺带结识新村民
            if (!friend.IsMet && _progressMode) continue;

            NPC? npc = Game1.getCharacterFromName(friend.NpcName);
            if (npc is null || !npc.CanSocialize) continue;

            if (!Game1.player.friendshipData.TryGetValue(friend.NpcName, out Friendship? friendship))
            {
                if (friend.IsMet) continue;

                // 常开模式：先建立结识记录，再走原版交谈好感度
                Game1.player.friendshipData[friend.NpcName] = new Friendship();
                friendship = Game1.player.friendshipData[friend.NpcName];
            }

            if (friendship.TalkedToToday) continue;

            npc.grantConversationFriendship(Game1.player);
            count++;
        }

        Game1.playSound(count > 0 ? "brush" : "cancel");
        Game1.showGlobalMessage(count > 0
            ? _translations.Get(I18n.Keys.RelaxValley_UI_Friendship_TalkAllResult)
                .Tokens(new { count })
            : I18n.RelaxValley_UI_Friendship_TalkAllNone());

        _allFriends.Clear();
        _allFriends.AddRange(BuildFriends());
        ApplyFilters();
    }

    /// <summary>
    /// 定位今日生日的村民：把今日生日者浮到列表顶部（紧跟置顶之后）并滚动到顶，
    /// 再点一次恢复默认排序。不传送、不弹对话框——问题对话框会把当前页面整个顶掉，
    /// 列表定位必须在页面保持打开的情况下完成。
    /// </summary>
    public void LocateBirthdayNpc()
    {
        _birthdayFirst = !_birthdayFirst;
        ApplyFilters();

        Game1.playSound(_birthdayFirst ? "bigSelect" : "bigDeSelect");
        Game1.showGlobalMessage(_birthdayFirst
            ? I18n.RelaxValley_UI_Friendship_LocateActive()
            : I18n.RelaxValley_UI_Friendship_LocateRestored());
    }

    // ── 批量送礼 ────────────────────────────────────────────────

    /// <summary>
    /// 会改变关系走向的物品，绝不批量赠送：花束（开始约会）、
    /// 枯萎花束（分手）、人鱼吊坠（求婚）。
    /// </summary>
    private static readonly HashSet<string> RelationshipItemIds =
        new(StringComparer.OrdinalIgnoreCase) { "458", "791", "858" };

    /// <summary>
    /// 批量送礼：把手持物品按原版送礼规则各送一份给今日未送礼、未超每周上限的村民。
    /// 资格判定逐条对照 <see cref="NPC.tryToReceiveActiveObject"/> 的原版分支（含 probe 语义）；
    /// 实际赠送直接走原版方法，好感度 / 礼物口味 / 每日每周计数 / 物品扣除全部由游戏本体处理，
    /// 只把逐个弹出的反应对话静默关掉。
    /// </summary>
    public void BatchGift()
    {
        StardewValley.Object? held = Game1.player.ActiveObject;
        if (held is null)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_GiftNeedsItem());
            return;
        }

        if (RelationshipItemIds.Contains(held.ItemId))
        {
            Game1.showGlobalMessage(_translations
                .Get(I18n.Keys.RelaxValley_UI_Friendship_BatchGiftForbidden)
                .Tokens(new { item = held.DisplayName }));
            return;
        }

        var targets = new List<NPC>();
        foreach (string npcName in Game1.player.friendshipData.Keys)
        {
            NPC? npc = Game1.getCharacterFromName(npcName);
            if (npc is null || npc.IsInvisible) continue;
            if (!CanBatchGift(npc, held)) continue;
            targets.Add(npc);
        }

        if (targets.Count == 0)
        {
            Game1.playSound("cancel");
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_BatchGiftNone());
            return;
        }

        int count = Math.Min(targets.Count, held.Stack);
        var location = Game1.currentLocation;
        if (location is null) return;

        string question = _translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_BatchGiftConfirm)
            .Tokens(new { item = held.DisplayName, count });

        location.createQuestionDialogue(
            question,
            new[]
            {
                new Response("Confirm", I18n.RelaxValley_UI_Friendship_BatchGiftConfirmOk()),
                new Response("Cancel", I18n.RelaxValley_UI_Friendship_Cancel())
            },
            (_, answer) =>
            {
                if (answer != "Confirm") return;
                ModEntry.RunWhenPlayerFree(() => ExecuteBatchGift(targets));
            });
    }

    /// <summary>按原版 tryToReceiveActiveObject 的礼品分支逐条判断资格（无副作用）。</summary>
    private static bool CanBatchGift(NPC npc, Item held)
    {
        if (!npc.CanReceiveGifts()) return false;
        if (!held.canBeGivenAsGift()) return false;
        if (ItemContextTagManager.HasBaseTag(held.QualifiedItemId, "not_giftable")) return false;

        if (!Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship)) return false;
        if (friendship.IsDivorced()) return false;
        if (friendship.GiftsToday >= 1) return false;

        // 每周 2 次上限；配偶 / 孩子 / 生日当天不受限（与原版一致）
        bool weeklyOk = friendship.GiftsThisWeek < 2
            || Game1.player.spouse == npc.Name
            || npc is StardewValley.Characters.Child
            || npc.isBirthday();
        if (!weeklyOk) return false;

        if (Game1.IsGreenRainingHere() && Game1.year == 1 && !npc.isMarried()) return false;

        // 被甩状态拒收（原版：activeDialogueEvents 含 dumped 且有对应对话）
        foreach (string key in Game1.player.activeDialogueEvents.Keys)
        {
            if (key.Contains("dumped", StringComparison.Ordinal) && npc.Dialogue.ContainsKey(key))
                return false;
        }

        return true;
    }

    private void ExecuteBatchGift(List<NPC> targets)
    {
        var player = Game1.player;
        string itemName = player.ActiveObject?.DisplayName ?? "?";
        int gifted = 0;

        foreach (NPC npc in targets)
        {
            if (player.ActiveObject is null) break;   // 原版把背包扣完自然停止

            if (!player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship)) continue;
            if (!CanBatchGift(npc, player.ActiveObject)) continue;
            int before = friendship.GiftsToday;

            npc.tryToReceiveActiveObject(player);

            // 反应对话立即静默关掉：好感度、口味判定、物品扣除已由原版入账
            if (Game1.activeClickableMenu is DialogueBox box)
                box.exitThisMenu(false);

            if (player.friendshipData.TryGetValue(npc.Name, out Friendship? after) && after.GiftsToday > before)
                gifted++;
        }

        Game1.playSound(gifted > 0 ? "give_gift" : "cancel");
        Game1.showGlobalMessage(_translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_BatchGiftDone)
            .Tokens(new { count = gifted, item = itemName }));

        _allFriends.Clear();
        _allFriends.AddRange(BuildFriends());
        ApplyFilters();
    }

    // ── INPC ────────────────────────────────────────────────────

    private void Set<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class FriendItem
{
    public string NpcName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsMet { get; set; }

    /// <summary>是否被玩家置顶。</summary>
    public bool IsPinned { get; set; }

    /// <summary>今天是否是此村民的生日。</summary>
    public bool IsBirthday { get; set; }

    public string PinText { get; set; } = "";
    public string BirthdayText { get; set; } = "";

    public string NameColor { get; set; } = "#FFFFFF";
    public string TextColor { get; set; } = "#FFFFFF";
    public float CardOpacity { get; set; } = 1f;

    /// <summary>精确好感度点数（250 点 = 1 心），用于排序。</summary>
    public int Points { get; set; }

    public int HeartValue { get; set; }
    public string HeartText { get; set; } = "";
    public int GiftsThisWeek { get; set; }
    public string GiftsThisWeekText { get; set; } = "";
    public string GiftStatus { get; set; } = "";
    public string GiftColor { get; set; } = "#FFFFFF";
    public bool GiftedToday { get; set; }

    public string PointsLabel { get; set; } = "";
    public string LocationLabel { get; set; } = "";
    public string ScheduleLabel { get; set; } = "";
    public string TooltipText { get; set; } = "";

    /// <summary>
    /// 传送到该村民身旁。
    /// 原版 API：<c>Game1.warpFarmer(locationName, tileX, tileY, isStructure)</c>
    /// </summary>
    public void Teleport()
    {
        var npc = Game1.getCharacterFromName(NpcName);
        if (npc == null)
        {
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_NotFound());
            return;
        }

        var location = npc.currentLocation;
        if (location == null)
        {
            Game1.showGlobalMessage(Format(I18n.Keys.RelaxValley_UI_Friendship_NoLocation, new { name = npc.displayName }));
            return;
        }

        // 传送到 NPC 旁边 1 格位置，避免和 NPC 重叠
        int targetX = npc.TilePoint.X;
        int targetY = npc.TilePoint.Y + 1;

        try
        {
            // 传送由游戏自己处理过场；这里不再手动 exitActiveMenu，
            // 避免在 StarML 点击事件尚未派发完时销毁当前视图。
            Game1.warpFarmer(location.NameOrUniqueName, targetX, targetY, false);
            Game1.playSound("wand");
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"传送到 {NpcName} 失败：{ex.Message}", LogLevel.Warn);
            Game1.showGlobalMessage(Format(I18n.Keys.RelaxValley_UI_Friendship_TeleportFailed, new { name = npc.displayName }));
        }
    }

    private static string Format(string key, object tokens) =>
        ModEntry.Instance.Helper.Translation.Get(key).Tokens(tokens);
}
