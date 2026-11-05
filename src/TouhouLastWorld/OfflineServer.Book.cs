using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalInventoryReward(int Type, int Id, int Count, int Capacity, int CostumeId = 0);
public sealed record LocalBookReward(int TaskId, int BookId, LocalInventoryReward Reward);

public sealed partial class OfflineServer
{
    private static LocalReply? ApplyInventoryReward(JsonObject updated, LocalInventoryReward reward)
    {
        if (reward.Type is not (3 or 6 or 12) || reward.Count <= 0 || reward.Capacity <= 0)
            return new(503, null, "Reward definition unsupported.");
        if (reward.Type == 12)
        {
            if (reward.Id <= 0) return new(503, null, "Item definition unavailable.");
            var inventory = updated["item"]!["item_list"]!.AsArray();
            var item = inventory.FirstOrDefault(entry => entry?["item_id"]?.GetValue<int>() == reward.Id);
            var total = (long)(item?["num"]?.GetValue<int>() ?? 0) + reward.Count;
            if (total > reward.Capacity) return new(409, null, "Item reward exceeds capacity.");
            if (item is null) inventory.Add(new JsonObject { ["item_id"] = reward.Id, ["num"] = (int)total });
            else item["num"] = (int)total;
        }
        else
        {
            var key = reward.Type == 3 ? "gold" : "crystal";
            var total = (long)updated[key]!.GetValue<int>() + reward.Count;
            if (total > reward.Capacity) return new(409, null, "Currency reward exceeds capacity.");
            updated[key] = (int)total;
        }
        return null;
    }

    private LocalReply ReceiveBookReward(LocalArguments? request)
    {
        if (request?.Value is null or <= 0) return new(400, null, "Positive task ID required.");
        var rule = request.BookReward;
        if (rule is null || rule.TaskId != request.Value || rule.BookId <= 0)
            return new(503, null, "Book reward definition unavailable.");
        var updated = user.DeepClone().AsObject();
        var book = updated["_local_picture_book"]?.AsArray().FirstOrDefault(entry => entry?["pb_id"]?.GetValue<int>() == rule.BookId);
        if (book?["complete_ls"] is not JsonArray completed || book["reward"] is not JsonArray received)
            return new(409, null, "Book progress unavailable.");
        if (received.Any(id => id!.GetValue<int>() == rule.TaskId)) return new(409, null, "Book reward already received.");
        var pending = completed.FirstOrDefault(id => id!.GetValue<int>() == rule.TaskId);
        if (pending is null) return new(409, null, "Book task not completed.");
        var error = ApplyInventoryReward(updated, rule.Reward);
        if (error is not null) return error;
        completed.Remove(pending);
        received.Add(rule.TaskId);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject
        {
            ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone()
        } });
    }
}
