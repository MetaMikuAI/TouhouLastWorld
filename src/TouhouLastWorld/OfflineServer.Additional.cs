using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static bool SupportsAdditional(string api) => api is "tutorial_update" or "logout" or "set_age"
        or "friend/search" or "user_info_update" or "get_profile_setting" || QueryTemplates.ContainsKey(api);

    private LocalReply? DispatchAdditional(string api, LocalArguments? arguments) => api switch
    {
        "tutorial_update" => UpdateProgress(arguments?.Value),
        "get_profile_setting" => new(0, new()
        {
            ["profile_setting"] = user["_local_profile_setting"]?.GetValue<string>() ?? "",
            ["achievement_id_list"] = new JsonArray()
        }),
        "user_info_update" => UpdateProfile(arguments?.Profile),
        "friend/search" => new(2000, null, "No matching local contact."),
        "set_age" => arguments?.Value is >= 1 and <= 3
            ? SetUserInteger("age_limit_type", arguments.Value.Value)
            : new(400, null, "Age category must be 1, 2 or 3."),
        "logout" => arguments?.Text == account!.UserId
            ? new(0, new()) : new(403, null, "Local identity does not match."),
        _ => QueryTemplates.TryGetValue(api, out var body) ? new(0, body.DeepClone().AsObject()) : null
    };

    private LocalReply UpdateProfile(LocalProfileUpdate? profile)
    {
        if (profile is null || (profile.PlayerName is null && profile.HeroName is null && profile.Comment is null)
            || (profile.PlayerName is not null && string.IsNullOrWhiteSpace(profile.PlayerName))
            || (profile.HeroName is not null && string.IsNullOrWhiteSpace(profile.HeroName))
            || new[] { profile.PlayerName, profile.HeroName, profile.Comment }.Any(text => text?.Any(char.IsControl) == true))
            return new(1000, null, "Invalid local profile text.");
        var delta = new JsonObject();
        if (profile.PlayerName is not null) delta["player_name"] = profile.PlayerName;
        if (profile.HeroName is not null) delta["hero_name"] = profile.HeroName;
        if (profile.Comment is not null) delta["comment"] = profile.Comment;
        var updated = user.DeepClone().AsObject();
        foreach (var (field, value) in delta) updated[field] = value!.DeepClone();
        Save(updated);
        return new(0, new() { ["edit_validity"] = 0, ["user_update"] = delta });
    }

    private LocalReply SetUserInteger(string field, int value)
    {
        var updated = user.DeepClone().AsObject();
        updated[field] = value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [field] = value } });
    }

    private LocalReply UpdateProgress(int? progress)
    {
        if (progress is null or < 0) return new(400, null, "Nonnegative progress required.");
        var updated = user.DeepClone().AsObject();
        var value = Math.Max(updated["progress_value"]!.GetValue<int>(), progress.Value);
        updated["progress_value"] = value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["progress_value"] = value } });
    }
}
