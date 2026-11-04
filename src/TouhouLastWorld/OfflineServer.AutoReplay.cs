using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply AutoReplayUnavailable(LocalArguments? request)
    {
        var target = request?.ReplayMeasure;
        if (target is null || target.QuestType is < 0 or > 4 || target.TargetId <= 0
            || target.Difficulty < 0 || request!.Value is null or <= 0)
            return new(400, null, "Invalid background replay request.");
        var replay = user["auto_replay"];
        if (replay?["target_id"]?.GetValue<long>() != 0)
            return new(409, null, "Existing background replay must be settled first.");
        return new(0, new() { ["user_update"] = new JsonObject { ["auto_replay"] = replay.DeepClone() } });
    }
}
