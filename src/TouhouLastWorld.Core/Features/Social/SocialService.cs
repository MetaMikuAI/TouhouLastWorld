using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class SocialService
{
    internal SocialService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply HelperCandidates(int? capacity)
    {
        if (capacity is null or <= 0)
            return new(503, null, "Contact capacity unavailable.");
        return new(0, new() { ["helper_list"] = new JsonArray(), ["daily_fp_gain_limit_flag"] = 0, ["is_friend_max"] = ((PlayerState.user["_local_friend_players"] as JsonArray)?.Count ?? 0) >= capacity ? 1 : 0, ["friend_request_count"] = (PlayerState.user["_local_friend_requests"] as JsonArray)?.Count ?? 0 });
    }

    internal LocalReply ApplyContact(ApplyContactCommand? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
            return new(400, null, "Contact ID required.");
        if (request.Value is null or <= 0)
            return new(503, null, "Contact capacity unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var friends = updated["_local_friends"] as JsonArray ?? new JsonArray();
        if (friends.Count >= request.Value)
            return new(20007, null, "Local contact list is full.");
        if (friends.Any(entry => entry?["public_id"]?.GetValue<string>() == request.Text))
            return new(409, null, "Local contact already exists.");
        var candidate = (updated["_local_friend_recommendations"] as JsonArray)?.FirstOrDefault(entry => entry?["public_id"]?.GetValue<string>() == request.Text);
        if (candidate is null)
            return new(404, null, "Local candidate not found.");
        var outgoing = updated["_local_friend_outgoing"] as JsonArray ?? new JsonArray();
        if (outgoing.Any(entry => entry?["public_id"]?.GetValue<string>() == request.Text))
            return new(409, null, "Local application already exists.");
        outgoing.Add(candidate.DeepClone());
        if (outgoing.Parent is null)
            updated["_local_friend_outgoing"] = outgoing;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply SendContactRequest(SendContactRequestCommand? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
            return new(400, null, "Player ID required.");
        if (request.Value is null or <= 0 || request.EntityId is null or <= 0)
            return new(503, null, "Contact and request capacities unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var outgoing = updated["_local_friend_requests"] as JsonArray ?? new JsonArray();
        var friends = updated["_local_friend_players"] as JsonArray ?? new JsonArray();
        var candidate = (updated["_local_friend_candidates"] as JsonArray)?.FirstOrDefault(entry => entry?["player_id"]?.GetValue<string>() == request.Text);
        if (friends.Count >= request.EntityId)
            return new(0, new() { ["result"] = 4, ["friend_request_list"] = outgoing.DeepClone() });
        if (candidate is null || outgoing.Count >= request.Value || outgoing.Any(entry => entry?["player_id"]?.GetValue<string>() == request.Text) || friends.Any(entry => entry?["player_id"]?.GetValue<string>() == request.Text))
            return new(0, new() { ["result"] = 1, ["friend_request_list"] = outgoing.DeepClone() });
        outgoing.Add(candidate.DeepClone());
        if (outgoing.Parent is null)
            updated["_local_friend_requests"] = outgoing;
        PlayerState.Save(updated);
        return new(0, new() { ["result"] = 0, ["friend_request_list"] = outgoing.DeepClone() });
    }

    internal LocalReply ApproveContactRequest(ApproveContactRequestCommand? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
            return new(400, null, "Player ID required.");
        if (request.Value is null or <= 0)
            return new(503, null, "Contact capacity unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var incoming = updated["_local_friend_approval_pending"] as JsonArray;
        var entry = incoming?.FirstOrDefault(value => value?["player_id"]?.GetValue<string>() == request.Text);
        var friends = updated["_local_friend_players"] as JsonArray ?? new JsonArray();
        if (entry is null || friends.Count >= request.Value || friends.Any(value => value?["player_id"]?.GetValue<string>() == request.Text))
            return new(0, new() { ["result"] = 1, ["friend_list"] = friends.DeepClone() });
        friends.Add(entry.DeepClone());
        incoming!.Remove(entry);
        if (friends.Parent is null)
            updated["_local_friend_players"] = friends;
        PlayerState.Save(updated);
        return new(0, new() { ["result"] = 0, ["friend_list"] = friends.DeepClone() });
    }

    internal LocalReply CancelContactRequest(CancelContactRequestCommand? request)
    {
        if (request?.Value is not (0 or 1) || (request.Value == 0 && string.IsNullOrWhiteSpace(request.Text)))
            return new(400, null, "Player ID and valid cancellation mode required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var incoming = updated["_local_friend_approval_pending"] as JsonArray;
        var outgoing = updated["_local_friend_requests"] as JsonArray;
        if (request.Value == 1)
            incoming?.Clear();
        else
        {
            var received = incoming?.FirstOrDefault(entry => entry?["player_id"]?.GetValue<string>() == request.Text);
            var sent = outgoing?.FirstOrDefault(entry => entry?["player_id"]?.GetValue<string>() == request.Text);
            if (received is null && sent is null)
                return new(0, new() { ["result"] = 1 });
            if (received is not null)
                incoming!.Remove(received);
            if (sent is not null)
                outgoing!.Remove(sent);
        }

        PlayerState.Save(updated);
        return new(0, new() { ["result"] = 0 });
    }

    internal LocalReply DeleteContact(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new(400, null, "Player ID required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var entries = updated["_local_friend_players"] as JsonArray ?? new JsonArray();
        var entry = entries.FirstOrDefault(value => value?["player_id"]?.GetValue<string>() == id);
        if (entry is null)
            return new(0, new() { ["result"] = 1, ["friend_list"] = entries.DeepClone() });
        entries.Remove(entry);
        PlayerState.Save(updated);
        return new(0, new() { ["result"] = 0, ["friend_list"] = entries.DeepClone() });
    }

    internal LocalReply ContactInfo(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new(400, null, "Contact ID required.");
        var friend = (PlayerState.user["_local_friends"] as JsonArray)?.FirstOrDefault(entry => entry?["public_id"]?.GetValue<string>() == id);
        if (friend is null)
            return new(404, null, "Local contact not found.");
        return new(0, new() { ["unit_get_count"] = friend["unit_get_count"]?.GetValue<int>() ?? 0, ["quest_complete_count"] = friend["quest_complete_count"]?.GetValue<int>() ?? 0, ["dispatch_complete_count"] = friend["dispatch_complete_count"]?.GetValue<int>() ?? 0 });
    }

    internal LocalReply AcceptContactApplication(AcceptContactApplicationCommand? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
            return new(400, null, "Contact ID required.");
        if (request.Value is null or <= 0)
            return new(503, null, "Contact capacity unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var applications = updated["_local_friend_applications"] as JsonArray;
        var entry = applications?.FirstOrDefault(value => value?["public_id"]?.GetValue<string>() == request.Text);
        if (entry is null)
            return new(404, null, "Local contact application not found.");
        var friends = updated["_local_friends"] as JsonArray ?? new JsonArray();
        if (friends.Any(value => value?["public_id"]?.GetValue<string>() == request.Text))
            return new(409, null, "Local contact already exists.");
        if (friends.Count >= request.Value)
            return new(20007, null, "Local contact list is full.");
        var friend = entry.DeepClone().AsObject();
        friend["profile"] = "";
        friend["intimacy"] = 0;
        friends.Add(friend);
        applications!.Remove(entry);
        if (friends.Parent is null)
            updated["_local_friends"] = friends;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply RefuseContactApplications(RefuseContactApplicationsCommand? request)
    {
        if (request?.Value is not (0 or 1) || (request.Value == 0 && string.IsNullOrWhiteSpace(request.Text)))
            return new(400, null, "Contact ID or all selection required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var entries = updated["_local_friend_applications"] as JsonArray ?? new JsonArray();
        if (request.Value == 1)
            entries.Clear();
        else
        {
            var entry = entries.FirstOrDefault(value => value?["public_id"]?.GetValue<string>() == request.Text);
            if (entry is null)
                return new(404, null, "Local contact application not found.");
            entries.Remove(entry);
        }

        if (entries.Parent is null)
            updated["_local_friend_applications"] = entries;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply ContactOverview()
    {
        var body = QueryCatalog.QueryTemplates["friend_top"].DeepClone().AsObject();
        body["friend_list"] = PlayerState.user["_local_friend_players"]?.DeepClone() ?? new JsonArray();
        body["friend_request_list"] = PlayerState.user["_local_friend_requests"]?.DeepClone() ?? new JsonArray();
        body["friend_approval_pending_list"] = PlayerState.user["_local_friend_approval_pending"]?.DeepClone() ?? new JsonArray();
        body["helper_user_unit_id1"] = PlayerState.user["helper_user_unit_id1"]!.DeepClone();
        return new(0, body);
    }

    internal LocalReply SetHelper(SetHelperCommand? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 0)
            return new(400, null, "Positive helper ID and nonnegative power required.");
        if (!PlayerState.user["unit"]!["unit_list"]!.AsArray().Any(unit => unit!["user_unit_id"]!.GetValue<long>() == request.EntityId))
            return new(404, null, "Helper unit not found.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["helper_user_unit_id1"] = request.EntityId.Value;
        updated["_local_helper_power"] = request.Value.Value;
        PlayerState.Save(updated);
        return new(0, new() { ["helper_user_unit_id1"] = request.EntityId.Value, ["user_update"] = new JsonObject { ["helper_user_unit_id1"] = request.EntityId.Value } });
    }

    internal LocalReply ReceiveContactGift(ReceiveContactGiftCommand? request)
    {
        if (request?.Value is not (0 or 1) || request.Value == 0 && string.IsNullOrWhiteSpace(request.Text))
            return new(400, null, "Contact ID or all selection required.");
        if (request.EntityId is null or <= 0 or > int.MaxValue)
            return new(503, null, "Gold capacity unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var contacts = updated["_local_friends"] as JsonArray ?? new JsonArray();
        var selected = contacts.Where(contact => request.Value == 1 || contact?["public_id"]?.GetValue<string>() == request.Text).ToArray();
        if (request.Value == 0 && selected.Length == 0)
            return new(404, null, "Local contact not found.");
        var day = PlayerState.Clock.GetUtcNow().ToString("yyyy-MM-dd");
        var count = updated["_local_friend_gift_day"]?.GetValue<string>() == day ? updated["_local_friend_gift_count"]?.GetValue<int>() ?? 0 : 0;
        if (count >= 30)
            return new(20006, null, "Daily gift limit reached.");
        if (count < 0)
            return new(503, null, "Invalid local gift count.");
        var inbox = updated["_local_friend_gifts_inbox"] as JsonArray ?? new JsonArray();
        var gold = updated["gold"]!.GetValue<int>();
        var received = new JsonArray();
        var ids = new HashSet<string>();
        foreach (var contact in selected)
        {
            if (contact is not JsonObject entry || entry["public_id"] is not JsonValue idValue || !idValue.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                return new(503, null, "Invalid local contact state.");
            if (entry["is_receive_gold"]?.GetValue<bool>() != true)
                continue;
            var gifts = inbox.Where(gift => gift?["public_id"]?.GetValue<string>() == id).ToArray();
            if (gifts.Length != 1 || gifts[0]?["gold"] is not JsonValue amountValue || !amountValue.TryGetValue<int>(out var amount) || amount <= 0)
                return new(503, null, "Local gift amount unavailable.");
            if (gold > request.EntityId.Value - amount)
                return new(409, null, "Gold capacity exceeded.");
            gold += amount;
            inbox.Remove(gifts[0]);
            entry["is_receive_gold"] = false;
            received.Add(id);
            if (++count == 30)
                break;
        }

        if (received.Count != 0)
        {
            updated["gold"] = gold;
            updated["_local_friend_gift_day"] = day;
            updated["_local_friend_gift_count"] = count;
            PlayerState.Save(updated);
        }

        return new(0, new() { ["user_list"] = received, ["user_update"] = new JsonObject { ["gold"] = gold } });
    }

    internal LocalReply GiveContactGift(GiveContactGiftCommand? request)
    {
        if (request?.Value is not (0 or 1) || request.Value == 0 && string.IsNullOrWhiteSpace(request.Text))
            return new(400, null, "Contact ID or all selection required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var contacts = updated["_local_friends"] as JsonArray ?? new JsonArray();
        var selected = contacts.Where(contact => request.Value == 1 || contact?["public_id"]?.GetValue<string>() == request.Text).ToArray();
        if (request.Value == 0 && selected.Length == 0)
            return new(404, null, "Local contact not found.");
        var sent = updated["_local_friend_gifts_sent"] as JsonArray ?? new JsonArray();
        var ids = new HashSet<string>();
        var changed = false;
        foreach (var contact in selected)
        {
            if (contact is not JsonObject entry || entry["public_id"] is not JsonValue idValue || !idValue.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                return new(503, null, "Invalid local contact state.");
            if (entry["is_give_gold"]?.GetValue<bool>() != true)
                continue;
            sent.Add(new JsonObject { ["public_id"] = id, ["sent_at"] = PlayerState.Clock.GetUtcNow().ToString("O") });
            entry["is_give_gold"] = false;
            changed = true;
        }

        if (changed)
        {
            if (sent.Parent is null)
                updated["_local_friend_gifts_sent"] = sent;
            PlayerState.Save(updated);
        }

        return new(0, new());
    }

    internal LocalReply UpdateLocalTwitterRelation(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity) || identity.Length > 256 || identity.Any(char.IsControl))
            return new(400, null, "Valid local identity required.");
        var savedIdentity = PlayerState.user["_local_twitter_user_id"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(savedIdentity) && !string.Equals(savedIdentity, identity, StringComparison.Ordinal))
            return new(409, null, "Clear the existing local relation first.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["_local_twitter_user_id"] = identity;
        updated["twitter_info"] = new JsonObject
        {
            ["is_related"] = 1
        };
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["twitter_info"] = updated["twitter_info"]!.DeepClone() } });
    }

    internal LocalReply ClearLocalRelation(string field)
    {
        var updated = PlayerState.user.DeepClone().AsObject();
        updated[field] = 0;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [field] = 0 } });
    }

    internal LocalReply DeleteLocalTwitterRelation(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return new(400, null, "Identity required.");
        var savedIdentity = PlayerState.user["_local_twitter_user_id"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(savedIdentity) && !string.Equals(savedIdentity, identity, StringComparison.Ordinal))
            return new(409, null, "Local identity does not match.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["twitter_info"] = new JsonObject
        {
            ["is_related"] = 0
        };
        updated.Remove("_local_twitter_user_id");
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["twitter_info"] = updated["twitter_info"]!.DeepClone() } });
    }
}
