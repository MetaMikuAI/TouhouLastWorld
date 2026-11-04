using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalItemExchange(int Id, int SourceId, int Cost, int TargetId, int Capacity);

public sealed partial class OfflineServer
{
    private LocalReply ExchangeItems(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or <= 0)
            return new(400, null, "Invalid exchange request.");
        var rule = request.ItemExchange;
        if (rule is null || rule.Id != request.EntityId || rule.SourceId <= 0 || rule.TargetId <= 0
            || rule.SourceId == rule.TargetId || rule.Cost <= 0 || rule.Capacity <= 0)
            return new(503, null, "Item exchange definition unavailable.");
        var updated = user.DeepClone().AsObject();
        var inventory = updated["item"]!["item_list"]!.AsArray();
        var source = inventory.FirstOrDefault(item => item!["item_id"]!.GetValue<int>() == rule.SourceId);
        var target = inventory.FirstOrDefault(item => item!["item_id"]!.GetValue<int>() == rule.TargetId);
        var cost = (long)rule.Cost * request.Value.Value;
        if (source is null || source["num"]!.GetValue<int>() < cost) return new(409, null, "Insufficient exchange materials.");
        var total = (long)(target?["num"]?.GetValue<int>() ?? 0) + request.Value.Value;
        if (total > rule.Capacity) return new(409, null, "Exchange exceeds item capacity.");
        source["num"] = source["num"]!.GetValue<int>() - (int)cost;
        if (target is null) inventory.Add(new JsonObject { ["item_id"] = rule.TargetId, ["num"] = (int)total });
        else target["num"] = (int)total;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["item"] = updated["item"]!.DeepClone() } });
    }
}
