using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using StardewValley;

namespace RelaxValley.Framework.UI;

public class FriendshipViewModel : INotifyPropertyChanged
{
    public string Title => "村民友谊";

    private readonly List<FriendItem> _allFriends;

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

    // 绑定 dropdown 的 selected-index，而不是 selected-option-text
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

    public FriendshipViewModel()
    {
        _allFriends = BuildFriends();
        ApplyFilters();
    }

    private List<FriendItem> BuildFriends()
    {
        // ... 这部分和你之前完全一样，不用改 ...
        var result = new List<FriendItem>();
        foreach (var pair in Game1.player.friendshipData.Pairs)
        {
            string npcName = pair.Key;
            var friendship = pair.Value;
            NPC? npc = Game1.getCharacterFromName(npcName);
            if (npc == null) continue;

            int hearts = friendship.Points / 250;
            int maxHearts = npc.isMarried() ? 14 : 10;
            int displayHearts = System.Math.Min(hearts, maxHearts);
            bool giftedToday = friendship.GiftsToday != 0;

            result.Add(new FriendItem
            {
                DisplayName = string.IsNullOrEmpty(npc.displayName) ? npcName : npc.displayName,
                HeartValue = displayHearts,
                HeartText = $"{displayHearts} / {maxHearts} 心",
                GiftsThisWeek = friendship.GiftsThisWeek,
                GiftsThisWeekText = $"本周送礼：{friendship.GiftsThisWeek} 次",
                GiftStatus = giftedToday ? "今日已送礼" : "今日可送礼",
                GiftColor = giftedToday ? "#999999" : "#50B450",
                GiftedToday = giftedToday
            });
        }
        return result;
    }

    private void ApplyFilters()
    {
        IEnumerable<FriendItem> filtered = _allFriends;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string q = SearchText.Trim().ToLower();
            filtered = filtered.Where(f => f.DisplayName.ToLower().Contains(q));
        }

        // 根据 SelectedSortIndex 决定排序
        filtered = SelectedSortIndex switch
        {
            0 => filtered.OrderByDescending(f => f.HeartValue),
            1 => filtered.OrderBy(f => f.HeartValue),
            2 => filtered.OrderBy(f => f.DisplayName),
            3 => filtered.OrderByDescending(f => f.GiftsThisWeek),
            4 => filtered.OrderBy(f => f.GiftedToday).ThenByDescending(f => f.HeartValue),
            _ => filtered.OrderByDescending(f => f.HeartValue)
        };

        Friends = filtered.ToList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class FriendItem
{
    public string DisplayName { get; set; } = "";
    public int HeartValue { get; set; }
    public string HeartText { get; set; } = "";
    public int GiftsThisWeek { get; set; }
    public string GiftsThisWeekText { get; set; } = "";
    public string GiftStatus { get; set; } = "";
    public string GiftColor { get; set; } = "#FFFFFF";
    public bool GiftedToday { get; set; }
}