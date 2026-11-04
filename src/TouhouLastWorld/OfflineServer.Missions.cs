using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalMissionRule(int Id, int Type, int EventId, int Required, bool Active, LocalInventoryReward[] Rewards);
public sealed record LocalMissionClaim(int Id, int All, int Type, int EventId, int ChapterId, LocalMissionRule[] Rules);

public sealed partial class OfflineServer
{
    private LocalReply MissionOverview()
    {
        var body = QueryTemplates["mission_top"].DeepClone().AsObject();
        if (user["_local_missions"] is JsonObject missions) body["mission"] = missions.DeepClone();
        var rows = body["mission"]!["unlock_list"]!.AsArray().Concat(body["mission"]!["clear_list"]!.AsArray()).ToArray();
        foreach (var field in new[] { "event_id", "chapter_id" })
            body[field + "_list"] = new JsonArray(rows.Select(row => row?[field]?.GetValue<int>() ?? 0)
                .Where(id => id > 0).Distinct().Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        return new(0, body);
    }

    private LocalReply ReceiveMission(LocalMissionClaim? request)
    {
        if (request is null || request.All is < 0 or > 1 || request.Type < 0 || request.EventId < 0 || request.ChapterId < 0
            || (request.All == 0 ? request.Id <= 0 : request.Id != 0))
            return new(400, null, "Invalid mission claim.");
        var updated = user.DeepClone().AsObject();
        var missions = updated["_local_missions"] as JsonObject;
        var ready = missions?["clear_list"] as JsonArray;
        if (ready is null || missions?["unlock_list"] is not JsonArray)
            return new(404, null, "Mission state unavailable.");
        var selected = ready.Where(row => row is JsonObject
            && (request.All == 1 || row["id"]?.GetValue<int>() == request.Id)
            && (request.Type == 0 || row["type"]?.GetValue<int>() == request.Type)
            && (request.EventId == 0 || row["event_id"]?.GetValue<int>() == request.EventId)
            && (request.ChapterId == 0 || row["chapter_id"]?.GetValue<int>() == request.ChapterId)).ToArray();
        if (selected.Length == 0 && request.All == 0) return new(404, null, "Mission is not claimable.");
        var received = updated["_local_received_missions"] as JsonArray ?? new JsonArray();
        if (updated["_local_received_missions"] is null) updated["_local_received_missions"] = received;
        var ids = new HashSet<int>();
        var rewards = new JsonArray();
        var receipts = new JsonArray();
        foreach (var mission in selected)
        {
            var id = mission!["id"]?.GetValue<int>() ?? 0;
            var rule = request.Rules.SingleOrDefault(row => row.Id == id);
            if (id <= 0 || !ids.Add(id) || received.Any(entry => entry?.GetValue<int>() == id))
                return new(409, null, "Mission already received or duplicated.");
            if (rule is null || !rule.Active || rule.Required <= 0 || rule.Rewards.Length == 0
                || mission["type"]?.GetValue<int>() != rule.Type || mission["event_id"]?.GetValue<int>() != rule.EventId
                || mission["complete_value"]?.GetValue<int>() != rule.Required
                || mission["current_value"]?.GetValue<int>() is not int current || current < rule.Required)
                return new(503, null, "Mission progress or reward definition unavailable.");
            var objects = new JsonArray();
            foreach (var reward in rule.Rewards)
            {
                var error = ApplyInventoryReward(updated, reward);
                if (error is not null) return error;
                var item = new JsonObject { ["object_type"] = reward.Type, ["object_id"] = reward.Id,
                    ["object_value"] = reward.Count, ["custom_parameter"] = "" };
                objects.Add(item.DeepClone());
                item["is_eff"] = false;
                rewards.Add(item);
            }
            receipts.Add(new JsonObject { ["mission_id"] = id, ["object_info"] = objects });
            received.Add(id);
            ready.Remove(mission);
        }
        if (selected.Length > 0) Save(updated);
        return new(0, new() { ["mission"] = missions!.DeepClone(), ["reward_list"] = rewards,
            ["receive_mission_list"] = receipts, ["not_receive_message"] = "",
            ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(),
                ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }
}
