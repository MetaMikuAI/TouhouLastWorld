using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static bool SupportsAdditional(string api) => api is "tutorial_update" or "logout" or "set_age"
        or "friend/search" or "user_info_update" or "get_profile_setting" or "update_profile_setting"
        or "update_profile_setting2" or "get_auto_replay_info" or "update_auto_replay_info"
        or "get_multiple_spirits_weekly_replay_info" or "update_multiple_spirits_weekly_replay_info"
        or "get_series_battle_replay_info" or "update_series_battle_replay_info"
        or "get_replay_info" or "update_replay_info" or "achievement_new_update" or "team_update"
        or "friend_setting_helper" or "home_select_unit_update" or "home_bgm_update" || QueryTemplates.ContainsKey(api);

    private LocalReply? DispatchAdditional(string api, LocalArguments? arguments) => api switch
    {
        "tutorial_update" => UpdateProgress(arguments?.Value),
        "home_select_unit_update" => SelectHomeUnit(arguments?.Value),
        "home_bgm_update" => arguments?.Value is >= 0 ? SetUserInteger("select_bgm", arguments.Value.Value)
            : new(400, null, "Nonnegative music selection required."),
        "team_update" => UpdateTeam(arguments?.Team),
        "friend_setting_helper" => SetHelper(arguments),
        "friend_top" => ContactOverview(),
        "achievement_new_update" => ClearAchievementMarkers(arguments?.Ids),
        "get_replay_info" => QuestReplay(arguments, false),
        "update_replay_info" => QuestReplay(arguments, true),
        "get_series_battle_replay_info" => GetSeriesReplays(arguments),
        "update_series_battle_replay_info" => SaveSeriesReplay(arguments),
        "get_auto_replay_info" => ReplayMeasurement(arguments?.ReplayMeasure, false),
        "get_multiple_spirits_weekly_replay_info" => GetStoredReplay(arguments?.EntityId),
        "update_multiple_spirits_weekly_replay_info" => SaveReplay(arguments?.ReplayContent),
        "update_auto_replay_info" => ReplayMeasurement(arguments?.ReplayMeasure, true),
        "update_profile_setting" => SaveProfileLayout(arguments?.Text),
        "update_profile_setting2" => new(0, new()
        {
            ["text_value"] = arguments?.Text is null || arguments.Text.Any(char.IsControl) ? 1 : 0
        }),
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

    private LocalReply ContactOverview()
    {
        var body = QueryTemplates["friend_top"].DeepClone().AsObject();
        body["helper_user_unit_id1"] = user["helper_user_unit_id1"]!.DeepClone();
        return new(0, body);
    }

    private LocalReply SetHelper(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 0)
            return new(400, null, "Positive helper ID and nonnegative power required.");
        if (!user["unit"]!["unit_list"]!.AsArray().Any(unit => unit!["user_unit_id"]!.GetValue<long>() == request.EntityId))
            return new(404, null, "Helper unit not found.");
        var updated = user.DeepClone().AsObject();
        updated["helper_user_unit_id1"] = request.EntityId.Value;
        updated["_local_helper_power"] = request.Value.Value;
        Save(updated);
        return new(0, new()
        {
            ["helper_user_unit_id1"] = request.EntityId.Value,
            ["user_update"] = new JsonObject { ["helper_user_unit_id1"] = request.EntityId.Value }
        });
    }

    private LocalReply ClearAchievementMarkers(long[]? ids)
    {
        if (ids is null || ids.Any(id => id <= 0 || id > int.MaxValue))
            return new(400, null, "Positive achievement IDs required.");
        var updated = user.DeepClone().AsObject();
        var achievement = updated["achievement"]!.AsObject();
        var recent = achievement["recently_received_achievement_id_list"]!.AsArray();
        var marked = ids.ToHashSet();
        for (var index = recent.Count - 1; index >= 0; index--)
            if (marked.Contains(recent[index]!.GetValue<long>())) recent.RemoveAt(index);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["achievement"] = achievement.DeepClone() } });
    }

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
