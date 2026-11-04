using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply ReceiveContactGift(LocalArguments? request)
    {
        if (request?.Value is not (0 or 1) || request.Value == 0 && string.IsNullOrWhiteSpace(request.Text))
            return new(400, null, "Contact ID or all selection required.");
        if (request.EntityId is null or <= 0 or > int.MaxValue)
            return new(503, null, "Gold capacity unavailable.");
        var updated = user.DeepClone().AsObject();
        var contacts = updated["_local_friends"] as JsonArray ?? new JsonArray();
        var selected = contacts.Where(contact => request.Value == 1 || contact?["public_id"]?.GetValue<string>() == request.Text).ToArray();
        if (request.Value == 0 && selected.Length == 0)
            return new(404, null, "Local contact not found.");
        var day = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var count = updated["_local_friend_gift_day"]?.GetValue<string>() == day
            ? updated["_local_friend_gift_count"]?.GetValue<int>() ?? 0 : 0;
        if (count >= 30) return new(20006, null, "Daily gift limit reached.");
        if (count < 0) return new(503, null, "Invalid local gift count.");
        var inbox = updated["_local_friend_gifts_inbox"] as JsonArray ?? new JsonArray();
        var gold = updated["gold"]!.GetValue<int>();
        var received = new JsonArray();
        var ids = new HashSet<string>();
        foreach (var contact in selected)
        {
            if (contact is not JsonObject entry || entry["public_id"] is not JsonValue idValue ||
                !idValue.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                return new(503, null, "Invalid local contact state.");
            if (entry["is_receive_gold"]?.GetValue<bool>() != true) continue;
            var gifts = inbox.Where(gift => gift?["public_id"]?.GetValue<string>() == id).ToArray();
            if (gifts.Length != 1 || gifts[0]?["gold"] is not JsonValue amountValue ||
                !amountValue.TryGetValue<int>(out var amount) || amount <= 0)
                return new(503, null, "Local gift amount unavailable.");
            if (gold > request.EntityId.Value - amount)
                return new(409, null, "Gold capacity exceeded.");
            gold += amount;
            inbox.Remove(gifts[0]);
            entry["is_receive_gold"] = false;
            received.Add(id);
            if (++count == 30) break;
        }
        if (received.Count != 0)
        {
            updated["gold"] = gold;
            updated["_local_friend_gift_day"] = day;
            updated["_local_friend_gift_count"] = count;
            Save(updated);
        }
        return new(0, new()
        {
            ["user_list"] = received, ["user_update"] = new JsonObject { ["gold"] = gold }
        });
    }

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
