using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply AcquirePages(LocalPagePurchase? request)
    {
        if (request is null || request.MaxPages <= 0 || request.UnitCost <= 0)
            return new(503, null, "Page definition or price unavailable.");
        if (request.Id <= 0 || request.Count <= 0)
            return new(400, null, "Positive collection ID and page count required.");
        var updated = user.DeepClone().AsObject();
        var entries = updated["comic_data"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["comic_id"]!.GetValue<int>() == request.Id);
        var received = entry?["received_page_count"]?.GetValue<int>() ?? 0;
        if (received < 0 || (long)received + request.Count > request.MaxPages)
            return new(409, null, "Page count exceeds collection limit.");
        var cost = (long)request.Count * request.UnitCost;
        var points = updated["comic_point"]!.GetValue<int>();
        if (points < cost) return new(409, null, "Insufficient points.");
        if (entry is null)
        {
            entry = new JsonObject { ["comic_id"] = request.Id, ["rad_page_count"] = 0 };
            entries.Add(entry);
        }
        entry["received_page_count"] = received + request.Count;
        updated["comic_point"] = points - (int)cost;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["comic_point"] = updated["comic_point"]!.DeepClone(), ["comic_data"] = entries.DeepClone()
            }
        });
    }

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
