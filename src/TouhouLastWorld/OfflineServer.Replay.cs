using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
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
