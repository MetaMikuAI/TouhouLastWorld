using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply PurchaseFood(LocalFoodPurchase? request)
    {
        if (request is null || request.PointCost < 0 || request.OrbCost < 0)
            return new(503, null, "Food price unavailable.");
        if (request.ItemId <= 0 || request.Count <= 0) return new(400, null, "Positive item and count required.");
        var updated = user.DeepClone().AsObject();
        var points = updated["phantasm_point"]!["points"]!.GetValue<int>();
        var orb = updated["orb"]!;
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        var pointCost = (long)request.PointCost * request.Count;
        var orbCost = (long)request.OrbCost * request.Count;
        if (points < pointCost || free < 0 || purchased < 0 || (decimal)free + purchased < orbCost)
            return new(409, null, "Insufficient local balance.");
        var entries = updated["item"]!["item_list"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["item_id"]!.GetValue<int>() == request.ItemId);
        var count = entry?["num"]?.GetValue<int>() ?? 0;
        if (count < 0 || (long)count + request.Count > int.MaxValue)
            return new(409, null, "Item count exceeds limit.");
        if (entry is null)
        {
            entry = new JsonObject { ["item_id"] = request.ItemId };
            entries.Add(entry);
        }
        entry["num"] = count + request.Count;
        updated["phantasm_point"]!["points"] = points - (int)pointCost;
        var freeSpent = Math.Min(free, orbCost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (orbCost - freeSpent);
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["item"] = updated["item"]!.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone(),
                ["orb"] = orb.DeepClone()
            }
        });
    }
}
