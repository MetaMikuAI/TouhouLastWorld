using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply ReadPages(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value is null or < 0)
            return new(400, null, "Positive collection ID and nonnegative page required.");
        var updated = user.DeepClone().AsObject();
        var entry = updated["comic_data"]!.AsArray().FirstOrDefault(value => value!["comic_id"]!.GetValue<int>() == request.EntityId);
        if (entry is null) return new(404, null, "Collection not acquired.");
        if (request.Value > entry["received_page_count"]!.GetValue<int>())
            return new(403, null, "Page not acquired.");
        entry["rad_page_count"] = Math.Max(entry["rad_page_count"]!.GetValue<int>(), request.Value.Value);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["comic_data"] = updated["comic_data"]!.DeepClone() } });
    }
}
