using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply GiveContactGift(LocalArguments? request)
    {
        if (request?.Value is not (0 or 1) || request.Value == 0 && string.IsNullOrWhiteSpace(request.Text))
            return new(400, null, "Contact ID or all selection required.");

        var updated = user.DeepClone().AsObject();
        var contacts = updated["_local_friends"] as JsonArray ?? new JsonArray();
        var selected = contacts.Where(contact => request.Value == 1 || contact?["public_id"]?.GetValue<string>() == request.Text).ToArray();
        if (request.Value == 0 && selected.Length == 0)
            return new(404, null, "Local contact not found.");
        var sent = updated["_local_friend_gifts_sent"] as JsonArray ?? new JsonArray();
        var ids = new HashSet<string>();
        var changed = false;
        foreach (var contact in selected)
        {
            if (contact is not JsonObject entry || entry["public_id"] is not JsonValue idValue ||
                !idValue.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                return new(503, null, "Invalid local contact state.");
            if (entry["is_give_gold"]?.GetValue<bool>() != true) continue;
            sent.Add(new JsonObject { ["public_id"] = id, ["sent_at"] = DateTimeOffset.UtcNow.ToString("O") });
            entry["is_give_gold"] = false;
            changed = true;
        }
        if (changed)
        {
            if (sent.Parent is null) updated["_local_friend_gifts_sent"] = sent;
            Save(updated);
        }
        return new(0, new());
    }
}
