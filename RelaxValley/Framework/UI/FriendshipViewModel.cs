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

    public string Title => I18n.RelaxValley_UI_Friendship_Title();

    public string SearchPlaceholder => I18n.RelaxValley_UI_Friendship_SearchPlaceholder();

    public string InteractionHint => I18n.RelaxValley_UI_Friendship_InteractionHint();

    public string PreviewHint => I18n.RelaxValley_UI_Friendship_PreviewHint();

    public string TalkAllText => I18n.RelaxValley_UI_Friendship_TalkAll();

    public string LocateBirthdayText => I18n.RelaxValley_UI_Friendship_LocateBirthday();

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

    public FriendshipViewModel(ITranslationHelper translations, bool progressMode = true)
    {
        _translations = translations;
        _progressMode = progressMode;
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
        bool isMet = friendships.TryGetValue(npcName, out Friendship foundFriendship);
        Friendship? friendship = isMet ? foundFriendship : null;

        int maxHearts = npc?.isMarried() == true ? 14 : 10;
        int points = friendship?.Points ?? 0;
        int hearts = Math.Min(points / 250, maxHearts);
        int maxPoints = maxHearts * 250;
        bool giftedToday = (friendship?.GiftsToday ?? 0) != 0;
        int giftsThisWeek = friendship?.GiftsThisWeek ?? 0;

        string displayName = ResolveDisplayName(npcName, npc, data);
        string locationName = npc?.currentLocation?.DisplayName;
        if (string.IsNullOrWhiteSpace(locationName))
            locationName = I18n.RelaxValley_UI_Friendship_Unknown();

        var schedule = GetNextSchedule(npc);

        string pointsLabel = _translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_PreviewPoints)
            .Tokens(new { points, max = maxPoints })
            .ToString();

        string locationLabel = _translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_PreviewLocation)
            .Tokens(new { location = locationName })
            .ToString();

        string scheduleLabel = schedule is { } s
            ? _translations
                .Get(I18n.Keys.RelaxValley_UI_Friendship_PreviewSchedule)
                .Tokens(new { time = s.Time, location = s.Location })
                .ToString()
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
        if (npc is null || npc.Schedule is null || npc.Schedule.Count == 0)
            return null;

        int now = Game1.timeOfDay;
        var next = npc.Schedule.OrderBy(p => p.Key).FirstOrDefault(p => p.Key > now);
        if (next.Value is null)
            return null;

        string? target = GetScheduleTargetLocation(next.Value);
        if (string.IsNullOrWhiteSpace(target))
            return null;

        string time = $"{next.Key / 100:00}:{next.Key % 100:00}";
        string location = Game1.getLocationFromName(target)?.DisplayName ?? target;
        return (time, location);
    }

    /// <summary>
    /// 1.6 里 SchedulePathDescription 的目标地点字段不同版本命名不一致
    /// （targetLocation / TargetLocation），这里兼容两种写法，避免版本差异导致编译失败。
    /// </summary>
    private static string? GetScheduleTargetLocation(object scheduleDescription)
    {
        try
        {
            var type = scheduleDescription.GetType();
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var field = type.GetField("TargetLocation", Flags) ?? type.GetField("targetLocation", Flags);
            if (field?.GetValue(scheduleDescription) is string fieldValue)
                return fieldValue;

            var property = type.GetProperty("TargetLocation", Flags) ?? type.GetProperty("targetLocation", Flags);
            return property?.GetValue(scheduleDescription) as string;
        }
        catch
        {
            return null;
        }
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

        HasPreview = true;
    }

    /// <summary>鼠标离开 NPC 卡片：隐藏左侧预览。</summary>
    public void OnFriendUnhovered()
    {
        HasPreview = false;
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
                    .Tokens(new { name = friend.DisplayName })
                    .ToString());
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
                .Tokens(new { name = friend.DisplayName })
                .ToString());
            return;
        }

        if (!Game1.player.friendshipData.ContainsKey(friend.NpcName))
            Game1.player.friendshipData[friend.NpcName] = new Friendship();

        // 原版 grantConversationFriendship：当日交谈标记 + 好感度（雕像祝福等加成由游戏处理）
        npc.grantConversationFriendship(Game1.player);
        Game1.playSound("bigSelect");

        Game1.showGlobalMessage(_translations
            .Get(I18n.Keys.RelaxValley_UI_Friendship_Met)
            .Tokens(new { name = friend.DisplayName })
            .ToString());

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
            .Tokens(new { name = npc.displayName })
            .ToString();

        location.createQuestionDialogue(
            question,
            new[]
            {
                new Response("Talk", I18n.RelaxValley_UI_Friendship_Talk()),
                new Response("Gift", I18n.RelaxValley_UI_Friendship_Gift()),
                new Response("Teleport", I18n.RelaxValley_UI_Friendship_Teleport()),
                new Response("Cancel", I18n.RelaxValley_UI_Friendship_Cancel())
            },
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
            var npc = Game1.getCharacterFromName(npcName);
            if (npc?.Sprite != null)
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

        // 置顶村民永远排在最前，其余保持所选排序
        Friends = filtered.OrderByDescending(f => f.IsPinned).ToList();
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

            if (!Game1.player.friendshipData.TryGetValue(friend.NpcName, out Friendship friendship))
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
                .ToString()
            : I18n.RelaxValley_UI_Friendship_TalkAllNone());

        _allFriends.Clear();
        _allFriends.AddRange(BuildFriends());
        ApplyFilters();
    }

    /// <summary>定位今日生日的村民：只有一位时直接传送，多位时让玩家选。</summary>
    public void LocateBirthdayNpc()
    {
        List<FriendItem> birthdayFriends = _allFriends
            .Where(f => f.IsBirthday && f.IsMet)
            .ToList();

        if (birthdayFriends.Count == 0)
        {
            Game1.playSound("cancel");
            Game1.showGlobalMessage(I18n.RelaxValley_UI_Friendship_NoBirthday());
            return;
        }

        if (birthdayFriends.Count == 1)
        {
            birthdayFriends[0].Teleport();
            return;
        }

        var location = Game1.currentLocation;
        if (location is null) return;

        location.createQuestionDialogue(
            I18n.RelaxValley_UI_Friendship_BirthdayChoose(),
            birthdayFriends
                .Select(f => new Response(f.NpcName, f.DisplayName))
                .Append(new Response("Cancel", I18n.RelaxValley_UI_Friendship_Cancel()))
                .ToArray(),
            (_, answer) =>
            {
                FriendItem? target = birthdayFriends
                    .FirstOrDefault(f => string.Equals(f.NpcName, answer, StringComparison.OrdinalIgnoreCase));
                if (target is null) return;

                ModEntry.RunWhenPlayerFree(target.Teleport);
            });
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
        ModEntry.Instance.Helper.Translation.Get(key).Tokens(tokens).ToString();
}
