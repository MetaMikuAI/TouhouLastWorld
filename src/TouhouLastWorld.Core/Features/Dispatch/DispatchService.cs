using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class DispatchService
{
    internal DispatchService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply EndDispatchBatch()
    {
        foreach (var task in PlayerState.user["dispatch"]!["dispatch_list"]!.AsArray())
        {
            if (!DateTimeOffset.TryParse(task?["end_time"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var finish))
                return new(409, null, "Invalid dispatch finish time.");
            if (finish <= PlayerState.Clock.GetUtcNow())
                return new(503, null, "Dispatch reward settlement unavailable.");
        }

        return new(0, new() { ["remove_user_dispatch_ids"] = new JsonArray(), ["result_dict_list"] = new JsonArray() });
    }

    internal int DispatchDefinitionId(long id) => PlayerState.user["_local_dispatch_offers"]?.AsArray().FirstOrDefault(entry => entry?["user_dispatch_id"]?.GetValue<long>() == id)?["dispatch_id"]?.GetValue<int>() ?? 0;
    internal LocalReply DispatchOverview()
    {
        var offers = PlayerState.user["_local_dispatch_offers"]?.DeepClone().AsArray() ?? new JsonArray();
        foreach (var task in PlayerState.user["dispatch"]!["dispatch_list"]!.AsArray())
        {
            var offer = offers.FirstOrDefault(entry => entry?["user_dispatch_id"]?.GetValue<long>() == task!["user_dispatch_id"]!.GetValue<long>());
            if (offer is null)
                offers.Add(task!.DeepClone());
            else
            {
                offer["end_time"] = task!["end_time"]!.DeepClone();
                offer["user_unit_id_list"] = task["user_unit_id_list"]!.DeepClone();
            }
        }

        return new(0, new() { ["dispatch_list"] = offers });
    }

    internal LocalReply StartDispatch(StartDispatchCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Ids is not { Length: > 0 and <= 6 } ids || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            return new(400, null, "Invalid dispatch members.");
        var rule = request.DispatchStart;
        if (rule is null || rule.DispatchId <= 0 || rule.Seconds <= 0 || rule.Cost < 0 || rule.Capacity <= 0 || rule.RequiredCount < 0 || rule.RequiredLevel < 0 || rule.QualifiedCount < 0 || !rule.Members.Select(member => member.Id).Order().SequenceEqual(ids.Order()))
            return new(503, null, "Dispatch definition unavailable.");
        var offer = PlayerState.user["_local_dispatch_offers"]?.AsArray().FirstOrDefault(entry => entry?["user_dispatch_id"]?.GetValue<long>() == request.EntityId);
        if (offer is null || offer["dispatch_id"]?.GetValue<int>() != rule.DispatchId)
            return new(404, null, "Dispatch offer not found.");
        var expiration = offer["appearance_finish_time"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(expiration) && (!DateTimeOffset.TryParse(expiration, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires) || expires <= PlayerState.Clock.GetUtcNow()))
            return new(409, null, "Dispatch offer expired.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var dispatch = updated["dispatch"]!;
        var tasks = dispatch["dispatch_list"]!.AsArray();
        if (tasks.Count >= rule.Capacity || tasks.Any(task => task!["user_dispatch_id"]!.GetValue<long>() == request.EntityId))
            return new(409, null, "Dispatch capacity exhausted or task already started.");
        var owned = updated["unit"]!["unit_list"]!.AsArray();
        if (rule.Members.Any(member => !member.Allowed || !owned.Any(unit => unit!["user_unit_id"]!.GetValue<long>() == member.Id && unit["unit_id"]!.GetValue<int>() == member.UnitId)) || ids.Length < rule.RequiredCount || rule.Members.Count(member => member.Level >= rule.RequiredLevel) < rule.QualifiedCount)
            return new(409, null, "Dispatch member requirements not met.");
        var occupied = tasks.SelectMany(task => task!["user_unit_id_list"]!.AsArray()).Select(id => id!.GetValue<long>()).ToHashSet();
        var occupiedTypes = owned.Where(unit => occupied.Contains(unit!["user_unit_id"]!.GetValue<long>())).Select(unit => unit!["unit_id"]!.GetValue<int>()).ToHashSet();
        if (rule.Members.Select(member => member.UnitId).Distinct().Count() != ids.Length || rule.Members.Any(member => occupied.Contains(member.Id) || occupiedTypes.Contains(member.UnitId)))
            return new(409, null, "Dispatch member already assigned.");
        var points = updated["phantasm_point"]!["points"]!.GetValue<int>();
        if (points < rule.Cost)
            return new(409, null, "Insufficient dispatch points.");
        tasks.Add(new JsonObject { ["user_dispatch_id"] = request.EntityId.Value, ["dispatch_id"] = rule.DispatchId, ["end_time"] = PlayerState.Clock.GetUtcNow().UtcDateTime.AddSeconds(rule.Seconds).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), ["user_unit_id_list"] = new JsonArray(ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()) });
        updated["phantasm_point"]!["points"] = points - rule.Cost;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["dispatch"] = dispatch.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone() } });
    }

    internal int ActiveDispatchDefinitionId(long id) => PlayerState.user["dispatch"]!["dispatch_list"]!.AsArray().FirstOrDefault(row => row?["user_dispatch_id"]?.GetValue<long>() == id)?["dispatch_id"]?.GetValue<int>() ?? 0;
    internal LocalReply EndDispatch(EndDispatchCommand? request)
    {
        if (request?.EntityId is not > 0)
            return new(400, null, "Positive dispatch identifier required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var tasks = updated["dispatch"]!["dispatch_list"]!.AsArray();
        var task = tasks.FirstOrDefault(row => row?["user_dispatch_id"]?.GetValue<long>() == request.EntityId);
        if (task is null)
            return new(404, null, "Active dispatch not found.");
        if (!DateTimeOffset.TryParse(task["end_time"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var finish) || finish > PlayerState.Clock.GetUtcNow())
            return new(409, null, "Dispatch is not complete.");
        var rule = request.DispatchSettlement;
        if (rule is null || rule.DispatchId <= 0 || rule.DispatchId != task["dispatch_id"]?.GetValue<int>())
            return new(503, null, "Dispatch settlement definition unavailable.");
        var settlements = updated["_local_dispatch_settlements"] as JsonArray;
        var matches = settlements?.Where(row => row?["user_dispatch_id"]?.GetValue<long>() == request.EntityId).ToArray();
        if (matches is not { Length: 1 } || matches[0] is not JsonObject settlement || settlement["dispatch_id"]?.GetValue<int>() != rule.DispatchId || (settlement["total_point"]?.GetValue<int>() ?? 0) != 0 || (settlement["exp_event_id"]?.GetValue<int>() ?? 0) != 0 || settlement["result"] is not JsonArray result || settlement["reward"] is not JsonArray rewards)
            return new(503, null, "Local dispatch outcome unavailable.");
        var members = task["user_unit_id_list"]!.AsArray().Select(id => id!.GetValue<long>()).Order().ToArray();
        if (members.Length is < 1 or > 6 || members.Distinct().Count() != members.Length || !result.Select(row => row?["user_unit_id"]?.GetValue<long>() ?? 0).Order().SequenceEqual(members))
            return new(503, null, "Dispatch outcome members do not match.");
        var owned = updated["unit"]!["unit_list"]!.AsArray();
        foreach (var row in result)
            if (row?["performance"]?.GetValue<int>() is not (>= 0 and <= 2) || row["dispatch_memo_id"]?.GetValue<int>() is not > 0 || !rule.MemoIds.Contains(row["dispatch_memo_id"]!.GetValue<int>()) || !owned.Any(unit => unit!["user_unit_id"]!.GetValue<long>() == row["user_unit_id"]!.GetValue<long>()))
                return new(503, null, "Dispatch member outcome unavailable.");
        var granted = new JsonArray();
        foreach (var reward in rewards)
        {
            var type = reward?["object_type"]?.GetValue<int>() ?? 0;
            var id = reward?["object_id"]?.GetValue<int>() ?? 0;
            var amount = reward?["amount"]?.GetValue<int>() ?? 0;
            var allowed = rule.Rewards.FirstOrDefault(row => row.Type == type && row.Id == id);
            if (amount <= 0 || allowed is null)
                return new(503, null, "Dispatch reward unavailable.");
            var error = EconomyService.ApplyInventoryReward(updated, allowed with { Count = amount });
            if (error is not null)
                return error;
            granted.Add(new JsonObject { ["object_type"] = type, ["object_id"] = id, ["amount"] = amount, ["is_new"] = 0, ["unique_id"] = 0 });
        }

        var response = new JsonObject
        {
            ["reward"] = granted,
            ["result"] = result.DeepClone(),
            ["total_point"] = 0,
            ["exp_event_id"] = 0,
            ["remove_user_dispatch_id"] = request.EntityId.Value
        };
        tasks.Remove(task);
        settlements!.Remove(settlement);
        if (updated["_local_dispatch_offers"] is JsonArray offers)
            foreach (var offer in offers.Where(row => row?["user_dispatch_id"]?.GetValue<long>() == request.EntityId).ToArray())
                offers.Remove(offer);
        var completions = updated[QuestsService.DispatchCompletionCount]?.GetValue<int>() ?? 0;
        updated[QuestsService.DispatchCompletionCount] = (int)Math.Min((long)completions + 1, int.MaxValue);
        PlayerState.Save(updated);
        response["user_update"] = new JsonObject
        {
            ["dispatch"] = updated["dispatch"]!.DeepClone(),
            ["gold"] = updated["gold"]!.DeepClone(),
            ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone()
        };
        return new(0, response);
    }
}
