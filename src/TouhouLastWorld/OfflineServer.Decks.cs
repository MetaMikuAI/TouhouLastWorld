using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply DeckOverview(int? maxNumber)
    {
        if (maxNumber is null or <= 0 or int.MaxValue)
            return new(503, null, "Deck capacity unavailable.");
        var updated = user.DeepClone().AsObject();
        var decks = updated["_local_saved_decks"] as JsonObject ?? new JsonObject();
        if (decks.Parent is null) updated["_local_saved_decks"] = decks;
        var saved = new JsonArray();
        var changed = false;
        for (var number = 0; number <= maxNumber.Value; number++)
        {
            var key = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (decks[key] is null)
            {
                decks[key] = new JsonObject { ["deck_no"] = number, ["deck_name"] = "", ["deck_list"] = new JsonArray() };
                changed = true;
            }
            saved.Add(decks[key]!.DeepClone());
        }
        if (changed) Save(updated);
        return new(0, new()
        {
            ["progress"] = 0, ["path_list"] = new JsonArray(), ["saved_decks"] = saved,
            ["map_id"] = 0, ["map_buff_id"] = 0, ["selected_seal_id_list"] = new JsonArray(),
            ["drawn_seals"] = new JsonArray(), ["play_details"] = "", ["remaining_rerolls"] = 0, ["remaining_draws"] = 0
        });
    }

    private LocalReply CallDeck(int? number)
    {
        if (number is null or < 0) return new(400, null, "Nonnegative deck number required.");
        var key = number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (user["_local_saved_decks"]?[key] is not JsonObject saved)
            return new(404, null, "Deck not saved.");
        var updated = user.DeepClone().AsObject();
        var active = saved.DeepClone();
        active["deck_no"] = 0;
        updated["_local_saved_decks"]!["0"] = active;
        Save(updated);
        return new(0, new());
    }

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
