using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalFreeItem(int Id, LocalInventoryReward Reward);
public sealed record LocalShopRefresh(int[] Costs, int[] ActiveGoods);

public sealed partial class OfflineServer
{
    private LocalReply RefreshExchangeShop(LocalArguments? request)
    {
        var rule = request?.ShopRefresh;
        if (rule is null || rule.Costs.Length == 0 || rule.Costs.Any(cost => cost < 0))
            return new(503, null, "Refresh definitions unavailable.");
        var updated = user.DeepClone().AsObject();
        var shops = (updated["_local_shop_list"] as JsonArray)?
            .Where(shop => shop?["type"]?.GetValue<int>() == 3).ToArray();
        if (shops is not { Length: 1 }) return new(409, null, "Local exchange shop unavailable.");
        var shop = shops[0]!;
        var count = shop["manual_update_count"]?.GetValue<int>() ?? -1;
        if (count < 0 || count >= rule.Costs.Length) return new(409, null, "Refresh limit reached.");
        if (updated["_local_gold_shop_batches"] is not JsonArray { Count: > 0 } batches
            || batches[0] is not JsonArray { Count: > 0 } goods)
            return new(503, null, "Next local goods batch unavailable.");
        if (goods.Any(item => item?["master_id"]?.GetValue<int>() is not > 0
                || !rule.ActiveGoods.Contains(item["master_id"]!.GetValue<int>())
                || item["shop_item_id"]?.GetValue<int>() is not > 0 || item["stock"]?.GetValue<int>() is not >= 0)
            || goods.Select(item => item!["shop_item_id"]!.GetValue<int>()).Distinct().Count() != goods.Count)
            return new(503, null, "Invalid local goods batch.");
        var orb = updated["orb"]!;
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        var cost = rule.Costs[count];
        if (free < 0 || purchased < 0 || (decimal)free + purchased < cost)
            return new(409, null, "Insufficient refresh balance.");
        var freeSpent = Math.Min(free, cost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (cost - freeSpent);
        shop["goods_list"] = goods.DeepClone();
        shop["manual_update_count"] = count + 1;
        batches.RemoveAt(0);
        Save(updated);
        return new(0, new() { ["exchange_shop"] = shop.DeepClone(),
            ["user_update"] = new JsonObject { ["orb"] = orb.DeepClone() } });
    }

    public int FreeItemDefinitionId()
    {
        if (user["_local_shop_list"] is not JsonArray shops) return 0;
        var goods = shops.Where(shop => shop?["type"]?.GetValue<int>() == 10)
            .SelectMany(shop => shop?["goods_list"] as JsonArray ?? new JsonArray()).ToArray();
        if (goods.Length != 1) return 0;
        var end = goods[0]?["end_at"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(end) && (!DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var expiry) || expiry <= DateTimeOffset.UtcNow)) return 0;
        return goods[0]?["master_id"]?.GetValue<int>() ?? 0;
    }

    private LocalReply GainFreeItem(LocalArguments? request)
    {
        var rule = request?.FreeItem;
        if (rule is null || rule.Id <= 0 || rule.Id != FreeItemDefinitionId())
            return new(503, null, "Free item definition unavailable.");
        var now = DateTimeOffset.UtcNow;
        var last = user["last_free_item_gain_at"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(last))
        {
            if (!DateTimeOffset.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var received))
                return new(409, null, "Invalid local receipt time.");
            if (received.UtcDateTime.Date >= now.UtcDateTime.Date)
                return new(409, null, "Daily item already received.");
        }
        var updated = user.DeepClone().AsObject();
        var error = ApplyInventoryReward(updated, rule.Reward);
        if (error is not null) return error;
        updated["last_free_item_gain_at"] = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject
        {
            ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone(), ["last_free_item_gain_at"] = updated["last_free_item_gain_at"]!.DeepClone()
        } });
    }
}
