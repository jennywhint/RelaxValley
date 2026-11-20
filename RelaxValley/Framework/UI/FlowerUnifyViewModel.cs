using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RelaxValley.Framework.UI;

/// <summary>统一花色中的一个花朵种类。</summary>
public sealed class FlowerTile
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string CountText { get; init; } = "";
    public Tuple<Texture2D, Rectangle>? Sprite { get; init; }
    public bool HasSprite => Sprite is not null;
}

/// <summary>
/// 统一花色：扫描背包与所有存储容器里的花，选一种作为目标后全部转换。
/// </summary>
public sealed class FlowerUnifyViewModel : INotifyPropertyChanged, ICloseAware
{
    private readonly ITranslationHelper _translation;
    private readonly IMonitor _monitor;
    private Action? _closeSelf;

    public FlowerUnifyViewModel(ITranslationHelper translation, IMonitor monitor)
    {
        _translation = translation;
        _monitor = monitor;

        Title = I18n.RelaxValley_UI_FlowerUnify_Title();
        Hint = I18n.RelaxValley_UI_FlowerUnify_Hint();
        EmptyText = I18n.RelaxValley_UI_FlowerUnify_Empty();

        Flowers = ScanFlowers();
    }

    public string Title { get; }

    public string Hint { get; }

    public string EmptyText { get; }

    public bool HasFlowers => Flowers.Count > 0;

    public List<FlowerTile> Flowers { get; }

    public void AttachClose(Action closeSelf) => _closeSelf = closeSelf;

    // ── 扫描 / 转换 ─────────────────────────────────────────────

    private sealed class ContainerSnapshot
    {
        /// <summary>容器引用；null 表示玩家背包。</summary>
        public Chest? Chest;
        public StorageFurniture? Furniture;
        public bool IsPlayerInventory;

        public IList<Item> Items
        {
            get
            {
                if (IsPlayerInventory) return Game1.player.Items;
                if (Chest is not null) return Chest.Items;
                return Furniture!.heldItems;
            }
        }
    }

    private List<FlowerTile> ScanFlowers()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var samples = new Dictionary<string, StardewValley.Object>(StringComparer.OrdinalIgnoreCase);

        foreach (ContainerSnapshot container in GetContainers())
        {
            foreach (Item? item in container.Items)
            {
                if (item is not StardewValley.Object obj) continue;
                if (obj.Category != StardewValley.Object.flowersCategory) continue;

                counts[obj.ItemId] = counts.GetValueOrDefault(obj.ItemId) + obj.Stack;
                samples.TryAdd(obj.ItemId, obj);
            }
        }

        var tiles = new List<FlowerTile>();
        foreach (var pair in counts.OrderByDescending(p => p.Value))
        {
            tiles.Add(new FlowerTile
            {
                Id = pair.Key,
                Name = samples[pair.Key].DisplayName,
                CountText = $"×{pair.Value}",
                Sprite = TryBuildSprite(samples[pair.Key])
            });
        }

        return tiles;
    }

    private Tuple<Texture2D, Rectangle>? TryBuildSprite(StardewValley.Object obj)
    {
        try
        {
            if (obj.ParentSheetIndex < 0) return null;

            Rectangle source = Game1.getSourceRectForStandardTileSheet(
                Game1.objectSpriteSheet, obj.ParentSheetIndex, 16, 16);

            return Tuple.Create(Game1.objectSpriteSheet, source);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 收集所有可以安全操作的存储容器。
    /// 明确排除会“出货”的箱子（内容会被卖掉），以及被模组扩容/接管的容器
    /// （条目数超过原版上限 36 的箱子几乎都是无限容量类模组管理的，
    /// 直接改写它们的列表可能破坏模组自己的状态，导致物品丢失）。
    /// </summary>
    private IEnumerable<ContainerSnapshot> GetContainers()
    {
        yield return new ContainerSnapshot { IsPlayerInventory = true };

        foreach (GameLocation location in Game1.locations)
        {
            foreach (StardewValley.Object obj in location.objects.Values)
            {
                if (obj is not Chest chest) continue;

                // 出货类 / 自动化类箱子：内容不属于普通存储，不参与统一
                // （原版出货箱是建筑 Buildings.ShippingBin，不会出现在 objects 里；
                //  这里排除的是迷你出货箱、自动采集器、富集器）
                if (chest.SpecialChestType is Chest.SpecialChestTypes.MiniShippingBin
                    or Chest.SpecialChestTypes.AutoLoader
                    or Chest.SpecialChestTypes.Enricher)
                {
                    continue;
                }

                // 超过原版容量上限 = 被无限容量类模组接管，跳过
                if (chest.Items.Count > Chest.capacity)
                {
                    _skippedContainers++;
                    continue;
                }

                yield return new ContainerSnapshot { Chest = chest };
            }

            foreach (Furniture furniture in location.furniture)
            {
                if (furniture is StorageFurniture storage)
                {
                    if (storage.heldItems.Count > Chest.capacity)
                    {
                        _skippedContainers++;
                        continue;
                    }

                    yield return new ContainerSnapshot { Furniture = storage };
                }
            }
        }
    }

    private int _skippedContainers;

    public void OnFlowerClicked(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;

        FlowerTile? target = Flowers.FirstOrDefault(f =>
            string.Equals(f.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (target is null) return;

        int converted = 0;
        _skippedContainers = 0;

        foreach (ContainerSnapshot snapshot in GetContainers())
        {
            converted += ConvertContainer(snapshot, target.Id);
        }

        Game1.playSound(converted > 0 ? "purchase" : "cancel");

        string message = converted > 0
            ? _translation
                .Get(I18n.Keys.RelaxValley_UI_FlowerUnify_Done)
                .Tokens(new { count = converted, name = target.Name })
                .ToString()
            : I18n.RelaxValley_UI_FlowerUnify_None();

        if (_skippedContainers > 0)
        {
            message += "\n" + _translation
                .Get(I18n.Keys.RelaxValley_UI_FlowerUnify_Skipped)
                .Tokens(new { count = _skippedContainers })
                .ToString();
        }

        Game1.showGlobalMessage(message);
        _closeSelf?.Invoke();
    }

    /// <summary>
    /// 在单个容器内做统一转换。
    /// 每次写回前都校验“该槽位仍然是我们扫描时的那个物品实例”，
    /// 任何不一致都立即放弃整个容器，绝不盲目按索引覆盖。
    /// </summary>
    private int ConvertContainer(ContainerSnapshot snapshot, string targetItemId)
    {
        IList<Item> items = snapshot.Items;

        // 快照：记录扫描时每个槽位的物品引用
        Item?[] slotSnapshot = new Item?[items.Count];
        for (int i = 0; i < items.Count; i++)
            slotSnapshot[i] = items[i];

        int converted = 0;

        for (int i = 0; i < slotSnapshot.Length; i++)
        {
            if (slotSnapshot[i] is not StardewValley.Object obj) continue;
            if (obj.Category != StardewValley.Object.flowersCategory) continue;
            if (string.Equals(obj.ItemId, targetItemId, StringComparison.OrdinalIgnoreCase)) continue;

            int stack = obj.Stack;
            int quality = obj.Quality;

            Item replacement;
            try
            {
                replacement = ItemRegistry.Create($"(O){targetItemId}", stack, quality);
            }
            catch (Exception ex)
            {
                _monitor.Log($"统一花色失败：{ex.Message}", LogLevel.Warn);
                continue;
            }

            // 写回前校验槽位没被其它逻辑（模组、自动化等）动过
            if (i >= items.Count || !ReferenceEquals(items[i], slotSnapshot[i]))
            {
                _monitor.Log("统一花色：容器内容在转换过程中发生了变化，已放弃该容器以保证安全。", LogLevel.Warn);
                return converted;
            }

            items[i] = replacement;
            converted += stack;
        }

        return converted;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
