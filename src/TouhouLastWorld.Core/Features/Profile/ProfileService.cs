using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class ProfileService
{
    internal ProfileService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply SelectAvatar(int? id)
    {
        if (id is null or <= 0)
            return new(400, null, "Positive avatar ID required.");
        var entry = PlayerState.user["HeadListItemList"]!.AsArray().FirstOrDefault(value => value!["avatar_id"]!.GetValue<int>() == id);
        if (entry is null)
            return new(404, null, "Avatar entry not found.");
        if (entry["is_clear"]?.GetValue<bool>() != true)
            return new(403, null, "Avatar is locked.");
        var filename = entry["filename"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(filename))
            return new(409, null, "Avatar resource unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["avatar_id"] = id.Value;
        updated["avatar_filename"] = filename;
        updated["avatar_title"] = entry["title"]?.GetValue<string>() ?? "";
        updated["avatar_desc"] = entry["desc"]?.GetValue<string>() ?? "";
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["avatar_id"] = id.Value, ["avatar_filename"] = filename, ["avatar_title"] = updated["avatar_title"]!.DeepClone(), ["avatar_desc"] = updated["avatar_desc"]!.DeepClone() } });
    }

    internal LocalReply PreviewAvatar(int? id)
    {
        if (id is null or <= 0)
            return new(400, null, "Positive avatar ID required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var entries = updated["HeadListItemList"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["avatar_id"]!.GetValue<int>() == id);
        if (entry is null)
            return new(404, null, "Avatar entry not found.");
        entry["is_preview"] = true;
        var unread = entries.Any(value => value!["is_clear"]!.GetValue<bool>() && !value["is_preview"]!.GetValue<bool>());
        updated["isShowRedPoint"] = unread;
        PlayerState.Save(updated);
        return new(0, new() { ["avatar_tips"] = unread ? 1 : 0 });
    }

    internal LocalReply AchievementOverview()
    {
        var body = QueryCatalog.QueryTemplates["achievement_top"].DeepClone().AsObject();
        body["achievement_list"] = PlayerState.user["_local_achievements"]?.DeepClone() ?? new JsonArray();
        return new(0, body);
    }

    internal LocalReply SelectAchievement(int? id)
    {
        if (id is null or < 0)
            return new(400, null, "Nonnegative achievement ID required.");
        if (id != 0 && !(PlayerState.user["_local_achievements"] as JsonArray ?? new JsonArray()).Any(entry => entry?["achievement_id"]?.GetValue<int>() == id && entry["is_acquired"]?.GetValue<int>() == 1))
            return new(403, null, "Achievement not acquired.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["achievement"]!["selected_achievement_id"] = id.Value;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["achievement"] = updated["achievement"]!.DeepClone() } });
    }

    internal LocalReply SetVoice(SetVoiceCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value is null or < 0)
            return new(400, null, "Positive unit ID and nonnegative voice selection required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var album = updated["album"]!.AsObject();
        var entries = album["unit_list"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["unit_id"]!.GetValue<int>() == request.EntityId);
        if (entry is null)
        {
            if (!updated["unit"]!["unit_list"]!.AsArray().Any(value => value!["unit_id"]!.GetValue<int>() == request.EntityId))
                return new(404, null, "Unit not found in local collection.");
            entry = new JsonObject
            {
                ["unit_id"] = (int)request.EntityId.Value,
                ["talk_rank"] = 0,
                ["character_episode_ids"] = new JsonArray(),
                ["home_motion_ids"] = new JsonArray()
            };
            entries.Add(entry);
        }

        entry["voice_set_id"] = request.Value.Value;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["album"] = album.DeepClone() } });
    }

    internal LocalReply MarkTalkRead(int? id)
    {
        if (id is null or <= 0)
            return new(400, null, "Positive talk ID required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var read = updated["_local_read_talk_ids"] as JsonArray ?? new JsonArray();
        if (read.Any(entry => entry!.GetValue<int>() == id.Value))
            return new(0, new());
        if (read.Parent is null)
            updated["_local_read_talk_ids"] = read;
        read.Add(id.Value);
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply ClearAchievementMarkers(long[]? ids)
    {
        if (ids is null || ids.Any(id => id <= 0 || id > int.MaxValue))
            return new(400, null, "Positive achievement IDs required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var achievement = updated["achievement"]!.AsObject();
        var recent = achievement["recently_received_achievement_id_list"]!.AsArray();
        var marked = ids.ToHashSet();
        for (var index = recent.Count - 1; index >= 0; index--)
            if (marked.Contains(recent[index]!.GetValue<long>()))
                recent.RemoveAt(index);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["achievement"] = achievement.DeepClone() } });
    }

    internal LocalReply UpdateProfile(LocalProfileUpdate? profile)
    {
        if (profile is null || (profile.PlayerName is null && profile.HeroName is null && profile.Comment is null) || (profile.PlayerName is not null && string.IsNullOrWhiteSpace(profile.PlayerName)) || (profile.HeroName is not null && string.IsNullOrWhiteSpace(profile.HeroName)) || new[]
        {
            profile.PlayerName,
            profile.HeroName,
            profile.Comment
        }.Any(text => text?.Any(char.IsControl) == true))
            return new(1000, null, "Invalid local profile text.");
        var delta = new JsonObject();
        if (profile.PlayerName is not null)
            delta["player_name"] = profile.PlayerName;
        if (profile.HeroName is not null)
            delta["hero_name"] = profile.HeroName;
        if (profile.Comment is not null)
            delta["comment"] = profile.Comment;
        var updated = PlayerState.user.DeepClone().AsObject();
        foreach (var (field, value) in delta)
            updated[field] = value!.DeepClone();
        PlayerState.Save(updated);
        return new(0, new() { ["edit_validity"] = 0, ["user_update"] = delta });
    }

    internal LocalReply SetUserInteger(string field, int value)
    {
        var updated = PlayerState.user.DeepClone().AsObject();
        updated[field] = value;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [field] = value } });
    }

    internal LocalReply UpdateProgress(int? progress)
    {
        if (progress is null or < 0)
            return new(400, null, "Nonnegative progress required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var value = Math.Max(updated["progress_value"]!.GetValue<int>(), progress.Value);
        updated["progress_value"] = value;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["progress_value"] = value } });
    }

    internal LocalReply SaveContactDetails(LocalContactDetails? details)
    {
        if (details is null || new[]
        {
            details.Name,
            details.ZipCode,
            details.Address,
            details.Phone
        }.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)))
            return new(400, null, "Invalid local contact details.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["_local_personal_info"] = new JsonObject
        {
            ["user_name"] = details.Name,
            ["zip_code"] = details.ZipCode,
            ["address"] = details.Address,
            ["phone_number"] = details.Phone
        };
        var state = updated["figure_present"] as JsonObject ?? new JsonObject
        {
            ["rest_days"] = 0
        };
        state["applied"] = 1;
        if (state.Parent is null)
            updated["figure_present"] = state;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["figure_present"] = state.DeepClone() } });
    }

    internal LocalReply SaveAnniversaryInfo(SaveAnniversaryInfoCommand? request)
    {
        var address = request?.Text?.Trim();
        if (request?.Value != 1 || string.IsNullOrEmpty(address) || !address.Contains('@') || address.Any(char.IsControl))
            return new(400, null, "Invalid local application.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["_local_anniversary_info"] = new JsonObject
        {
            ["mail_address"] = address
        };
        var state = updated["anniversary_present"] as JsonObject ?? new JsonObject
        {
            ["rest_days"] = 0
        };
        state["applied"] = 1;
        if (state.Parent is null)
            updated["anniversary_present"] = state;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["anniversary_present"] = state.DeepClone() } });
    }

    internal LocalReply ReceiveBookReward(ReceiveBookRewardCommand? request)
    {
        if (request?.Value is null or <= 0)
            return new(400, null, "Positive task ID required.");
        var rule = request.BookReward;
        if (rule is null || rule.TaskId != request.Value || rule.BookId <= 0)
            return new(503, null, "Book reward definition unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var book = updated["_local_picture_book"]?.AsArray().FirstOrDefault(entry => entry?["pb_id"]?.GetValue<int>() == rule.BookId);
        if (book?["complete_ls"] is not JsonArray completed || book["reward"] is not JsonArray received)
            return new(409, null, "Book progress unavailable.");
        if (received.Any(id => id!.GetValue<int>() == rule.TaskId))
            return new(409, null, "Book reward already received.");
        var pending = completed.FirstOrDefault(id => id!.GetValue<int>() == rule.TaskId);
        if (pending is null)
            return new(409, null, "Book task not completed.");
        var error = EconomyService.ApplyInventoryReward(updated, rule.Reward);
        if (error is not null)
            return error;
        completed.Remove(pending);
        received.Add(rule.TaskId);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(), ["item"] = updated["item"]!.DeepClone() } });
    }

    internal const int NoBgm = -1;
    internal LocalReply UpdateHome(LocalHomeUpdate? request)
    {
        if (request?.Units is null || request.Units.Length is < 1 or > 6 || request.RandomMode is not (0 or 1) || request.SelectedIndex < 0 || request.SelectedIndex >= request.Units.Length)
            return new(400, null, "Home entries, valid selection and random flag required.");
        var entries = new JsonArray();
        foreach (var entry in request.Units)
        {
            if (entry is null || entry.UserUnitId <= 0 || entry.UnitId <= 0 || entry.CostumeId < 0 || entry.Motion < 0 || entry.Music < NoBgm)
                return new(400, null, "Invalid home entry.");
            var owned = PlayerState.user["unit"]!["unit_list"]!.AsArray().FirstOrDefault(unit => unit!["user_unit_id"]!.GetValue<long>() == entry.UserUnitId);
            if (owned is null)
                return new(404, null, "Home unit not found.");
            if (owned["unit_id"]!.GetValue<int>() != entry.UnitId)
                return new(400, null, "Home unit identifiers do not match.");
            entries.Add(new JsonObject { ["unit_id"] = entry.UnitId, ["user_unit_id"] = entry.UserUnitId, ["costume_id"] = entry.CostumeId, ["home_motion"] = entry.Motion, ["bgm"] = entry.Music });
        }

        var info = new JsonObject
        {
            ["unit_param"] = entries,
            ["select_unit_index"] = request.SelectedIndex,
            ["random_select_mode"] = request.RandomMode
        };
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["home_unit_info"] = info;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["home_unit_info"] = info.DeepClone() } });
    }

    internal LocalReply SetUnitPreference(SetUnitPreferenceCommand? request, string field, int minimum = 0)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null || request.Value < minimum)
            return new(400, null, "Invalid unit ID or preference.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var unit = updated["unit"]!["unit_list"]!.AsArray().FirstOrDefault(entry => entry!["user_unit_id"]!.GetValue<long>() == request.EntityId);
        if (unit is null)
            return new(404, null, "Unit not found.");
        unit[field] = request.Value.Value;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit"] = PlayerState.user["unit"]!.DeepClone() } });
    }

    internal LocalReply SelectHomeUnit(int? index)
    {
        var info = PlayerState.user["home_unit_info"]!.AsObject();
        if (index is null or < 0 || index >= info["unit_param"]!.AsArray().Count)
            return new(400, null, "Home selection index out of range.");
        var updated = PlayerState.user.DeepClone().AsObject();
        updated["home_unit_info"]!["select_unit_index"] = index.Value;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["home_unit_info"] = PlayerState.user["home_unit_info"]!.DeepClone() } });
    }

    internal LocalReply AcquirePages(LocalPagePurchase? request)
    {
        if (request is null || request.MaxPages <= 0 || request.UnitCost <= 0)
            return new(503, null, "Page definition or price unavailable.");
        if (request.Id <= 0 || request.Count <= 0)
            return new(400, null, "Positive collection ID and page count required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var entries = updated["comic_data"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["comic_id"]!.GetValue<int>() == request.Id);
        var received = entry?["received_page_count"]?.GetValue<int>() ?? 0;
        if (received < 0 || (long)received + request.Count > request.MaxPages)
            return new(409, null, "Page count exceeds collection limit.");
        var cost = (long)request.Count * request.UnitCost;
        var points = updated["comic_point"]!.GetValue<int>();
        if (points < cost)
            return new(409, null, "Insufficient points.");
        if (entry is null)
        {
            entry = new JsonObject
            {
                ["comic_id"] = request.Id,
                ["rad_page_count"] = 0
            };
            entries.Add(entry);
        }

        entry["received_page_count"] = received + request.Count;
        updated["comic_point"] = points - (int)cost;
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["comic_point"] = updated["comic_point"]!.DeepClone(), ["comic_data"] = entries.DeepClone() } });
    }

    internal LocalReply ReadPages(ReadPagesCommand? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value is null or < 0)
            return new(400, null, "Positive collection ID and nonnegative page required.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var entry = updated["comic_data"]!.AsArray().FirstOrDefault(value => value!["comic_id"]!.GetValue<int>() == request.EntityId);
        if (entry is null)
            return new(404, null, "Collection not acquired.");
        if (request.Value > entry["received_page_count"]!.GetValue<int>())
            return new(403, null, "Page not acquired.");
        entry["rad_page_count"] = Math.Max(entry["rad_page_count"]!.GetValue<int>(), request.Value.Value);
        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["comic_data"] = updated["comic_data"]!.DeepClone() } });
    }

    internal LocalReply SaveProfileLayout(string? text)
    {
        JsonObject? layout;
        try
        {
            layout = text is null ? null : JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return new(400, null, "Invalid layout JSON.");
        }

        if (layout is null)
            return new(400, null, "Layout object required.");
        foreach (var (field, value) in layout)
        {
            var valid = field switch
            {
                "backgroundPath" or "comment" => value is JsonValue s && s.TryGetValue<string>(out _),
                "characterId" or "efudaId" or "achievementId" => value is JsonValue n && n.TryGetValue<int>(out var id) && id >= 0,
                "achievementVisible" or "playerIdVisible" or "playerLvlVisible" or "fellowCountVisible" or "efudaCountVisible" or "searchCountVisible" or "errandCountVisible" or "commentVisible" or "playerInfoPos" => value is JsonValue b && b.TryGetValue<bool>(out _),
                "efudaPos" or "efudaScale" => value is JsonObject vector && new[]
                {
                    "x",
                    "y",
                    "z"
                }.All(axis => vector[axis] is JsonValue v && v.TryGetValue<double>(out var coordinate) && double.IsFinite(coordinate)),
                _ => true
            };
            if (!valid)
                return new(400, null, "Invalid layout field: " + field);
        }

        var updated = PlayerState.user.DeepClone().AsObject();
        updated["_local_profile_setting"] = text;
        PlayerState.Save(updated);
        return new(0, new());
    }
}
