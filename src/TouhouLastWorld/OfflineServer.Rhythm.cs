using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply StartRhythm(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue)
            return new(400, null, "Positive music ID required.");
        if (request.Value != 1) return new(404, null, "Music definition unavailable.");
        var updated = user.DeepClone().AsObject();
        updated["_local_rhythm_active"] = (int)request.EntityId.Value;
        Save(updated);
        return new(0, new());
    }
}
