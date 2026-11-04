using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalFreeItem(int Id, LocalInventoryReward Reward);

public sealed partial class OfflineServer
{
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
