using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply GetSeriesReplays(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Ids is null || request.Ids.Any(area => area is < 1 or > 5))
            return new(400, null, "Stage and area indices 1 through 5 required.");
        var body = new JsonObject();
        for (var area = 1; area <= 5; area++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"{request.EntityId}:{area}");
            body["replay_info" + area] = request.Ids.Contains(area)
                ? user["_local_series_replays"]?[key]?.GetValue<string>() ?? "" : "";
        }
        return new(0, body);
    }

    private LocalReply SaveReplay(LocalReplayContent? request)
    {
        if (request is null || request.TargetId <= 0 || request.Level < 0 || request.Content is null || request.Rules is null)
            return new(400, null, "Replay target, level, rules and content required.");
        var updated = user.DeepClone().AsObject();
        var records = updated["_local_weekly_replays"] as JsonObject ?? new JsonObject();
        if (records.Parent is null) updated["_local_weekly_replays"] = records;
        var key = request.TargetId.ToString(CultureInfo.InvariantCulture);
        if (request.Content.Length == 0) records.Remove(key);
        else records[key] = new JsonObject
        {
            ["level"] = request.Level, ["add_rules"] = request.Rules, ["replay_info"] = request.Content
        };
        Save(updated);
        return new(0, new());
    }

    private LocalReply GetStoredReplay(long? target)
    {
        if (target is null or <= 0 or > int.MaxValue) return new(400, null, "Valid replay target required.");
        var key = target.Value.ToString(CultureInfo.InvariantCulture);
        return new(0, user["_local_weekly_replays"]?[key]?.DeepClone().AsObject()
            ?? new JsonObject { ["level"] = 0, ["add_rules"] = "", ["replay_info"] = "" });
    }

    private LocalReply ReplayMeasurement(LocalReplayMeasure? request, bool write)
    {
        if (request is null || request.QuestType is < 0 or > 4 || request.TargetId <= 0 || request.Difficulty < 0)
            return new(400, null, "Valid replay target required.");
        var key = string.Create(CultureInfo.InvariantCulture, $"{request.QuestType}:{request.TargetId}:{request.Difficulty}");
        if (!write)
            return new(0, user["_local_replay_measurements"]?[key]?.DeepClone().AsObject()
                ?? new JsonObject { ["win_rate"] = 0, ["action_num"] = 0 });
        if (request.WinRate is null or < 0 || request.ActionNum is null or < 0)
            return new(400, null, "Nonnegative replay measurements required.");
        var updated = user.DeepClone().AsObject();
        var records = updated["_local_replay_measurements"] as JsonObject ?? new JsonObject();
        if (records.Parent is null) updated["_local_replay_measurements"] = records;
        records[key] = new JsonObject { ["win_rate"] = request.WinRate.Value, ["action_num"] = request.ActionNum.Value };
        Save(updated);
        return new(0, new());
    }
}
