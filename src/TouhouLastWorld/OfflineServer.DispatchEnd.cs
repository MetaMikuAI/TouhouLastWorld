using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDispatchSettlement(int DispatchId, int[] MemoIds, LocalInventoryReward[] Rewards);

public sealed partial class OfflineServer
{
    public int ActiveDispatchDefinitionId(long id) => user["dispatch"]!["dispatch_list"]!.AsArray()
        .FirstOrDefault(row => row?["user_dispatch_id"]?.GetValue<long>() == id)?["dispatch_id"]?.GetValue<int>() ?? 0;

    private LocalReply EndDispatch(LocalArguments? request)
    {
        if (request?.EntityId is not > 0) return new(400, null, "Positive dispatch identifier required.");
        var updated = user.DeepClone().AsObject();
        var tasks = updated["dispatch"]!["dispatch_list"]!.AsArray();
        var task = tasks.FirstOrDefault(row => row?["user_dispatch_id"]?.GetValue<long>() == request.EntityId);
        if (task is null) return new(404, null, "Active dispatch not found.");
        if (!DateTimeOffset.TryParse(task["end_time"]?.GetValue<string>(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var finish) || finish > DateTimeOffset.UtcNow)
            return new(409, null, "Dispatch is not complete.");
        var rule = request.DispatchSettlement;
        if (rule is null || rule.DispatchId <= 0 || rule.DispatchId != task["dispatch_id"]?.GetValue<int>())
            return new(503, null, "Dispatch settlement definition unavailable.");
        var settlements = updated["_local_dispatch_settlements"] as JsonArray;
        var matches = settlements?.Where(row => row?["user_dispatch_id"]?.GetValue<long>() == request.EntityId).ToArray();
        if (matches is not { Length: 1 } || matches[0] is not JsonObject settlement
            || settlement["dispatch_id"]?.GetValue<int>() != rule.DispatchId
            || (settlement["total_point"]?.GetValue<int>() ?? 0) != 0 || (settlement["exp_event_id"]?.GetValue<int>() ?? 0) != 0
            || settlement["result"] is not JsonArray result || settlement["reward"] is not JsonArray rewards)
            return new(503, null, "Local dispatch outcome unavailable.");
        var members = task["user_unit_id_list"]!.AsArray().Select(id => id!.GetValue<long>()).Order().ToArray();
        if (members.Length is < 1 or > 6 || members.Distinct().Count() != members.Length
            || !result.Select(row => row?["user_unit_id"]?.GetValue<long>() ?? 0).Order().SequenceEqual(members))
            return new(503, null, "Dispatch outcome members do not match.");
        var owned = updated["unit"]!["unit_list"]!.AsArray();
        foreach (var row in result)
            if (row?["performance"]?.GetValue<int>() is not (>= 0 and <= 2)
                || row["dispatch_memo_id"]?.GetValue<int>() is not > 0
                || !rule.MemoIds.Contains(row["dispatch_memo_id"]!.GetValue<int>())
                || !owned.Any(unit => unit!["user_unit_id"]!.GetValue<long>() == row["user_unit_id"]!.GetValue<long>()))
                return new(503, null, "Dispatch member outcome unavailable.");
        var granted = new JsonArray();
        foreach (var reward in rewards)
        {
            var type = reward?["object_type"]?.GetValue<int>() ?? 0;
            var id = reward?["object_id"]?.GetValue<int>() ?? 0;
            var amount = reward?["amount"]?.GetValue<int>() ?? 0;
            var allowed = rule.Rewards.FirstOrDefault(row => row.Type == type && row.Id == id);
            if (amount <= 0 || allowed is null) return new(503, null, "Dispatch reward unavailable.");
            var error = ApplyInventoryReward(updated, allowed with { Count = amount });
            if (error is not null) return error;
            granted.Add(new JsonObject { ["object_type"] = type, ["object_id"] = id, ["amount"] = amount,
                ["is_new"] = 0, ["unique_id"] = 0 });
        }
        var response = new JsonObject { ["reward"] = granted, ["result"] = result.DeepClone(),
            ["total_point"] = 0, ["exp_event_id"] = 0, ["remove_user_dispatch_id"] = request.EntityId.Value };
        tasks.Remove(task);
        settlements!.Remove(settlement);
        if (updated["_local_dispatch_offers"] is JsonArray offers)
            foreach (var offer in offers.Where(row => row?["user_dispatch_id"]?.GetValue<long>() == request.EntityId).ToArray()) offers.Remove(offer);
        var completions = updated[DispatchCompletionCount]?.GetValue<int>() ?? 0;
        updated[DispatchCompletionCount] = (int)Math.Min((long)completions + 1, int.MaxValue);
        Save(updated);
        response["user_update"] = new JsonObject { ["dispatch"] = updated["dispatch"]!.DeepClone(),
            ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() };
        return new(0, response);
    }
}
