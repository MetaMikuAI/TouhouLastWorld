using System.Text.Json;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply SaveProfileLayout(string? text)
    {
        JsonObject? layout;
        try { layout = text is null ? null : JsonNode.Parse(text) as JsonObject; }
        catch (JsonException) { return new(400, null, "Invalid layout JSON."); }
        if (layout is null) return new(400, null, "Layout object required.");
        foreach (var (field, value) in layout)
        {
            var valid = field switch
            {
                "backgroundPath" or "comment" => value is JsonValue s && s.TryGetValue<string>(out _),
                "characterId" or "efudaId" or "achievementId" => value is JsonValue n && n.TryGetValue<int>(out var id) && id >= 0,
                "achievementVisible" or "playerIdVisible" or "playerLvlVisible" or "fellowCountVisible"
                    or "efudaCountVisible" or "searchCountVisible" or "errandCountVisible"
                    or "commentVisible" or "playerInfoPos" => value is JsonValue b && b.TryGetValue<bool>(out _),
                "efudaPos" or "efudaScale" => value is JsonObject vector && new[] { "x", "y", "z" }
                    .All(axis => vector[axis] is JsonValue v && v.TryGetValue<double>(out var coordinate) && double.IsFinite(coordinate)),
                _ => true
            };
            if (!valid) return new(400, null, "Invalid layout field: " + field);
        }
        var updated = user.DeepClone().AsObject();
        updated["_local_profile_setting"] = text;
        Save(updated);
        return new(0, new());
    }
}
