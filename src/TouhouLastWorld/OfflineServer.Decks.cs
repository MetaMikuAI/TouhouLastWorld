using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply SaveDeck(LocalArguments? request)
    {
        if (request?.Value is null or < 0 || request.Ids is null || request.Ids.Any(id => id <= 0 || id > int.MaxValue)
            || request.Text is null || request.Text.Any(char.IsControl))
            return new(400, null, "Invalid deck number, entries or name.");
        var updated = user.DeepClone().AsObject();
        var decks = updated["_local_saved_decks"] as JsonObject ?? new JsonObject();
        if (decks.Parent is null) updated["_local_saved_decks"] = decks;
        var entry = new JsonObject
        {
            ["deck_no"] = request.Value.Value, ["deck_name"] = request.Text,
            ["deck_list"] = new JsonArray(request.Ids.Select(id => (JsonNode?)JsonValue.Create((int)id)).ToArray())
        };
        decks[request.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)] = entry;
        var active = entry.DeepClone();
        active["deck_no"] = 0;
        decks["0"] = active;
        Save(updated);
        return new(0, new());
    }
}
