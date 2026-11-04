using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
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
