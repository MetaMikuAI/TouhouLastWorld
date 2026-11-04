using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply GatherPoints(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value != user["level"]?.GetValue<int>())
            return new(503, null, "Current level capacity unavailable.");
        if (user["phantasm_point"] is not JsonObject points)
            return new(503, null, "Local point balance unavailable.");
        var stock = user["_local_gathering_phantasm_point"]?.GetValue<int>() ?? 0;
        var balance = points["points"]!.GetValue<int>();
        if (stock < 0 || balance < 0) return new(503, null, "Invalid local balance.");
        var amount = (int)Math.Min(stock, Math.Max(0, request.EntityId.Value - balance));
        if (amount != 0)
        {
            var updated = user.DeepClone().AsObject();
            updated["phantasm_point"]!["points"] = balance + amount;
            updated["_local_gathering_phantasm_point"] = stock - amount;
            Save(updated);
        }
        return new(0, new()
        {
            ["gathering_phantasm_point"] = stock - amount,
            ["user_update"] = new JsonObject { ["phantasm_point"] = user["phantasm_point"]!.DeepClone() }
        });
    }

    private LocalReply GatherGold(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value != user["level"]?.GetValue<int>())
            return new(503, null, "Current level capacity unavailable.");
        var stock = user["_local_gathering_gold"]?.GetValue<int>() ?? 0;
        var balance = user["gold"]!.GetValue<int>();
        if (stock < 0 || balance < 0) return new(503, null, "Invalid local balance.");
        var amount = (int)Math.Min(stock, Math.Max(0, request.EntityId.Value - balance));
        if (amount != 0)
        {
            var updated = user.DeepClone().AsObject();
            updated["gold"] = balance + amount;
            updated["_local_gathering_gold"] = stock - amount;
            Save(updated);
        }
        return new(0, new()
        {
            ["gathering_gold"] = stock - amount,
            ["user_update"] = new JsonObject { ["gold"] = balance + amount }
        });
    }
}
