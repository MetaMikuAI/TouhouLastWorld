using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply UpdateLocalTwitterRelation(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity) || identity.Length > 256 || identity.Any(char.IsControl))
            return new(400, null, "Valid local identity required.");
        var savedIdentity = user["_local_twitter_user_id"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(savedIdentity) && !string.Equals(savedIdentity, identity, StringComparison.Ordinal))
            return new(409, null, "Clear the existing local relation first.");
        var updated = user.DeepClone().AsObject();
        updated["_local_twitter_user_id"] = identity;
        updated["twitter_info"] = new JsonObject { ["is_related"] = 1 };
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["twitter_info"] = updated["twitter_info"]!.DeepClone() } });
    }

    private LocalReply ClearLocalRelation(string field)
    {
        var updated = user.DeepClone().AsObject();
        updated[field] = 0;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [field] = 0 } });
    }

    private LocalReply DeleteLocalTwitterRelation(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return new(400, null, "Identity required.");
        var savedIdentity = user["_local_twitter_user_id"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(savedIdentity) && !string.Equals(savedIdentity, identity, StringComparison.Ordinal))
            return new(409, null, "Local identity does not match.");
        var updated = user.DeepClone().AsObject();
        updated["twitter_info"] = new JsonObject { ["is_related"] = 0 };
        updated.Remove("_local_twitter_user_id");
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["twitter_info"] = updated["twitter_info"]!.DeepClone() } });
    }
}
